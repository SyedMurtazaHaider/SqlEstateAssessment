using SqlEstatePortal.Models;

namespace SqlEstatePortal.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
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
                    new RolePermission { Module = AppModules.Roles, CanView = true }
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
                    new RolePermission { Module = AppModules.Roles, CanView = true }
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
    }
}
