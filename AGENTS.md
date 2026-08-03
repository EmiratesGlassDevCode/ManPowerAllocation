# AGENTS.md

Guidance for AI agents and engineers working in this repository, and a baseline to
reuse when starting the next internal application. Read this before making changes.

## Engineering principles

- **Do not preserve backward compatibility.** Remove obsolete paths instead of adding
  compatibility layers, fallbacks, or migrations.
- **Choose the simplest implementation that fully meets the current requirements.** Avoid
  speculative abstractions, configuration, and indirection.
- **Grow the system in layers.** Start from the smallest version that works end to end, and
  add each new capability on top of a product that already works. Never trade a working
  product for unfinished complexity.
- **Keep components modular and concerns clearly separated.**
- **Prefer established, well-maintained libraries** when they reduce overall complexity or
  improve reliability. Do not reimplement common functionality without a clear reason.
- **Lean on the dependencies already in the project** before writing your own implementation
  or adding packages. Do not assume a library lacks a capability without checking its
  documentation and types.
- **Make architectural decisions for the long term.** Do not accept a stopgap that only works
  for now and is meant to be replaced later.

---

# Technical baseline

Production-grade internal line-of-business web application for a single organization. Follow
this technical baseline exactly unless told otherwise. Prioritize correctness, security,
auditability, and operational visibility over cleverness.

## Stack (pin these)
- .NET 8, C# latest, Nullable enabled, ImplicitUsings enabled, GenerateDocumentationFile=true.
- Blazor Server (interactive server render) + Microsoft Fluent UI components for the UI.
- ASP.NET Core Minimal APIs for the JSON/data surface (grouped under /api).
- EF Core + SQL Server for the governed database (parameterized queries only; no raw SQL).
- ClosedXML for Excel exports, PDFsharp for branded PDF reports.
- FluentValidation for request validation.
- Centralize shared build settings in Directory.Build.props.

## Architecture — Clean layering, dependencies point inward
- Domain: entities, enums, value objects, pure business rules. No framework/EF references.
- Application: use-case services behind interfaces, DTOs, validators, abstractions
  (IApplicationDbContext, IClock, ICurrentUser, and any external-integration interfaces).
  Contains a pure, side-effect-free "calculator" class for the core domain math so the
  rules can be unit-tested in isolation from persistence.
- Infrastructure: EF Core DbContext + configurations + migrations, implementations of the
  Application abstractions, background workers, external integrations, export generators.
- Web: Blazor components/pages, Minimal API endpoints, auth, security middleware, DI wiring.
- Each layer registers its own services via an AddXxx() DI extension. The Application layer
  never talks to infrastructure concerns directly — only through its abstractions.

## Data & external integration
- Expose the DbContext to the Application layer through an IApplicationDbContext interface.
- Add an optimistic-concurrency RowVersion token on entities edited by both users and
  background jobs; on conflict, reload the conflicting rows and retry rather than aborting.
- Provide a transactional helper (ExecuteInTransactionAsync) for operations that must save
  more than once (e.g., insert + audit row) so they commit together or not at all.
- For any read-only external source (e.g., a vendor view/DB), use a SEPARATE, keyless,
  read-only EF context that is never migrated or written to. Make its view/schema/column
  names configuration-driven so a renamed source needs no code change. Access it only
  through an Application interface; if it is unconfigured, degrade safely (no-op), never crash.

## AuthN / AuthZ (SSO-first, deny by default)
- Sole login = corporate SSO (Entra ID / OpenID Connect authorization-code flow; tokens off
  the front channel). No local username/password.
- Deny-by-default authorization fallback: every route requires auth unless explicitly opened.
- Role-based policies (e.g., Viewer/User/Admin) resolved from a role-assignment table via an
  IClaimsTransformation on each request. Provision a default lowest role on first login;
  never auto-downgrade an existing assignment.
- Provide a tightly-scoped, fully-audited, time-boxed "break-glass" emergency access path
  that signs into the same cookie scheme, auto-disables after a fixed window, and is enforced
  on live sessions (not just at login). Store its secret as a hash in configuration, never in DB.
- UI role checks are usability only; enforce every role server-side on the page AND endpoint.

## Security hardening (apply all)
- HSTS (preload), HTTPS redirect, and a security-headers middleware on every response.
- Harden the auth cookie: __Host- prefix, HttpOnly, Secure, SameSite=Lax (Lax is required for
  interactive SSO return; document the deviation), Path=/, sliding expiration.
- Rate limiting on the API surface and a far stricter bucket on any credential endpoint;
  partition by authenticated principal, else client IP.
- Persist Data Protection keys to a stable folder so cookies/antiforgery survive app-pool
  recycles (behind IIS). Honor X-Forwarded-For/-Proto from the reverse proxy.
- A global exception handler that returns generic ProblemDetails — never leak stack traces,
  SQL, secrets, or paths. Log the real cause server-side with a PII-free structured line.

