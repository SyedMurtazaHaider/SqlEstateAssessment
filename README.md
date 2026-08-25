# Charles Taylor SQL Estate Management

Read-only SQL Server estate assessment toolkit:

1. **PowerShell collector** (`Assess-SqlEstate.ps1`) — connects to listed SQL instances (SELECT/DMVs only), writes HTML / JSON / Markdown reports under `reports\`.
2. **ASP.NET Core portal** (`SqlEstatePortal`) — web UI for dashboards, assessment history, team members, roles/permissions, and running assessments.

The portal never modifies assessed databases. It only stores assessment results and application security data in its own `SqlEstatePortal` database.

> **Deploying to a server?** Follow the full guide: **[DEPLOYMENT.md](DEPLOYMENT.md)**  
> (deployment steps + IIS configuration, step by step)

---

## Contents

- [Requirements](#requirements)
- [Repository layout](#repository-layout)
- [Local development](#local-development)
- [Default login](#default-login)
- [Portal features](#portal-features)
- [PowerShell collector](#powershell-collector)
- [Deploy & IIS (summary)](#deploy--iis-summary)
- [Configuration reference](#configuration-reference)
- [Troubleshooting](#troubleshooting)

---

## Requirements

| Component | Notes |
|-----------|--------|
| Windows 10/11 or Windows Server | Portal host and/or build machine |
| .NET 8 SDK | Local build / `dotnet run` |
| .NET 8 Hosting Bundle | IIS hosting (includes ASP.NET Core Module) |
| SQL Server | Portal database + instances to assess |
| PowerShell 5.1+ | Runs `Assess-SqlEstate.ps1` |
| Network / Windows auth | Portal host must reach assessed SQL instances |

**Not supported by the collector:** Azure SQL with Entra ID MFA interactive login.

---

## Repository layout

```text
SqlEstateAssessment\
  README.md                     # This file
  DEPLOYMENT.md                 # Full deployment + IIS steps
  Assess-SqlEstate.ps1          # Read-only collector
  servers.example.txt           # Sample server list
  servers.txt                   # Your list (create on deploy)
  reports\                      # HTML / JSON / Markdown output
  SqlEstatePortal\              # ASP.NET Core MVC app
```

---

## Local development

### 1. Database

Create (or allow the app to create) database `SqlEstatePortal` on a local SQL instance. Update connection string in:

- `SqlEstatePortal\appsettings.json`
- `SqlEstatePortal\appsettings.Development.json` (optional overrides)

Example:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOURPC\\SQL2022;Database=SqlEstatePortal;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

On startup the app runs schema ensure/seed (roles, default admin).

### 2. Server list

Copy `servers.example.txt` to `servers.txt` and list instances (one per line). Lines starting with `#` are ignored.

### 3. Run the portal

```powershell
cd SqlEstatePortal
dotnet run --urls http://0.0.0.0:5188
```

Open: **http://localhost:5188**

### 4. Run the collector alone (optional)

```powershell
.\Assess-SqlEstate.ps1 -ServerListPath .\servers.txt -TrustServerCertificate
```

Reports land in `.\reports\`.

---

## Default login

| Field | Value |
|-------|--------|
| Username | `admin` |
| Password | `Admin@123` |

Change the password immediately via **Change password** (top-right).

Seeded roles include **Administrator** (full), **Operator** (run assessments), and **Viewer** (read-only). Admin users with **Admin Access** bypass permission checks.

---

## Portal features

- **Left sidebar** — Dashboard, Assessments, Team Members, Roles (shown only if the role has View)
- **Rights-based buttons** — Add / Edit / Delete / Run hidden without Insert / Update / Delete
- **Dashboard** — KPI cards, Chart.js visuals, assessment date + server filters
- **Assessments** — history, details with tabs (findings, databases, volumes, waits, jobs, …)
- **Table UX** — column search row under headings, paging (10/25/50/100), global search
- **Change password** — signed-in users can update their own password
- **Run assessment** — portal invokes PowerShell, imports JSON into SQL, keeps full history

---

## PowerShell collector

### Important

- **Read-only** — SELECT / DMV monitoring only; does not change data, schemas, or configuration.
- Prefer **Windows Authentication** (same pattern as Azure Data Studio / SSMS).
- Use `-TrustServerCertificate` when Encrypt is on and certificates are self-signed.

### Common parameters

| Parameter | Purpose |
|-----------|---------|
| `-Servers` | One or more server names |
| `-ServerListPath` | Text file, one server per line |
| `-OutputDirectory` | Report folder (default `.\reports`) |
| `-Credential` | SQL auth (omit for Windows auth) |
| `-TrustServerCertificate` | Trust server cert |
| `-SampleSeconds` | Counter sample interval |

### Example

```powershell
.\Assess-SqlEstate.ps1 `
  -ServerListPath .\servers.txt `
  -TrustServerCertificate `
  -SampleSeconds 2
```

Outputs (timestamped):

- `reports\sql-estate-*.html`
- `reports\sql-estate-*.json`
- `reports\sql-estate-*.md`

---

## Deploy & IIS (summary)

Full click-by-click instructions: **[DEPLOYMENT.md](DEPLOYMENT.md)**

### Quick outline

1. Install .NET 8 Hosting Bundle + IIS on the target server.  
2. Publish: `dotnet publish -c Release -o C:\Publish\SqlEstatePortal`  
3. Copy to `C:\Apps\SqlEstateAssessment\` (portal + `Assess-SqlEstate.ps1` + `servers.txt` + `reports\`).  
4. Edit `appsettings.json` (SQL connection + absolute script paths).  
5. Create database `SqlEstatePortal`.  
6. IIS app pool: **No Managed Code**, domain identity with SQL/file rights.  
7. IIS site physical path: `C:\Apps\SqlEstateAssessment\SqlEstatePortal`.  
8. Grant folder + SQL permissions; open firewall; recycle app pool.  
9. Login `admin` / `Admin@123` → change password → run one assessment.

---

## Configuration reference

### `ConnectionStrings:DefaultConnection`

SQL Server used for portal data (users, roles, assessment history).

### `Assessment` section

| Key | Purpose |
|-----|---------|
| `ScriptPath` | Full path to `Assess-SqlEstate.ps1` |
| `ServerListPath` | Full path to `servers.txt` |
| `WorkingDirectory` | Working directory for PowerShell (report root) |
| `SampleSeconds` | Passed through to the collector |
| `TrustServerCertificate` | Passed through to the collector |

Paths in `appsettings.json` that are relative are resolved against the portal content root; **absolute paths are safer on IIS**.

---

## Troubleshooting

| Symptom | Likely fix |
|---------|------------|
| 500 / DB error on start | Connection string, SQL reachable, login can use/create DB |
| IIS 500.19 / 500.30 | Install Hosting Bundle; recycle IIS (`net stop was /y` then `net start w3svc`) |
| IIS 502.5 | App failed to start — enable stdout logs (see DEPLOYMENT.md) |
| Blank site / wrong framework | App pool must be **No Managed Code** |
| Run assessment fails | Wrong `ScriptPath` / `ServerListPath`; app-pool can’t run PowerShell or write `reports` |
| Assessment works locally, fails remotely | App-pool identity has no rights on remote SQL |
| Azure SQL MFA targets fail | Not supported by the collector yet |
| Old UI after deploy | Recycle app pool; hard-refresh (**Ctrl+F5**) |

More detail: **[DEPLOYMENT.md](DEPLOYMENT.md#d-troubleshooting)**

---

## License / usage note

Assessment queries are intended for **monitoring and discovery only**. Always validate network and credential scope in your environment before pointing the collector at production estates.
