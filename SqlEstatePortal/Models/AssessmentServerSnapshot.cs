using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class AssessmentServerSnapshot
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun AssessmentRun { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    public bool Reachable { get; set; }

    [MaxLength(100)]
    public string? Product { get; set; }

    [MaxLength(50)]
    public string? SupportStatus { get; set; }

    [MaxLength(150)]
    public string? Edition { get; set; }

    [MaxLength(150)]
    public string? Version { get; set; }

    public int? CpuCount { get; set; }
    public decimal? MemoryMb { get; set; }
    public decimal? AllocatedGb { get; set; }
    public int? UserDatabaseCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public int? UserConnections { get; set; }
    public int? PageLifeExpectancySec { get; set; }
    public decimal? BatchRequestsPerSec { get; set; }

    [MaxLength(80)]
    public string? HostPlatform { get; set; }

    [MaxLength(120)]
    public string? HostDistribution { get; set; }

    [MaxLength(80)]
    public string? Collation { get; set; }

    [MaxLength(50)]
    public string? VirtualMachineType { get; set; }

    [MaxLength(50)]
    public string? LicenseType { get; set; }

    public string? Error { get; set; }
}
