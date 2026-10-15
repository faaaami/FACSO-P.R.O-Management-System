# PRO Management System — Architecture & Readiness Review

**Client:** FASCO Document Clearing LLC (UAE PRO services)
**Branch reviewed:** `faaaamitask` (including uncommitted working-tree changes), 2026-10-16
**Scope:** read-only review. Only this file was written; the solution was built (`dotnet build`, `dotnet ef migrations has-pending-model-changes`) but no source was changed.

> Note: the review brief I received was truncated after section B's first bullet and resumed at "4) the few missing PRO features…". Sections C–F below follow the usual structure for a review like this (bugs/security → architecture → production gaps → prioritised roadmap ending in missing PRO features with effort). Adjust if your original brief differed.

---

## A. Overview

### What the app does today
A three-portal PRO back-office for a UAE document-clearing firm:

| Portal | Who | What they can do |
|---|---|---|
| **Admin** | FASCO management | Manage client companies → entities (branches/licences) → contacts and employees; register documents (upload → OCR/AI extraction → review → confirm); manage renewal tasks (assign, service fee, unblock); service contracts, retainer and service-fee invoices, government-fee disbursements, payment orders; staff; audit log; dashboard (summary, revenue, expiry alerts). |
| **Agent** (PRO staff) | Field PRO officers | See only clients and tasks assigned to them; move a renewal task through `Submitted → FeePaid → AwaitingApproval → Approved → Updated`; block a task; log steps; register documents via extraction. |
| **Client** | Customer company contacts | Read-only view of their company, entities, employees, documents (and files), expiring documents, renewal tasks, contracts, invoices, and gov-fee disbursements; raise service requests; pay invoices online (Razorpay); download a service report PDF. |

Background automation (Hangfire, daily):
- `document-expiry-scanner`: auto-creates a renewal task for **every** document expiring within 90 days.
- `document-overdue-sweep`: does the same for documents that are already overdue.
- `extraction-expiry-cleanup`: deletes abandoned extraction uploads.

Real-time in-app notifications use SignalR with a Redis backplane.

### Tech stack
| Layer | Technology |
|---|---|
| Runtime | .NET 10 (`net10.0`), ASP.NET Core Web API, C# with nullable enabled |
| Architecture | Clean Architecture: `Domain` ← `Application` (MediatR CQRS + FluentValidation) ← `Infrastructure` (EF Core + Dapper) ← `Api` |
| DB | PostgreSQL via EF Core 10 / Npgsql (writes) + Dapper (read repositories); 28 migrations; global soft-delete query filter |
| Cache / backplane | Redis (`IDistributedCache`, SignalR backplane, health check) |
| Jobs | Hangfire 1.8 + Hangfire.PostgreSql |
| Auth | JWT bearer (HS256, 60 min) + rotating refresh token (SHA-256 hashed in DB, 7 days); BCrypt passwords; fixed-window rate limit on `/auth/*` |
| Documents | Local disk storage; extraction providers `Mock` / `Local` (PdfPig + Tesseract + PDFium OCR, rule-based classifier) / `OpenRouter` (LLM) |
| PDF | QuestPDF (invoices, client service report) |
| Payments | Razorpay (orders + HMAC-verified webhook) |
| Logging | Serilog (console only) |
| Frontend | React 18 + Vite 6 + Tailwind 4, React Query, React Router 6, react-hook-form + zod, axios. JavaScript (no TS). Lives in `Frontend/`, which is **also a separate nested git repo** (`faaaami/Fasco-PRO-Management-System-Frontend`). |
| Tests | xUnit, `WebApplicationFactory` + **Testcontainers.PostgreSql**: 129 files, ~1,485 `[Fact]/[Theory]` attributes. Mostly integration tests. |

