using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SqlEstatePortal.Data;
using SqlEstatePortal.Models;
using SqlEstatePortal.Services;

namespace SqlEstatePortal.Services;

public class AssessmentOptions
{
    public string ScriptPath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int SampleSeconds { get; set; } = 2;
    public bool TrustServerCertificate { get; set; } = true;
}

public class AssessmentRunnerService
{
    private readonly AppDbContext _db;
    private readonly AssessmentOptions _options;
    private readonly ILogger<AssessmentRunnerService> _logger;

    public AssessmentRunnerService(
        AppDbContext db,
        IOptions<AssessmentOptions> options,
        ILogger<AssessmentRunnerService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AssessmentRun> RunAsync(string? triggeredBy, CancellationToken cancellationToken = default)
    {
        var servers = await _db.CtServers
            .Where(x => x.ServerStatus == ServerReachabilityService.StatusReachable)
            .OrderBy(x => x.ServerName)
            .Select(x => x.ServerName)
            .ToListAsync(cancellationToken);

        var run = new AssessmentRun
        {
            StartedAt = DateTime.UtcNow,
            Status = "Running",
            TriggeredBy = triggeredBy,
            ServerListPath = servers.Count == 0 ? null : string.Join("; ", servers)
        };
        _db.AssessmentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        string? tempListPath = null;
        try
        {
            if (!File.Exists(_options.ScriptPath))
                throw new FileNotFoundException("Assessment script not found.", _options.ScriptPath);
            if (servers.Count == 0)
                throw new InvalidOperationException(
                    "No Reachable servers found. Open Servers and run Check Server Status first.");

            tempListPath = Path.Combine(
                Path.GetTempPath(),
                $"sql-estate-servers-{run.Id}-{Guid.NewGuid():N}.txt");
            await File.WriteAllLinesAsync(tempListPath, servers, cancellationToken);

            var args = new List<string>
            {
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", Quote(_options.ScriptPath),
                "-ServerListPath", Quote(tempListPath),
                "-SampleSeconds", _options.SampleSeconds.ToString()
            };
            if (_options.TrustServerCertificate)
                args.Add("-TrustServerCertificate");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = string.Join(" ", args),
                WorkingDirectory = string.IsNullOrWhiteSpace(_options.WorkingDirectory)
                    ? Path.GetDirectoryName(_options.ScriptPath)!
                    : _options.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            run.OutputLog = (stdout + Environment.NewLine + stderr).Trim();

            if (process.ExitCode != 0)
            {
                run.Status = "Failed";
                run.ErrorMessage = string.IsNullOrWhiteSpace(stderr) ? $"PowerShell exited with code {process.ExitCode}." : stderr;
                run.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return run;
            }

            var jsonPath = ExtractPath(stdout, "JSON:");
            var htmlPath = ExtractPath(stdout, "HTML:");
            run.ReportJsonPath = jsonPath;
            run.ReportHtmlPath = htmlPath;

            if (!string.IsNullOrWhiteSpace(jsonPath) && File.Exists(jsonPath))
                await ImportJsonAsync(run, jsonPath, htmlPath, cancellationToken);
            else
            {
                run.Status = "Failed";
                run.ErrorMessage = "Assessment finished but JSON report path was not found.";
            }

            if (run.Status == "Running")
                run.Status = "Succeeded";

            run.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assessment run {RunId} failed", run.Id);
            run.Status = "Failed";
            run.ErrorMessage = ex.Message;
            run.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempListPath) && File.Exists(tempListPath))
            {
                try { File.Delete(tempListPath); } catch { /* ignore */ }
            }
        }
    }

    public async Task<AssessmentRun> ImportFileAsync(
        string jsonPath,
        string? triggeredBy,
        string? htmlPath = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.AssessmentRuns
            .Include(x => x.Databases)
            .FirstOrDefaultAsync(x => x.ReportJsonPath == jsonPath, cancellationToken);

        if (existing != null && !force && existing.Databases.Count > 0)
            return existing;

        if (existing != null)
        {
            _db.AssessmentRuns.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var run = new AssessmentRun
        {
            StartedAt = DateTime.UtcNow,
            Status = "Succeeded",
            TriggeredBy = triggeredBy,
            ReportJsonPath = jsonPath,
            ReportHtmlPath = htmlPath
        };
        _db.AssessmentRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        await ImportJsonAsync(run, jsonPath, htmlPath, cancellationToken);
        run.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return run;
    }

    private async Task ImportJsonAsync(AssessmentRun run, string jsonPath, string? htmlPath, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!string.IsNullOrWhiteSpace(htmlPath) && File.Exists(htmlPath))
            run.HtmlContent = await File.ReadAllTextAsync(htmlPath, cancellationToken);

        if (root.TryGetProperty("ExecutiveSummary", out var summary))
        {
            run.ServerCount = GetInt(summary, "ServerCount");
            run.ReachableCount = GetInt(summary, "ReachableCount");
            run.UnreachableCount = GetInt(summary, "UnreachableCount");
            run.EndOfSupportCount = GetInt(summary, "EndOfSupportCount");
            run.AllocatedStorageGb = GetDecimal(summary, "AllocatedStorageGB") ?? 0;
            run.EstimatedLicensedCores = GetInt(summary, "EstimatedLicensedCores");
            var generated = GetDate(summary, "GeneratedUtc") ?? GetDate(summary, "GeneratedLocal");
            if (generated.HasValue)
                run.StartedAt = generated.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(generated.Value, DateTimeKind.Utc)
                    : generated.Value.ToUniversalTime();

            if (summary.TryGetProperty("FindingCounts", out var counts))
            {
                run.CriticalCount = GetInt(counts, "Critical");
                run.HighCount = GetInt(counts, "High");
                run.MediumCount = GetInt(counts, "Medium");
                run.LowCount = GetInt(counts, "Low");
                run.InfoCount = GetInt(counts, "Info");
            }
        }

        foreach (var f in Enumerate(root, "Findings"))
        {
            run.Findings.Add(new AssessmentFinding
            {
                ServerName = GetString(f, "Server"),
                Severity = GetString(f, "Severity"),
                Area = GetString(f, "Area"),
                Finding = GetString(f, "Finding"),
                Recommendation = GetString(f, "Recommendation")
            });
        }

        if (run.InfoCount == 0)
            run.InfoCount = run.Findings.Count(x => string.Equals(x.Severity, "Info", StringComparison.OrdinalIgnoreCase));

        foreach (var s in Enumerate(root, "Servers"))
        {
            var support = Obj(s, "Support");
            var instance = Obj(s, "Instance");
            var cost = Obj(s, "Cost");
            var perf = Obj(s, "Performance");
            var host = Enumerate(s, "Host").FirstOrDefault();
            var serverName = GetString(s, "Server");

            run.Servers.Add(new AssessmentServerSnapshot
            {
                ServerName = serverName,
                Reachable = GetBool(s, "Reachable"),
                Product = support.ValueKind == JsonValueKind.Object ? GetString(support, "Product") : null,
                SupportStatus = support.ValueKind == JsonValueKind.Object ? GetString(support, "Status") : null,
                Edition = instance.ValueKind == JsonValueKind.Object ? GetString(instance, "Edition") : null,
                Version = instance.ValueKind == JsonValueKind.Object
                    ? $"{GetString(instance, "ProductVersion")} {GetString(instance, "ProductLevel")} {GetString(instance, "ProductUpdateLevel")}".Trim()
                    : null,
                CpuCount = instance.ValueKind == JsonValueKind.Object ? GetIntOrNull(instance, "CpuCount") : GetIntOrNull(cost, "CpuCount"),
                MemoryMb = instance.ValueKind == JsonValueKind.Object ? GetDecimal(instance, "PhysicalMemoryMB") : null,
                AllocatedGb = cost.ValueKind == JsonValueKind.Object ? GetDecimal(cost, "AllocatedDataAndLogGB") : null,
                UserDatabaseCount = cost.ValueKind == JsonValueKind.Object ? GetIntOrNull(cost, "UserDatabaseCount") : null,
                StartedAt = instance.ValueKind == JsonValueKind.Object ? GetDate(instance, "SqlServerStartTime") : null,
                UserConnections = perf.ValueKind == JsonValueKind.Object ? GetIntOrNull(perf, "UserConnections") : null,
                PageLifeExpectancySec = perf.ValueKind == JsonValueKind.Object ? GetIntOrNull(perf, "PageLifeExpectancySec") : null,
                BatchRequestsPerSec = perf.ValueKind == JsonValueKind.Object ? GetDecimal(perf, "BatchRequestsPerSec") : null,
                HostPlatform = host.ValueKind == JsonValueKind.Object ? GetString(host, "host_platform") : null,
                HostDistribution = host.ValueKind == JsonValueKind.Object ? GetString(host, "host_distribution") : null,
                Collation = instance.ValueKind == JsonValueKind.Object ? GetString(instance, "Collation") : null,
                VirtualMachineType = instance.ValueKind == JsonValueKind.Object ? GetString(instance, "VirtualMachineType") : null,
                LicenseType = cost.ValueKind == JsonValueKind.Object ? GetString(cost, "LicenseType") : null,
                Error = NullIfEmpty(GetString(s, "Error"))
            });

            foreach (var db in Enumerate(s, "Databases"))
            {
                run.Databases.Add(new AssessmentDatabase
                {
                    ServerName = serverName,
                    Name = GetString(db, "name"),
                    State = GetString(db, "state_desc"),
                    RecoveryModel = GetString(db, "recovery_model_desc"),
                    CompatibilityLevel = GetIntOrNull(db, "compatibility_level"),
                    PageVerify = GetString(db, "page_verify_option_desc"),
                    IsEncrypted = GetBool(db, "is_encrypted"),
                    DataMb = GetDecimal(db, "DataMB"),
                    LogMb = GetDecimal(db, "LogMB"),
                    OwnerName = GetString(db, "owner_name"),
                    LastGoodCheckDbTime = SanitizeCheckDb(GetDate(db, "LastGoodCheckDbTime"))
                });
            }

            foreach (var v in Enumerate(s, "Volumes"))
            {
                run.Volumes.Add(new AssessmentVolume
                {
                    ServerName = serverName,
                    MountPoint = GetString(v, "volume_mount_point"),
                    LogicalName = GetString(v, "logical_volume_name"),
                    TotalGb = GetDecimal(v, "TotalGB"),
                    FreeGb = GetDecimal(v, "FreeGB"),
                    FreePct = GetDecimal(v, "FreePct")
                });
            }

            foreach (var svc in Enumerate(s, "Services"))
            {
                run.Services.Add(new AssessmentService
                {
                    ServerName = serverName,
                    ServiceName = GetString(svc, "servicename"),
                    StartupType = GetString(svc, "startup_type_desc"),
                    Status = GetString(svc, "status_desc"),
                    ServiceAccount = GetString(svc, "service_account"),
                    InstantFileInitialization = GetString(svc, "instant_file_initialization_enabled")
                });
            }

            foreach (var w in Enumerate(s, "Waits"))
            {
                run.Waits.Add(new AssessmentWait
                {
                    ServerName = serverName,
                    WaitType = GetString(w, "wait_type"),
                    WaitingTasks = GetLong(w, "waiting_tasks_count"),
                    WaitTimeMs = GetLong(w, "wait_time_ms"),
                    SignalWaitTimeMs = GetLong(w, "signal_wait_time_ms"),
                    WaitPct = GetDecimal(w, "WaitPct")
                });
            }

            foreach (var job in Enumerate(s, "Jobs"))
            {
                run.Jobs.Add(new AssessmentJob
                {
                    ServerName = serverName,
                    JobName = GetString(job, "JobName"),
                    Enabled = GetBool(job, "enabled") || GetInt(job, "enabled") == 1,
                    LastRunStatus = GetString(job, "LastRunStatus"),
                    LastRun = GetDate(job, "LastRun"),
                    Message = NullIfEmpty(GetString(job, "Message"))
                });
            }

            foreach (var admin in Enumerate(s, "Sysadmins"))
            {
                run.Sysadmins.Add(new AssessmentSysadmin
                {
                    ServerName = serverName,
                    Name = GetString(admin, "name"),
                    TypeDesc = GetString(admin, "type_desc"),
                    IsDisabled = GetBool(admin, "is_disabled"),
                    CreateDate = GetDate(admin, "create_date")
                });
            }

            foreach (var cfg in Enumerate(s, "Configuration"))
            {
                run.Configurations.Add(new AssessmentConfiguration
                {
                    ServerName = serverName,
                    Name = GetString(cfg, "name"),
                    Minimum = GetLongOrNull(cfg, "minimum"),
                    Maximum = GetLongOrNull(cfg, "maximum"),
                    ConfigValue = GetLongOrNull(cfg, "config_value") ?? GetLongOrNull(cfg, "value"),
                    RunValue = GetLongOrNull(cfg, "run_value") ?? GetLongOrNull(cfg, "value_in_use"),
                    Description = NullIfEmpty(GetString(cfg, "description")),
                    IsDynamic = GetBool(cfg, "is_dynamic"),
                    IsAdvanced = GetBool(cfg, "is_advanced")
                });
            }

            foreach (var b in Enumerate(s, "Backups"))
            {
                var dbName = FirstNonEmpty(GetString(b, "DatabaseName"), GetString(b, "database_name"));
                if (string.IsNullOrWhiteSpace(dbName))
                    continue;

                run.Backups.Add(new AssessmentBackup
                {
                    ServerName = serverName,
                    DatabaseName = dbName,
                    LastFullBackup = GetDate(b, "LastFullBackup") ?? GetDate(b, "LastFull"),
                    LastDifferentialBackup = GetDate(b, "LastDifferentialBackup") ?? GetDate(b, "LastDiff"),
                    LastLogBackup = GetDate(b, "LastLogBackup") ?? GetDate(b, "LastLog")
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<JsonElement> Enumerate(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el) || el.ValueKind == JsonValueKind.Null)
            return [];
        if (el.ValueKind == JsonValueKind.Array)
            return el.EnumerateArray();
        if (el.ValueKind == JsonValueKind.Object)
            return [el];
        return [];
    }

    private static JsonElement Obj(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var el) ? el : default;

    private static string Quote(string value) => $"\"{value}\"";

    private static string? ExtractPath(string output, string marker)
    {
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                return trimmed[marker.Length..].Trim();
        }
        return null;
    }

    private static int GetInt(JsonElement el, string name)
        => GetIntOrNull(el, name) ?? 0;

    private static int? GetIntOrNull(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (p.TryGetInt32(out var i)) return i;
        if (p.TryGetDecimal(out var d)) return (int)d;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    private static long GetLong(JsonElement el, string name)
        => GetLongOrNull(el, name) ?? 0;

    private static long? GetLongOrNull(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (p.TryGetInt64(out var i)) return i;
        if (p.TryGetDecimal(out var d)) return (long)d;
        if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    private static decimal? GetDecimal(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (p.TryGetDecimal(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out var s)) return s;
        return null;
    }

    private static bool GetBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return false;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => p.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => bool.TryParse(p.GetString(), out var b) && b,
            _ => false
        };
    }

    private static string GetString(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return string.Empty;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return string.Empty;
        return p.ValueKind == JsonValueKind.String ? (p.GetString() ?? string.Empty) : p.ToString();
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return string.Empty;
    }

    private static DateTime? SanitizeCheckDb(DateTime? value)
        => value is { Year: < 1990 } ? null : value;

    private static DateTime? GetDate(JsonElement el, string name)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (p.ValueKind == JsonValueKind.String)
            return ParsePsDate(p.GetString());
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("value", out var inner))
            return ParsePsDate(inner.GetString());
        return null;
    }

    private static DateTime? ParsePsDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParse(raw, out var dt)) return dt;
        var start = raw.IndexOf("/Date(", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        var from = start + 6;
        var to = raw.IndexOf(')', from);
        if (to < 0) return null;
        var token = raw[from..to];
        var i = token.StartsWith('-') ? 1 : 0;
        while (i < token.Length && char.IsDigit(token[i])) i++;
        if (!long.TryParse(token[..i], out var ms)) return null;
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
        }
        catch
        {
            return null;
        }
    }
}
