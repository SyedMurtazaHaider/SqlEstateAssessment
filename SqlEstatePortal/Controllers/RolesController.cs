using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class RolesController : Controller
{
    private readonly AppDbContext _db;

    public RolesController(AppDbContext db)
    {
        _db = db;
    }

    [RequirePermission(AppModules.Roles, "view")]
    public async Task<IActionResult> Index()
    {
        var roles = await _db.AccessRoles
            .Include(x => x.Permissions)
            .OrderBy(x => x.Name)
            .ToListAsync();
        return View(roles);
    }

    [RequirePermission(AppModules.Roles, "insert")]
    public IActionResult Create()
    {
        return View("Edit", BlankForm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Roles, "insert")]
    public async Task<IActionResult> Create(RoleFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View("Edit", EnsurePermissionRows(model));

        var role = new AccessRole
        {
            Name = model.Name,
            Description = model.Description,
            IsActive = model.IsActive,
            Permissions = model.Permissions.Select(p => new RolePermission
            {
                Module = p.Module,
                CanView = p.CanView,
                CanInsert = p.CanInsert,
                CanUpdate = p.CanUpdate,
                CanDelete = p.CanDelete
            }).ToList()
        };
        _db.AccessRoles.Add(role);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Role created.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(AppModules.Roles, "update")]
    public async Task<IActionResult> Edit(int id)
    {
        var role = await _db.AccessRoles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id);
        if (role == null) return NotFound();

        var vm = new RoleFormViewModel
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            Permissions = AppModules.All.Select(m =>
            {
                var existing = role.Permissions.FirstOrDefault(p => p.Module == m);
                return new RolePermissionRow
                {
                    Module = m,
                    CanView = existing?.CanView ?? false,
                    CanInsert = existing?.CanInsert ?? false,
                    CanUpdate = existing?.CanUpdate ?? false,
                    CanDelete = existing?.CanDelete ?? false
                };
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Roles, "update")]
    public async Task<IActionResult> Edit(int id, RoleFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
            return View(EnsurePermissionRows(model));

        var role = await _db.AccessRoles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id);
        if (role == null) return NotFound();

        role.Name = model.Name;
        role.Description = model.Description;
        role.IsActive = model.IsActive;

        _db.RolePermissions.RemoveRange(role.Permissions);
        role.Permissions = model.Permissions.Select(p => new RolePermission
        {
            AccessRoleId = role.Id,
            Module = p.Module,
            CanView = p.CanView,
            CanInsert = p.CanInsert,
            CanUpdate = p.CanUpdate,
            CanDelete = p.CanDelete
        }).ToList();

        await _db.SaveChangesAsync();
        TempData["Success"] = "Role updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Roles, "delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.AccessRoles.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == id);
        if (role == null) return NotFound();
        if (role.Members.Any())
        {
            TempData["Error"] = "Cannot delete a role that is assigned to team members.";
            return RedirectToAction(nameof(Index));
        }

        _db.AccessRoles.Remove(role);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Role deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static RoleFormViewModel BlankForm()
        => EnsurePermissionRows(new RoleFormViewModel { IsActive = true });

    private static RoleFormViewModel EnsurePermissionRows(RoleFormViewModel model)
    {
        if (model.Permissions.Count == 0)
        {
            model.Permissions = AppModules.All.Select(m => new RolePermissionRow { Module = m }).ToList();
        }
        return model;
    }
}
