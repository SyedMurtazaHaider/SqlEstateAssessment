using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class ServersController : Controller
{
    private readonly AppDbContext _db;

    public ServersController(AppDbContext db)
    {
        _db = db;
    }

    [RequirePermission(AppModules.Servers, "view")]
    public async Task<IActionResult> Index()
    {
        var servers = await _db.EstateServers
            .OrderByDescending(x => x.Enabled)
            .ThenBy(x => x.ServerName)
            .ToListAsync();
        return View(servers);
    }

    [RequirePermission(AppModules.Servers, "insert")]
    public IActionResult Create()
    {
        return View("Edit", new EstateServerFormViewModel { Enabled = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Servers, "insert")]
    public async Task<IActionResult> Create(EstateServerFormViewModel model)
    {
        model.ServerName = model.ServerName?.Trim() ?? string.Empty;
        if (!ModelState.IsValid)
            return View("Edit", model);

        if (await _db.EstateServers.AnyAsync(x => x.ServerName == model.ServerName))
        {
            ModelState.AddModelError(nameof(model.ServerName), "Server name already exists.");
            return View("Edit", model);
        }

        _db.EstateServers.Add(new EstateServer
        {
            ServerName = model.ServerName,
            Enabled = model.Enabled,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Server added.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(AppModules.Servers, "update")]
    public async Task<IActionResult> Edit(int id)
    {
        var server = await _db.EstateServers.FindAsync(id);
        if (server == null) return NotFound();

        return View(new EstateServerFormViewModel
        {
            Id = server.Id,
            ServerName = server.ServerName,
            Enabled = server.Enabled,
            Notes = server.Notes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Servers, "update")]
    public async Task<IActionResult> Edit(int id, EstateServerFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        model.ServerName = model.ServerName?.Trim() ?? string.Empty;
        if (!ModelState.IsValid)
            return View(model);

        var server = await _db.EstateServers.FindAsync(id);
        if (server == null) return NotFound();

        if (await _db.EstateServers.AnyAsync(x => x.ServerName == model.ServerName && x.Id != id))
        {
            ModelState.AddModelError(nameof(model.ServerName), "Server name already exists.");
            return View(model);
        }

        server.ServerName = model.ServerName;
        server.Enabled = model.Enabled;
        server.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        server.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Server updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Servers, "update")]
    public async Task<IActionResult> ToggleEnabled(int id)
    {
        var server = await _db.EstateServers.FindAsync(id);
        if (server == null) return NotFound();

        server.Enabled = !server.Enabled;
        server.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = server.Enabled
            ? $"Enabled {server.ServerName}."
            : $"Disabled {server.ServerName}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Servers, "delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var server = await _db.EstateServers.FindAsync(id);
        if (server == null) return NotFound();
        _db.EstateServers.Remove(server);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Server deleted.";
        return RedirectToAction(nameof(Index));
    }
}
