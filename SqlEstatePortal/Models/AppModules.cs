namespace SqlEstatePortal.Models;

public static class AppModules
{
    public const string Dashboard = "Dashboard";
    public const string TeamMembers = "TeamMembers";
    public const string Roles = "Roles";
    public const string Assessments = "Assessments";
    public const string Servers = "Servers";
    public const string Applications = "Applications";
    public const string Databases = "Databases";
    public const string InventoryServers = "InventoryServers";
    public const string Costs = "Costs";

    public static readonly string[] All =
    [
        Dashboard,
        TeamMembers,
        Roles,
        Assessments,
        Servers,
        Applications,
        Databases,
        InventoryServers,
        Costs
    ];
}
