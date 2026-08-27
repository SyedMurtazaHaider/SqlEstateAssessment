using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SqlEstatePortal.Models;

[Table("ct_costs")]
public class CtCost
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("application_id")]
    public int? ApplicationId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("l1_support")]
    public string? L1Support { get; set; }

    [Column("service_name")]
    public string? ServiceName { get; set; }

    [Column("cost_grade")]
    public string? CostGrade { get; set; }

    [Column("cost_narrative")]
    public string? CostNarrative { get; set; }

    [Column("estimated_revenue")]
    public string? EstimatedRevenue { get; set; }

    [Column("hosting_cost")]
    public string? HostingCost { get; set; }

    [Column("azure_hosting_cost")]
    public decimal? AzureHostingCost { get; set; }

    [Column("azure_cost_period")]
    [MaxLength(32)]
    public string? AzureCostPeriod { get; set; }

    [Column("azure_cost_synced_at")]
    public DateTime? AzureCostSyncedAt { get; set; }

    [Column("azure_cost_currency")]
    [MaxLength(8)]
    public string? AzureCostCurrency { get; set; }

    [Column("license_cost")]
    public string? LicenseCost { get; set; }

    [Column("support_cost")]
    public string? SupportCost { get; set; }

    [Column("change_cost")]
    public string? ChangeCost { get; set; }

    [Column("tco")]
    public string? Tco { get; set; }

    [Column("total_users")]
    public string? TotalUsers { get; set; }

    [Column("cost_per_head")]
    public string? CostPerHead { get; set; }
}
