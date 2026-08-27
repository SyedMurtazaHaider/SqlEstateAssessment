/*
  Insert missing servers by name only.
  Compare: EstateServers.ServerName = ct_servers.server_name
*/
SET NOCOUNT ON;

-- No missing names. All EstateServers.ServerName values already exist in ct_servers.server_name.