### Solution layout
```
PROManagementSystem.slnx
├─ PROManagementSystem.Domain          21 entities, 13 enums, BaseEntity (Guid Id, timestamps, soft delete)
├─ PROManagementSystem.Application     ~190 command/query folders by feature (Auth, Clients, Employees,
│                                      Documents, Tasks, ServiceRequests, Contracts, *Invoices,
│                                      GovFeeDisbursements, PaymentOrders, Staff, Notifications,
│                                      Agent/*, Client/*), jobs, domain events
├─ PROManagementSystem.Infrastructure  DbContext + configs + migrations, Dapper read repos,
│                                      AI/OCR extraction, storage, JWT, BCrypt, Razorpay, QuestPDF, seeders
├─ PROManagementSystem (Api)           29 controllers (~250 routes), middlewares, SignalR hub,
│                                      Hangfire auth filter, rate limiting, Dockerfile
├─ PROManagementSystem.Tests           integration + unit tests
├─ Frontend/                           React SPA (nested git repo)
├─ signalr-test/                       small Node SignalR test client
├─ *.Postman.json                      two Postman collections
└─ PROManagementSystem.PROManagementSystem..._/placeholder.js   ← stray folder, delete
```

### How to run (as it stands)
1. Start PostgreSQL on **port 5433** and Redis on **6379**. There is no docker-compose.
2. Create `PROManagementSystem/appsettings.Local.json` (gitignored) with `ConnectionStrings`, `Jwt:Secret`, `RenewalAutomation:UserId`, `Razorpay:*`, and optionally `Seeding:EnableMockClientData=true` and `Extraction:*`.
3. `dotnet ef database update --project PROManagementSystem.Infrastructure --startup-project PROManagementSystem`
4. `dotnet run --project PROManagementSystem --launch-profile https` → `https://localhost:7280` (Swagger in Development).
5. `cd Frontend && npm install && npm run dev` → `http://localhost:5173`. The API base URL is hard-coded to `https://localhost:7280/api/v1` in `src/api/axios.js`.
6. Dev logins (mock seeder only, password `TestPassword123!`): `automation@internal.local` (Admin), `agent-a@test.local` (Agent), `faamil@gmail.com` (Client).
7. Tests: `dotnet test` needs **Docker** running (Testcontainers). I could not run the tests here because Docker was not available. The build succeeds with 0 errors and 10 warnings, and the EF model has no pending changes.

---

## B. Feature inventory

Status was checked against handler logic, not just whether a screen exists.
**Working** = end-to-end in API and UI. **Partly** = works with significant gaps. **Broken** = cannot work as intended. **Missing** = not implemented.

### Core PRO data
| Feature | API | UI | Status | Notes |
|---|---|---|---|---|
| Corporate customers (company → entities/licences → contacts) | ✅ CRUD | ✅ Admin | **Working** | `ClientCompany` → `ClientEntity` (trade licence, emirate) → `User` contacts. |
| **Individual customers** (e.g. family-visa sponsors, domestic workers) | ❌ | ❌ | **Missing** | Every record hangs off `ClientCompany`/`ClientEntity`. An individual sponsor has nowhere to live. |
| Employees / persons | ✅ CRUD | ⚠️ list/view only | **Partly** | No create/edit/delete employee in the Admin UI (`POST /admin/entities/{id}/employees` exists but no frontend calls it). Thin entity: no Emirates ID no., UID, MOHRE person code, gender, mobile, email, Arabic name, marital status, or visa sponsor. Dependents (spouse/children) are not modelled. |
| Client contacts & approval | ✅ | ✅ | **Working** | Admin creates contact with a password (`EmailVerified=true`), then approves. |
| Public self-registration | ✅ endpoint | ❌ page | **Broken** | `POST /auth/register` creates a Client user with **no `ClientCompanyId`** and `EmailVerified=false`. `ApproveClient` only matches contacts *inside a company*, and no real email is sent, so this account can never log in. Remove it or design it properly. |
| Staff (agents) | ✅ | ✅ | **Working** | Create, update, deactivate, workload. |
| **Admin user management** | ❌ | ❌ | **Missing / Broken** | No endpoint creates an Admin. In non-Development environments the only Admin is the automation account, which has a random, discarded password. **A fresh production install has no usable Admin login.** |

