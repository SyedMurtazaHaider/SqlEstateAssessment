# Deployment & IIS configuration

Step-by-step guide to deploy **Charles Taylor SQL Estate Management** on another Windows server and host the portal in **IIS**.

Related: see [README.md](README.md) for local development, features, and the PowerShell collector.

---

## Contents

- [A. Deployment steps](#a-deployment-steps)
- [B. Configure IIS](#b-configure-iis)
- [C. After go-live checklist](#c-after-go-live-checklist)
- [D. Troubleshooting](#d-troubleshooting)

---

## A. Deployment steps

### A1. Prerequisites on the target Windows server

Install / confirm:

| Item | Notes |
|------|--------|
| Windows Server | Admin rights |
| SQL Server | Local or remote (portal database) |
| .NET 8 Hosting Bundle | Required for IIS — [download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| PowerShell 5.1+ | Built-in on Windows Server |
| Network access | To portal SQL DB and to SQL instances you will assess |

After installing the Hosting Bundle, restart IIS:

```powershell
net stop was /y
net start w3svc
```

Verify runtimes:

```powershell
dotnet --list-runtimes
```

You should see `Microsoft.AspNetCore.App 8.x`.

---

### A2. Create the folder structure

On the target server, create:

```text
C:\Apps\SqlEstateAssessment\
  SqlEstatePortal\
    Assess-SqlEstate.ps1
    servers.txt
    reports\
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force -Path C:\Apps\SqlEstateAssessment\SqlEstatePortal\reports | Out-Null
```

---

### A3. Publish the web application

On your **build / developer machine** (where the source code lives):

```powershell
cd C:\Users\Murtaza\Projects\SqlEstateAssessment\SqlEstatePortal
dotnet publish -c Release -o C:\Publish\SqlEstatePortal
```

Copy everything from `C:\Publish\SqlEstatePortal\` to:

```text
C:\Apps\SqlEstateAssessment\SqlEstatePortal\
```

Confirm these files exist on the server:

- `SqlEstatePortal.dll`
- `web.config`
- `appsettings.json`
- `Assess-SqlEstate.ps1`
- `wwwroot\` folder

---

### A4. Copy collector files

`dotnet publish` copies `Assess-SqlEstate.ps1` and `servers.example.txt` into the publish output. On the server, in `C:\Apps\SqlEstateAssessment\SqlEstatePortal\`:

| Source | Destination |
|--------|-------------|
| `Assess-SqlEstate.ps1` | Already in the publish folder |
| `servers.example.txt` | Copy to `servers.txt` (rename and edit) |

Edit `servers.txt` — one SQL instance per line:

```text
SQLPROD01
SQLPROD01\INST2
# comments start with #
```

---

### A5. Create the portal database

On the SQL instance that will store portal data:

1. Open SSMS / Azure Data Studio.
2. Create database:

```sql
CREATE DATABASE SqlEstatePortal;
```

3. Note the server name (e.g. `APPSERVER\SQL2022` or `sqlhost.domain.com`).

Tables, roles, and the default admin user are created automatically the first time the website starts.

---

### A6. Configure `appsettings.json`

Edit:

`C:\Apps\SqlEstateAssessment\SqlEstatePortal\appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=TARGET\\INSTANCE;Database=SqlEstatePortal;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Assessment": {
    "ScriptPath": "C:\\Apps\\SqlEstateAssessment\\SqlEstatePortal\\Assess-SqlEstate.ps1",
    "ServerListPath": "C:\\Apps\\SqlEstateAssessment\\SqlEstatePortal\\servers.txt",
    "WorkingDirectory": "C:\\Apps\\SqlEstateAssessment\\SqlEstatePortal",
    "SampleSeconds": 2,
    "TrustServerCertificate": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Replace `TARGET\\INSTANCE` with your portal SQL server.

**SQL authentication** (if not using Windows auth):

```text
Server=TARGET\\INSTANCE;Database=SqlEstatePortal;User Id=portal_user;Password=YourStrongPassword;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Important:

- Use **absolute paths** for `ScriptPath`, `ServerListPath`, and `WorkingDirectory`.
- Do **not** use `ASPNETCORE_ENVIRONMENT=Development` in production.
- You can delete or ignore `appsettings.Development.json` on the server.

---

### A7. Optional smoke test (without IIS)

```powershell
cd C:\Apps\SqlEstateAssessment\SqlEstatePortal
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet SqlEstatePortal.dll --urls http://0.0.0.0:5188
```

Open `http://localhost:5188` and sign in with `admin` / `Admin@123`.

Stop the process (`Ctrl+C`) before configuring IIS on the same port.

---

### A8. Service account rights (before IIS)

Decide which Windows account will run the site (IIS app pool identity). That account needs:

| Resource | Permission |
|----------|------------|
| `C:\Apps\SqlEstateAssessment\SqlEstatePortal\` | Read & execute |
| `C:\Apps\SqlEstateAssessment\SqlEstatePortal\reports\` | Modify (write reports) |
| Database `SqlEstatePortal` | `db_owner` (or equivalent) |
| Assessed SQL servers | Connect + read (Windows auth assessments) |

For production assessments with Windows Authentication, use a **domain service account**.

---

## B. Configure IIS

### B1. Install IIS (if not already installed)

1. Open **Server Manager**.
2. **Manage** → **Add Roles and Features**.
3. Select **Web Server (IIS)**.
4. Include at least:
   - Web Server → Common HTTP Features (Default Document, Static Content, …)
   - Management Tools → **IIS Management Console**
5. Finish the wizard.

---

### B2. Install .NET 8 Hosting Bundle

1. Download **ASP.NET Core 8.0 Hosting Bundle**.
2. Run the installer on the IIS server.
3. Restart IIS:

```powershell
net stop was /y
net start w3svc
```

Without this bundle, ASP.NET Core sites fail with **500.19** / **500.30** / handler errors.

---

### B3. Create the Application Pool

1. Open **IIS Manager** (`inetmgr`).
2. Left tree → **Application Pools**.
3. Right-click → **Add Application Pool…**
4. Set:
   - **Name:** `SqlEstatePortalAppPool`
   - **.NET CLR version:** **No Managed Code** ← required for ASP.NET Core
   - **Managed pipeline mode:** Integrated
5. Click **OK**.

#### Advanced settings

1. Select `SqlEstatePortalAppPool` → **Advanced Settings…**
2. Recommended:
   - **Identity** → Custom account → domain service account (SQL + file rights)
   - **Idle Time-out (minutes):** `0` (optional, keeps pool warm)
   - **Start Mode:** `AlwaysRunning` (optional)

---

### B4. Create the Website

1. In IIS Manager → **Sites**.
2. Right-click → **Add Website…**
3. Set:

| Field | Example value |
|-------|----------------|
| Site name | `SqlEstatePortal` |
| Application pool | `SqlEstatePortalAppPool` |
| Physical path | `C:\Apps\SqlEstateAssessment\SqlEstatePortal` |
| Binding type | `http` |
| IP address | All Unassigned (or a specific IP) |
| Port | `80` (or `5188` if 80 is taken) |
| Host name | blank, or `sqlestate.company.com` |

4. Click **OK**.

#### HTTPS (optional)

1. Select the site → **Bindings…** → **Add…**
2. Type: `https`, port `443`, select your SSL certificate.

---

### B5. Set folder permissions for the app pool

1. In File Explorer, right-click `C:\Apps\SqlEstateAssessment\SqlEstatePortal` → **Properties** → **Security** → **Edit** → **Add…**
2. Add the app-pool identity:
   - Custom domain account: `DOMAIN\SvcSqlEstate`
   - Or built-in pool identity: `IIS AppPool\SqlEstatePortalAppPool`
3. Grant:

| Path | Rights |
|------|--------|
| `C:\Apps\SqlEstateAssessment\SqlEstatePortal` | Read & execute |
| `C:\Apps\SqlEstateAssessment\SqlEstatePortal\reports` | Modify |

4. In SQL Server, map the same Windows login to `SqlEstatePortal` and grant `db_owner` (or suitable roles).

---

### B6. Confirm / edit `web.config`

Open:

`C:\Apps\SqlEstateAssessment\SqlEstatePortal\web.config`

It should look like this (created by `dotnet publish`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore"
             path="*"
             verb="*"
             modules="AspNetCoreModuleV2"
             resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SqlEstatePortal.dll"
                  stdoutLogEnabled="false"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

If `<environmentVariables>` is missing, add the Production entry as above.

---

### B7. Open the firewall

For HTTP on port 80:

```powershell
New-NetFirewallRule -DisplayName "Charles Taylor SQL Estate Management HTTP" `
  -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
```

If you used port `5188`:

```powershell
New-NetFirewallRule -DisplayName "Charles Taylor SQL Estate Management 5188" `
  -Direction Inbound -Protocol TCP -LocalPort 5188 -Action Allow
```

---

### B8. Start the site

1. IIS Manager → select `SqlEstatePortal`.
2. **Manage Website** → **Start** (if stopped).
3. Select `SqlEstatePortalAppPool` → **Recycle**.
4. Browse:
   - `http://localhost/`
   - `http://SERVERNAME/`
   - or `http://SERVERNAME:5188/` if that port was used

---

### B9. Enable stdout logging (only if the site fails)

1. Create folder:

```powershell
New-Item -ItemType Directory -Force -Path C:\Apps\SqlEstateAssessment\SqlEstatePortal\logs
```

2. In `web.config`, set:

```xml
stdoutLogEnabled="true"
```

3. Recycle the app pool.
4. Open the newest file under `logs\stdout_*.log`.
5. After fixing the issue, set `stdoutLogEnabled="false"` again.

---

## C. After go-live checklist

1. Open the site in a browser.  
2. Sign in: **admin** / **Admin@123**.  
3. Top-right → **Change password**.  
4. Confirm **Dashboard** loads (charts / KPIs).  
5. Confirm `servers.txt` lists the correct instances.  
6. **Assessments** → **Run PowerShell assessment** (or Run from Dashboard if permitted).  
7. Open the latest assessment → check tabs, filters, search, paging.  
8. Create Team Members / Roles as needed.  
9. Confirm menu items and buttons hide correctly for Viewer / Operator roles.

---

## D. Troubleshooting

| Symptom | Fix |
|---------|-----|
| **500.19** / **500.30** | Install .NET 8 Hosting Bundle; restart IIS (`net stop was /y` + `net start w3svc`) |
| **502.5** Process failure | Enable stdout logs; check connection string and paths |
| Blank page / wrong runtime | App pool **.NET CLR = No Managed Code** |
| Cannot connect to database | Fix `DefaultConnection`; allow SQL login / Windows account |
| Run assessment fails | Check `ScriptPath` / `ServerListPath`; app-pool write access to `reports\` |
| Assessment cannot reach remote SQL | Use domain app-pool identity with rights on those servers |
| Site works on server, not from other PCs | Firewall / bindings / host name |
| Old UI after update | Recycle app pool; browser **Ctrl+F5** |

### Quick connectivity tests (on the IIS server)

```powershell
Test-NetConnection YOUR-PORTAL-SQL-HOST -Port 1433

powershell -File C:\Apps\SqlEstateAssessment\SqlEstatePortal\Assess-SqlEstate.ps1 `
  -ServerListPath C:\Apps\SqlEstateAssessment\SqlEstatePortal\servers.txt `
  -TrustServerCertificate
```

---

## Default credentials (change immediately)

| Field | Value |
|-------|--------|
| Username | `admin` |
| Password | `Admin@123` |
