# FASCO PRO Management System

PRO services management for FASCO Document Clearing LLC: subscription and
walk-in customers, a service catalogue (visa, Emirates ID, MOHRE, company,
typing, attestation), jobs with step-by-step workflows, a walk-in counter,
document registry and expiry tracking, renewal tasks, service requests,
contracts, invoicing and online payments. Three portals: Admin, Agent (PRO
staff) and Client.

| Part | Technology |
|---|---|
| API | ASP.NET Core (.NET 10), Clean Architecture, MediatR, EF Core + Dapper |
| Database | PostgreSQL |
| Cache / realtime | Redis (cache, SignalR backplane) |
| Jobs | Hangfire (PostgreSQL storage) |
| Email | SMTP (MailKit) |
| Frontend | React 18 + Vite + Tailwind (`Frontend/`) |

## Customers, services and jobs

**Customers.** Every customer has a type and a counter code (`C-000123`):

| Type | Who | Typical flow |
|---|---|---|
| Subscription | Companies on a retainer contract, with portal logins | Requests from the portal, contract prices per service |
| Walk-in company | Companies served job by job | Registered at the Counter |
| Walk-in individual | Sponsors, families, visitors | Registered at the Counter; a person record is created for them |

People under a customer (employees, partners, dependents, domestic workers)
carry passport, Emirates ID, UID and MOHRE person code. Dependents and
domestic workers link to their sponsor.

**Service catalogue** (Admin → Services). Each service has a category, the
authority, who it is for (person / company / none), the default service fee
(before VAT), typical government fees, the documents to collect and the
standard steps. A service can *renew* a document type: completing such a job
updates the linked document's expiry. A starter catalogue of 36 common UAE
services is seeded on first start **with fees set to 0 — set your prices**.
Subscription pricing per contract (`PUT /api/v1/admin/contracts/{id}/service-prices/{serviceId}`)
overrides the default fee or marks the service as included in the retainer.

**Jobs** (Admin → Jobs, Agent → My Jobs, Client → Jobs). A job is one service
for one customer, numbered `JOB-2026-000042`. Lifecycle:
`New → In progress ⇄ On hold → Completed`, or `Cancelled` (admin only, with
a reason). Steps and the document checklist are copied from the service when
the job is created, so editing a service never rewrites existing jobs. A job
can only be completed when every step is done or skipped. Staff record the
government application number, step reference numbers, government fees paid
and notes (internal, or shared with the client, who is notified). Agents see
only jobs assigned to them; clients see their company's jobs without internal
notes.

**Counter** (Admin → Counter). Search by name, customer code, mobile,
passport or Emirates ID, or register a walk-in, then open jobs for them.

**Service requests** from the client portal can name a catalogue service and
can be converted into a job for any service (`Convert to job`), including new
visas, which previously could only be rejected.

| API | Purpose |
|---|---|
| `GET/POST/PUT /api/v1/admin/services`, `PATCH …/{id}/active` | Catalogue |
| `GET /api/v1/services` | Active catalogue for any signed-in role (no prices) |
| `POST /api/v1/admin/customers/walk-in`, `GET …/customers/lookup?q=` | Counter |
| `GET/PUT /api/v1/admin/customers/{id}/profile`, `…/persons` | Customer type, TRN, people |
| `/api/v1/admin/service-jobs` (+ `/agent/…`, `/client/…`) | Jobs, per role |
| `POST /api/v1/admin/service-jobs/from-request/{requestId}` | Convert a request to a job |

## Configuration

**No secret is ever committed.** `appsettings.json` holds non-secret defaults,
`appsettings.Development.json` holds local-machine defaults, and
`appsettings.Production.json` holds only `<set ...>` placeholders. Outside
Development the API **refuses to start** while any required value is missing,
a placeholder, or (for `Jwt__Secret`) shorter than 32 characters.

ASP.NET Core reads environment variables with `__` as the section separator,
for example `Smtp__Host` sets `Smtp:Host`.

### API environment variables

Required outside Development:

| Variable | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (or `Staging`). |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string, e.g. `Host=db;Port=5432;Database=pro;Username=pro;Password=...` |
| `ConnectionStrings__Redis` | Redis connection string, e.g. `redis:6379,password=...` |
| `Jwt__Secret` | JWT signing key, **at least 32 characters** of random data. |
| `App__FrontendBaseUrl` | Public URL of the web app, used in email links, e.g. `https://pro.fasco.ae` |
| `Cors__AllowedOrigins__0` | Allowed browser origin (add `__1`, `__2`, ... for more). Usually the same as `App__FrontendBaseUrl`. |
| `Smtp__Host` | SMTP server, e.g. `smtp.office365.com` |
| `Smtp__FromAddress` | Sender address, e.g. `no-reply@fasco.ae` |
| `Razorpay__KeyId` | Razorpay key id. |
| `Razorpay__KeySecret` | Razorpay key secret. |
| `Razorpay__WebhookSecret` | Razorpay webhook signing secret. |
| `Storage__RootPath` | Directory for uploaded documents. Mount a persistent, backed-up volume here. |

Optional (defaults shown):

| Variable | Default | Description |
|---|---|---|
| `Smtp__Port` | `587` | `465` uses implicit TLS, any other port uses STARTTLS. |
| `Smtp__Username` / `Smtp__Password` | empty | Leave empty for relays without authentication. |
| `Smtp__EnableSsl` | `true` | Set `false` only for a local test server such as Mailpit. |
| `Smtp__FromName` | `FASCO PRO Services` | Display name of the sender. |
| `Smtp__TimeoutSeconds` | `30` | |
| `Jwt__Issuer` / `Jwt__Audience` | `PROManagementSystem` / `PROManagementSystem.Client` | |
| `Jwt__AccessTokenExpirationMinutes` | `60` | |
| `Jwt__RefreshTokenExpirationDays` | `7` | |
| `Auth__Tokens__InvitationLifetimeHours` | `72` | Client invitation link lifetime. |
| `Auth__Tokens__EmailVerificationLifetimeHours` | `24` | Email verification link lifetime. Password-reset links always expire after **1 hour**. |
| `RenewalAutomation__UserId` | `00000000-0000-0000-0000-0000000000aa` | Id of the system account the expiry jobs act as. Seeded automatically; nobody can sign in to it. |
| `Razorpay__BaseUrl` | `https://api.razorpay.com/` | |
| `Extraction__Provider` | `Mock` | `Mock` (fixed placeholder values) or `Local` (the extraction pipeline, see [Document extraction](#document-extraction)). `OpenRouter` is still accepted and means `Local` with AI enabled. |
| `Extraction__Ai__Enabled` | `false` | Sends recognised text to the AI service for types without an MRZ. Off by default; when off, nothing leaves the server. |
| `Extraction__OpenRouter__Endpoint` / `__Model` / `__ApiKey` | empty | Required only when AI is enabled. Pin a specific model rather than `openrouter/auto`. |
| `Extraction__OcrTimeoutSeconds` | `60` | Deadline for reading one document; slower reads end with "took too long" and manual entry. |
| `Extraction__MaxConcurrentOcr` | `2` | Documents read at the same time; further uploads wait for a slot. |
| `Extraction__MaxImageLongEdge` / `__MinImageShortEdge` | `2400` / `600` | Photos are downscaled to the long edge before reading; images below the short edge are reported as too small. |
| `RateLimiting__Authentication__PermitLimit` / `__WindowSeconds` | `10` / `60` | Per-IP limit on the anonymous `/auth/*` endpoints. |

First start only (see [First Admin](#first-admin)):

| Variable | Description |
|---|---|
| `ADMIN_EMAIL` | Email of the first Admin account. |
| `ADMIN_PASSWORD` | Temporary password, at least 12 characters. Must be changed at first sign-in. |

### Frontend build variables

| Variable | Default | Description |
|---|---|---|
| `VITE_API_BASE_URL` | `/api/v1` | API base URL as seen by the browser. Leave unset when the web server proxies `/api` to the API (recommended). |
| `VITE_DEV_API_PROXY_TARGET` | `https://localhost:7280` | Development only: where `npm run dev` forwards `/api`. |

## First Admin

On startup, if `ADMIN_EMAIL` and `ADMIN_PASSWORD` are set **and no Admin exists
yet**, the API creates that Admin. The account must change its password at
first sign-in; until then every API call except changing the password returns
`403 PASSWORD_CHANGE_REQUIRED`. Once an Admin exists the variables are ignored,
so remove them after the first successful start. The result is logged as
`Admin bootstrap result: ...`.

## Accounts and email

- There is **no public registration.** Admins create staff. Admins (or Agents,
  for clients assigned to them) invite client contacts.
- An invited contact receives an email with a single-use link to set their own
  password (72 hours by default; can be resent). An Admin must also approve the
  contact before they can sign in.
- "Forgot password" emails a single-use reset link that expires after 1 hour.
  Requesting a new link invalidates the previous one, and a reset signs out
  existing sessions.
- Tokens are never stored in plain text (SHA-256 hashes only) and never logged.

## Document extraction

Staff choose the document type, then upload a PDF, JPG, PNG or HEIC photo
(HEIC is stored as JPEG). Nothing is saved until a person confirms the review
form.

- **Passport / Emirates ID:** the MRZ is read (photo page; back of the Emirates
  ID) and every check digit verified. Emirates ID numbers must be
  `784-YYYY-NNNNNNN-N`. MRZ passport numbers always say "Read from MRZ –
  compare with the number printed on the passport", because check digits
  cannot catch every look-alike (L/1, I/8, S/8); Indian, Pakistani and UK
  numbers also get layout hints.
- **Other types:** labelled values from a PDF text layer or OCR; AI only when
  `Extraction__Ai__Enabled` is true.
- Each field shows its source (MRZ / OCR / AI / manual) with errors and
  warnings, and a message explains when a document could not be read.
- Logs record only document type, duration and outcome code: never text,
  values or images.

Real sample documents for local testing go in `samples/` (gitignored, never
committed); redact anything not needed for the test.

## Deployment (Docker)

`docker-compose.yml` runs PostgreSQL, Redis, the API and the web app (nginx
serving the SPA and proxying `/api`). Copy `deploy/.env.example` to `.env`,
fill it in, then `docker compose up -d --build`. The API applies pending
migrations on start (`Database__MigrateOnStartup=true`) and trusts the proxy's
`X-Forwarded-*` headers (`ForwardedHeaders__Enabled=true`) so the per-IP login
rate limit sees real client addresses. Terminate TLS in front of port 8080 and
schedule `deploy/backup.sh` (database dump + uploaded documents).

**Azure.** [`deploy/azure/README.md`](deploy/azure/README.md) deploys the same
setup to Azure Container Apps (Bicep template plus a GitHub Actions workflow
that deploys `main` after CI passes).

**OCR on Linux.** The Linux binaries inside the Tesseract NuGet package need
OpenSSL 3.2+, which Debian 12 / Ubuntu 24.04 (the .NET images) do not have, so
OCR silently failed on Linux. The API image installs the distribution's
Tesseract and links it in with `scripts/link-linux-ocr.sh`; do the same on any
Linux host or CI runner (`apt install libtesseract5 liblept5`, then run the
script against the build output). Uploaded images are decoded and re-encoded
by Magick.NET before they reach the native Leptonica library, so unreadable
files are rejected in managed code.

## Running locally

Prerequisites: .NET 10 SDK, Node.js 18+, PostgreSQL (port 5433 by default),
Redis (6379), and Docker for the integration tests.

1. Put local secrets in `PROManagementSystem/appsettings.Local.json` (gitignored)
   or in user-secrets (`dotnet user-secrets set "Jwt:Secret" "..." --project PROManagementSystem`).
   At minimum: `ConnectionStrings:DefaultConnection` (with password) and
   `Jwt:Secret`.
2. Apply migrations:
   `dotnet ef database update --project PROManagementSystem.Infrastructure --startup-project PROManagementSystem`
3. Start the API: `dotnet run --project PROManagementSystem --launch-profile https`
   (Swagger at `https://localhost:7280/swagger`).
4. Start the frontend: `cd Frontend && npm install && npm run dev`
   (http://localhost:5173; `/api` is proxied to the API).

**Email in development.** Without `Smtp:Host`, Development uses a log-only
sender that records the recipient and subject but never the link. To open the
actual emails, run a local catcher such as Mailpit
(`docker run -p 8025:8025 -p 1025:1025 axllent/mailpit`) and set
`Smtp:Host=localhost`, `Smtp:Port=1025`, `Smtp:EnableSsl=false`,
`Smtp:FromAddress=dev@localhost`; then view mail at http://localhost:8025.

## Tests

`dotnet test` runs unit and integration tests. Integration tests start
PostgreSQL in Docker through Testcontainers, so Docker must be running.
Without Docker, point them at any PostgreSQL server instead (a throwaway
database is created and dropped per run):

```
PROMS_TEST_DATABASE="Host=localhost;Port=5432;Username=postgres;Password=..." dotnet test
```

CI (`.github/workflows/ci.yml`) builds and tests the API against a PostgreSQL
service and lints and builds the frontend.
