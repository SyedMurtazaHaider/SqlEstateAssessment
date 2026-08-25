using Microsoft.EntityFrameworkCore;

namespace SqlEstatePortal.Data;

public static class AssessmentSchema
{
    public static async Task ApplyAsync(AppDbContext db)
    {
        var statements = new[]
        {
            "IF COL_LENGTH('AssessmentRuns','UnreachableCount') IS NULL ALTER TABLE AssessmentRuns ADD UnreachableCount int NOT NULL CONSTRAINT DF_AR_Unreachable DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','EndOfSupportCount') IS NULL ALTER TABLE AssessmentRuns ADD EndOfSupportCount int NOT NULL CONSTRAINT DF_AR_Eos DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','AllocatedStorageGb') IS NULL ALTER TABLE AssessmentRuns ADD AllocatedStorageGb decimal(18,2) NOT NULL CONSTRAINT DF_AR_Gb DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','EstimatedLicensedCores') IS NULL ALTER TABLE AssessmentRuns ADD EstimatedLicensedCores int NOT NULL CONSTRAINT DF_AR_Cores DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','InfoCount') IS NULL ALTER TABLE AssessmentRuns ADD InfoCount int NOT NULL CONSTRAINT DF_AR_Info DEFAULT 0;",
            "IF COL_LENGTH('AssessmentRuns','HtmlContent') IS NULL ALTER TABLE AssessmentRuns ADD HtmlContent nvarchar(max) NULL;",
            @"IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID('AssessmentFindings') AND c.name = 'Finding' AND t.name = N'nvarchar' AND c.max_length <> -1)
              ALTER TABLE AssessmentFindings ALTER COLUMN Finding nvarchar(max) NOT NULL;",
            @"IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID('AssessmentFindings') AND c.name = 'Recommendation' AND t.name = N'nvarchar' AND c.max_length <> -1)
              ALTER TABLE AssessmentFindings ALTER COLUMN Recommendation nvarchar(max) NOT NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','UserDatabaseCount') IS NULL ALTER TABLE AssessmentServerSnapshots ADD UserDatabaseCount int NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','StartedAt') IS NULL ALTER TABLE AssessmentServerSnapshots ADD StartedAt datetime2 NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','UserConnections') IS NULL ALTER TABLE AssessmentServerSnapshots ADD UserConnections int NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','PageLifeExpectancySec') IS NULL ALTER TABLE AssessmentServerSnapshots ADD PageLifeExpectancySec int NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','BatchRequestsPerSec') IS NULL ALTER TABLE AssessmentServerSnapshots ADD BatchRequestsPerSec decimal(18,2) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','HostPlatform') IS NULL ALTER TABLE AssessmentServerSnapshots ADD HostPlatform nvarchar(80) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','HostDistribution') IS NULL ALTER TABLE AssessmentServerSnapshots ADD HostDistribution nvarchar(120) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','Collation') IS NULL ALTER TABLE AssessmentServerSnapshots ADD Collation nvarchar(80) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','VirtualMachineType') IS NULL ALTER TABLE AssessmentServerSnapshots ADD VirtualMachineType nvarchar(50) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','LicenseType') IS NULL ALTER TABLE AssessmentServerSnapshots ADD LicenseType nvarchar(50) NULL;",
            "IF COL_LENGTH('AssessmentServerSnapshots','Error') IS NOT NULL ALTER TABLE AssessmentServerSnapshots ALTER COLUMN Error nvarchar(max) NULL;",
            @"IF OBJECT_ID('AssessmentDatabases','U') IS NULL
              CREATE TABLE AssessmentDatabases (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                Name nvarchar(128) NOT NULL,
                State nvarchar(60) NULL,
                RecoveryModel nvarchar(30) NULL,
                CompatibilityLevel int NULL,
                PageVerify nvarchar(30) NULL,
                IsEncrypted bit NOT NULL,
                DataMb decimal(18,2) NULL,
                LogMb decimal(18,2) NULL,
                OwnerName nvarchar(128) NULL,
                LastGoodCheckDbTime datetime2 NULL
              );",
            @"IF OBJECT_ID('AssessmentVolumes','U') IS NULL
              CREATE TABLE AssessmentVolumes (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                MountPoint nvarchar(200) NOT NULL,
                LogicalName nvarchar(100) NULL,
                TotalGb decimal(18,2) NULL,
                FreeGb decimal(18,2) NULL,
                FreePct decimal(8,2) NULL
              );",
            @"IF OBJECT_ID('AssessmentServices','U') IS NULL
              CREATE TABLE AssessmentServices (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                ServiceName nvarchar(200) NOT NULL,
                StartupType nvarchar(50) NULL,
                Status nvarchar(50) NULL,
                ServiceAccount nvarchar(200) NULL,
                InstantFileInitialization nvarchar(10) NULL
              );",
            @"IF OBJECT_ID('AssessmentWaits','U') IS NULL
              CREATE TABLE AssessmentWaits (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                WaitType nvarchar(120) NOT NULL,
                WaitingTasks bigint NOT NULL,
                WaitTimeMs bigint NOT NULL,
                SignalWaitTimeMs bigint NOT NULL,
                WaitPct decimal(8,2) NULL
              );",
            @"IF OBJECT_ID('AssessmentJobs','U') IS NULL
              CREATE TABLE AssessmentJobs (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                JobName nvarchar(200) NOT NULL,
                Enabled bit NOT NULL,
                LastRunStatus nvarchar(50) NULL,
                LastRun datetime2 NULL,
                Message nvarchar(max) NULL
              );",
            @"IF OBJECT_ID('AssessmentSysadmins','U') IS NULL
              CREATE TABLE AssessmentSysadmins (
                Id int IDENTITY PRIMARY KEY,
                AssessmentRunId int NOT NULL REFERENCES AssessmentRuns(Id) ON DELETE CASCADE,
                ServerName nvarchar(200) NOT NULL,
                Name nvarchar(200) NOT NULL,
                TypeDesc nvarchar(50) NULL,
                IsDisabled bit NOT NULL,
                CreateDate datetime2 NULL
              );",
            @"IF OBJECT_ID('EstateServers','U') IS NULL
              CREATE TABLE EstateServers (
                Id int IDENTITY PRIMARY KEY,
                ServerName nvarchar(200) NOT NULL,
                Enabled bit NOT NULL CONSTRAINT DF_EstateServers_Enabled DEFAULT 1,
                Notes nvarchar(500) NULL,
                CreatedAt datetime2 NOT NULL,
                UpdatedAt datetime2 NULL
              );",
            @"IF OBJECT_ID('EstateServers','U') IS NOT NULL
              AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_EstateServers_ServerName' AND object_id = OBJECT_ID('EstateServers'))
              CREATE UNIQUE INDEX IX_EstateServers_ServerName ON EstateServers(ServerName);"
        };

        foreach (var sql in statements)
            await db.Database.ExecuteSqlRawAsync(sql);
    }
}
