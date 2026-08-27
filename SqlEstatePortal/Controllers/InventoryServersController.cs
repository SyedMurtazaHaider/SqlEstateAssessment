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
public class InventoryServersController : Controller
{
    private readonly AppDbContext _db;
    private readonly ServerReachabilityService _reachability;

    public InventoryServersController(AppDbContext db, ServerReachabilityService reachability)
    {
        _db = db;
        _reachability = reachability;
    }

    [RequirePermission(AppModules.InventoryServers, "view")]
    public async Task<IActionResult> Index(
        string? serverName,
        string? environment,
        string? status,
        string? tower,
        string? subscription,
        string? dataCentre)
    {
        serverName = Norm(serverName);
        environment = Norm(environment);
        status = Norm(status);
        tower = Norm(tower);
        subscription = Norm(subscription);
        dataCentre = Norm(dataCentre);

        var all = await _db.CtServers.AsNoTracking()
            .OrderBy(s => s.ServerName)
            .ToListAsync();

        var dbStats = await _db.Database.SqlQueryRaw<ServerDbStatRow>(
            """
            SELECT
                server_name AS ServerName,
                COUNT(*) AS DatabaseCount,
                ISNULL(SUM(CASE WHEN database_status IN (N'Normal', N'Online') THEN 1 ELSE 0 END), 0) AS OnlineDatabaseCount,
                COUNT(DISTINCT NULLIF(LTRIM(RTRIM(elastic_pool_name)), N'')) AS PoolCount
            FROM dbo.ct_database
            WHERE server_name IS NOT NULL AND LTRIM(RTRIM(server_name)) <> N''
            GROUP BY server_name
            """).ToListAsync();

        var statsLookup = dbStats.ToDictionary(
            x => x.ServerName,
            x => x,
            StringComparer.OrdinalIgnoreCase);

        var appLinkRows = await _db.Database.SqlQueryRaw<ServerAppLinkRow>(
            """
            SELECT server_id AS ServerId, server_name AS ServerName, application_id AS ApplicationId
            FROM dbo.ct_application_server
            """).ToListAsync();

        var filtered = all.AsEnumerable();
        if (serverName != null)
            filtered = filtered.Where(s => string.Equals(s.ServerName, serverName, StringComparison.OrdinalIgnoreCase));
        if (environment != null)
            filtered = filtered.Where(s => string.Equals(s.Environment, environment, StringComparison.OrdinalIgnoreCase));
        if (status != null)
            filtered = filtered.Where(s => string.Equals(s.ServerStatus, status, StringComparison.OrdinalIgnoreCase));
        if (tower != null)
            filtered = filtered.Where(s => string.Equals(s.Tower, tower, StringComparison.OrdinalIgnoreCase));
        if (subscription != null)
            filtered = filtered.Where(s => string.Equals(s.Subscription, subscription, StringComparison.OrdinalIgnoreCase));
        if (dataCentre != null)
            filtered = filtered.Where(s => string.Equals(s.DataCentreLocation, dataCentre, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();

        int LinkedAppCount(CtServer s) =>
            appLinkRows.Count(l =>
                (l.ServerId.HasValue && l.ServerId.Value == s.TxId) ||
                (!l.ServerId.HasValue && string.Equals(l.ServerName, s.ServerName, StringComparison.OrdinalIgnoreCase)));

        var vm = new ServerRegisterViewModel
        {
            ServerName = serverName,
            Environment = environment,
            Status = status,
            Tower = tower,
            Subscription = subscription,
            DataCentre = dataCentre,
            TotalCount = all.Count,
            ServerNameOptions = DistinctSorted(all.Select(s => s.ServerName)),
            EnvironmentOptions = DistinctSorted(all.Select(s => s.Environment)),
            StatusOptions = DistinctSorted(all.Select(s => s.ServerStatus)),
            TowerOptions = DistinctSorted(all.Select(s => s.Tower)),
            SubscriptionOptions = DistinctSorted(all.Select(s => s.Subscription)),
            DataCentreOptions = DistinctSorted(all.Select(s => s.DataCentreLocation)),
            Servers = list.Select(s =>
            {
                statsLookup.TryGetValue(s.ServerName, out var stats);
                return new ServerRowViewModel
                {
                    TxId = s.TxId,
                    ServerName = s.ServerName,
                    Environment = s.Environment,
                    ServerStatus = s.ServerStatus,
                    Tower = s.Tower,
                    Subscription = s.Subscription,
                    DataCentreLocation = s.DataCentreLocation,
                    DatabaseCount = stats?.DatabaseCount ?? 0,
                    OnlineDatabaseCount = stats?.OnlineDatabaseCount ?? 0,
                    PoolCount = stats?.PoolCount ?? 0,
                    LinkedApplicationCount = LinkedAppCount(s)
                };
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(AppModules.InventoryServers, "update")]
    public async Task<IActionResult> CheckServerStatus(CancellationToken cancellationToken)
    {
        var result = await _reachability.CheckAllAsync(cancellationToken);
        var message =
            $"Server status checked for {result.Total} servers: {result.Reachable} Reachable, {result.Unreachable} UnReachable.";

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
                unreachable = result.Unreachable,
                redirectUrl = Url.Action(nameof(Index))
            });
        }

        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission(AppModules.InventoryServers, "view")]
    public async Task<IActionResult> Details(int id)
    {
        var server = await _db.CtServers.AsNoTracking().FirstOrDefaultAsync(s => s.TxId == id);
        if (server == null) return NotFound();

        var linkedDatabases = await _db.CtDatabases.AsNoTracking()
            .Where(d => d.ServerName == server.ServerName)
            .OrderBy(d => d.DatabaseName)
            .Select(d => new LinkedDatabaseItemViewModel
            {
                DatabaseId = d.TxId,
                DatabaseName = d.DatabaseName,
                ServerName = d.ServerName,
                Environment = d.Environment,
                DatabaseStatus = d.DatabaseStatus,
                DatabaseEdition = d.DatabaseEdition,
                DataCentreLocation = d.DataCentreLocation
            })
            .ToListAsync();

        var linkedApplications = await _db.Database.SqlQueryRaw<LinkedApplicationItemViewModel>(
            """
            SELECT
                a.id AS ApplicationId,
                a.name AS ApplicationName,
                a.status AS Status,
                a.[function] AS [Function],
                a.application_type AS ApplicationType,
                a.location AS Location,
                a.service_owner AS ServiceOwner,
                a.operating_region AS OperatingRegion
            FROM dbo.ct_application_server l
            INNER JOIN dbo.ct_applications a ON a.id = l.application_id
            WHERE l.server_id = {0}
               OR (l.server_id IS NULL AND LOWER(l.server_name) = LOWER({1}))
            ORDER BY a.name
            """, id, server.ServerName).ToListAsync();

        return View(new ServerDetailsViewModel
        {
            Server = server,
            LinkedDatabases = linkedDatabases,
            LinkedApplications = linkedApplications
        });
    }

    [RequirePermission(AppModules.InventoryServers, "view")]
    public async Task<IActionResult> ServerDatabases(int id)
    {
        var server = await _db.CtServers.AsNoTracking().FirstOrDefaultAsync(s => s.TxId == id);
        if (server == null) return NotFound();

        var databases = await _db.CtDatabases.AsNoTracking()
            .Where(d => d.ServerName == server.ServerName)
            .OrderBy(d => d.DatabaseName)
            .Select(d => new
            {
                databaseId = d.TxId,
                databaseName = d.DatabaseName,
                serverName = d.ServerName,
                environment = d.Environment,
                databaseStatus = d.DatabaseStatus,
                databaseEdition = d.DatabaseEdition,
                serviceObjective = d.CurrentServiceObjectiveName,
                currentSizeMb = d.CurrentSizeMb,
                freeSpaceMb = d.FreeSpaceMb,
                compatibilityLevel = d.CompatibilityLevel,
                recoveryModel = d.RecoveryModel,
                region = d.DataCentreLocation,
                elasticPoolName = d.ElasticPoolName
            })
            .ToListAsync();

        return Json(new
        {
            serverId = id,
            serverName = server.ServerName,
            count = databases.Count,
            databases
        });
    }

    [RequirePermission(AppModules.InventoryServers, "view")]
    public async Task<IActionResult> LinkedApplications(int id)
    {
        var server = await _db.CtServers.AsNoTracking().FirstOrDefaultAsync(s => s.TxId == id);
        if (server == null) return NotFound();

        var applications = await _db.Database.SqlQueryRaw<LinkedApplicationDto>(
            """
            SELECT
                a.id AS ApplicationId,
                a.name AS ApplicationName,
                a.status AS Status,
                a.[function] AS [Function],
                a.application_type AS ApplicationType,
                a.location AS Location,
                a.service_owner AS ServiceOwner,
                a.operating_region AS OperatingRegion,
                l.server_name AS LinkedServerName
            FROM dbo.ct_application_server l
            INNER JOIN dbo.ct_applications a ON a.id = l.application_id
            WHERE l.server_id = {0}
               OR (l.server_id IS NULL AND LOWER(l.server_name) = LOWER({1}))
            ORDER BY a.name
            """, id, server.ServerName).ToListAsync();

        return Json(new
        {
            serverId = id,
            serverName = server.ServerName,
            count = applications.Count,
            applications
        });
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

    private sealed class ServerDbStatRow
    {
        public string ServerName { get; set; } = string.Empty;
        public int DatabaseCount { get; set; }
        public int OnlineDatabaseCount { get; set; }
        public int PoolCount { get; set; }
    }

    private sealed class ServerAppLinkRow
    {
        public int? ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public int ApplicationId { get; set; }
    }

    private sealed class LinkedApplicationDto
    {
        public int ApplicationId { get; set; }
        public string? ApplicationName { get; set; }
        public string? Status { get; set; }
        public string? Function { get; set; }
        public string? ApplicationType { get; set; }
        public string? Location { get; set; }
        public string? ServiceOwner { get; set; }
        public string? OperatingRegion { get; set; }
        public string? LinkedServerName { get; set; }
    }
}
