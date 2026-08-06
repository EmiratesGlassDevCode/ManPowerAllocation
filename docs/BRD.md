# Business Requirements Document (BRD)
## Manpower Allocation Dashboard

| | |
|---|---|
| **Application** | Manpower Allocation Dashboard |
| **Organisation** | TechSource IT — Dubai Investments Group |
| **Business owner** | Factory Operations / Workforce Planning |
| **Classification** | CONTROLLED (production security & governance standard) |
| **Document version** | 1.0 |
| **Date** | 6 August 2026 |
| **Status** | For review |

### Version history
| Version | Date | Author | Summary |
|---|---|---|---|
| 1.0 | 2026-08-06 | TechSource IT — Application Development | Initial BRD reflecting the delivered production system. |

---

## 1. Executive summary

The Manpower Allocation Dashboard is an internal web application that gives factory operations a
single, live view of daily manpower against requirements across three divisions — **EGL**,
**Functional Support** and **BRG**. It measures **present headcount** (own staff plus outsourced
labour) against each department's **per-shift requirement**, highlights shortages, and preserves a
daily record for trend analysis.

It replaces a single-file browser prototype in which data lived only in the page. All data now
resides in a governed Microsoft SQL Server database, presence is sourced automatically from the
company's biometric attendance system, and every change is authenticated, authorised and audited.

The business value is timely, trustworthy staffing visibility: supervisors and managers can see
who is present per shift and per department right now, act on shortages, and report on staffing
over time — without manual spreadsheet consolidation.

---

## 2. Background and business drivers

- **Manual, error-prone consolidation.** Staffing was tracked in per-division Excel workbooks and a
  throwaway browser prototype, with no shared source of truth and no history.
- **No live attendance link.** Presence was maintained by hand, so the numbers lagged reality and
  invited disputes.
- **No governance.** There was no access control, no audit of who changed what, and no retention of
  the daily picture for management reporting.
- **Compliance expectation.** As a CONTROLLED-tier internal application, it must meet the group's
  security, access-control and auditability standards.

**Drivers:** a governed single source of truth; automatic, biometric-driven presence; role-based
access; a durable daily record; and standard, repeatable reporting.

---

## 3. Business objectives

| # | Objective | Success measure |
|---|---|---|
| OBJ-1 | Provide a live, accurate view of present vs required manpower per division, department and shift. | Dashboards reflect biometric presence within one sync interval (default 10 minutes). |
| OBJ-2 | Eliminate manual presence keying by sourcing attendance from the biometric system. | Present/absent set automatically; supervisors manage only vacation and outsourced staff. |
| OBJ-3 | Retain the daily staffing picture for trend analysis and management reporting. | A day and a night snapshot captured every operational day; queryable history and exports. |
| OBJ-4 | Enforce controlled access with a complete audit trail. | SSO on every route; every create/update/delete audited; role-based permissions. |
| OBJ-5 | Deliver standard reports (on-screen, downloadable, and scheduled by email) without manual effort. | Excel/CSV/PDF exports on demand; the day report emailed automatically after the 10:00 cut-off. |

---

## 4. Scope

### 4.1 In scope
- Live allocation dashboards: factory summary, per-division views, and employee search.
- Automatic attendance synchronisation from the biometric attendance view.
- Per-department shift schedules (configurable day/night windows and grace) driving presence.
- Manual overrides limited to vacation (own staff) and full status for outsourced/supply workers.
- Daily report archiving (day and night snapshots) with history and analytics.
- On-demand exports (Excel, CSV, PDF) and a scheduled daily-report email.
- Administration: role assignment, master-data import, department cleanup, shift settings/schedules,
  attendance-sync control, biometric reconciliation, email configuration, audit trail, break-glass
  status.
- Security and governance controls (SSO, RBAC, audit, hardening) to the CONTROLLED standard.

### 4.2 Out of scope
- Payroll, HR records of truth, leave approval workflow, and rostering/scheduling of individuals.
- Being the system of record for biometric punches (that remains the attendance system).
- Public/external access; the application is internal only.
- Self-service account or role registration (roles are provisioned by administrators).
- Mobile-native applications (the web UI is browser-based).

