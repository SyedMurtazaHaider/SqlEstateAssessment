using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Data;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.Services;

public class ServerReachabilityResult
{
    public int Total { get; set; }
    public int Reachable { get; set; }
    public int Unreachable { get; set; }
}

public class ServerReachabilityService
{
    public const string StatusReachable = "Reachable";
    public const string StatusUnreachable = "UnReachable";

    private readonly AppDbContext _db;
    private readonly ILogger<ServerReachabilityService> _logger;

    public ServerReachabilityService(AppDbContext db, ILogger<ServerReachabilityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ServerReachabilityResult> CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _db.CtServers.ToListAsync(cancellationToken);
        var result = new ServerReachabilityResult { Total = servers.Count };
        if (servers.Count == 0)
            return result;

        var updates = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();

        await Parallel.ForEachAsync(
            servers,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 16,
                CancellationToken = cancellationToken
            },
            async (server, ct) =>
            {
                var host = ResolvePingHost(server);
                var reachable = await PingHostAsync(host, ct);
                updates[server.TxId] = reachable;
            });

        var now = DateTime.UtcNow;
        foreach (var server in servers)
        {
            var reachable = updates.TryGetValue(server.TxId, out var ok) && ok;
            server.ServerStatus = reachable ? StatusReachable : StatusUnreachable;
            server.UpdatedOn = now;
            server.UpdatedBy = "Check Server Status";
            if (reachable) result.Reachable++;
            else result.Unreachable++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Server reachability check finished. Total={Total}, Reachable={Reachable}, UnReachable={Unreachable}",
            result.Total, result.Reachable, result.Unreachable);
        return result;
    }

    internal static string? ResolvePingHost(CtServer server)
    {
        if (!string.IsNullOrWhiteSpace(server.IpAddress))
            return server.IpAddress.Trim();

        if (!string.IsNullOrWhiteSpace(server.Fqdn))
            return StripInstance(server.Fqdn.Trim());

        if (!string.IsNullOrWhiteSpace(server.ServerName))
            return StripInstance(server.ServerName.Trim());

        return null;
    }

    private static string StripInstance(string value)
    {
        var comma = value.IndexOf(',');
        if (comma >= 0)
            value = value[..comma].Trim();

        var slash = value.IndexOf('\\');
        if (slash >= 0)
            value = value[..slash].Trim();

        return value;
    }

    private static async Task<bool> PingHostAsync(string? host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        // Skip obvious non-host values
        if (host.Contains(' ') || host.Contains('/') || host.Contains('(') || host.StartsWith('.'))
            return false;

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 2000);
            return reply.Status == IPStatus.Success;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
