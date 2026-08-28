using SqlEstatePortal.Models;

namespace SqlEstatePortal.ViewModels;

public class AssessmentCompareViewModel
{
    public int? BaseRunId { get; set; }
    public int? TargetRunId { get; set; }
    public AssessmentRun? BaseRun { get; set; }
    public AssessmentRun? TargetRun { get; set; }
    public List<AssessmentRunSummary> AvailableRuns { get; set; } = [];

    // KPI Summary
    public List<CompareKpiRow> Kpis { get; set; } = [];

    // Findings Diff
    public List<CompareFindingRow> FindingsDiff { get; set; } = [];
    public int NewFindingsCount { get; set; }
    public int ResolvedFindingsCount { get; set; }
    public int OngoingFindingsCount { get; set; }

    // Server Diff
    public List<CompareServerRow> ServersDiff { get; set; } = [];
    public int NewServersCount { get; set; }
    public int RemovedServersCount { get; set; }
    public int ChangedServersCount { get; set; }
    public int UnchangedServersCount { get; set; }

    // Database Diff
    public List<CompareDatabaseRow> DatabasesDiff { get; set; } = [];
    public int NewDatabasesCount { get; set; }
    public int RemovedDatabasesCount { get; set; }
    public int ChangedDatabasesCount { get; set; }
    public int UnchangedDatabasesCount { get; set; }

    // Backup Diff
    public List<CompareBackupRow> BackupsDiff { get; set; } = [];

    // Configurations Diff
    public List<CompareConfigRow> ConfigsDiff { get; set; } = [];
    public int ChangedConfigsCount { get; set; }
}

public class CompareKpiRow
{
    public string Category { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string BaseValue { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public string Delta { get; set; } = string.Empty;
    /// <summary>
    /// "improved", "degraded", "neutral", "unchanged"
    /// </summary>
    public string Status { get; set; } = "neutral";
}

public class CompareFindingRow
{
    /// <summary>
    /// "New", "Resolved", "Ongoing"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public class CompareServerRow
{
    /// <summary>
    /// "New", "Removed", "Changed", "Unchanged"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public bool? BaseReachable { get; set; }
    public bool? TargetReachable { get; set; }
    public string? BaseEdition { get; set; }
    public string? TargetEdition { get; set; }
    public string? BaseVersion { get; set; }
    public string? TargetVersion { get; set; }
    public string? BaseSupportStatus { get; set; }
    public string? TargetSupportStatus { get; set; }
    public int? BaseCpuCount { get; set; }
    public int? TargetCpuCount { get; set; }
    public decimal? BaseMemoryMb { get; set; }
    public decimal? TargetMemoryMb { get; set; }
    public decimal? BaseAllocatedGb { get; set; }
    public decimal? TargetAllocatedGb { get; set; }
    public int? BaseUserDbCount { get; set; }
    public int? TargetUserDbCount { get; set; }
    public List<string> Changes { get; set; } = [];
}

public class CompareDatabaseRow
{
    /// <summary>
    /// "New", "Removed", "Changed", "Unchanged"
    /// </summary>
    public string Status { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string? BaseState { get; set; }
    public string? TargetState { get; set; }
    public string? BaseRecoveryModel { get; set; }
    public string? TargetRecoveryModel { get; set; }
    public int? BaseCompatLevel { get; set; }
    public int? TargetCompatLevel { get; set; }
    public decimal? BaseDataMb { get; set; }
    public decimal? TargetDataMb { get; set; }
    public decimal? BaseLogMb { get; set; }
    public decimal? TargetLogMb { get; set; }
    public decimal? TotalMbDelta { get; set; }
    public List<string> Changes { get; set; } = [];
}

public class CompareBackupRow
{
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime? BaseLastFullBackup { get; set; }
    public DateTime? TargetLastFullBackup { get; set; }
    public DateTime? BaseLastDifferentialBackup { get; set; }
    public DateTime? TargetLastDifferentialBackup { get; set; }
    public DateTime? BaseLastLogBackup { get; set; }
    public DateTime? TargetLastLogBackup { get; set; }
    /// <summary>
    /// "Updated", "Stale", "New", "Removed"
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

public class CompareConfigRow
{
    public string ServerName { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public long? BaseRunValue { get; set; }
    public long? TargetRunValue { get; set; }
    public string Status { get; set; } = "Changed";
}
