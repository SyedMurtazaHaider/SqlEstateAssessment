using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.Services;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class InventorySyncController : Controller
{
    private readonly AppDbContext _db;
    private readonly InventorySyncService _sync;

    public InventorySyncController(AppDbContext db, InventorySyncService sync)
    {
        _db = db;
        _sync = sync;
    }

    [RequirePermission(AppModules.Databases, "view")]
    public async Task<IActionResult> Index(string? status)
    {
        var q = _db.InventorySyncBatches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(b => b.Status == status);

        var batches = await q
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(100)
            .ToListAsync();

        ViewBag.Status = status;
        return View(batches);
    }

    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> Generate(int assessmentRunId)
    {
        try
        {
            var existing = await _db.InventorySyncBatches.AsNoTracking()
                .Where(b => b.AssessmentRunId == assessmentRunId)
                .OrderByDescending(b => b.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                if (string.Equals(existing.Status, InventorySyncService.StatusPending, StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction(nameof(Review), new { id = existing.Id });

                if (string.Equals(existing.Status, InventorySyncService.StatusApplied, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(existing.Status, InventorySyncService.StatusRejected, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = $"Assessment #{assessmentRunId} sync is already {existing.Status.ToLowerInvariant()}.";
                    return RedirectToAction("Index", "Assessments");
                }
            }

            var actor = User.Identity?.Name ?? "unknown";
            var batch = await _sync.GenerateAsync(assessmentRunId, actor);
            TempData["Success"] = $"Sync batch #{batch.Id} created: {batch.NewCount} new, {batch.ChangedCount} changed, {batch.RemovedCount} removed.";
            return RedirectToAction(nameof(Review), new { id = batch.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Details", "Assessments", new { id = assessmentRunId });
        }
    }

    [RequirePermission(AppModules.Databases, "view")]
    public async Task<IActionResult> Review(int id)
    {
        var batch = await _db.InventorySyncBatches
            .Include(b => b.Items).ThenInclude(i => i.Fields)
            .Include(b => b.Audits)
            .Include(b => b.AssessmentRun)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id);
        if (batch == null) return NotFound();

        ViewBag.CanApply = await CanApplyAsync();
        return View(batch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Databases, "update")]
    public async Task<IActionResult> Save(int id, IFormCollection form)
    {
        try
        {
            var updates = ParseSelections(form);
            await _sync.UpdateSelectionsAsync(id, updates, User.Identity?.Name ?? "unknown");
            TempData["Success"] = "Selections saved.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Databases, "update")]
    public async Task<IActionResult> Approve(int id, IFormCollection form)
    {
        try
        {
            var updates = ParseSelections(form);
            var actor = User.Identity?.Name ?? "unknown";
            await _sync.UpdateSelectionsAsync(id, updates, actor);
            var (inserted, updated, unlinked) = await _sync.ApproveAndApplyAsync(id, actor);
            TempData["Success"] = $"Applied sync #{id}: {inserted} inserted, {updated} updated, {unlinked} unlinked.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Databases, "update")]
    public async Task<IActionResult> Reject(int id, string? notes)
    {
        try
        {
            await _sync.RejectAsync(id, User.Identity?.Name ?? "unknown", notes);
            TempData["Success"] = $"Sync batch #{id} rejected.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Review), new { id });
    }

    private Task<bool> CanApplyAsync()
    {
        // Permission already gated on POST actions; UI only needs auth for show/hide.
        return Task.FromResult(User.Identity?.IsAuthenticated == true);
    }

    private static List<InventorySyncService.SelectionUpdate> ParseSelections(IFormCollection form)
    {
        var itemIds = form["itemId"].Select(v => int.TryParse(v, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();
        var selectedItems = new HashSet<int>(
            form["selectedItem"].Select(v => int.TryParse(v, out var id) ? id : 0).Where(id => id > 0));

        var updates = new List<InventorySyncService.SelectionUpdate>();
        foreach (var itemId in itemIds)
        {
            var fieldMap = new Dictionary<int, bool>();
            var fieldIds = form[$"fieldId_{itemId}"];
            var selectedFields = new HashSet<int>(
                form[$"selectedField_{itemId}"].Select(v => int.TryParse(v, out var id) ? id : 0).Where(id => id > 0));

            foreach (var fidRaw in fieldIds)
            {
                if (!int.TryParse(fidRaw, out var fid) || fid <= 0) continue;
                fieldMap[fid] = selectedFields.Contains(fid);
            }

            updates.Add(new InventorySyncService.SelectionUpdate
            {
                ItemId = itemId,
                Selected = selectedItems.Contains(itemId),
                FieldSelections = fieldMap
            });
        }
        return updates;
    }
}
