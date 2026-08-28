using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Services;

public class AssessmentCompareService
{
    public AssessmentCompareViewModel Compare(AssessmentRun baseRun, AssessmentRun targetRun, List<AssessmentRunSummary> availableRuns)
    {
        var vm = new AssessmentCompareViewModel
        {
            BaseRunId = baseRun.Id,
            TargetRunId = targetRun.Id,
            BaseRun = baseRun,
            TargetRun = targetRun,
            AvailableRuns = availableRuns
        };

        // 1. KPIs
        vm.Kpis = BuildKpiComparison(baseRun, targetRun);

        // 2. Findings Diff
        vm.FindingsDiff = BuildFindingsDiff(baseRun, targetRun);
        vm.NewFindingsCount = vm.FindingsDiff.Count(f => f.Status == "New");
        vm.ResolvedFindingsCount = vm.FindingsDiff.Count(f => f.Status == "Resolved");
        vm.OngoingFindingsCount = vm.FindingsDiff.Count(f => f.Status == "Ongoing");

        // 3. Servers Diff
        vm.ServersDiff = BuildServersDiff(baseRun, targetRun);
        vm.NewServersCount = vm.ServersDiff.Count(s => s.Status == "New");
        vm.RemovedServersCount = vm.ServersDiff.Count(s => s.Status == "Removed");
        vm.ChangedServersCount = vm.ServersDiff.Count(s => s.Status == "Changed");
        vm.UnchangedServersCount = vm.ServersDiff.Count(s => s.Status == "Unchanged");

        // 4. Databases Diff
        vm.DatabasesDiff = BuildDatabasesDiff(baseRun, targetRun);
        vm.NewDatabasesCount = vm.DatabasesDiff.Count(d => d.Status == "New");
        vm.RemovedDatabasesCount = vm.DatabasesDiff.Count(d => d.Status == "Removed");
        vm.ChangedDatabasesCount = vm.DatabasesDiff.Count(d => d.Status == "Changed");
        vm.UnchangedDatabasesCount = vm.DatabasesDiff.Count(d => d.Status == "Unchanged");

        // 5. Backups Diff
        vm.BackupsDiff = BuildBackupsDiff(baseRun, targetRun);

        // 6. Configurations Diff
        vm.ConfigsDiff = BuildConfigsDiff(baseRun, targetRun);
        vm.ChangedConfigsCount = vm.ConfigsDiff.Count;

        return vm;
    }

    private static List<CompareKpiRow> BuildKpiComparison(AssessmentRun a, AssessmentRun b)
    {
        var list = new List<CompareKpiRow>();

        void AddIntKpi(string category, string name, int valA, int valB, bool higherIsBetter = false, bool isNeutral = false)
        {
            var delta = valB - valA;
            var deltaStr = delta > 0 ? $"+{delta}" : delta.ToString();
            string status;
            if (isNeutral || delta == 0)
            {
                status = delta == 0 ? "unchanged" : "neutral";
            }
            else if (higherIsBetter)
            {
                status = delta > 0 ? "improved" : "degraded";
            }
            else
            {
                status = delta < 0 ? "improved" : "degraded";
            }

            list.Add(new CompareKpiRow
            {
                Category = category,
                MetricName = name,
                BaseValue = valA.ToString("N0"),
                TargetValue = valB.ToString("N0"),
                Delta = deltaStr,
                Status = status
            });
        }

        void AddDecimalKpi(string category, string name, decimal valA, decimal valB, string format = "N2")
        {
            var delta = valB - valA;
            var deltaStr = delta > 0 ? $"+{delta.ToString(format)}" : delta.ToString(format);
            list.Add(new CompareKpiRow
            {
                Category = category,
                MetricName = name,
                BaseValue = valA.ToString(format),
                TargetValue = valB.ToString(format),
                Delta = deltaStr,
                Status = delta == 0 ? "unchanged" : "neutral"
            });
        }

        // Infrastructure
        AddIntKpi("Infrastructure", "Total Servers Assessed", a.ServerCount, b.ServerCount, isNeutral: true);
        AddIntKpi("Infrastructure", "Reachable Servers", a.ReachableCount, b.ReachableCount, higherIsBetter: true);
        AddIntKpi("Infrastructure", "Unreachable Servers", a.UnreachableCount, b.UnreachableCount, higherIsBetter: false);
        AddIntKpi("Infrastructure", "End of Support Servers", a.EndOfSupportCount, b.EndOfSupportCount, higherIsBetter: false);
        AddDecimalKpi("Infrastructure", "Allocated Storage (GB)", a.AllocatedStorageGb, b.AllocatedStorageGb);
        AddIntKpi("Infrastructure", "Estimated Licensed Cores", a.EstimatedLicensedCores, b.EstimatedLicensedCores, isNeutral: true);
        AddIntKpi("Infrastructure", "Databases Assessed", a.Databases.Count, b.Databases.Count, isNeutral: true);

        // Risk & Findings
        AddIntKpi("Risk & Findings", "Critical Findings", a.CriticalCount, b.CriticalCount, higherIsBetter: false);
        AddIntKpi("Risk & Findings", "High Findings", a.HighCount, b.HighCount, higherIsBetter: false);
        AddIntKpi("Risk & Findings", "Medium Findings", a.MediumCount, b.MediumCount, higherIsBetter: false);
        AddIntKpi("Risk & Findings", "Low Findings", a.LowCount, b.LowCount, higherIsBetter: false);
        AddIntKpi("Risk & Findings", "Total Findings", a.Findings.Count, b.Findings.Count, higherIsBetter: false);

        return list;
    }

