using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AssessmentOptions>(builder.Configuration.GetSection("Assessment"));
builder.Services.PostConfigure<AssessmentOptions>(options =>
{
    string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, path));
    }

    options.ScriptPath = Resolve(options.ScriptPath);
    options.ServerListPath = Resolve(options.ServerListPath);
    options.WorkingDirectory = Resolve(options.WorkingDirectory);
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<AssessmentRunnerService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await AssessmentSchema.ApplyAsync(db);
    await DbSeeder.SeedAsync(db);

    var json = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "reports", "sql-estate-20260824-182501.json"));
    var html = Path.ChangeExtension(json, ".html");
    if (File.Exists(json))
    {
        var runner = scope.ServiceProvider.GetRequiredService<AssessmentRunnerService>();
        await runner.ImportFileAsync(json, "html-report", File.Exists(html) ? html : null, force: false);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
