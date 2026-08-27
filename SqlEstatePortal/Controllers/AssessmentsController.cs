using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.Services;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class AssessmentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly AssessmentRunnerService _runner;
    private readonly ServerReachabilityService _reachability;

    public AssessmentsController(
        AppDbContext db,
        AssessmentRunnerService runner,
        ServerReachabilityService reachability)
    {
        _db = db;
        _runner = runner;
        _reachability = reachability;
    }

    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> Index()
    {
        var runs = await _db.AssessmentRuns
            .OrderByDescending(x => x.StartedAt)
            .Take(50)
            .ToListAsync();
        return View(runs);
    }

    [RequirePermission(AppModules.Assessments, "view")]
    public async Task<IActionResult> Details(int id)
    {
        var run = await _db.AssessmentRuns
            .Include(x => x.Findings)
            .Include(x => x.Servers)
            .Include(x => x.Databases)
            .Include(x => x.Volumes)
            .Include(x => x.Services)
            .Include(x => x.Waits)
            .Include(x => x.Jobs)
            .Include(x => x.Sysadmins)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (run == null) return NotFound();

        var available = await _db.AssessmentRuns
            .OrderByDescending(x => x.StartedAt)
            .Take(50)
            .Select(r => new AssessmentRunSummary
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                Status = r.Status
            })
            .ToListAsync();

        return View(new AssessmentDetailsViewModel
        {
            Run = run,
            AvailableRuns = available
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Assessments, "insert")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? "unknown";
        var run = await _runner.RunAsync(username, cancellationToken);
        var succeeded = run.Status == "Succeeded";
        var message = succeeded
            ? $"Assessment #{run.Id} completed."
            : $"Assessment #{run.Id} failed: {run.ErrorMessage}";

        var wantsJson = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || (Request.Headers.Accept.ToString()?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (wantsJson)
        {
            return Json(new
            {
                ok = succeeded,
                id = run.Id,
                status = run.Status,
                message,
                redirectUrl = Url.Action(nameof(Details), new { id = run.Id })
            });
        }

        TempData[succeeded ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Details), new { id = run.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Assessments, "insert")]
    public async Task<IActionResult> CheckServerStatus(CancellationToken cancellationToken)
    {
        var result = await _reachability.CheckAllAsync(cancellationToken);
        var message =
            $"Server status checked for {result.Total} servers: {result.Reachable} Reachable, {result.Unreachable} UnReachable.";
        TempData["Success"] = message;

        var wantsJson = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || (Request.Headers.Accept.ToString()?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (wantsJson)
        {
            return Json(new
            {
                ok = true,
                message,
                total = result.Total,
                reachable = result.Reachable,
                unreachable = result.Unreachable
            });
        }

        return RedirectToAction(nameof(Index));
    }
}
