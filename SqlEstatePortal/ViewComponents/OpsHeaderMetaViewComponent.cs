using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;

namespace SqlEstatePortal.ViewComponents;

public class OpsHeaderMetaViewComponent : ViewComponent
{
    private readonly AppDbContext _db;

    public OpsHeaderMetaViewComponent(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        DateTime? lastStatusCheck = null;
        try
        {
            lastStatusCheck = await _db.CtServers
                .Where(s => s.StatusCheckedAt != null)
                .MaxAsync(s => s.StatusCheckedAt);
        }
        catch
        {
            // Column may not exist yet on first boot before schema apply finishes elsewhere.
        }

        var lastAssessment = await _db.AssessmentRuns
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new { r.StartedAt, r.CompletedAt, r.Status, r.Id })
            .FirstOrDefaultAsync();

        return View(new OpsHeaderMetaModel
        {
            LastStatusCheckUtc = lastStatusCheck,
            LastAssessmentAtUtc = lastAssessment?.CompletedAt ?? lastAssessment?.StartedAt,
            LastAssessmentStatus = lastAssessment?.Status,
            LastAssessmentId = lastAssessment?.Id
        });
    }
}

public sealed class OpsHeaderMetaModel
{
    public DateTime? LastStatusCheckUtc { get; set; }
    public DateTime? LastAssessmentAtUtc { get; set; }
    public string? LastAssessmentStatus { get; set; }
    public int? LastAssessmentId { get; set; }
}