---

## 5. Stakeholders and user roles

### 5.1 Stakeholders
| Stakeholder | Interest |
|---|---|
| Factory operations managers | Real-time staffing visibility and shortage awareness. |
| Shift supervisors | Day-to-day allocation, vacation and outsourced-staff management. |
| Workforce planning / management | Historical trends and standard reporting. |
| IT (TechSource) | Access control, security, availability, and support. |
| Information security / compliance | Governance, auditability, risk acceptance of documented deviations. |

### 5.2 Application roles (RBAC)
| Role | Purpose | Capabilities |
|---|---|---|
| **Viewer** | Read-only visibility. | View all dashboards, employee search, history and reports. Default role granted on first sign-in. |
| **User** | Day-to-day operation. | Everything a Viewer can do, plus edit allocation (vacation overrides, outsourced-staff status, department on/off, employee maintenance). |
| **Admin** | Administration and governance. | Everything a User can do, plus role assignment, master-data import, department cleanup, shift settings & schedules, attendance-sync control, reconciliation, email configuration, and viewing the audit trail and break-glass status. |

Access is denied by default; a signed-in user with no assignment is provisioned **Viewer** and can
be elevated only by an Admin.

---

## 6. Current vs proposed process

**Current (before):** each division maintained an Excel workbook; presence was typed by hand; there
was no shared, live view, no history, and no access control or audit.

**Proposed (this system):**
1. Master data (departments, requirements, employees) is imported once and then maintained in-app.
2. The biometric system feeds presence automatically every few minutes.
3. Supervisors adjust only vacation (own staff) and outsourced-staff status.
4. Dashboards show present vs required live, flagging shortages.
5. The system captures a day and a night snapshot each operational day.
6. Reports are available on demand and the day report is emailed automatically.
7. Every change is authenticated, authorised and recorded in an immutable audit trail.

---

## 7. Functional requirements

Requirements are grouped by capability. Priority: **M** = Must, **S** = Should, **C** = Could.

### 7.1 Allocation dashboards
| ID | Requirement | Priority |
|---|---|---|
| BR-001 | Display a factory-wide **Summary** of present vs required across all three divisions, with totals for on-roll, present, absent, on-vacation and outsourced present. | M |
| BR-002 | Provide a **per-division view** (EGL, Functional Support, BRG) listing each department with required, present, absent, vacation, outsourced-present, variance and status. | M |
| BR-003 | Allow filtering by **shift** (Day / Night / combined) so requirements and presence reflect the selected shift. | M |
| BR-004 | Compute **present total = present own staff + present outsourced**, and **variance = total present − required**. | M |
| BR-005 | Classify each department as **Short** only when the deficit exceeds 10% of requirement, **Excess** when present exceeds required, otherwise **Optimal**; an **Off** department contributes its present staff to the available/excess pool. | M |
| BR-006 | Present an **"outsource by department"** summary on the Summary and each division view so outsourced presence is visible at department level. | S |
| BR-007 | Provide **employee search** across divisions/departments showing each person's division, department, shift and status. | M |
| BR-008 | Allow authorised users to turn a **department on/off**; an off department is excluded from requirement but its present staff still count as available. | S |

### 7.2 Attendance & presence
| ID | Requirement | Priority |
|---|---|---|
| BR-010 | Source presence automatically from the external **biometric attendance view**, keyed by employee badge number, on a scheduled interval (default every 10 minutes). | M |
| BR-011 | Mark an employee **Present** when they have a check-in within their shift window for the operational day; a check-in also clears a prior vacation (they have resumed). | M |
| BR-012 | Preserve a supervisor-set **On Vacation** status when there is no check-in. | M |
| BR-013 | Mark **Absent** when there is neither a check-in nor a vacation. | M |
| BR-014 | Restrict manual status editing to **vacation** for own staff (present/absent are system-owned) and to **full status** for outsourced/supply workers (who are not in the biometric system). | M |
| BR-015 | Provide an Admin **"Sync now"** action and show the outcome (present/absent/vacation/changed counts) and last-run status. | S |
| BR-016 | If the attendance source is unavailable or unconfigured, the sync must be a **safe no-op** (never mark everyone absent) and retry on the next interval. | M |
| BR-017 | Provide a **biometric reconciliation** view: unmatched biometric IDs (punches with no employee) and employees with no badge (unmatchable), with a verification summary and an Excel export. | S |

