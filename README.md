# FASCO PRO Management System

PRO services management for FASCO Document Clearing LLC: client companies,
employees, document registry and expiry tracking, renewal tasks, service
requests, contracts, invoicing and online payments. Three portals: Admin,
Agent (PRO staff) and Client.

| Part | Technology |
|---|---|
| API | ASP.NET Core (.NET 10), Clean Architecture, MediatR, EF Core + Dapper |
| Database | PostgreSQL |
| Cache / realtime | Redis (cache, SignalR backplane) |
| Jobs | Hangfire (PostgreSQL storage) |
| Email | SMTP (MailKit) |
| Frontend | React 18 + Vite + Tailwind (`Frontend/`) |

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
| `Extraction__Provider` | `Mock` | `Mock`, `Local` (on-server OCR) or `OpenRouter`. |
| `Extraction__OpenRouter__Endpoint` / `__Model` / `__ApiKey` | empty | Required only when the provider is `OpenRouter`. |
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