### Documents
| Feature | Status | Notes |
|---|---|---|
| Document registry (entity + employee docs, versions, file download) | **Working** | Uniqueness enforced in DB, translated to 409. Path-traversal guarded storage. |
| Upload → extraction (Mock/Local OCR/OpenRouter) → review → confirm | **Working** | Well engineered, with an OCR concurrency gate, timeouts, and strict provider config. |
| Document edit / new version / delete / dependencies in UI | **Partly** | The API exists; the Admin UI only does extract + confirm + view. |
| Document types | **Partly** | Only Passport, Visa, LaborCard, EmiratesId, TradeLicense, EstablishmentCard, Other. Missing types PROs handle daily: Work Permit, Entry Permit, Residence Visa vs Visit Visa, Labour Contract, Medical Fitness, Health Insurance, Ejari/Tenancy, Chamber of Commerce cert, MOA/AOA, Immigration (Establishment) Card vs MOHRE Establishment, ILOE insurance, Golden Visa. |
| Expiry tracking & alerts | **Partly** | Computed status, dashboard alerts and in-app notifications work. **No email/SMS/WhatsApp reminders to clients**, and no per-type reminder schedule (e.g. 60/30/7 days). |

### Work / tasks
| Feature | Status | Notes |
|---|---|---|
| Renewal tasks (assign, status chain, block/unblock, steps, history, cancel) | **Working** | Row-locked (`FOR UPDATE`) transitions; history and audit rows; completion sets the new expiry date and auto-creates a service-fee invoice in the same transaction. Solid. |
| Auto-creation of renewal tasks (daily job) | **Partly / risky** | Creates a task for **every** document type within 90 days, including **Passports** (renewed by the embassy, not the PRO) and "Other". There is no client confirmation step, and every auto task starts at status `Submitted`, which reads as if the work has been filed with the government. Expect noise and accidental billing. |
| Renewal completion | **Partly** | Updates only `ExpiryDate`. It does not capture the new document number, issue date or scan (the renewed visa/EID usually has a new number). Step "proof" is a free-text URL; there is no upload. |
| **New-service work** (new visa, new work permit, cancellation, licence amendment, company formation, attestation, typing) | **Missing** | The only work item is `RenewalTask`, which requires an existing `DocumentId`. |
| Service requests (client → admin convert/reject) | **Partly / Broken for NewVisa** | Convert **requires a linked document**, so `NewVisa` (and most `Other`) requests can only be rejected. |
| Service catalogue / price list / quotation | **Missing** | The service fee is typed in by hand per task. |

### Billing
| Feature | Status | Notes |
|---|---|---|
| Service contracts (retainer) | **Partly** | API complete. The Admin UI cannot create or edit contracts (`api/admin/contracts.js` is GET-only). |
| Retainer invoices / service-fee invoices + PDF | **Partly** | Create, void, mark-paid and PDF all work. **No VAT, no TRN, no "Tax Invoice" wording, and invoice numbers are typed by hand or `SFI-yyyyMMdd-<guid8>`, which is not sequential.** Not FTA-compliant (see C). |
| Government fee disbursements | **Partly** | Recorded with statuses, but status `InvoicedToClient` has **no link to an actual invoice**, so gov fees are never on a bill. |
| Payment recording | **Partly** | `MarkPaid` takes only an `Id`: no amount, method (cash/cheque/bank transfer/card), reference, or date. No partial payments, receipts, credit notes, or statement of account. |
| Online payments (Razorpay) | **Partly** | Order creation and HMAC-verified webhook are implemented carefully. **Commercial risk:** Razorpay is an Indian gateway. Check that your merchant account can actually accept AED from UAE customers; UAE-native options include Network International (N-Genius), Telr, Checkout.com, and Stripe (UAE). |
| Revenue dashboard | **Working** | Reads from the invoice tables. |

### Platform
| Feature | Status | Notes |
|---|---|---|
| Login / refresh / logout | **Working** | Hashed rotating refresh token, rate-limited. One refresh token per user, so a second device logs the first one out. |
| Email verification / forgot password / reset password | **Broken** | Only `DevelopmentEmailService` exists and it **sends nothing**. No frontend pages for register, forgot, reset or verify. |
| Role-based access & client tenancy | **Working** | `client_company_id` claim; agent scope limited to assigned tasks; dedicated tenancy/security tests. |
| Audit log | **Working** | Written explicitly in handlers (not automatically), so coverage depends on discipline. There is a coverage test. |
| Notifications (in-app + SignalR) | **Working** | In-app only. |
| Hangfire dashboard | **Partly** | Protected by JWT role, but a browser navigating to `/hangfire` sends no bearer token, so in practice it is unreachable. |
| Reports | **Partly** | Client service report PDF only. No expiry report export, task SLA report, receivables ageing, or Excel export. |
| Arabic / bilingual | **Missing** | No Arabic names or RTL. It matters for MOHRE/ICP typing and for client-facing documents. |