### 7.3 Shift schedules
| ID | Requirement | Priority |
|---|---|---|
| BR-020 | Support named **shift schedules** defining a day-shift start (night starts 12 hours later) and a grace window applied before/after each shift. | M |
| BR-021 | Allow each **department to be assigned** a shift schedule; presence is measured against that department's own windows. | M |
| BR-022 | Provide day and night 12-hour shifts with a configurable grace (default 60 minutes) to capture early comers and late leavers. | M |
| BR-023 | Default existing departments to the **07:00–19:00** schedule on first rollout. | M |
| BR-024 | Allow Admins to create, edit and assign schedules from an administration screen. | S |

### 7.4 Daily report archiving, history & analytics
| ID | Requirement | Priority |
|---|---|---|
| BR-030 | Automatically capture a **day-shift snapshot at 10:00** and a **night-shift snapshot at 22:00** (factory-local) each operational day, storing the full per-department and per-employee picture. | M |
| BR-031 | Be **self-healing and idempotent**: a snapshot missed while the app was down is captured on the next start, and re-capture never duplicates. | M |
| BR-032 | Provide a **history/analytics** screen to browse captured reports over a date range. | S |
| BR-033 | Retain snapshots as an immutable record for trend analysis. | M |

### 7.5 Reporting & exports
| ID | Requirement | Priority |
|---|---|---|
| BR-040 | Provide on-demand **Excel** exports: requirements template, staffing report, and full attendance list. | M |
| BR-041 | Provide **snapshot-history** exports over a date range as Excel, CSV and a branded PDF. | S |
| BR-042 | Provide a **per-snapshot** report as a branded management PDF and as an Excel workbook. | S |
| BR-043 | Provide a **biometric reconciliation** Excel export. | S |
| BR-044 | Support a **round-trip** workflow where an exported report can be edited and its changes re-applied. | C |

### 7.6 Scheduled daily-report email
| ID | Requirement | Priority |
|---|---|---|
| BR-050 | Automatically **email the captured day report** to configured recipients once per operational day, after the 10:00 day-shift cut-off, with the report PDF attached. | M |
| BR-051 | Be **idempotent** (one send per day) and **self-healing** (a missed send goes out on the next start). | M |
| BR-052 | Let Admins configure all SMTP settings in-app: **unauthenticated local SMTP, authenticated SMTP, Office 365 (basic), and Office 365 (modern OAuth2)**, over None / STARTTLS / SSL, with host/IP and port. | M |
| BR-053 | Capture **From**, **To**, **CC**, **BCC** and **Reply-To**; recipient lists accept comma-, semicolon- or new-line-separated addresses. | M |
| BR-054 | Store SMTP password and OAuth client secret **encrypted**, write-only in the UI (never displayed or returned), removable via an explicit "clear" action. | M |
| BR-055 | Provide a **"Send test email"** action and show last-checked / last-sent / last-failure status. | S |

### 7.7 Administration
| ID | Requirement | Priority |
|---|---|---|
| BR-060 | **Role assignment** screen for Admins to grant/adjust Viewer/User/Admin roles by Entra object id, preventing removal of the last remaining Admin. | M |
| BR-061 | **Master-data import** from the existing Excel workbooks (employees and requirements) as a one-time seeding step. | M |
| BR-062 | **Department cleanup** utility for consolidating/managing departments. | S |
| BR-063 | **Shift settings** and **shift schedules** administration. | M |
| BR-064 | **Attendance-sync** control and status. | S |
| BR-065 | **Email configuration** screen (see 7.6). | M |
| BR-066 | **Audit trail** viewer (Admin) with an option to view break-glass sessions only. | M |
| BR-067 | **Break-glass status** screen showing the emergency account state and lifecycle. | S |

