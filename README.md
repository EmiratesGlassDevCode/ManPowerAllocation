# Manpower Allocation Dashboard

An internal business application for **TechSource IT (Dubai Investments Group)** that tracks daily
manpower allocation and attendance across three factory divisions — **EGL**, **Functional Support**
and **BRG** — measuring present headcount (own staff + outsource) against per-shift requirements.

It is a production rebuild of a single-file browser prototype. All data now lives in a governed
MSSQL database; nothing is stored in the browser or in flat files.

This application is classified **CONTROLLED** and is built to the full production security and
governance standard (see "Security & governance controls" below).

## Architecture

Clean, layered solution targeting **.NET 8**:

| Project | Responsibility |
| --- | --- |
| `ManpowerAllocation.Domain` | Entities, enums and pure invariants. No dependencies. |
| `ManpowerAllocation.Application` | Use-case services, DTOs, FluentValidation validators, and the abstractions the outer layers implement. |
| `ManpowerAllocation.Infrastructure` | EF Core (MSSQL) persistence, the audit writer, the break-glass service + background worker, outbound alerting, and Excel import parsing. |
| `ManpowerAllocation.Web` | **Blazor Server** (Fluent UI wired in) front-end plus the governed **Minimal API** surface, authentication, hardening and authorization. |

- **UI:** Blazor Server, chosen for the dashboard's live, stateful interactivity (per-employee
  status toggles, department on/off switches, shift/department filtering). Microsoft Fluent UI
  Blazor components are registered and available.
- **Backend endpoints:** ASP.NET Core Minimal API under `/api`.
- **Persistence:** EF Core against MSSQL. Every query is parameterised through EF Core; there is
  no raw SQL anywhere.

### Data model

- **Division** (EGL / Functional Support / BRG). A department belongs to exactly one division.
- **Department** — name (canonical upper-case, unique within division), day/night required
  headcount, display sequence, active flag.
- **Employee** — name, badge number (non-unique), division, department, shift (Day/Night),
  attendance status (Present/Absent/OnVacation), outsource (supply) flag, notes.
- **RoleAssignment** — Entra object id → role. The only user data stored; never a credential.
- **AuditLogEntry** — immutable record of every create/update/delete.
- **BreakGlassAccount** — a single emergency account row (disabled by default). Stores no credential.

### Staffing rules (preserved from the prototype)

- Present total = present own staff + present outsource.
- Variance = total present − required (required = day, night, or their sum depending on the shift filter).
- A department is **Short** only when the deficit exceeds 10% of its requirement, **Excess** when
  present exceeds required, otherwise **Optimal**; an **Off** department contributes its present
  staff to the available/excess pool instead.

## Security & governance controls

| Control | Where |
| --- | --- |
| **Entra ID is the sole login** (Microsoft.Identity.Web, OIDC) | `Program.cs` |
| **SSO enforced on every route** (deny-by-default fallback policy) | `Security/AuthorizationPolicies.cs` |
| **Role-based access control** (Viewer / User / Admin) enforced server-side on every endpoint **and** inside every mutating service (defence in depth) | endpoint `.RequireAuthorization(...)`, and `Require(...)` guards in the services |
| **No credential ever stored in the app DB** — only the Entra-object-id→role mapping | `RoleAssignment` |
| **Session cookie** `HttpOnly`, `Secure`, `SameSite=Strict` | `Program.cs` |
| **Break-glass exception** — single emergency account, disabled by default, enabled only by a manual DB change, alerts the IT Head on enable and every login, auto-disables after 4 hours, all actions flagged `IsBreakGlassSession` | `Infrastructure/BreakGlass/*`, `Security/BreakGlassAuthEndpoints.cs` |
| **Audit trail** of every create/update/delete (user, timestamp, action, entity, record, old/new value, break-glass flag), written in the **same transaction** as the change | `AuditLogEntry`, `Auditing/AuditWriter.cs`, service `ExecuteInTransactionAsync` usage |
| **FluentValidation** on every server entry point | `Application/**/**Validators.cs`, `Api/ValidationFilter.cs` |
| **Generic error responses** (no stack traces / SQL / paths leaked) | `Api/GlobalExceptionHandler.cs` |
| **Security headers** — CSP, X-Frame-Options: DENY, X-Content-Type-Options: nosniff, HSTS, Referrer-Policy, Permissions-Policy | `Security/SecurityHeadersMiddleware.cs`, `Program.cs` (HSTS) |
| **Rate limiting** on the API and the emergency login | `Program.cs`, `Api/ApiEndpoints.cs` |
| **Structured, PII-free request logging** (user, action, timestamp, result) | `Security/RequestLoggingMiddleware.cs` |

### Break-glass design note

The general rule is that the application database stores **no** credential. The emergency account
honours this: the database row holds only the enabled flag and lifecycle timestamps, while the
emergency secret's **PBKDF2 hash lives in protected configuration** (`BreakGlass:SecretHashBase64`
/ `SaltBase64`), ideally sourced from Azure Key Vault. The account is enabled only by flipping
`IsEnabled` directly in the database — there is deliberately no code path (UI or API) to enable it.

## Configuration

Secrets must come from user-secrets (development) or Key Vault / environment variables
(production) — never from committed files.

- `ConnectionStrings:ManpowerDatabase` — MSSQL connection string.
- `AzureAd:*` — `TenantId`, `ClientId`, `Domain`; the client secret via user-secrets/Key Vault.
- `BreakGlass:SecretHashBase64`, `BreakGlass:SaltBase64`, `BreakGlass:Iterations` — the emergency
  secret hash (see `BreakGlassSecretHasher.Derive`).
- `Alerts:*` — SMTP and/or a Teams incoming-webhook URL, plus the IT Head email. If neither channel
  is configured, break-glass alerts are logged at warning level so they are never silent.

## First-run setup

The .NET SDK is required to build and to generate the initial EF Core migration (this repository
is committed from an environment without the SDK, so the migration is created on first checkout):

```bash
# 1. Restore & build
dotnet build

# 2. Generate the initial migration and create the database
dotnet ef migrations add InitialCreate \
  -p src/ManpowerAllocation.Infrastructure \
  -s src/ManpowerAllocation.Web
dotnet ef database update \
  -p src/ManpowerAllocation.Infrastructure \
  -s src/ManpowerAllocation.Web

# 3. Run
dotnet run --project src/ManpowerAllocation.Web
```

On startup the app applies migrations and seeds the disabled break-glass account row.

### Bootstrapping the first administrator

Because roles are stored in the database and there is no self-service, insert the first Admin
assignment directly (the only manual DB step, analogous to the break-glass enable):

```sql
INSERT INTO RoleAssignments (EntraObjectId, DisplayName, Role, CreatedAtUtc)
VALUES ('<your-entra-object-id>', '<your name>', 3 /* Admin */, SYSUTCDATETIME());
```

Thereafter, all role management is done in-app under **Administration → Role Assignments**.

### Loading master data

Sign in as an Admin and use **Administration → Master Data Import** to seed employees and
requirements from the existing Excel workbooks (EGL / Functional Support / BRG sheets). This is a
one-time seeding step; day-to-day changes are made through the dashboards and are individually
audited.