## Auditing, background work, observability
- Immutable audit trail: write one entry per meaningful change (actor, timestamp UTC, action,
  entity, old/new value, break-glass flag). For high-frequency jobs write ONE summary entry
  per run, not one per record.
- Background workers as IHostedService with PeriodicTimer: self-healing and idempotent
  (poll + "capture if missing" rather than sleep-until-exact-instant; back-fill missed windows).
  A failed tick logs and retries on the next tick — it never throws out of the loop or stops
  the worker, and a transient outage never takes the app down.
- Expose a shared, thread-safe "last run / heartbeat" status for each worker; surface it in the
  UI and in health checks.
- Health checks: anonymous /health/live (process) and /health/ready (DB reachable + each
  external feed/worker healthy) with a compact JSON summary and no sensitive detail.
- One structured, PII-free log line per request.

## Conventions & quality bar
- Bind all tunables via the Options pattern from configuration (secrets never in the DB).
- Inject an IClock/IFactoryClock abstraction; never call DateTime.Now directly in logic.
- Keep domain math in pure static calculators; keep services thin and side-effect-explicit.
- Guard UI actions against re-entrancy (a _busy flag) so double-clicks/rapid toggles can't
  reuse the circuit-scoped DbContext mid-query.
- XML-document public types/methods; suppress CS1591 only for record DTO members.
- Validate every write request with FluentValidation behind a validation filter.
- Confirm destructive actions in the UI; handle DbUpdateException/concurrency gracefully.
- Excel/PDF exports share a branded header helper; exports stream via Content-Disposition.
- Model any shift/period/tenant dimension into the domain from the start. Never pool figures
  across periods that don't occur together (it distorts derived numbers such as absenteeism).

## Deployment & CI
- Target IIS via framework-dependent publish (server has the .NET Hosting Bundle).
- GitHub Actions: a CI workflow that restores + builds Release on every push/PR (compile gate),
  and a manually-triggered publish workflow that dotnet publishes and uploads the output as a
  downloadable zip artifact.

## Testing (baseline from day one)
- Unit-test the pure calculators and each Application service against an in-memory/abstracted
  DbContext. Cover the shift/boundary/aggregation edge cases explicitly.
- Add the test project to the solution and run it in CI as a required gate.

## Deliverables when scaffolding a new app
- A compiling solution with the four layers, DI wiring, initial migration + a database
  initializer that applies migrations and seeds required singleton rows at startup, the CI +
  publish workflows, and a README documenting configuration keys and deployment steps.
- Ask for the domain model (entities, roles, external sources, report shapes) and generate the
  specifics on top of this baseline.

---

# Reusable stage prompts

The technical baseline above is **Stage B1 (Build)**. Reuse the two prompts below, in order,
once the build is complete.

## Stage B2 — Testing

```
You previously built the application described below. Now act as a
senior QA lead and validate it against the full Controlled-application
testing standard. Do not declare this stage complete until all three
categories pass.

[Reference the completed build from Stage B1 — same session or same
codebase]

1. FUNCTIONAL TESTING
   - Generate test scenarios matching the real business workflow
     described in the original requirement.
   - Execute each scenario against the running application and record
     pass/fail.

2. EDGE CASE TESTING
   - Generate scenarios for: unexpected/invalid input, missing or
     malformed data, concurrent edits by two users, and an unavailable
     dependency (e.g. database briefly unreachable).
   - Execute each scenario and record pass/fail.

3. SECURITY & ACCESS TESTING
   - Log in as each defined role (Viewer, User, Admin) and confirm each
     role can only perform its permitted actions.
   - Attempt to call each API endpoint without a valid authenticated
     session and confirm it is rejected.
   - Confirm the audit log correctly records a sample of create/update/
     delete actions with accurate old and new values.
   - Confirm the break-glass account is disabled by default and, if
     enabled and used, is correctly flagged in the audit trail.

4. REGRESSION
   - Fix any failures found above.
   - Re-run all three categories after any fix until everything passes.

Summarise all scenarios and results in a structured UAT Test Case format
(minimum 10 scenarios total across the three categories) ready for
business owner sign-off.
```

## Stage B3 — Documentation

```
Based on the completed and fully tested codebase, generate the following
documents:

1. System Architecture Document — tech stack, component diagram,
   integrations
2. Data Flow Diagram — where data comes from, where it goes, what leaves
   the boundary
3. Database Schema / Data Dictionary — tables, fields, relationships,
   including the audit log and role assignment tables
4. API Reference — endpoints, auth requirement, request/response format
5. Security Checklist — confirm each control from Stage B1 is implemented,
   including the break-glass exception and its safeguards
6. UAT Test Cases — the full test scenario set and results from Stage B2
7. Deployment Runbook — step-by-step production deployment guide,
   including how to enable/disable the break-glass account
8. User Manual — plain English, with screenshot placeholders marked
   [SCREENSHOT]
9. Known Issues Log — any limitations or workarounds identified during
   build or testing
```
