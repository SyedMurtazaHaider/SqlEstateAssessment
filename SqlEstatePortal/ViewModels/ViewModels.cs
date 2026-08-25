using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.ViewModels;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8, ErrorMessage = "New password must be at least 8 characters."), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class TeamMemberFormViewModel
{
    public int Id { get; set; }

    [Required, Display(Name = "Member Name")]
    public string MemberName { get; set; } = string.Empty;

    [Required, Display(Name = "Access Role")]
    public int AccessRoleId { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    [Display(Name = "Team Name")]
    public int? TeamId { get; set; }

    public string? Designation { get; set; }

    [Required, Display(Name = "Admin Access")]
    public bool AdminAccess { get; set; }

    [Required]
    public string Status { get; set; } = "Active";

    [Display(Name = "Password (auto-generated)")]
    public string GeneratedPassword { get; set; } = string.Empty;

    public IEnumerable<SelectListItem> RoleOptions { get; set; } = [];
    public IEnumerable<SelectListItem> TeamOptions { get; set; } = [];
    public IEnumerable<SelectListItem> StatusOptions { get; set; } =
    [
        new("Active", "Active"),
        new("Inactive", "Inactive")
    ];
}

public class RolePermissionRow
{
    public string Module { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanInsert { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}

public class RoleFormViewModel
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<RolePermissionRow> Permissions { get; set; } = [];
}

public class AssessmentDetailsViewModel
{
    public AssessmentRun Run { get; set; } = null!;
    public List<AssessmentRunSummary> AvailableRuns { get; set; } = [];
}

public class DashboardViewModel
{
    public int TotalRuns { get; set; }
    public int SucceededRuns { get; set; }
    public int FailedRuns { get; set; }
    public int SelectedRunId { get; set; }
    public string? SelectedServer { get; set; }
    public int ServerCount { get; set; }
    public int ReachableCount { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int DatabaseCount { get; set; }
    public int FindingCount { get; set; }
    public decimal AllocatedStorageGb { get; set; }
    public AssessmentRunSummary? SelectedRun { get; set; }
    public List<AssessmentRunSummary> AvailableRuns { get; set; } = [];
    public List<string> ServerNames { get; set; } = [];
    public List<AssessmentRunSummary> RecentRuns { get; set; } = [];
    public string ChartsJson { get; set; } = "{}";
}

public class DashboardCharts
{
    public ChartSlice[] FindingsBySeverity { get; set; } = [];
    public ChartSlice[] FindingsByArea { get; set; } = [];
    public ChartSlice[] FindingsByServer { get; set; } = [];
    public ChartSlice[] RecoveryModels { get; set; } = [];
    public ChartSlice[] SupportStatus { get; set; } = [];
    public ChartSlice[] Editions { get; set; } = [];
    public ChartSlice[] JobStatus { get; set; } = [];
    public ChartSlice[] ServiceStatus { get; set; } = [];
    public NamedValue[] VolumeFreePct { get; set; } = [];
    public NamedValue[] TopDatabasesMb { get; set; } = [];
    public NamedValue[] TopWaits { get; set; } = [];
    public HistoryPoint[] RunHistory { get; set; } = [];
}

public class ChartSlice
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class NamedValue
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class HistoryPoint
{
    public string Label { get; set; } = string.Empty;
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
}

public class AssessmentRunSummary
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TriggeredBy { get; set; }
    public int ServerCount { get; set; }
    public int ReachableCount { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
}

public class FindingSummary
{
    public string Severity { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;
}

public class ServerSummary
{
    public string ServerName { get; set; } = string.Empty;
    public bool Reachable { get; set; }
    public string? Product { get; set; }
    public string? SupportStatus { get; set; }
    public string? Edition { get; set; }
}