### 7.8 Authentication & access
| ID | Requirement | Priority |
|---|---|---|
| BR-070 | Use **Entra ID (OpenID Connect)** as the sole interactive login; there is no general local username/password. | M |
| BR-071 | Enforce **SSO on every route** (deny-by-default) and role checks both at the endpoint and inside every mutating service (defence in depth). | M |
| BR-072 | Provide an audited **emergency break-glass** account, disabled by default, enabled only by a manual database change, that alerts the IT Head on enable and on every login and auto-disables after 4 hours. | M |
| BR-073 | Complete an interrupted or transient sign-in gracefully (transparent re-try) rather than presenting an error. | S |

---

## 8. Data requirements

| Entity | Key attributes | Notes |
|---|---|---|
| **Division** | EGL / Functional Support / BRG | A department belongs to exactly one division. |
| **Department** | Name (unique within division), day/night required headcount, display sequence, active flag, assigned shift schedule | Canonical upper-case name. |
| **Employee** | Name, badge number (non-unique), division, department, shift (Day/Night), status (Present/Absent/OnVacation), outsource/supply flag, notes | Badge number links to the biometric view. |
| **ShiftSchedule** | Name, day-shift start, grace minutes | Night derived as +12h. |
| **RoleAssignment** | Entra object id → role, display name, timestamps | The only user data stored; never a credential. |
| **AllocationSnapshot** | Operational date, shift, captured-at, source, and full department/employee detail | Immutable daily record. |
| **EmailSettings** | SMTP mode/security/host/port, From/To/CC/BCC/Reply-To, send time, attach-PDF, encrypted secrets | Single configuration row; secrets encrypted. |
| **AuditLogEntry** | User, timestamp, action, entity, record id, old/new value, break-glass flag | Immutable; written in the same transaction as the change. |
| **BreakGlassAccount** | Enabled flag, lifecycle timestamps | Stores no credential. |

**Retention:** snapshots and audit entries are retained indefinitely (immutable history). No
credential is ever stored in the application database.

---

## 9. Integrations / interfaces

| Interface | Direction | Purpose | Notes |
|---|---|---|---|
| **Entra ID (OIDC)** | Inbound auth | Sole interactive login. | Authorization-code flow; roles resolved from the app's own table. |
| **Biometric attendance view** | Inbound (read-only) | Source of employee presence. | Separate database, least-privilege SELECT only; view/columns are configuration-driven; keyed on badge number. |
| **SMTP / Office 365** | Outbound | Scheduled daily-report email. | Unauthenticated, authenticated, and O365 (basic + modern OAuth2); None/STARTTLS/SSL. |
| **SMTP / Teams webhook** | Outbound | Break-glass and shortage alerts to the IT Head. | Optional; if unconfigured, alerts are logged so they are never silent. |

---

## 10. Non-functional requirements

| Category | Requirement |
|---|---|
| **Security** | Entra ID SSO; RBAC (defence in depth); no credential stored in the app DB; SMTP/OAuth secrets encrypted; audited break-glass; security headers (CSP, X-Frame-Options DENY, nosniff, HSTS, Referrer-/Permissions-Policy); rate limiting; generic error responses (no stack traces/SQL/paths leaked). |
| **Auditability** | Every create/update/delete recorded with actor, timestamp, before/after values and break-glass flag, in the same transaction as the change. |
| **Availability & resilience** | Background workers are self-healing and idempotent; a source outage is a safe no-op; liveness and readiness health checks are exposed for monitoring. |
| **Data integrity** | All access via parameterised EF Core queries (no raw SQL); schema changes via versioned migrations applied at startup. |
| **Performance** | Dashboards reflect the latest sync (default 10-minute cadence); interactive UI updates in real time. |
| **Usability** | Blazor Server + Fluent UI; light/dark themes; plain-language presentation of technical data (e.g. reconciliation). |
| **Privacy** | Request logging is structured and PII-free; only sign-in identity and role are stored for users. |
| **Deployability** | Self-hosted on IIS (Windows Server, .NET 8 Hosting Bundle); HTTPS required at the front door; secrets supplied via environment variables or a secured config file, never committed. |
| **Time zone** | Operational cut-offs and schedules use the factory-local time zone (default "Arabian Standard Time"). |

