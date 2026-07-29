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

Secrets must come from user-secrets (development) or environment variables / a secret store
(production) — never from committed files. On-prem, environment variables on the host are the
recommended source; ASP.NET Core maps `__` to configuration nesting (for example
`ConnectionStrings__ManpowerDatabase`).

- `ConnectionStrings:ManpowerDatabase` — MSSQL connection string (see the on-prem section below
  for the non-encrypted-server form).
- `AzureAd:*` — `TenantId`, `ClientId`, `Domain`; the client secret via user-secrets / an
  environment variable. Register redirect URI `https://<host>/signin-oidc` and signed-out
  callback `https://<host>/signout-callback-oidc`.
- `BreakGlass:SecretHashBase64`, `BreakGlass:SaltBase64`, `BreakGlass:Iterations` — the emergency
  secret hash. Generate these with the `BreakGlassHasher` tool (see below); the database never
  stores the secret.
- `Alerts:*` — SMTP and/or a Teams incoming-webhook URL, plus the IT Head email. If neither channel
  is configured, break-glass alerts are logged at warning level so they are never silent.

### Generating the break-glass secret values

```bash
dotnet run --project tools/BreakGlassHasher -- "<the-emergency-secret>"
```

It prints ready-to-paste `BreakGlass__SaltBase64`, `BreakGlass__SecretHashBase64` and
`BreakGlass__Iterations` values. Keep the plain secret only in IT's password vault; rotating it
means re-running the tool and updating configuration.

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

## On-premises deployment (self-hosted MSSQL)

### Connection string when the SQL Server has no TLS certificate

EF Core 8 uses `Microsoft.Data.SqlClient`, which **defaults to requiring an encrypted
connection**. Against a SQL Server that is not configured for TLS, the connection must opt out
explicitly or it will fail:

```
Server=YOUR-SQL-HOST;Database=ManpowerAllocation;User Id=svc_manpower;Password=***;Encrypt=False
```

(Windows/integrated auth: replace the credentials with `Trusted_Connection=True`.) The same
connection string is used by `dotnet ef database update` and by the running app; the app applies
migrations on startup.

> **⚠ Security note — flagged to IT.** `Encrypt=False` means traffic between the application and
> SQL Server — including employee data and the SQL login — travels **unencrypted** on the network.
> For a CONTROLLED-tier application the recommended posture is to enable TLS on SQL Server (install
> a certificate and use `Encrypt=True`), and to enable Transparent Data Encryption (TDE) for
> encryption at rest. The application works either way, but running with `Encrypt=False` is a
> conscious risk acceptance that should be recorded and, ideally, mitigated by keeping the
> app-to-SQL traffic on an isolated, trusted network segment. This is an infrastructure decision;
> no application change is required when TLS/TDE are later enabled.

### Hosting checklist

- **HTTPS is required at the front door.** The app sets a `Secure`, `SameSite=Strict` session
  cookie, HSTS and HTTPS redirection, so the site must be served over HTTPS (a TLS certificate on
  IIS/Kestrel or the reverse proxy). Over plain HTTP the auth cookie is not sent and sign-in
  appears to fail. Note this HTTPS requirement is independent of the SQL connection encryption
  above.
- **Service account.** Run the app pool / service under an account that has `db_datareader` +
  `db_datawriter` (and rights to run migrations) on the `ManpowerAllocation` database.
- **Secrets** (`ConnectionStrings__ManpowerDatabase`, `AzureAd__ClientSecret`,
  `BreakGlass__SecretHashBase64`, `BreakGlass__SaltBase64`) are supplied as environment variables
  or a secured `appsettings.Production.json` kept out of source control — never committed.
- **Break-glass enable** remains a manual `UPDATE BreakGlassAccounts SET IsEnabled = 1` performed by
  IT; the app stamps the start time, alerts the IT Head, and auto-disables it four hours later.