---

## C. Critical issues (bugs, security, compliance)

Ordered by severity.

1. **No way to create an Admin in production.** `CreateStaff` and `Register` only ever create Agent/Client. `AutomationUserSeeder` creates the only Admin, with a random discarded password. Production go-live is blocked.
2. **Email is never sent.** `IEmailService` → `DevelopmentEmailService` only logs. Verification, password reset and client invitations are dead, and a client who forgets their password is locked out until an Admin intervenes. There is also no expiry-reminder email.
3. **Invoices are not UAE VAT compliant.** There is no VAT amount or rate, no supplier or customer TRN, no "Tax Invoice" title, and no sequential numbering. FASCO almost certainly exceeds the AED 375k mandatory-registration threshold. Issuing these as invoices is a regulatory exposure. Gov-fee disbursements also need correct treatment: generally outside the scope of VAT when recharged at cost as a disbursement.
4. **About half of the working tree is not in git.** 50 untracked and 68 modified files, including **the entire Admin and Agent frontend** (`Frontend/src/api/admin`, `components/admin`, `components/agent`, …) and a new migration (`20261013184243_DateOnlyCalendarDates`). One disk failure or a bad `git clean` loses months of work. The `Frontend/` folder is also tracked by *both* the outer repo and its own nested `.git`, so it's unclear which repo is authoritative.
5. **Live secrets on disk in plain text.** `appsettings.Local.json` holds a real OpenRouter API key, Razorpay key/secret, DB password and JWT secret. It is correctly gitignored and was not found in history, but treat these as exposed if this file has ever been zipped, shared or synced. Rotate the keys and move them to user-secrets / environment variables / a vault for production.
6. **The auto-renewal job creates billable work nobody asked for.** It covers every document type, including Passports, and the task starts as `Submitted`. Combined with auto-invoicing on completion, this is a realistic path to wrong invoices and confused clients.
7. **Service-request conversion is broken for new services.** `NewVisa` can never be converted because conversion requires `DocumentId`.
8. **Public `/auth/register` creates orphan accounts** that can't be approved or verified. It's an unauthenticated write endpoint with no business value today.
9. **Configuration is hard-coded.** CORS allows only `http://localhost:5173`, and the frontend API URL is `https://localhost:7280`. A real deployment would fail. Use `VITE_API_BASE_URL` and a configurable CORS origin list.
10. **Storage is local disk** (`PROManagementSystem/storage`). The Dockerfile mounts no volume, so files are lost on container redeploy, and nothing is backed up. Passport and EID scans are sensitive personal data, so encryption at rest and a retention policy are expected.
11. **Minor auth items:**
    - Login checks `IsActive` *before* the password, which reveals that an account exists.
    - Deactivated users keep access until their access token expires (≤ 60 min).
    - No per-account lockout (only per-IP rate limiting).
    - Tokens are stored in `localStorage`, which is acceptable for an internal app but vulnerable to XSS.

---

## D. Architecture & code quality

**Strengths. Keep these.**
- Clean Architecture boundaries are respected. CQRS with MediatR is consistent, and FluentValidation runs as a pipeline behaviour.
- Concurrency is handled seriously: `SELECT … FOR UPDATE` on task and request transitions, DB-level uniqueness constraints translated into 409s, and events published inside the owning transaction.
- File storage has path-traversal protection, the OCR has a workload gate and timeouts, and Razorpay webhook verification uses the raw body.
- A large, purposeful integration test suite against real PostgreSQL (tenancy, concurrency, security, rate limiting, audit coverage).
- Agent data is scoped to assigned work.

