using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Filters;
using SqlEstatePortal.Models;
using SqlEstatePortal.ViewModels;

namespace SqlEstatePortal.Controllers;

[Authorize]
public class CostsController : Controller
{
    private readonly AppDbContext _db;

    public CostsController(AppDbContext db)
    {
        _db = db;
    }

    [RequirePermission(AppModules.Costs, "view")]
    public async Task<IActionResult> Index(string? application, string? service, string? grade)
    {
        application = Norm(application);
        service = Norm(service);
        grade = Norm(grade);

        var all = await _db.CtCosts.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        var filtered = all.AsEnumerable();
        if (application != null)
            filtered = filtered.Where(c => string.Equals(c.Name, application, StringComparison.OrdinalIgnoreCase));
        if (service != null)
            filtered = filtered.Where(c => string.Equals(c.ServiceName, service, StringComparison.OrdinalIgnoreCase));
        if (grade != null)
            filtered = filtered.Where(c => string.Equals(c.CostGrade, grade, StringComparison.OrdinalIgnoreCase));

        var list = filtered.Select(c =>
        {
            var hosting = ParseMoney(c.HostingCost);
            var license = ParseMoney(c.LicenseCost);
            var support = ParseMoney(c.SupportCost);
            var change = ParseMoney(c.ChangeCost);
            var tco = ParseMoney(c.Tco);
            return new CostRowViewModel
            {
                Id = c.Id,
                ApplicationId = c.ApplicationId,
                Name = string.IsNullOrWhiteSpace(c.Name) ? $"(unnamed #{c.Id})" : c.Name!,
                ServiceName = c.ServiceName,
                CostGrade = c.CostGrade,
                HostingCost = hosting,
                LicenseCost = license,
                SupportCost = support,
                ChangeCost = change,
                Tco = tco,
                TotalUsers = c.TotalUsers,
                HasHosting = hosting.GetValueOrDefault() > 0,
                HasLicense = license.GetValueOrDefault() > 0,
                HasSupport = support.GetValueOrDefault() > 0,
                HasChange = change.GetValueOrDefault() > 0
            };
        }).ToList();

        var vm = new CostRegisterViewModel
        {
            Application = application,
            Service = service,
            Grade = grade,
            TotalCount = all.Count,
            ApplicationOptions = DistinctSorted(all.Select(c => c.Name)),
            ServiceOptions = DistinctSorted(all.Select(c => c.ServiceName)),
            GradeOptions = DistinctSorted(all.Select(c => c.CostGrade)),
            Costs = list,
            TotalHosting = list.Sum(x => x.HostingCost.GetValueOrDefault()),
            TotalLicense = list.Sum(x => x.LicenseCost.GetValueOrDefault()),
            TotalSupport = list.Sum(x => x.SupportCost.GetValueOrDefault()),
            TotalChange = list.Sum(x => x.ChangeCost.GetValueOrDefault()),
            TotalTco = list.Sum(x => x.Tco.GetValueOrDefault())
        };

        return View(vm);
    }

    [RequirePermission(AppModules.Costs, "view")]
    public async Task<IActionResult> Details(int id)
    {
        var cost = await _db.CtCosts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cost == null) return NotFound();
        return View(cost);
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

    private static decimal? ParseMoney(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = raw.Replace("£", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Trim();
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
    }
}
