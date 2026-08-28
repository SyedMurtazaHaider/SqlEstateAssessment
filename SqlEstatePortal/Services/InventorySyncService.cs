using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SqlEstatePortal.Data;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.Services;

public class InventorySyncOptions
{
    public const string ModeMakerChecker = "MakerChecker";
    public const string ModeAutoDirect = "AutoDirect";

    public string Mode { get; set; } = ModeMakerChecker;

    public bool IsAutoDirect => string.Equals(Mode, ModeAutoDirect, StringComparison.OrdinalIgnoreCase);
}

public class InventorySyncService
{
    public const string StatusPending = "PendingApproval";
    public const string StatusApplied = "Applied";
    public const string StatusRejected = "Rejected";

    public const string ChangeNew = "New";
    public const string ChangeChanged = "Changed";
    public const string ChangeRemoved = "Removed";

    public const string EntityDatabase = "Database";
    public const string EntityServer = "Server";

    private readonly AppDbContext _db;
    private readonly InventorySyncOptions _options;

    public InventorySyncService(AppDbContext db, IOptions<InventorySyncOptions>? options = null)
    {
        _db = db;
        _options = options?.Value ?? new InventorySyncOptions();
    }

    public InventorySyncOptions Options => _options;
    public bool IsAutoDirect => _options.IsAutoDirect;

    /// <summary>
    /// When Mode is set to AutoDirect, automatically generates and applies the sync batch immediately upon assessment completion.
    /// </summary>
    public async Task<InventorySyncBatch?> AutoSyncIfEnabledAsync(int assessmentRunId, string actor, CancellationToken ct = default)
    {
        if (!_options.IsAutoDirect)
            return null;

        var hasChanges = await HasChangesAsync(assessmentRunId, ct);
        if (!hasChanges)
            return null;

        var batch = await GenerateAsync(assessmentRunId, actor, ct);
        await ApproveAndApplyAsync(batch.Id, actor, ct);
        return batch;
    }

    public async Task<InventorySyncBatch> GenerateAsync(int assessmentRunId, string actor, CancellationToken ct = default)
    {
        var batch = await BuildDiffBatchAsync(assessmentRunId, actor, ct);
        if (batch.Items.Count == 0)
            throw new InvalidOperationException("No changes found — register already matches this assessment.");

        _db.InventorySyncBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(batch.Id, "Generated", actor, new
        {
            assessmentRunId,
            batch.NewCount,
            batch.ChangedCount,
            batch.RemovedCount,
            batch.UnchangedCount
        }, ct);

        return batch;
    }

    /// <summary>
    /// True when assessment differs from Server/Database registers (new, changed, or unlink candidates).
    /// </summary>
    public async Task<bool> HasChangesAsync(int assessmentRunId, CancellationToken ct = default)
    {
        var batch = await BuildDiffBatchAsync(assessmentRunId, actor: null, ct);
        return batch.Items.Count > 0;
    }

    private async Task<InventorySyncBatch> BuildDiffBatchAsync(int assessmentRunId, string? actor, CancellationToken ct)
    {
        var run = await _db.AssessmentRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == assessmentRunId, ct)
            ?? throw new InvalidOperationException($"Assessment #{assessmentRunId} not found.");

