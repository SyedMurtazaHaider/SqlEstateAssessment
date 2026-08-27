/*
  Insert servers from dbo.EstateServers into dbo.ct_servers
  that are missing by name only.

  Compare: EstateServers.ServerName = ct_servers.server_name
  Insert:  server_name only (other columns use table defaults / NULL)
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

INSERT INTO dbo.ct_servers (server_name)
SELECT e.ServerName
FROM dbo.EstateServers AS e
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.ct_servers AS c
    WHERE c.server_name = e.ServerName
);

DECLARE @Inserted int = @@ROWCOUNT;

COMMIT TRAN;

SELECT @Inserted AS RowsInserted;
SELECT COUNT(*) AS CtServersTotal FROM dbo.ct_servers;