    private static List<CompareFindingRow> BuildFindingsDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentFinding f) => $"{f.ServerName.Trim()}|{f.Area.Trim()}|{f.Finding.Trim()}".ToLowerInvariant();

        var baseDict = a.Findings
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.Findings
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareFindingRow>();

        // New in Target
        foreach (var kvp in targetDict)
        {
            if (!baseDict.ContainsKey(kvp.Key))
            {
                var tf = kvp.Value;
                result.Add(new CompareFindingRow
                {
                    Status = "New",
                    Severity = tf.Severity,
                    ServerName = tf.ServerName,
                    Area = tf.Area,
                    Finding = tf.Finding,
                    Recommendation = tf.Recommendation
                });
            }
        }

        // Resolved (in Base, not in Target)
        foreach (var kvp in baseDict)
        {
            if (!targetDict.ContainsKey(kvp.Key))
            {
                var bf = kvp.Value;
                result.Add(new CompareFindingRow
                {
                    Status = "Resolved",
                    Severity = bf.Severity,
                    ServerName = bf.ServerName,
                    Area = bf.Area,
                    Finding = bf.Finding,
                    Recommendation = bf.Recommendation
                });
            }
        }

        // Ongoing (in both)
        foreach (var kvp in targetDict)
        {
            if (baseDict.ContainsKey(kvp.Key))
            {
                var tf = kvp.Value;
                result.Add(new CompareFindingRow
                {
                    Status = "Ongoing",
                    Severity = tf.Severity,
                    ServerName = tf.ServerName,
                    Area = tf.Area,
                    Finding = tf.Finding,
                    Recommendation = tf.Recommendation
                });
            }
        }

        int StatusOrder(string s) => s == "New" ? 0 : s == "Resolved" ? 1 : 2;
        int SeverityOrder(string s) => s.ToLowerInvariant() switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" => 2,
            "low" => 3,
            _ => 4
        };

        return result
            .OrderBy(r => StatusOrder(r.Status))
            .ThenBy(r => SeverityOrder(r.Severity))
            .ThenBy(r => r.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<CompareServerRow> BuildServersDiff(AssessmentRun a, AssessmentRun b)
    {
        var baseDict = a.Servers
            .GroupBy(s => s.ServerName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var targetDict = b.Servers
            .GroupBy(s => s.ServerName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var allServerNames = baseDict.Keys.Union(targetDict.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
        var result = new List<CompareServerRow>();

        foreach (var name in allServerNames)
        {
            baseDict.TryGetValue(name, out var bs);
            targetDict.TryGetValue(name, out var ts);

            if (bs == null && ts != null)
            {
                result.Add(new CompareServerRow
                {
                    Status = "New",
                    ServerName = ts.ServerName,
                    TargetReachable = ts.Reachable,
                    TargetEdition = ts.Edition,
                    TargetVersion = ts.Version,
                    TargetSupportStatus = ts.SupportStatus,
                    TargetCpuCount = ts.CpuCount,
                    TargetMemoryMb = ts.MemoryMb,
                    TargetAllocatedGb = ts.AllocatedGb,
                    TargetUserDbCount = ts.UserDatabaseCount
                });
            }
            else if (bs != null && ts == null)
            {
                result.Add(new CompareServerRow
                {
                    Status = "Removed",
                    ServerName = bs.ServerName,
                    BaseReachable = bs.Reachable,
                    BaseEdition = bs.Edition,
                    BaseVersion = bs.Version,
                    BaseSupportStatus = bs.SupportStatus,
                    BaseCpuCount = bs.CpuCount,
                    BaseMemoryMb = bs.MemoryMb,
                    BaseAllocatedGb = bs.AllocatedGb,
                    BaseUserDbCount = bs.UserDatabaseCount
                });
            }
            else if (bs != null && ts != null)
            {
                var changes = new List<string>();
                if (bs.Reachable != ts.Reachable)
                    changes.Add($"Reachable: {(bs.Reachable ? "Yes" : "No")} \u2192 {(ts.Reachable ? "Yes" : "No")}");
                if (!string.Equals(bs.Edition, ts.Edition, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bs.Edition) || !string.IsNullOrWhiteSpace(ts.Edition)))
                    changes.Add($"Edition: {bs.Edition ?? "—"} \u2192 {ts.Edition ?? "—"}");
                if (!string.Equals(bs.Version, ts.Version, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bs.Version) || !string.IsNullOrWhiteSpace(ts.Version)))
                    changes.Add($"Version: {bs.Version ?? "—"} \u2192 {ts.Version ?? "—"}");
                if (!string.Equals(bs.SupportStatus, ts.SupportStatus, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bs.SupportStatus) || !string.IsNullOrWhiteSpace(ts.SupportStatus)))
                    changes.Add($"Support: {bs.SupportStatus ?? "—"} \u2192 {ts.SupportStatus ?? "—"}");
                if (bs.CpuCount != ts.CpuCount && (bs.CpuCount != null || ts.CpuCount != null))
                    changes.Add($"CPU Cores: {bs.CpuCount?.ToString() ?? "—"} \u2192 {ts.CpuCount?.ToString() ?? "—"}");
                if (bs.MemoryMb != ts.MemoryMb && (bs.MemoryMb != null || ts.MemoryMb != null))
                    changes.Add($"Memory MB: {bs.MemoryMb?.ToString("N0") ?? "—"} \u2192 {ts.MemoryMb?.ToString("N0") ?? "—"}");
                if (bs.AllocatedGb != ts.AllocatedGb && (bs.AllocatedGb != null || ts.AllocatedGb != null))
                    changes.Add($"Allocated GB: {bs.AllocatedGb?.ToString("N2") ?? "—"} \u2192 {ts.AllocatedGb?.ToString("N2") ?? "—"}");
                if (bs.UserDatabaseCount != ts.UserDatabaseCount && (bs.UserDatabaseCount != null || ts.UserDatabaseCount != null))
                    changes.Add($"Databases: {bs.UserDatabaseCount?.ToString() ?? "—"} \u2192 {ts.UserDatabaseCount?.ToString() ?? "—"}");

                var status = changes.Count > 0 ? "Changed" : "Unchanged";

                result.Add(new CompareServerRow
                {
                    Status = status,
                    ServerName = ts.ServerName,
                    BaseReachable = bs.Reachable,
                    TargetReachable = ts.Reachable,
                    BaseEdition = bs.Edition,
                    TargetEdition = ts.Edition,
                    BaseVersion = bs.Version,
                    TargetVersion = ts.Version,
                    BaseSupportStatus = bs.SupportStatus,
                    TargetSupportStatus = ts.SupportStatus,
                    BaseCpuCount = bs.CpuCount,
                    TargetCpuCount = ts.CpuCount,
                    BaseMemoryMb = bs.MemoryMb,
                    TargetMemoryMb = ts.MemoryMb,
                    BaseAllocatedGb = bs.AllocatedGb,
                    TargetAllocatedGb = ts.AllocatedGb,
                    BaseUserDbCount = bs.UserDatabaseCount,
                    TargetUserDbCount = ts.UserDatabaseCount,
                    Changes = changes
                });
            }
        }

        int StatusOrder(string s) => s == "New" ? 0 : s == "Removed" ? 1 : s == "Changed" ? 2 : 3;
        return result.OrderBy(r => StatusOrder(r.Status)).ThenBy(r => r.ServerName).ToList();
    }

    private static List<CompareDatabaseRow> BuildDatabasesDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentDatabase d) => $"{d.ServerName.Trim()}|{d.Name.Trim()}".ToLowerInvariant();

        var baseDict = a.Databases
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.Databases
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var allKeys = baseDict.Keys.Union(targetDict.Keys).ToList();
        var result = new List<CompareDatabaseRow>();

        foreach (var key in allKeys)
        {
            baseDict.TryGetValue(key, out var bd);
            targetDict.TryGetValue(key, out var td);

            if (bd == null && td != null)
            {
                result.Add(new CompareDatabaseRow
                {
                    Status = "New",
                    ServerName = td.ServerName,
                    DatabaseName = td.Name,
                    TargetState = td.State,
                    TargetRecoveryModel = td.RecoveryModel,
                    TargetCompatLevel = td.CompatibilityLevel,
                    TargetDataMb = td.DataMb,
                    TargetLogMb = td.LogMb
                });
            }
            else if (bd != null && td == null)
            {
                result.Add(new CompareDatabaseRow
                {
                    Status = "Removed",
                    ServerName = bd.ServerName,
                    DatabaseName = bd.Name,
                    BaseState = bd.State,
                    BaseRecoveryModel = bd.RecoveryModel,
                    BaseCompatLevel = bd.CompatibilityLevel,
                    BaseDataMb = bd.DataMb,
                    BaseLogMb = bd.LogMb
                });
            }
            else if (bd != null && td != null)
            {
                var changes = new List<string>();
                if (!string.Equals(bd.State, td.State, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bd.State) || !string.IsNullOrWhiteSpace(td.State)))
                    changes.Add($"State: {bd.State ?? "—"} \u2192 {td.State ?? "—"}");
                if (!string.Equals(bd.RecoveryModel, td.RecoveryModel, StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrWhiteSpace(bd.RecoveryModel) || !string.IsNullOrWhiteSpace(td.RecoveryModel)))
                    changes.Add($"Recovery: {bd.RecoveryModel ?? "—"} \u2192 {td.RecoveryModel ?? "—"}");
                if (bd.CompatibilityLevel != td.CompatibilityLevel && (bd.CompatibilityLevel != null || td.CompatibilityLevel != null))
                    changes.Add($"Compat: {bd.CompatibilityLevel?.ToString() ?? "—"} \u2192 {td.CompatibilityLevel?.ToString() ?? "—"}");

                var baseTotalMb = (bd.DataMb ?? 0) + (bd.LogMb ?? 0);
                var targetTotalMb = (td.DataMb ?? 0) + (td.LogMb ?? 0);
                var sizeDiffMb = targetTotalMb - baseTotalMb;

                if (Math.Abs(sizeDiffMb) >= 1m)
                {
                    var sign = sizeDiffMb > 0 ? "+" : "";
                    changes.Add($"Size: {baseTotalMb:N1} MB \u2192 {targetTotalMb:N1} MB ({sign}{sizeDiffMb:N1} MB)");
                }

                var status = changes.Count > 0 ? "Changed" : "Unchanged";

                result.Add(new CompareDatabaseRow
                {
                    Status = status,
                    ServerName = td.ServerName,
                    DatabaseName = td.Name,
                    BaseState = bd.State,
                    TargetState = td.State,
                    BaseRecoveryModel = bd.RecoveryModel,
                    TargetRecoveryModel = td.RecoveryModel,
                    BaseCompatLevel = bd.CompatibilityLevel,
                    TargetCompatLevel = td.CompatibilityLevel,
                    BaseDataMb = bd.DataMb,
                    TargetDataMb = td.DataMb,
                    BaseLogMb = bd.LogMb,
                    TargetLogMb = td.LogMb,
                    TotalMbDelta = sizeDiffMb,
                    Changes = changes
                });
            }
        }

        int StatusOrder(string s) => s == "New" ? 0 : s == "Removed" ? 1 : s == "Changed" ? 2 : 3;
        return result.OrderBy(r => StatusOrder(r.Status)).ThenBy(r => r.ServerName).ThenBy(r => r.DatabaseName).ToList();
    }

    private static List<CompareBackupRow> BuildBackupsDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentBackup bk) => $"{bk.ServerName.Trim()}|{bk.DatabaseName.Trim()}".ToLowerInvariant();

        var baseDict = a.Backups
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.Backups
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var allKeys = baseDict.Keys.Union(targetDict.Keys).ToList();
        var result = new List<CompareBackupRow>();

        foreach (var key in allKeys)
        {
            baseDict.TryGetValue(key, out var bb);
            targetDict.TryGetValue(key, out var tb);

            if (bb == null && tb != null)
            {
                result.Add(new CompareBackupRow
                {
                    Status = "New",
                    ServerName = tb.ServerName,
                    DatabaseName = tb.DatabaseName,
                    TargetLastFullBackup = tb.LastFullBackup,
                    TargetLastDifferentialBackup = tb.LastDifferentialBackup,
                    TargetLastLogBackup = tb.LastLogBackup
                });
            }
            else if (bb != null && tb == null)
            {
                result.Add(new CompareBackupRow
                {
                    Status = "Removed",
                    ServerName = bb.ServerName,
                    DatabaseName = bb.DatabaseName,
                    BaseLastFullBackup = bb.LastFullBackup,
                    BaseLastDifferentialBackup = bb.LastDifferentialBackup,
                    BaseLastLogBackup = bb.LastLogBackup
                });
            }
            else if (bb != null && tb != null)
            {
                var updated = (tb.LastFullBackup > bb.LastFullBackup) ||
                              (tb.LastDifferentialBackup > bb.LastDifferentialBackup) ||
                              (tb.LastLogBackup > bb.LastLogBackup);

                result.Add(new CompareBackupRow
                {
                    Status = updated ? "Updated" : "Stale",
                    ServerName = tb.ServerName,
                    DatabaseName = tb.DatabaseName,
                    BaseLastFullBackup = bb.LastFullBackup,
                    TargetLastFullBackup = tb.LastFullBackup,
                    BaseLastDifferentialBackup = bb.LastDifferentialBackup,
                    TargetLastDifferentialBackup = tb.LastDifferentialBackup,
                    BaseLastLogBackup = bb.LastLogBackup,
                    TargetLastLogBackup = tb.LastLogBackup
                });
            }
        }

        return result.OrderBy(r => r.ServerName).ThenBy(r => r.DatabaseName).ToList();
    }

    private static List<CompareConfigRow> BuildConfigsDiff(AssessmentRun a, AssessmentRun b)
    {
        string Key(AssessmentConfiguration c) => $"{c.ServerName.Trim()}|{c.Name.Trim()}".ToLowerInvariant();

        var baseDict = a.Configurations
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var targetDict = b.Configurations
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<CompareConfigRow>();
        foreach (var kvp in targetDict)
        {
            if (baseDict.TryGetValue(kvp.Key, out var bc))
            {
                var tc = kvp.Value;
                if (bc.RunValue != tc.RunValue)
                {
                    result.Add(new CompareConfigRow
                    {
                        ServerName = tc.ServerName,
                        ConfigName = tc.Name,
                        BaseRunValue = bc.RunValue,
                        TargetRunValue = tc.RunValue,
                        Status = "Changed"
                    });
                }
            }
        }

        return result.OrderBy(r => r.ServerName).ThenBy(r => r.ConfigName).ToList();
    }
}
