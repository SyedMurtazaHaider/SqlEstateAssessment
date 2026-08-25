using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class TeamMembersController : Controller
{
    private readonly AppDbContext _db;

    public TeamMembersController(AppDbContext db)
    {
        _db = db;
    }

    [RequirePermission(AppModules.TeamMembers, "view")]
    public async Task<IActionResult> Index()
    {
        var members = await _db.TeamMembers
            .Include(x => x.AccessRole)
            .Include(x => x.Team)
            .OrderBy(x => x.MemberName)
            .ToListAsync();
        return View(members);
    }

    [RequirePermission(AppModules.TeamMembers, "insert")]
    public async Task<IActionResult> Create()
    {
        var vm = await BuildFormAsync(new TeamMemberFormViewModel
        {
            Status = "Active",
            GeneratedPassword = GeneratePassword()
        });
        return View("Edit", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.TeamMembers, "insert")]
    public async Task<IActionResult> Create(TeamMemberFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await BuildFormAsync(model);
            return View("Edit", model);
        }

        if (await _db.TeamMembers.AnyAsync(x => x.Username == model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Username already exists.");
            await BuildFormAsync(model);
            return View("Edit", model);
        }

        if (string.IsNullOrWhiteSpace(model.GeneratedPassword))
            model.GeneratedPassword = GeneratePassword();

        _db.TeamMembers.Add(new TeamMember
        {
            MemberName = model.MemberName,
            AccessRoleId = model.AccessRoleId,
            Username = model.Username,
            Email = model.Email,
            TeamId = model.TeamId,
            Designation = model.Designation,
            AdminAccess = model.AdminAccess,
            Status = model.Status,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.GeneratedPassword),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Team member saved. Temporary password: {model.GeneratedPassword}";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(AppModules.TeamMembers, "update")]
    public async Task<IActionResult> Edit(int id)
    {
        var member = await _db.TeamMembers.FindAsync(id);
        if (member == null) return NotFound();

        var vm = await BuildFormAsync(new TeamMemberFormViewModel
        {
            Id = member.Id,
            MemberName = member.MemberName,
            AccessRoleId = member.AccessRoleId,
            Username = member.Username,
            Email = member.Email,
            TeamId = member.TeamId,
            Designation = member.Designation,
            AdminAccess = member.AdminAccess,
            Status = member.Status,
            GeneratedPassword = "(unchanged)"
        });
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.TeamMembers, "update")]
    public async Task<IActionResult> Edit(int id, TeamMemberFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            await BuildFormAsync(model);
            return View(model);
        }

        var member = await _db.TeamMembers.FindAsync(id);
        if (member == null) return NotFound();

        if (await _db.TeamMembers.AnyAsync(x => x.Username == model.Username && x.Id != id))
        {
            ModelState.AddModelError(nameof(model.Username), "Username already exists.");
            await BuildFormAsync(model);
            return View(model);
        }

        member.MemberName = model.MemberName;
        member.AccessRoleId = model.AccessRoleId;
        member.Username = model.Username;
        member.Email = model.Email;
        member.TeamId = model.TeamId;
        member.Designation = model.Designation;
        member.AdminAccess = model.AdminAccess;
        member.Status = model.Status;
        member.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(model.GeneratedPassword) &&
            model.GeneratedPassword != "(unchanged)")
        {
            member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.GeneratedPassword);
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Team member updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.TeamMembers, "delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var member = await _db.TeamMembers.FindAsync(id);
        if (member == null) return NotFound();
        _db.TeamMembers.Remove(member);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Team member deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<TeamMemberFormViewModel> BuildFormAsync(TeamMemberFormViewModel model)
    {
        model.RoleOptions = await _db.AccessRoles
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
        model.TeamOptions = await _db.Teams
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
        return model;
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 12).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
