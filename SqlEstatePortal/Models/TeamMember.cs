using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class TeamMember
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string MemberName { get; set; } = string.Empty;

    public int AccessRoleId { get; set; }
    public AccessRole AccessRole { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    [MaxLength(150)]
    public string? Designation { get; set; }

    public bool AdminAccess { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Active";

    [Required, MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