        if (!string.Equals(run.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only succeeded assessments can be synced.");

        var assessedServers = await _db.AssessmentServerSnapshots.AsNoTracking()
            .Where(s => s.AssessmentRunId == assessmentRunId)
            .ToListAsync(ct);

        var assessed = await _db.AssessmentDatabases.AsNoTracking()
            .Where(d => d.AssessmentRunId == assessmentRunId)
            .ToListAsync(ct);

        var backups = await _db.AssessmentBackups.AsNoTracking()
            .Where(b => b.AssessmentRunId == assessmentRunId)
            .ToListAsync(ct);
        var backupLookup = backups.ToDictionary(
            b => Key(b.ServerName, b.DatabaseName),
            b => b,
            StringComparer.OrdinalIgnoreCase);

        var serversInAssessment = assessedServers
            .Select(s => s.ServerName)
            .Concat(assessed.Select(d => d.ServerName))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var registerDbs = (await _db.CtDatabases.AsNoTracking().ToListAsync(ct))
            .Where(d => !string.IsNullOrWhiteSpace(d.ServerName) &&
                        serversInAssessment.Contains(d.ServerName!))
            .ToList();

        var ctServers = await _db.CtServers.AsNoTracking().ToListAsync(ct);
        var serverLookup = ctServers
            .GroupBy(s => s.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var assessedKeys = new HashSet<string>(
            assessed.Select(d => Key(d.ServerName, d.Name)),
            StringComparer.OrdinalIgnoreCase);

        var batch = new InventorySyncBatch
        {
            AssessmentRunId = assessmentRunId,
            Status = StatusPending,
            CreatedBy = actor,
            CreatedAtUtc = DateTime.UtcNow
        };

        // --- Servers (estate status → ct_servers) ---
        foreach (var snap in assessedServers.OrderBy(s => s.ServerName))
        {
            var proposed = BuildServerProposed(snap);
            serverLookup.TryGetValue(snap.ServerName, out var existing);

            if (existing == null)
            {
                batch.Items.Add(new InventorySyncItem
                {
                    EntityType = EntityServer,
                    ServerName = snap.ServerName,
                    DatabaseName = "",
                    ChangeType = ChangeNew,
                    Selected = true,
                    NewSnapshotJson = JsonSerializer.Serialize(proposed),
                    Fields = BuildServerFields(null, proposed, selectAll: true)
                });
                batch.NewCount++;
            }
            else
            {
                var oldSnap = BuildServerFromRegister(existing);
                var fields = BuildServerFields(oldSnap, proposed, selectAll: false);
                if (fields.Count == 0)
                {
                    batch.UnchangedCount++;
                    continue;
                }

                batch.Items.Add(new InventorySyncItem
                {
                    EntityType = EntityServer,
                    ServerName = snap.ServerName,
                    DatabaseName = "",
                    ChangeType = ChangeChanged,
                    CtServerId = existing.TxId,
                    Selected = true,
                    OldSnapshotJson = JsonSerializer.Serialize(oldSnap),
                    NewSnapshotJson = JsonSerializer.Serialize(proposed),
                    Fields = fields
                });
                batch.ChangedCount++;
            }
        }

        // --- Databases ---
        foreach (var a in assessed.OrderBy(x => x.ServerName).ThenBy(x => x.Name))
        {
            backupLookup.TryGetValue(Key(a.ServerName, a.Name), out var bak);
            serverLookup.TryGetValue(a.ServerName, out var srv);
            var proposed = BuildProposed(a, bak, srv);

            var existing = registerDbs.FirstOrDefault(r =>
                string.Equals(r.ServerName, a.ServerName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.DatabaseName, a.Name, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                batch.Items.Add(new InventorySyncItem
                {
                    EntityType = EntityDatabase,
                    ServerName = a.ServerName,
                    DatabaseName = a.Name,
                    ChangeType = ChangeNew,
                    Selected = true,
                    NewSnapshotJson = JsonSerializer.Serialize(proposed),
                    Fields = BuildFields(null, proposed, selectAll: true)
                });
                batch.NewCount++;
            }
            else
            {
                var oldSnap = BuildFromRegister(existing);
                var fields = BuildFields(oldSnap, proposed, selectAll: false);
                if (fields.Count == 0)
                {
                    batch.UnchangedCount++;
                    continue;
                }

                batch.Items.Add(new InventorySyncItem
                {
                    EntityType = EntityDatabase,
                    ServerName = a.ServerName,
                    DatabaseName = a.Name,
                    ChangeType = ChangeChanged,
                    CtDatabaseId = existing.TxId,
                    Selected = true,
                    OldSnapshotJson = JsonSerializer.Serialize(oldSnap),
                    NewSnapshotJson = JsonSerializer.Serialize(proposed),
                    Fields = fields
                });
                batch.ChangedCount++;
            }
        }

        // Removed: active register DBs on same servers missing from assessment
        foreach (var r in registerDbs.Where(d => d.IsActive))
        {
            var k = Key(r.ServerName ?? "", r.DatabaseName);
            if (assessedKeys.Contains(k))
                continue;

            var oldSnap = BuildFromRegister(r);
            batch.Items.Add(new InventorySyncItem
            {
                EntityType = EntityDatabase,
                ServerName = r.ServerName ?? "",
                DatabaseName = r.DatabaseName,
                ChangeType = ChangeRemoved,
                CtDatabaseId = r.TxId,
                Selected = false,
                OldSnapshotJson = JsonSerializer.Serialize(oldSnap),
                Fields = []
            });
            batch.RemovedCount++;
        }

        return batch;
    }

    public async Task UpdateSelectionsAsync(int batchId, IEnumerable<SelectionUpdate> updates, string actor, CancellationToken ct = default)
    {
        var batch = await _db.InventorySyncBatches
            .Include(b => b.Items).ThenInclude(i => i.Fields)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new InvalidOperationException("Sync batch not found.");

        if (batch.Status is StatusApplied or StatusRejected)
            throw new InvalidOperationException("This sync batch can no longer be edited.");

        var map = updates.ToDictionary(u => u.ItemId);
        foreach (var item in batch.Items)
        {
            if (!map.TryGetValue(item.Id, out var u))
                continue;
            item.Selected = u.Selected;
            if (u.FieldSelections == null) continue;
            foreach (var f in item.Fields)
            {
                if (u.FieldSelections.TryGetValue(f.Id, out var sel))
                    f.Selected = sel;
            }
        }

        await _db.SaveChangesAsync(ct);
        await AddAuditAsync(batchId, "Saved", actor, new { notes = "Selections updated" }, ct);
    }

    public async Task RejectAsync(int batchId, string actor, string? notes, CancellationToken ct = default)
    {
        var batch = await _db.InventorySyncBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new InvalidOperationException("Sync batch not found.");
        if (batch.Status == StatusApplied)
            throw new InvalidOperationException("Applied batches cannot be rejected.");

        batch.Status = StatusRejected;
        batch.RejectedBy = actor;
        batch.RejectedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
            batch.Notes = notes.Trim();
        await _db.SaveChangesAsync(ct);
        await AddAuditAsync(batchId, "Rejected", actor, new { notes }, ct);
    }

    public async Task<(int inserted, int updated, int unlinked)> ApproveAndApplyAsync(int batchId, string actor, CancellationToken ct = default)
    {
        var batch = await _db.InventorySyncBatches
            .Include(b => b.Items).ThenInclude(i => i.Fields)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new InvalidOperationException("Sync batch not found.");

        if (batch.Status == StatusApplied)
            throw new InvalidOperationException("Batch already applied.");
        if (batch.Status == StatusRejected)
            throw new InvalidOperationException("Rejected batches cannot be applied.");

        await AddAuditAsync(batchId, "Approved", actor, new
        {
            selected = batch.Items.Count(i => i.Selected)
        }, ct);

        var inserted = 0;
        var updated = 0;
        var unlinked = 0;

        try
        {
            foreach (var item in batch.Items.Where(i => i.Selected))
            {
                if (IsServerItem(item))
                {
                    if (item.ChangeType == ChangeNew)
                    {
                        var snap = DeserializeServer(item.NewSnapshotJson);
                        var row = new CtServer
                        {
                            ServerName = item.ServerName,
                            ServerType = "SQL Servers",
                            ServerStatus = snap.ServerStatus,
                            SqlProduct = snap.SqlProduct,
                            SupportStatus = snap.SupportStatus,
                            SqlEdition = snap.SqlEdition,
                            SqlVersion = snap.SqlVersion,
                            VmCpu = snap.VmCpu,
                            VmRam = snap.VmRam,
                            VmStorageGb = snap.VmStorageGb,
                            SqlStartedAt = snap.SqlStartedAt,
                            IsActive = true,
                            CreatedBy = actor,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = actor,
                            UpdatedOn = DateTime.UtcNow
                        };
                        _db.CtServers.Add(row);
                        await _db.SaveChangesAsync(ct);
                        item.CtServerId = row.TxId;
                        item.Applied = true;
                        inserted++;
                    }
                    else if (item.ChangeType == ChangeChanged && item.CtServerId is int sid)
                    {
                        var row = await _db.CtServers.FirstOrDefaultAsync(s => s.TxId == sid, ct);
                        if (row == null) continue;
                        if (!string.Equals(row.ServerName, item.ServerName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var snap = DeserializeServer(item.NewSnapshotJson);
                        foreach (var f in item.Fields.Where(x => x.Selected))
                            ApplyServerField(row, f.FieldName, snap);

                        row.UpdatedBy = actor;
                        row.UpdatedOn = DateTime.UtcNow;
                        item.Applied = true;
                        updated++;
                    }

                    continue;
                }

                // Database items
                if (item.ChangeType == ChangeNew)
                {
                    var snap = DeserializeDb(item.NewSnapshotJson);
                    var row = new CtDatabase
                    {
                        DatabaseName = item.DatabaseName,
                        ServerName = item.ServerName,
                        DatabaseStatus = snap.DatabaseStatus,
                        RecoveryModel = snap.RecoveryModel,
                        CompatibilityLevel = snap.CompatibilityLevel,
                        CurrentSizeMb = snap.CurrentSizeMb,
                        CollationName = snap.CollationName,
                        CreationDate = snap.CreationDate,
                        BackupInfo = snap.BackupInfo,
                        LastFullBackup = snap.LastFullBackup,
                        LastDifferentialBackup = snap.LastDifferentialBackup,
                        LastLogBackup = snap.LastLogBackup,
                        DatabaseOwner = snap.DatabaseOwner,
                        Environment = snap.Environment,
                        Tower = snap.Tower,
                        Subscription = snap.Subscription,
                        SubscriptionId = snap.SubscriptionId,
                        DataCentreLocation = snap.DataCentreLocation,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.CtDatabases.Add(row);
                    await _db.SaveChangesAsync(ct);
                    item.CtDatabaseId = row.TxId;
                    item.Applied = true;
                    inserted++;
                }
                else if (item.ChangeType == ChangeChanged && item.CtDatabaseId is int id)
                {
                    var row = await _db.CtDatabases.FirstOrDefaultAsync(d => d.TxId == id, ct);
                    if (row == null) continue;

                    if (!string.Equals(row.ServerName, item.ServerName, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(row.DatabaseName, item.DatabaseName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var snap = DeserializeDb(item.NewSnapshotJson);
                    foreach (var f in item.Fields.Where(x => x.Selected))
                        ApplyDbField(row, f.FieldName, snap);

                    item.Applied = true;
                    updated++;
                }
                else if (item.ChangeType == ChangeRemoved && item.CtDatabaseId is int rid)
                {
                    var row = await _db.CtDatabases.FirstOrDefaultAsync(d => d.TxId == rid, ct);
                    if (row == null) continue;

                    if (!string.Equals(row.ServerName, item.ServerName, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(row.DatabaseName, item.DatabaseName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    row.IsActive = false;
                    item.Applied = true;
                    unlinked++;
                }
            }

            await _db.SaveChangesAsync(ct);
            batch.Status = StatusApplied;
            batch.ApprovedBy = actor;
            batch.ApprovedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await AddAuditAsync(batchId, "Applied", actor, new { inserted, updated, unlinked }, ct);
            return (inserted, updated, unlinked);
        }
        catch (Exception ex)
        {
            await AddAuditAsync(batchId, "ApplyFailed", actor, new { error = ex.Message }, ct);
            throw;
        }
    }

    private static bool IsServerItem(InventorySyncItem item) =>
        string.Equals(item.EntityType, EntityServer, StringComparison.OrdinalIgnoreCase);

    private static void ApplyDbField(CtDatabase row, string field, DbSnapshot snap)
    {
        switch (field)
        {
            case "DatabaseStatus": row.DatabaseStatus = snap.DatabaseStatus; break;
            case "RecoveryModel": row.RecoveryModel = snap.RecoveryModel; break;
            case "CompatibilityLevel": row.CompatibilityLevel = snap.CompatibilityLevel; break;
            case "CurrentSizeMb": row.CurrentSizeMb = snap.CurrentSizeMb; break;
            case "CollationName": row.CollationName = snap.CollationName; break;
            case "CreationDate": row.CreationDate = snap.CreationDate; break;
            case "BackupInfo": row.BackupInfo = snap.BackupInfo; break;
            case "LastFullBackup":
                row.LastFullBackup = snap.LastFullBackup;
                if (!string.IsNullOrWhiteSpace(snap.BackupInfo)) row.BackupInfo = snap.BackupInfo;
                break;
            case "LastDifferentialBackup":
                row.LastDifferentialBackup = snap.LastDifferentialBackup;
                if (!string.IsNullOrWhiteSpace(snap.BackupInfo)) row.BackupInfo = snap.BackupInfo;
                break;
            case "LastLogBackup":
                row.LastLogBackup = snap.LastLogBackup;
                if (!string.IsNullOrWhiteSpace(snap.BackupInfo)) row.BackupInfo = snap.BackupInfo;
                break;
            case "DatabaseOwner": row.DatabaseOwner = snap.DatabaseOwner; break;
            case "Environment": row.Environment = snap.Environment; break;
            case "Tower": row.Tower = snap.Tower; break;
            case "Subscription": row.Subscription = snap.Subscription; break;
            case "SubscriptionId": row.SubscriptionId = snap.SubscriptionId; break;
            case "DataCentreLocation": row.DataCentreLocation = snap.DataCentreLocation; break;
            case "IsActive": row.IsActive = snap.IsActive; break;
        }
    }

    private static void ApplyServerField(CtServer row, string field, ServerSnapshot snap)
    {
        switch (field)
        {
            case "ServerStatus": row.ServerStatus = snap.ServerStatus; break;
            case "SqlProduct": row.SqlProduct = snap.SqlProduct; break;
            case "SupportStatus": row.SupportStatus = snap.SupportStatus; break;
            case "SqlEdition": row.SqlEdition = snap.SqlEdition; break;
            case "SqlVersion": row.SqlVersion = snap.SqlVersion; break;
            case "VmCpu": row.VmCpu = snap.VmCpu; break;
            case "VmRam": row.VmRam = snap.VmRam; break;
            case "VmStorageGb": row.VmStorageGb = snap.VmStorageGb; break;
            case "SqlStartedAt": row.SqlStartedAt = snap.SqlStartedAt; break;
        }
    }

    private static ServerSnapshot BuildServerProposed(AssessmentServerSnapshot s) => new()
    {
        ServerStatus = s.Reachable ? "Reachable" : "UnReachable",
        SqlProduct = NullIfEmpty(s.Product),
        SupportStatus = NullIfEmpty(s.SupportStatus),
        SqlEdition = NullIfEmpty(s.Edition),
        SqlVersion = NullIfEmpty(s.Version),
        VmCpu = s.CpuCount?.ToString(),
        VmRam = s.MemoryMb?.ToString("0.##"),
        VmStorageGb = s.AllocatedGb?.ToString("0.##"),
        SqlStartedAt = s.StartedAt
    };

    private static ServerSnapshot BuildServerFromRegister(CtServer s) => new()
    {
        ServerStatus = s.ServerStatus,
        SqlProduct = s.SqlProduct,
        SupportStatus = s.SupportStatus,
        SqlEdition = s.SqlEdition,
        SqlVersion = s.SqlVersion,
        VmCpu = s.VmCpu,
        VmRam = s.VmRam,
        VmStorageGb = s.VmStorageGb,
        SqlStartedAt = s.SqlStartedAt
    };

    private static List<InventorySyncField> BuildServerFields(ServerSnapshot? oldSnap, ServerSnapshot neu, bool selectAll)
    {
        var fields = new (string Name, string? Old, string? New)[]
        {
            ("ServerStatus", oldSnap?.ServerStatus, neu.ServerStatus),
            ("SqlProduct", oldSnap?.SqlProduct, neu.SqlProduct),
            ("SupportStatus", oldSnap?.SupportStatus, neu.SupportStatus),
            ("SqlEdition", oldSnap?.SqlEdition, neu.SqlEdition),
            ("SqlVersion", oldSnap?.SqlVersion, neu.SqlVersion),
            ("VmCpu", oldSnap?.VmCpu, neu.VmCpu),
            ("VmRam", oldSnap?.VmRam, neu.VmRam),
            ("VmStorageGb", oldSnap?.VmStorageGb, neu.VmStorageGb),
            ("SqlStartedAt", Fmt(oldSnap?.SqlStartedAt), Fmt(neu.SqlStartedAt))
        };
        return DiffFields(oldSnap != null, selectAll, fields);
    }

    private DbSnapshot BuildProposed(AssessmentDatabase a, AssessmentBackup? bak, CtServer? srv)
    {
        var size = (int?)Math.Round((a.DataMb ?? 0) + (a.LogMb ?? 0));
        if (size == 0) size = a.DataMb.HasValue ? (int?)Math.Round(a.DataMb.Value) : null;

        var backupParts = new List<string>();
        if (bak?.LastFullBackup != null)
            backupParts.Add($"Full={bak.LastFullBackup:yyyy-MM-dd HH:mm}");
        if (bak?.LastDifferentialBackup != null)
            backupParts.Add($"Diff={bak.LastDifferentialBackup:yyyy-MM-dd HH:mm}");
        if (bak?.LastLogBackup != null)
            backupParts.Add($"Log={bak.LastLogBackup:yyyy-MM-dd HH:mm}");

        return new DbSnapshot
        {
            DatabaseStatus = a.State,
            RecoveryModel = a.RecoveryModel,
            CompatibilityLevel = a.CompatibilityLevel?.ToString(),
            CurrentSizeMb = size,
            CollationName = a.CollationName,
            CreationDate = a.CreationDate,
            BackupInfo = backupParts.Count == 0 ? null : string.Join("; ", backupParts),
            LastFullBackup = bak?.LastFullBackup,
            LastDifferentialBackup = bak?.LastDifferentialBackup,
            LastLogBackup = bak?.LastLogBackup,
            DatabaseOwner = string.IsNullOrWhiteSpace(a.OwnerName) ? null : a.OwnerName.Trim(),
            Environment = srv?.Environment,
            Tower = srv?.Tower,
            Subscription = srv?.Subscription,
            SubscriptionId = srv?.SubscriptionId,
            DataCentreLocation = srv?.DataCentreLocation,
            IsActive = true
        };
    }

    private static DbSnapshot BuildFromRegister(CtDatabase d)
    {
        // Legacy sync packed owner into backup_info as "Owner=...". Split that out so
        // diffs use DatabaseOwner and do not look like BackupInfo is being cleared.
        var (backupInfo, ownerFromBackup) = SplitLegacyOwnerFromBackupInfo(d.BackupInfo);
        var owner = string.IsNullOrWhiteSpace(d.DatabaseOwner)
            ? ownerFromBackup
            : d.DatabaseOwner.Trim();

        return new DbSnapshot
        {
            DatabaseStatus = d.DatabaseStatus,
            RecoveryModel = d.RecoveryModel,
            CompatibilityLevel = d.CompatibilityLevel,
            CurrentSizeMb = d.CurrentSizeMb,
            CollationName = d.CollationName,
            CreationDate = d.CreationDate,
            BackupInfo = backupInfo,
            LastFullBackup = d.LastFullBackup,
            LastDifferentialBackup = d.LastDifferentialBackup,
            LastLogBackup = d.LastLogBackup,
            DatabaseOwner = owner,
            Environment = d.Environment,
            Tower = d.Tower,
            Subscription = d.Subscription,
            SubscriptionId = d.SubscriptionId,
            DataCentreLocation = d.DataCentreLocation,
            IsActive = d.IsActive
        };
    }

    /// <summary>
    /// Removes "Owner=..." segments from backup_info and returns the owner value if present.
    /// </summary>
    private static (string? BackupInfo, string? Owner) SplitLegacyOwnerFromBackupInfo(string? backupInfo)
    {
        if (string.IsNullOrWhiteSpace(backupInfo))
            return (null, null);

        string? owner = null;
        var kept = new List<string>();
        foreach (var part in backupInfo.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("Owner=", StringComparison.OrdinalIgnoreCase))
            {
                var value = part["Owner=".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    owner = value;
                continue;
            }
            kept.Add(part);
        }

        return (kept.Count == 0 ? null : string.Join("; ", kept), owner);
    }

    private static List<InventorySyncField> BuildFields(DbSnapshot? oldSnap, DbSnapshot neu, bool selectAll)
    {
        var fields = new (string Name, string? Old, string? New)[]
        {
            ("DatabaseStatus", oldSnap?.DatabaseStatus, neu.DatabaseStatus),
            ("RecoveryModel", oldSnap?.RecoveryModel, neu.RecoveryModel),
            ("CompatibilityLevel", oldSnap?.CompatibilityLevel, neu.CompatibilityLevel),
            ("CurrentSizeMb", oldSnap?.CurrentSizeMb?.ToString(), neu.CurrentSizeMb?.ToString()),
            ("CollationName", oldSnap?.CollationName, neu.CollationName),
            ("CreationDate", Fmt(oldSnap?.CreationDate), Fmt(neu.CreationDate)),
            ("BackupInfo", oldSnap?.BackupInfo, neu.BackupInfo),
            ("LastFullBackup", Fmt(oldSnap?.LastFullBackup), Fmt(neu.LastFullBackup)),
            ("LastDifferentialBackup", Fmt(oldSnap?.LastDifferentialBackup), Fmt(neu.LastDifferentialBackup)),
            ("LastLogBackup", Fmt(oldSnap?.LastLogBackup), Fmt(neu.LastLogBackup)),
            ("DatabaseOwner", oldSnap?.DatabaseOwner, neu.DatabaseOwner),
            ("Environment", oldSnap?.Environment, neu.Environment),
            ("Tower", oldSnap?.Tower, neu.Tower),
            ("Subscription", oldSnap?.Subscription, neu.Subscription),
            ("SubscriptionId", oldSnap?.SubscriptionId, neu.SubscriptionId),
            ("DataCentreLocation", oldSnap?.DataCentreLocation, neu.DataCentreLocation),
            ("IsActive", oldSnap?.IsActive.ToString(), neu.IsActive.ToString())
        };
        return DiffFields(oldSnap != null, selectAll, fields);
    }

    private static List<InventorySyncField> DiffFields(
        bool hasOld,
        bool selectAll,
        (string Name, string? Old, string? New)[] fields)
    {
        var list = new List<InventorySyncField>();
        foreach (var (name, o, n) in fields)
        {
            if (hasOld && string.Equals(Norm(o), Norm(n), StringComparison.OrdinalIgnoreCase))
                continue;
            if (!hasOld && string.IsNullOrWhiteSpace(n))
                continue;

            list.Add(new InventorySyncField
            {
                FieldName = name,
                OldValue = o,
                NewValue = n,
                Selected = selectAll || hasOld
            });
        }
        return list;
    }

    private static string? Fmt(DateTime? d) => d?.ToString("O");
    private static string Norm(string? v) => (v ?? "").Trim();
    private static string Key(string server, string db) => $"{server.Trim()}|{db.Trim()}";
    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static DbSnapshot DeserializeDb(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new DbSnapshot();
        return JsonSerializer.Deserialize<DbSnapshot>(json) ?? new DbSnapshot();
    }

    private static ServerSnapshot DeserializeServer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ServerSnapshot();
        return JsonSerializer.Deserialize<ServerSnapshot>(json) ?? new ServerSnapshot();
    }

    private async Task AddAuditAsync(int batchId, string eventType, string actor, object detail, CancellationToken ct)
    {
        _db.InventorySyncAudits.Add(new InventorySyncAudit
        {
            BatchId = batchId,
            EventType = eventType,
            Actor = actor,
            OccurredAtUtc = DateTime.UtcNow,
            DetailJson = JsonSerializer.Serialize(detail)
        });
        await _db.SaveChangesAsync(ct);
    }

    public sealed class SelectionUpdate
    {
        public int ItemId { get; set; }
        public bool Selected { get; set; }
        public Dictionary<int, bool>? FieldSelections { get; set; }
    }

    private sealed class DbSnapshot
    {
        public string? DatabaseStatus { get; set; }
        public string? RecoveryModel { get; set; }
        public string? CompatibilityLevel { get; set; }
        public int? CurrentSizeMb { get; set; }
        public string? CollationName { get; set; }
        public DateTime? CreationDate { get; set; }
        public string? BackupInfo { get; set; }
        public DateTime? LastFullBackup { get; set; }
        public DateTime? LastDifferentialBackup { get; set; }
        public DateTime? LastLogBackup { get; set; }
        public string? DatabaseOwner { get; set; }
        public string? Environment { get; set; }
        public string? Tower { get; set; }
        public string? Subscription { get; set; }
        public string? SubscriptionId { get; set; }
        public string? DataCentreLocation { get; set; }
        public bool IsActive { get; set; } = true;
    }

    private sealed class ServerSnapshot
    {
        public string? ServerStatus { get; set; }
        public string? SqlProduct { get; set; }
        public string? SupportStatus { get; set; }
        public string? SqlEdition { get; set; }
        public string? SqlVersion { get; set; }
        public string? VmCpu { get; set; }
        public string? VmRam { get; set; }
        public string? VmStorageGb { get; set; }
        public DateTime? SqlStartedAt { get; set; }
    }
}
