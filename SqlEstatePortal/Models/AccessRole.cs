using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class AccessRole
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}
