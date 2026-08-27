using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SqlEstatePortal.Models;

[Table("InventorySyncBatches")]
public class InventorySyncBatch
{
    public int Id { get; set; }
    public int AssessmentRunId { get; set; }
    public AssessmentRun? AssessmentRun { get; set; }

    [MaxLength(40)]
    public string Status { get; set; } = "PendingApproval";

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    [MaxLength(100)]
    public string? RejectedBy { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public int NewCount { get; set; }
    public int ChangedCount { get; set; }
    public int RemovedCount { get; set; }
    public int UnchangedCount { get; set; }

    public ICollection<InventorySyncItem> Items { get; set; } = new List<InventorySyncItem>();
    public ICollection<InventorySyncAudit> Audits { get; set; } = new List<InventorySyncAudit>();
}

[Table("InventorySyncItems")]
public class InventorySyncItem
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public InventorySyncBatch Batch { get; set; } = null!;

    [MaxLength(200)]
    public string ServerName { get; set; } = string.Empty;

    /// <summary>Database | Server</summary>
    [MaxLength(20)]
    public string EntityType { get; set; } = "Database";

    [MaxLength(500)]
    public string DatabaseName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ChangeType { get; set; } = string.Empty; // New | Changed | Removed

    public int? CtDatabaseId { get; set; }
    public int? CtServerId { get; set; }
    public bool Selected { get; set; }
    public bool Applied { get; set; }

    public string? OldSnapshotJson { get; set; }
    public string? NewSnapshotJson { get; set; }

    public ICollection<InventorySyncField> Fields { get; set; } = new List<InventorySyncField>();
}

[Table("InventorySyncFields")]
public class InventorySyncField
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public InventorySyncItem Item { get; set; } = null!;

    [MaxLength(80)]
    public string FieldName { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public bool Selected { get; set; }
}

[Table("InventorySyncAudits")]
public class InventorySyncAudit
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public InventorySyncBatch Batch { get; set; } = null!;
    public int? ItemId { get; set; }

    [MaxLength(40)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Actor { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public string? DetailJson { get; set; }
}
