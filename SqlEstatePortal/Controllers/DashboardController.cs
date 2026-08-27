using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [RequirePermission(AppModules.Dashboard, "view")]
    public async Task<IActionResult> Index(int? runId, string? server)
    {
        var available = await _db.AssessmentRuns
            .OrderByDescending(x => x.StartedAt)
            .Take(50)
            .Select(r => new AssessmentRunSummary
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Status = r.Status,
                TriggeredBy = r.TriggeredBy,
                ServerCount = r.ServerCount,
                ReachableCount = r.ReachableCount,
                CriticalCount = r.CriticalCount,
                HighCount = r.HighCount,
                MediumCount = r.MediumCount,
                LowCount = r.LowCount
            })
            .ToListAsync();

        AssessmentRun? selected = null;
        if (runId.HasValue)
            selected = await LoadRunAsync(runId.Value);
        if (selected == null && available.Count > 0)
            selected = await LoadRunAsync(available[0].Id);

        var serverNames = selected?.Servers
            .Select(s => s.ServerName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList() ?? [];

        if (!string.IsNullOrWhiteSpace(server) &&
            !serverNames.Contains(server, StringComparer.OrdinalIgnoreCase))
        {
            server = null;
        }

        var selectedSummary = selected == null
            ? null
            : available.FirstOrDefault(r => r.Id == selected.Id) ?? new AssessmentRunSummary
            {
                Id = selected.Id,
                StartedAt = selected.StartedAt,
                Status = selected.Status,
                ServerCount = selected.ServerCount,
                ReachableCount = selected.ReachableCount,
                CriticalCount = selected.CriticalCount,
                HighCount = selected.HighCount,
                MediumCount = selected.MediumCount,
                LowCount = selected.LowCount
            };

        var snapshot = Slice(selected, server);
        var charts = BuildCharts(snapshot, available);
        return View(new DashboardViewModel
        {
            TotalRuns = await _db.AssessmentRuns.CountAsync(),
            SucceededRuns = await _db.AssessmentRuns.CountAsync(x => x.Status == "Succeeded"),
            FailedRuns = await _db.AssessmentRuns.CountAsync(x => x.Status == "Failed"),
            SelectedRunId = selected?.Id ?? 0,
            SelectedServer = server,
            ServerCount = snapshot.Servers.Count,
            ReachableCount = snapshot.Servers.Count(s => s.Reachable),
            CriticalCount = snapshot.Findings.Count(f => f.Severity == "Critical"),
            HighCount = snapshot.Findings.Count(f => f.Severity == "High"),
            DatabaseCount = snapshot.Databases.Count,
            FindingCount = snapshot.Findings.Count,
            AllocatedStorageGb = string.IsNullOrWhiteSpace(server)
                ? (selected?.AllocatedStorageGb ?? 0)
                : snapshot.Servers.Sum(s => s.AllocatedGb ?? 0),
            SelectedRun = selectedSummary,
            AvailableRuns = available,
            ServerNames = serverNames,
            RecentRuns = available.Take(10).ToList(),
            ChartsJson = JsonSerializer.Serialize(charts, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default
            })
        });
    }

    private static AssessmentRun Slice(AssessmentRun? run, string? server)
    {
        if (run == null)
        {
            return new AssessmentRun();
        }

        bool Match(string name) =>
            string.IsNullOrWhiteSpace(server) ||
            string.Equals(name, server, StringComparison.OrdinalIgnoreCase);

        return new AssessmentRun
        {
            Id = run.Id,
            StartedAt = run.StartedAt,
            Status = run.Status,
            AllocatedStorageGb = run.AllocatedStorageGb,
            Findings = run.Findings.Where(f => Match(f.ServerName)).ToList(),
            Servers = run.Servers.Where(s => Match(s.ServerName)).ToList(),
            Databases = run.Databases.Where(d => Match(d.ServerName)).ToList(),
            Volumes = run.Volumes.Where(v => Match(v.ServerName)).ToList(),
            Services = run.Services.Where(s => Match(s.ServerName)).ToList(),
            Waits = run.Waits.Where(w => Match(w.ServerName)).ToList(),
            Jobs = run.Jobs.Where(j => Match(j.ServerName)).ToList(),
            Sysadmins = run.Sysadmins.Where(a => Match(a.ServerName)).ToList(),
            Configurations = run.Configurations.Where(c => Match(c.ServerName)).ToList(),
            Backups = run.Backups.Where(b => Match(b.ServerName)).ToList()
        };
    }

    private static DashboardCharts BuildCharts(AssessmentRun run, List<AssessmentRunSummary> recent)
    {
        var charts = new DashboardCharts
        {
            RunHistory = recent
                .OrderBy(r => r.StartedAt)
                .Select(r => new HistoryPoint
                {
                    Label = r.StartedAt.ToLocalTime().ToString("MM/dd HH:mm"),
                    Critical = r.CriticalCount,
                    High = r.HighCount,
                    Medium = r.MediumCount,
                    Low = r.LowCount
                })
                .ToArray(),
            FindingsBySeverity = CountBy(run.Findings.Select(f => f.Severity)),
            FindingsByArea = CountBy(run.Findings.Select(f => f.Area)).Take(12).ToArray(),
            FindingsByServer = CountBy(run.Findings.Select(f => f.ServerName)),
            RecoveryModels = CountBy(run.Databases.Select(d => d.RecoveryModel)),
            SupportStatus = CountBy(run.Servers.Select(s => s.SupportStatus)),
            Editions = CountBy(run.Servers.Select(s => ShortEdition(s.Edition))),
            JobStatus = CountBy(run.Jobs.Select(j => string.IsNullOrWhiteSpace(j.LastRunStatus) ? "Unknown" : j.LastRunStatus)),
            ServiceStatus = CountBy(run.Services.Select(s => string.IsNullOrWhiteSpace(s.Status) ? "Unknown" : s.Status)),
            VolumeFreePct = run.Volumes
                .Where(v => v.FreePct.HasValue)
                .OrderBy(v => v.FreePct)
                .Take(12)
                .Select(v => new NamedValue
                {
                    Label = TrimLabel($"{v.ServerName} {v.MountPoint}"),
                    Value = decimal.Round(v.FreePct.GetValueOrDefault(), 1)
                })
                .ToArray(),
            TopDatabasesMb = run.Databases
                .Where(d => d.DataMb.HasValue)
                .OrderByDescending(d => d.DataMb)
                .Take(12)
                .Select(d => new NamedValue
                {
                    Label = TrimLabel($"{d.ServerName} / {d.Name}"),
                    Value = decimal.Round(d.DataMb.GetValueOrDefault(), 1)
                })
                .ToArray(),
            TopWaits = run.Waits
                .OrderByDescending(w => w.WaitTimeMs)
                .Take(10)
                .Select(w => new NamedValue
                {
                    Label = TrimLabel(w.WaitType),
                    Value = w.WaitTimeMs
                })
                .ToArray()
        };

        return charts;
    }

    private static ChartSlice[] CountBy(IEnumerable<string?> values) =>
        values
            .Select(v => string.IsNullOrWhiteSpace(v) ? "Unknown" : v.Trim())
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ChartSlice { Label = g.First(), Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .ToArray();

    private static string ShortEdition(string? edition)
    {
        if (string.IsNullOrWhiteSpace(edition))
            return "Unknown";
        var text = edition.Replace("Edition", "", StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(text) ? "Unknown" : TrimLabel(text);
    }

    private static string TrimLabel(string value) =>
        value.Length <= 42 ? value : value[..39] + "...";

    private Task<AssessmentRun?> LoadRunAsync(int id) =>
        _db.AssessmentRuns
            .Include(x => x.Findings)
            .Include(x => x.Servers)
            .Include(x => x.Databases)
            .Include(x => x.Volumes)
            .Include(x => x.Services)
            .Include(x => x.Waits)
            .Include(x => x.Jobs)
            .Include(x => x.Sysadmins)
            .Include(x => x.Configurations)
            .Include(x => x.Backups)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
}
