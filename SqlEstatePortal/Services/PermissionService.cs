using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.Services;

public class PermissionService
{
    private readonly AppDbContext _db;

    public PermissionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasAsync(ClaimsPrincipal user, string module, string action)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        if (string.Equals(user.FindFirstValue("AdminAccess"), "true", StringComparison.OrdinalIgnoreCase))
            return true;

        var roleIdText = user.FindFirstValue("AccessRoleId");
        if (!int.TryParse(roleIdText, out var roleId))
            return false;

        var perm = await _db.RolePermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.AccessRoleId == roleId && p.Module == module);

        if (perm == null)
            return false;

        return action.ToLowerInvariant() switch
        {
            "view" => perm.CanView,
            "insert" => perm.CanInsert,
            "update" => perm.CanUpdate,
            "delete" => perm.CanDelete,
            _ => false
        };
    }
}
