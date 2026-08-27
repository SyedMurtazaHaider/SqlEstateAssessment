using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class ApplicationsController : Controller
{
    private readonly AppDbContext _db;

    public ApplicationsController(AppDbContext db)
    {
        _db = db;
    }

    [RequirePermission(AppModules.Applications, "view")]
    public async Task<IActionResult> Index(
        string? application,
        string? status,
        string? function,
        string? timeRoadmap,
        string? techGrade,
        string? location,
        string? complianceGrade,
        string? technicalDebt,
        string? operatingRegion,
        string? monitoring,
        string? vendor)
    {
        application = Norm(application);
        status = Norm(status);
        function = Norm(function);
        timeRoadmap = Norm(timeRoadmap);
        techGrade = Norm(techGrade);
        location = Norm(location);
        complianceGrade = Norm(complianceGrade);
        technicalDebt = Norm(technicalDebt);
        operatingRegion = Norm(operatingRegion);
        monitoring = Norm(monitoring);
        vendor = Norm(vendor);

        var all = await _db.CtApplications.AsNoTracking().OrderBy(a => a.Name).ToListAsync();

        var tcoLookup = (await _db.Database.SqlQueryRaw<AppTcoRow>(
                """
                SELECT application_id AS ApplicationId, tco AS Tco
                FROM dbo.ct_costs
                WHERE application_id IS NOT NULL AND tco IS NOT NULL AND LTRIM(RTRIM(tco)) <> N''
                """).ToListAsync())
            .GroupBy(x => x.ApplicationId)
            .ToDictionary(g => g.Key, g => g.First().Tco);

        var linkedLookup = (await _db.Database.SqlQueryRaw<AppLinkCountRow>(
                """
                SELECT application_id AS ApplicationId, COUNT(*) AS [Count]
                FROM dbo.ct_application_database
                GROUP BY application_id
                """).ToListAsync())
            .ToDictionary(x => x.ApplicationId, x => x.Count);

        var linkedServerLookup = (await _db.Database.SqlQueryRaw<AppLinkCountRow>(
                """
                SELECT application_id AS ApplicationId, COUNT(*) AS [Count]
                FROM dbo.ct_application_server
                GROUP BY application_id
                """).ToListAsync())
            .ToDictionary(x => x.ApplicationId, x => x.Count);

        var filtered = all.AsEnumerable();
        if (application != null)
            filtered = filtered.Where(a => string.Equals(a.Name, application, StringComparison.OrdinalIgnoreCase));
        if (status != null)
            filtered = filtered.Where(a => string.Equals(a.Status, status, StringComparison.OrdinalIgnoreCase));
        if (function != null)
            filtered = filtered.Where(a => string.Equals(a.Function, function, StringComparison.OrdinalIgnoreCase));
        if (timeRoadmap != null)
            filtered = filtered.Where(a => string.Equals(a.TimeRoadmap, timeRoadmap, StringComparison.OrdinalIgnoreCase));
        if (techGrade != null)
            filtered = filtered.Where(a => string.Equals(a.TechGrade, techGrade, StringComparison.OrdinalIgnoreCase));
        if (location != null)
            filtered = filtered.Where(a => string.Equals(a.Location, location, StringComparison.OrdinalIgnoreCase));
        if (complianceGrade != null)
            filtered = filtered.Where(a => string.Equals(a.ComplianceGrade, complianceGrade, StringComparison.OrdinalIgnoreCase));
        if (technicalDebt != null)
            filtered = filtered.Where(a => string.Equals(a.TechnicalDebt, technicalDebt, StringComparison.OrdinalIgnoreCase));
        if (operatingRegion != null)
            filtered = filtered.Where(a => string.Equals(a.OperatingRegion, operatingRegion, StringComparison.OrdinalIgnoreCase));
        if (monitoring != null)
            filtered = filtered.Where(a => string.Equals(a.MonitoringGrade, monitoring, StringComparison.OrdinalIgnoreCase));
        if (vendor != null)
            filtered = filtered.Where(a => string.Equals(a.Vendor, vendor, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();

        var vm = new ApplicationRegisterViewModel
        {
            Application = application,
            Status = status,
            Function = function,
            TimeRoadmap = timeRoadmap,
            TechGrade = techGrade,
            Location = location,
            ComplianceGrade = complianceGrade,
            TechnicalDebt = technicalDebt,
            OperatingRegion = operatingRegion,
            Monitoring = monitoring,
            Vendor = vendor,
            TotalCount = all.Count,
            ApplicationOptions = DistinctSorted(all.Select(a => a.Name)),
            StatusOptions = DistinctSorted(all.Select(a => a.Status)),
            FunctionOptions = DistinctSorted(all.Select(a => a.Function)),
            TimeRoadmapOptions = DistinctSorted(all.Select(a => a.TimeRoadmap)),
            TechGradeOptions = DistinctSorted(all.Select(a => a.TechGrade)),
            LocationOptions = DistinctSorted(all.Select(a => a.Location)),
            ComplianceGradeOptions = DistinctSorted(all.Select(a => a.ComplianceGrade)),
            TechnicalDebtOptions = DistinctSorted(all.Select(a => a.TechnicalDebt)),
            OperatingRegionOptions = DistinctSorted(all.Select(a => a.OperatingRegion)),
            MonitoringOptions = DistinctSorted(all.Select(a => a.MonitoringGrade)),
            VendorOptions = DistinctSorted(all.Select(a => a.Vendor)),
            Applications = list.Select(a => new ApplicationRowViewModel
            {
                Id = a.Id,
                Name = string.IsNullOrWhiteSpace(a.Name) ? $"(unnamed #{a.Id})" : a.Name!,
                Status = a.Status,
                Function = a.Function,
                ApplicationType = a.ApplicationType,
                TimeRoadmap = a.TimeRoadmap,
                TechGrade = a.TechGrade,
                ComplianceGrade = a.ComplianceGrade,
                Location = a.Location,
                BusinessCriticality = a.BusinessCriticality,
                Tco = tcoLookup.GetValueOrDefault(a.Id),
                LinkedDbCount = linkedLookup.GetValueOrDefault(a.Id),
                LinkedServerCount = linkedServerLookup.GetValueOrDefault(a.Id),
                ServiceOwner = a.ServiceOwner,
                OperatingRegion = a.OperatingRegion,
                Vendor = a.Vendor,
                TechnicalDebt = a.TechnicalDebt,
                MonitoringGrade = a.MonitoringGrade
            }).ToList()
        };

        return View(vm);
    }

    [RequirePermission(AppModules.Applications, "view")]
    public async Task<IActionResult> Details(int id)
    {
        var app = await _db.CtApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        var linkedDatabases = await _db.Database.SqlQueryRaw<LinkedDatabaseItemViewModel>(
            """
            SELECT
                d.tx_id AS DatabaseId,
                d.database_name AS DatabaseName,
                d.server_name AS ServerName,
                d.environment AS Environment,
                d.database_status AS DatabaseStatus,
                d.database_edition AS DatabaseEdition,
                d.data_centre_location AS DataCentreLocation
            FROM dbo.ct_application_database l
            INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
            WHERE l.application_id = {0}
            ORDER BY d.database_name
            """, id).ToListAsync();

        var linkedServers = await _db.Database.SqlQueryRaw<LinkedServerItemViewModel>(
            """
            SELECT
                l.server_id AS ServerId,
                l.server_name AS ServerName,
                s.environment AS Environment,
                s.server_status AS ServerStatus,
                s.tower AS Tower,
                s.subscription AS Subscription,
                s.data_centre_location AS DataCentreLocation
            FROM dbo.ct_application_server l
            LEFT JOIN dbo.ct_servers s ON s.tx_id = l.server_id
            WHERE l.application_id = {0}
            ORDER BY l.server_name
            """, id).ToListAsync();

        return View(new ApplicationDetailsViewModel
        {
            Application = app,
            LinkedDatabases = linkedDatabases,
            LinkedServers = linkedServers
        });
    }

    [RequirePermission(AppModules.Applications, "view")]
    public async Task<IActionResult> LinkedDatabases(int id)
    {
        var app = await _db.CtApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        var databases = await _db.Database.SqlQueryRaw<LinkedDatabaseDto>(
            """
            SELECT
                l.id AS LinkId,
                d.tx_id AS DatabaseId,
                d.database_name AS DatabaseName,
                d.server_name AS ServerName,
                d.environment AS Environment,
                d.database_status AS DatabaseStatus,
                d.database_edition AS DatabaseEdition,
                d.current_service_objective_name AS ServiceObjective,
                d.current_size_mb AS CurrentSizeMb,
                d.free_space_mb AS FreeSpaceMb,
                d.compatibility_level AS CompatibilityLevel,
                d.recovery_model AS RecoveryModel,
                d.data_centre_location AS Region
            FROM dbo.ct_application_database l
            INNER JOIN dbo.ct_database d ON d.tx_id = l.database_id
            WHERE l.application_id = {0}
            ORDER BY d.database_name
            """, id).ToListAsync();

        return Json(new
        {
            applicationId = id,
            applicationName = app.Name ?? $"#{id}",
            count = databases.Count,
            databases
        });
    }

    [RequirePermission(AppModules.Applications, "view")]
    public async Task<IActionResult> LinkedServers(int id)
    {
        var app = await _db.CtApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();

        var servers = await _db.Database.SqlQueryRaw<LinkedServerDto>(
            """
            SELECT
                l.id AS LinkId,
                l.server_id AS ServerId,
                l.server_name AS ServerName,
                l.source_text AS SourceText,
                s.environment AS Environment,
                s.server_status AS ServerStatus,
                s.tower AS Tower,
                s.subscription AS Subscription,
                s.data_centre_location AS DataCentreLocation
            FROM dbo.ct_application_server l
            LEFT JOIN dbo.ct_servers s ON s.tx_id = l.server_id
            WHERE l.application_id = {0}
            ORDER BY l.server_name
            """, id).ToListAsync();

        return Json(new
        {
            applicationId = id,
            applicationName = app.Name ?? $"#{id}",
            count = servers.Count,
            servers
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.Applications, "delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var app = await _db.CtApplications.FindAsync(id);
        if (app == null) return NotFound();

        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dbo.ct_application_database WHERE application_id = {0}", id);
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dbo.ct_application_server WHERE application_id = {0}", id);
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dbo.ct_costs WHERE application_id = {0}", id);

        _db.CtApplications.Remove(app);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Deleted {app.Name ?? ("#" + id)}.";
        return RedirectToAction(nameof(Index));
    }

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> DistinctSorted(IEnumerable<string?> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class AppTcoRow
    {
        public int ApplicationId { get; set; }
        public string? Tco { get; set; }
    }

    private sealed class AppLinkCountRow
    {
        public int ApplicationId { get; set; }
        public int Count { get; set; }
    }

    private sealed class LinkedDatabaseDto
    {
        public int LinkId { get; set; }
        public int DatabaseId { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string? ServerName { get; set; }
        public string? Environment { get; set; }
        public string? DatabaseStatus { get; set; }
        public string? DatabaseEdition { get; set; }
        public string? ServiceObjective { get; set; }
        public int? CurrentSizeMb { get; set; }
        public int? FreeSpaceMb { get; set; }
        public string? CompatibilityLevel { get; set; }
        public string? RecoveryModel { get; set; }
        public string? Region { get; set; }
    }

    private sealed class LinkedServerDto
    {
        public int LinkId { get; set; }
        public int? ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string? SourceText { get; set; }
        public string? Environment { get; set; }
        public string? ServerStatus { get; set; }
        public string? Tower { get; set; }
        public string? Subscription { get; set; }
        public string? DataCentreLocation { get; set; }
    }
}
