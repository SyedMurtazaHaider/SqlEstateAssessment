using System.ComponentModel.DataAnnotations;

namespace SqlEstatePortal.Models;

public class RolePermission
{
    public int Id { get; set; }

    public int AccessRoleId { get; set; }
    public AccessRole AccessRole { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    public bool CanView { get; set; }
    public bool CanInsert { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}