**Weaknesses.**
- **Anemic domain.** All rules live in handlers, for example the status-transition table inside `UpdateRenewalTaskStatusCommandHandler`. Logic is duplicated across Admin, Agent and Client variants (e.g. `AgentExtractDocument` vs `ExtractDocument`, and `GetAgent*` vs `Get*` vs `GetMy*`).
- **Boilerplate everywhere.** `CreatedAt/UpdatedAt/IsDeleted/DeletedAt` and `AuditLog` objects are hand-built in every handler. `SaveChangesAsync` is overridden but does nothing. An EF `SaveChanges` interceptor for timestamps, soft delete and audit would remove hundreds of lines and close the gaps.
- **Two data-access stacks.** EF Core for writes, Dapper with hand-written SQL for ~22 read repositories. This is fine for performance, but it doubles the places a column rename must be applied and bypasses the soft-delete filter, which every query must remember to apply.
- **`Program.cs` is ~950 lines.** The automation-user GUID parsing is repeated four times. Split it into `AddInfrastructure()`, `AddApplication()` and `AddApi()` extension methods.
- **Inconsistent exceptions.** A mix of `InvalidOperationException`, `KeyNotFoundException`, `NotFoundException`, `ConflictException` and `UnauthorizedAccessException`. Status-code mapping depends on the middleware guessing correctly.
- **Naming typos** (`PROMangementSystem.Domain.Common` namespace, `ClientEntityDocumentReadRepository..cs`) and a stray folder `PROManagementSystem.PROManagementSystem..._/`.
- **Status strings.** `DocumentExtraction.Status` is a string (`"Pending"`), not an enum. `RenewalTaskStatus.Updated` means "Completed". Rename it for clarity, since the UI and clients read these values.
- **Excessive comments.** Many comments narrate history ("Task 7C.3.1", "P10.6-B"). That belongs in commit messages, not code.
- **Frontend.** No TypeScript, no tests, no `.env` usage. Very large page components (`ClientDashboard.jsx` 917 lines, `AdminInvoicesPage.jsx` 764 lines).

---

## E. Production-readiness gaps

| Area | Gap |
|---|---|
| Deployment | No docker-compose (API + Postgres + Redis + frontend), no reverse proxy config, no CI pipeline, no root README. |
| Configuration | Secrets in a JSON file; hard-coded URLs and CORS. |
| Observability | Serilog to console only. No file/Seq/App Insights sink, no correlation to Hangfire jobs, no alerting when the daily jobs fail. |
| Data protection | No backups strategy, no encryption of document files, no retention/purge policy for personal data (UAE PDPL). |
| Email/SMS | No provider (SMTP/SendGrid/AWS SES; SMS/WhatsApp gateway). |
| HTTPS / headers | `UseHttpsRedirection` runs after CORS. No HSTS or security headers. Swagger is Development-only, which is good. |
| Hangfire | Dashboard unreachable via browser. No retry or alerting policy defined for the jobs. |
| Time zone | Dates are UTC. UAE is UTC+4, so "expires today" around midnight can be off by a day in alerts. Use a configurable business time zone (`Asia/Dubai`) for day-boundary logic. |

---

## F. Recommended roadmap

Effort is in developer-days for one developer familiar with the codebase.

### 1) Blockers. Do these before anyone real uses the system.
| # | Item | Effort |
|---|---|---|
| 1.1 | **Commit and push the working tree.** Decide on one repo for `Frontend/` (remove the nested `.git` or make it a submodule), commit the untracked admin/agent UI and the new migration, and remove the stray folder. | 0.5 |
| 1.2 | **Admin bootstrap and management.** A one-time seed from env vars (`BOOTSTRAP_ADMIN_EMAIL/PASSWORD`) plus an Admin-only "create admin/staff with role" endpoint. | 1 |
| 1.3 | **Real email provider** (SMTP/SES/SendGrid) behind `IEmailService`, plus frontend pages for forgot/reset password. Remove or disable public `/auth/register`. | 2–3 |
| 1.4 | **Secrets and config.** Rotate the exposed keys, move them to environment variables or user-secrets, make CORS origins and the frontend API URL configurable. | 1 |
| 1.5 | **VAT-compliant invoicing.** Company TRN and client TRN fields, VAT rate per line (5% / out-of-scope for gov-fee disbursements), "Tax Invoice" PDF layout, DB-sequence invoice numbering, credit notes instead of hard void. | 4–6 |
| 1.6 | **Make auto-renewal safe.** Per-document-type config (enabled? lead days?), exclude Passport/Other by default, introduce a `Proposed`/`AwaitingClientConfirmation` status before `Submitted`. | 2 |

