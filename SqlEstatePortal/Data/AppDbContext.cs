using Microsoft.EntityFrameworkCore;
using SqlEstatePortal.Models;

namespace SqlEstatePortal.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<AccessRole> AccessRoles => Set<AccessRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<EstateServer> EstateServers => Set<EstateServer>();
    public DbSet<CtApplication> CtApplications => Set<CtApplication>();
    public DbSet<CtDatabase> CtDatabases => Set<CtDatabase>();
    public DbSet<CtServer> CtServers => Set<CtServer>();
    public DbSet<CtCost> CtCosts => Set<CtCost>();
    public DbSet<AssessmentRun> AssessmentRuns => Set<AssessmentRun>();
    public DbSet<AssessmentFinding> AssessmentFindings => Set<AssessmentFinding>();
    public DbSet<AssessmentServerSnapshot> AssessmentServerSnapshots => Set<AssessmentServerSnapshot>();
    public DbSet<AssessmentDatabase> AssessmentDatabases => Set<AssessmentDatabase>();
    public DbSet<AssessmentVolume> AssessmentVolumes => Set<AssessmentVolume>();
    public DbSet<AssessmentService> AssessmentServices => Set<AssessmentService>();
    public DbSet<AssessmentWait> AssessmentWaits => Set<AssessmentWait>();
    public DbSet<AssessmentJob> AssessmentJobs => Set<AssessmentJob>();
    public DbSet<AssessmentSysadmin> AssessmentSysadmins => Set<AssessmentSysadmin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeamMember>()
            .HasIndex(x => x.Username)
            .IsUnique();

        modelBuilder.Entity<EstateServer>()
            .HasIndex(x => x.ServerName)
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasIndex(x => new { x.AccessRoleId, x.Module })
            .IsUnique();

        modelBuilder.Entity<TeamMember>()
            .HasOne(x => x.AccessRole)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.AccessRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TeamMember>()
            .HasOne(x => x.Team)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RolePermission>()
            .HasOne(x => x.AccessRole)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.AccessRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentFinding>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Findings)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentServerSnapshot>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Servers)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentDatabase>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Databases)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentVolume>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Volumes)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentService>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Services)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentWait>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Waits)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentJob>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Jobs)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentSysadmin>()
            .HasOne(x => x.AssessmentRun)
            .WithMany(x => x.Sysadmins)
            .HasForeignKey(x => x.AssessmentRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssessmentServerSnapshot>().Property(x => x.MemoryMb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentServerSnapshot>().Property(x => x.AllocatedGb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentServerSnapshot>().Property(x => x.BatchRequestsPerSec).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentRun>().Property(x => x.AllocatedStorageGb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentDatabase>().Property(x => x.DataMb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentDatabase>().Property(x => x.LogMb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentVolume>().Property(x => x.TotalGb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentVolume>().Property(x => x.FreeGb).HasPrecision(18, 2);
        modelBuilder.Entity<AssessmentVolume>().Property(x => x.FreePct).HasPrecision(8, 2);
        modelBuilder.Entity<AssessmentWait>().Property(x => x.WaitPct).HasPrecision(8, 2);
    }
}