---

## 11. Assumptions, constraints and dependencies

**Assumptions**
- The biometric view exposes a badge/employee identifier that is text-comparable to `Employee.BadgeNumber`.
- Users who reach the app already have an Entra account and are assigned to the enterprise application.
- Master data (departments, requirements, employees) is seeded once from the existing workbooks.

**Constraints**
- Internal, browser-based, HTTPS-only; Entra ID is the only interactive login.
- The application is read-only against the attendance database and never a system of record for punches.
- CONTROLLED classification: documented deviations require IT/security risk acceptance.

**Dependencies**
- Microsoft SQL Server (governed app database + read-only attendance database).
- Entra ID tenant and app registration with the correct redirect URIs.
- Network line-of-sight from the host to SQL Server and to `login.microsoftonline.com`.
- ASP.NET Core Data Protection keys persisted to a stable (and, if load-balanced, shared) location.
- Optional SMTP/Office 365 and/or Teams webhook for email and alerts.

---

## 12. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Data Protection keys not persisted | Intermittent sign-in failures and forced re-logins on app-pool recycle. | Configure a stable (shared, if load-balanced) key path; graceful sign-in retry in the app; startup warning if unset. |
| Badge/biometric key mismatch | Presence not matched to employees. | Reconciliation view + export; configurable mapping. |
| Unencrypted app-to-SQL traffic (`Encrypt=False`) | Data exposure on the network. | Recommend TLS on SQL + TDE; otherwise record a conscious risk acceptance and isolate the network segment. |
| Loss of Data Protection keys | Stored SMTP/OAuth secrets become undecryptable. | Back up the key folder; secrets can be re-entered if lost. |
| Break-glass misuse | Emergency bypass of normal auth. | Disabled by default; DB-only enable; IT Head alerts; 4-hour auto-disable; all actions audited. |

---

## 13. Acceptance criteria (UAT summary)

- A signed-in user with no role sees a read-only dashboard (provisioned Viewer); a User can edit
  allocation; only an Admin can reach administration screens.
- Dashboards show present vs required per division/department/shift, with correct variance and
  status classification, and reflect biometric presence within one sync interval.
- Marking vacation and outsourced-staff status works; present/absent for own staff is system-owned.
- Day (10:00) and night (22:00) snapshots are captured and appear in history; exports (Excel/CSV/PDF)
  download correctly.
- The day report is emailed once per day after the 10:00 cut-off with the PDF attached; a test email
  succeeds using each supported SMTP mode; secrets are never displayed.
- Every create/update/delete appears in the audit trail with actor, timestamp and before/after values.
- Enabling break-glass alerts the IT Head, is audited, and the session auto-expires after 4 hours.

---

## 14. Glossary

| Term | Meaning |
|---|---|
| **Division** | One of EGL, Functional Support, BRG. |
| **On-roll** | Employees belonging to a department for the selected shift. |
| **Present total** | Present own staff + present outsourced staff. |
| **Variance** | Present total − required for the selected shift. |
| **Short / Optimal / Excess / Off** | Department status: Short only when the deficit exceeds 10% of requirement; Excess when present exceeds required; Optimal otherwise; Off means the department is switched off but its present staff still count as available. |
| **Outsource / Supply** | Contracted labour not in the biometric system; status managed manually. |
| **Snapshot** | An immutable capture of the daily report for one operational date and shift. |
| **Break-glass** | The audited emergency account used only during an Entra outage. |
| **CONTROLLED** | The security/governance classification the application is built to. |
| **RBAC** | Role-Based Access Control (Viewer / User / Admin). |
| **Entra ID** | Microsoft Entra ID (formerly Azure AD), the identity provider. |

---

*Prepared by TechSource IT — Application Development. This BRD describes the delivered production
system and supersedes the prototype specification.*
