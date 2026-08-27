using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class AssessmentDatabase
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? State { get; set; }

    [MaxLength(30)]
    public string? RecoveryModel { get; set; }

    public int? CompatibilityLevel { get; set; }

    [MaxLength(30)]
    public string? PageVerify { get; set; }

    public bool IsEncrypted { get; set; }
    public decimal? DataMb { get; set; }
    public decimal? LogMb { get; set; }

    [MaxLength(128)]
    public string? OwnerName { get; set; }

    public DateTime? LastGoodCheckDbTime { get; set; }
}

public class AssessmentVolume
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string MountPoint { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LogicalName { get; set; }

    public decimal? TotalGb { get; set; }
    public decimal? FreeGb { get; set; }
    public decimal? FreePct { get; set; }
}

public class AssessmentService
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ServiceName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? StartupType { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(200)]
    public string? ServiceAccount { get; set; }

    [MaxLength(10)]
    public string? InstantFileInitialization { get; set; }
}

public class AssessmentWait
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string WaitType { get; set; } = string.Empty;

    public long WaitingTasks { get; set; }
    public long WaitTimeMs { get; set; }
    public long SignalWaitTimeMs { get; set; }
    public decimal? WaitPct { get; set; }
}

public class AssessmentJob
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string JobName { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    [MaxLength(50)]
    public string? LastRunStatus { get; set; }

    public DateTime? LastRun { get; set; }
    public string? Message { get; set; }
}

public class AssessmentSysadmin
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TypeDesc { get; set; }

    public bool IsDisabled { get; set; }
    public DateTime? CreateDate { get; set; }
}

public class AssessmentConfiguration
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public long? Minimum { get; set; }
    public long? Maximum { get; set; }
    public long? ConfigValue { get; set; }
    public long? RunValue { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDynamic { get; set; }
    public bool IsAdvanced { get; set; }
}

public class AssessmentBackup
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string DatabaseName { get; set; } = string.Empty;

    public DateTime? LastFullBackup { get; set; }
    public DateTime? LastDifferentialBackup { get; set; }
    public DateTime? LastLogBackup { get; set; }
}