### 2) Fix what's half-built.
| # | Item | Effort |
|---|---|---|
| 2.1 | Admin UI: create/edit/delete employee; create/edit contract; manual task creation; admin status advance; document edit/new version/delete. | 4–5 |
| 2.2 | Renewal completion captures new number, issue date and scan (creates a `DocumentVersion`); step proof becomes a real file upload. | 2 |
| 2.3 | Payment recording: method, reference, date, amount (partial payments) and a receipt PDF. Link gov-fee disbursements to the invoice that bills them. | 3–4 |
| 2.4 | Persistent storage: S3-compatible / Azure Blob implementation of `IFileStorageService` (or at minimum a Docker volume plus backups); encrypt at rest. | 2 |
| 2.5 | Deployment: docker-compose (api, postgres, redis, nginx+SPA), health checks, Serilog file/Seq sink, GitHub Actions running build and tests. | 2–3 |
| 2.6 | Use `Asia/Dubai` for all day-boundary logic (expiry, due dates, "today"). | 1 |

### 3) Clean-up. Pays for itself.
| # | Item | Effort |
|---|---|---|
| 3.1 | EF `SaveChanges` interceptor for timestamps and soft delete, plus a small audit helper; delete the hand-written boilerplate as handlers are touched. | 2 |
| 3.2 | Split `Program.cs` into DI extension methods; one exception taxonomy → ProblemDetails. | 1–2 |
| 3.3 | Merge duplicated Admin/Agent/Client handlers behind one handler with a scope parameter, where practical. | 3 |
| 3.4 | Rename `RenewalTaskStatus.Updated` → `Completed`; enum for extraction status; fix namespace typo. | 1 |

### 4) The few missing PRO features that matter most
Keep it focused. These are what make it *PRO software* rather than a document tracker.

| # | Feature | Why it matters | Effort |
|---|---|---|---|
| 4.1 | **Generic service jobs** (replacing or extending `RenewalTask`): job type from a catalogue (New Employment Visa, Renewal, Cancellation, Work Permit, EID, Medical, Labour Contract, Trade Licence Renewal/Amendment, Establishment Card, Attestation, Golden Visa, Family Visa…), each with a **step checklist template**, required documents, and optional link to a document. Service requests convert into any job type. | Most PRO revenue is new visas and cancellations, which the system cannot model today. | 6–8 |
| 4.2 | **Service catalogue and price list** (service fee + typical gov fees per job type, per-client override from the contract), plus a **quotation** → client approval → job flow. | Removes hand-typed fees; gives clients a price before work starts. | 4–5 |
| 4.3 | **Individual customers and dependents.** A `Customer` that is either Corporate or Individual; persons (employee / sponsor / dependent) with richer identity fields (EID no., UID, MOHRE person code, gender, mobile, Arabic name, sponsor link). | Family visas and domestic workers are core PRO business. | 4–6 |
| 4.4 | **Client expiry reminders** by email (and later SMS/WhatsApp) on a 60/30/7-day schedule, with a monthly "upcoming expiries" digest per client. | The main value clients pay a PRO for. Depends on 1.3. | 2–3 |
| 4.5 | **Statement of account and receivables ageing** per client, including gov-fee disbursements outstanding. | Cash collection; disbursements are fronted cash. | 3 |
| 4.6 | **Operational reports with Excel export**: expiring documents by client, open jobs by agent/status/age, blocked jobs, revenue by service. | Daily management of the PRO team. | 2–3 |

**Rough totals:** blockers ≈ 11–14 days · half-built fixes ≈ 14–17 days · clean-up ≈ 7–8 days · missing PRO features ≈ 21–28 days.
