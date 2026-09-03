# Release Notes - September 3, 2026

## Linked Apps / Servers via Databases
- **Application Register**: Linked server counts, detail lists, and popup JSON now include servers inferred from linked databases (`ct_application_database` → `ct_database.server_name`), merged with explicit `ct_application_server` links and deduplicated by server name.
- **Server Register**: Linked application counts, detail lists, and popup JSON now include applications inferred the same way (apps whose linked databases sit on that server), with distinct application IDs so counts stay accurate.
- **Shared query helpers**: `GetLinkedServersAsync` / `GetLinkedApplicationsAsync` centralize the union logic for details views and Linked popups.

---

# Release Notes - August 28, 2026

## 1. Assessment Comparison Feature
- **Side-by-Side Assessment Diff**: Added comprehensive assessment comparison tool (`AssessmentCompareService`, `Assessments/Compare.cshtml`) allowing side-by-side analysis between any two assessment runs.
- **Executive KPI Summary**: Displays deltas with contextual improved/degraded badges for Total Servers, Reachable Servers, Total Databases, Storage (Allocated/Data/Log), Backup coverage, and High/Medium/Low findings.
- **Findings Diff**: Categorizes findings into **New**, **Resolved**, and **Ongoing** issues across runs.
- **Granular Infrastructure Diff**: Highlights server, database, backup timestamp, and SQL configuration changes with clean `Old → New` visual indicators (only shown when values changed).
- **Navigation Shortcuts**: Added "Compare Assessments" buttons across Dashboard, Assessments list, and Assessment detail views.

## 2. Inventory Synchronization & Approval Workflow
- **Maker-Checker vs. Auto-Direct Modes**: Configurable `InventorySync:Mode` (`MakerChecker` or `AutoDirect`) in `appsettings.json` with status badge on Sync History.
- **Enforced Maker-Checker Protection**: Removed direct startup database updates for backup times and database owners to ensure all changes flow through the approval review workflow.
- **Intelligent Change Detection**: "Sync to register" button only displays when differences exist; displays "No changes found" otherwise.
- **Large Batch Dual-Protection Fix**:
  - Configured global ASP.NET Core `FormOptions` and action attributes (`[RequestFormLimits]`, `[RequestSizeLimit]`) to eliminate HTTP 400 Bad Request errors on batches with thousands of fields.
  - Implemented client-side JSON serialization on form submit for high-speed, lightweight payload delivery.
- **Progress Overlay on Sync Actions**: Animated loading bar and live elapsed duration timer displayed during "Approve & apply" and "Save for later".
- **Formatting Fix**: Resolved date-parser issue ensuring decimal numbers (e.g. storage GB, CPU counts) are preserved accurately as numbers.

## 3. Server Type Categorization & Assessment Scoping
- **`server_type` Column**: Added `server_type` to `ct_servers` with automatic categorization (`APP Servers`, `SQL Servers`, `Others`).
- **Server Register Grid & Filter**: Added searchable "Type" filter and sortable "Type" column on the Servers register and details views.
- **Targeted SQL Operations**: Restricted "Check Server Status" reachability pings and assessment execution to SQL servers only.

## 4. Navigation & Layout Improvements
- **Sidebar Menu Button**: Placed dedicated menu toggle button inside sidebar header adjacent to brand logo.
- **Responsive Drawer**: Added backdrop overlay, click-outside dismissal, Escape key closing, and auto-dismissal when clicking links on mobile/narrow viewports.
