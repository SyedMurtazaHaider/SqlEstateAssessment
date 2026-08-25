using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IWebHostEnvironment? env = null)
    {
        if (!db.Teams.Any())
        {
            db.Teams.AddRange(
                new Team { Name = "DBA" },
                new Team { Name = "Operations" },
                new Team { Name = "Security" });
            await db.SaveChangesAsync();
        }

        if (!db.AccessRoles.Any())
        {
            var admin = new AccessRole
            {
                Name = "Administrator",
                Description = "Full access to all modules",
                Permissions = AppModules.All.Select(m => new RolePermission
                {
                    Module = m,
                    CanView = true,
                    CanInsert = true,
                    CanUpdate = true,
                    CanDelete = true
                }).ToList()
            };

            var viewer = new AccessRole
            {
                Name = "Viewer",
                Description = "Read-only dashboard and assessments",
                Permissions =
                [
                    new RolePermission { Module = AppModules.Dashboard, CanView = true },
                    new RolePermission { Module = AppModules.Assessments, CanView = true },
                    new RolePermission { Module = AppModules.TeamMembers, CanView = true },
                    new RolePermission { Module = AppModules.Roles, CanView = true },
                    new RolePermission { Module = AppModules.Servers, CanView = true }
                ]
            };

            var operatorRole = new AccessRole
            {
                Name = "Operator",
                Description = "Can run assessments and view results",
                Permissions =
                [
                    new RolePermission { Module = AppModules.Dashboard, CanView = true },
                    new RolePermission { Module = AppModules.Assessments, CanView = true, CanInsert = true },
                    new RolePermission { Module = AppModules.TeamMembers, CanView = true },
                    new RolePermission { Module = AppModules.Roles, CanView = true },
                    new RolePermission { Module = AppModules.Servers, CanView = true, CanInsert = true, CanUpdate = true }
                ]
            };

            db.AccessRoles.AddRange(admin, viewer, operatorRole);
            await db.SaveChangesAsync();

            if (!db.TeamMembers.Any())
            {
                var dbaTeam = db.Teams.First(t => t.Name == "DBA");
                db.TeamMembers.Add(new TeamMember
                {
                    MemberName = "System Administrator",
                    Username = "admin",
                    Email = "admin@local",
                    AccessRoleId = admin.Id,
                    TeamId = dbaTeam.Id,
                    Designation = "Portal Admin",
                    AdminAccess = true,
                    Status = "Active",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123")
                });
                await db.SaveChangesAsync();
            }
        }

        await EnsureModulePermissionsAsync(db);
        await SeedEstateServersAsync(db, env);
    }

    private static async Task EnsureModulePermissionsAsync(AppDbContext db)
    {
        var roles = await db.AccessRoles.Include(r => r.Permissions).ToListAsync();
        var changed = false;

        foreach (var role in roles)
        {
            foreach (var module in AppModules.All)
            {
                if (role.Permissions.Any(p => p.Module == module))
                    continue;

                var isAdmin = string.Equals(role.Name, "Administrator", StringComparison.OrdinalIgnoreCase);
                var isOperator = string.Equals(role.Name, "Operator", StringComparison.OrdinalIgnoreCase);
                role.Permissions.Add(new RolePermission
                {
                    Module = module,
                    CanView = true,
                    CanInsert = isAdmin || (isOperator && (module == AppModules.Servers || module == AppModules.Assessments)),
                    CanUpdate = isAdmin || (isOperator && module == AppModules.Servers),
                    CanDelete = isAdmin
                });
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private static async Task SeedEstateServersAsync(AppDbContext db, IWebHostEnvironment? env)
    {
        if (await db.EstateServers.AnyAsync())
            return;

        var names = new List<string>();
        var examplePath = env != null
            ? Path.Combine(env.ContentRootPath, "servers.example.txt")
            : Path.Combine(AppContext.BaseDirectory, "servers.example.txt");

        if (File.Exists(examplePath))
        {
            names = File.ReadAllLines(examplePath)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (names.Count == 0)
            return;

        foreach (var name in names)
        {
            db.EstateServers.Add(new EstateServer
            {
                ServerName = name,
                Enabled = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
