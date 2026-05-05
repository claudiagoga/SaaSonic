### SaaSonic

A production-ready ASP.NET Core REST API template for SaaS products. Covers auth, multi-tenant workspaces, role-based access control, and Stripe billing out of the box. Designed to be consumed by any web or mobile frontend.

### Tech Stack

**.NET 10** · **PostgreSQL** · **Entity Framework Core 10** · **MediatR 14** · **FluentValidation** · **Stripe** · **JWT + Refresh Tokens**
** Scalar UI **

### Features

- Multi-tenant workspaces — users can belong to multiple workspaces with different roles in each. Each workspace can can multiple users with different roles and permissions
- Per-workspace RBAC — Owner, Admin, Editor, Viewer roles scoped to the workspace, not the user
- Workspace-scoped billing — each workspace has its own subscription and payment method (not the user)
- Payment provider abstraction — swap Stripe for another processor without a destructive migration
- JWT authentication with refresh token rotation
- DB stored Email templates and placeholder substitution for transactional emails (email verification, welcom email...etc)
- MediatR pipeline — validation and logging handled globally via behaviors, not per-handler
- Transactional outbox pattern for email — all emails are queued atomically with the triggering DB transaction and delivered by a background worker with exponential-backoff retries
- Social login — Google and Facebook OAuth
- Audit log table for workspace-level activity


---

### Getting Started

# Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) running locally (default port 5432)
- An SMTP server or service (e.g. Mailpit for local dev, Resend/Postmark for production)
- `dotnet-ef` CLI tool:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

---

# 1. Clone the repository

```bash
git clone https://github.com/claudiagoga/SaaSonic.git
cd SaaSonic
```

---

# 2. Configure appsettings.Development.json

Open `src/SaaSonic.Api/appsettings.Development.json` and fill in your values (Connection strings, SMTP settings, JWT secret...etc)

**Required:**
- `ConnectionStrings.Default` — your local PostgreSQL connection string
- `Jwt.SecretKey` — any strong random string (minimum 32 characters). Generate one with:
  ```bash
  openssl rand -base64 32
  ```

**Optional (social login):**  
Google and Facebook credentials are only needed if you want to test OAuth login. The rest of the API works without them.

**SMTP for local dev:**  
I used Twilio with SMTP relay. Other options might be [Mailpit](https://mailpit.axllent.org/) — it's a local SMTP server with a web UI that catches all outgoing emails without sending them. Default port is 1025.

---

# 3. Create the database and run migrations

```bash
cd src/SaaSonic.Api
dotnet ef database update --project ../SaaSonic.Infrastructure
```

This creates the `saasonic_dev` database and applies all migrations. The 5 default roles (`SystemAdmin`, `Owner`, `Admin`, `Editor`, `Viewer`) are seeded automatically by EF Core — no manual SQL required. Also `email-verification` and `password-reset` email templates are seeded.

---

# 4. Run the API

```bash
dotnet run --project src/SaaSonic.Api
```

The API starts at `https://localhost:5000` by default. Scalar UI is available at `/scalar`.

---

# 5. Run the tests

```bash
dotnet test
```

Tests are split across three projects: Domain, Application, and API layers.

---

### Project Structure

```
src/
  SaaSonic.Domain/          # Entities, enums, domain logic — no external dependencies
  SaaSonic.Application/     # Use cases organized by feature (Auth, Workspaces, Subscriptions...)
  SaaSonic.Infrastructure/  # EF Core, email, payment provider implementations
  SaaSonic.Api/             # Controllers, middleware, DI composition root

tests/
  SaaSonic.Domain.Tests/
  SaaSonic.Application.Tests/
  SaaSonic.Api.Tests/
```

---

### Architectural Decisions

1. **Clean Architecture with CQRS-lite and MediatR**
    - The domain layer has no external dependencies, keeping business rules testable and framework-agnostic.
    - The application layer is organized by feature (Auth, Workspaces, Subscriptions..etc), with each use case contained in a single file (command/query + validator + handler), borrowing the discoverability benefits of vertical slice architecture without abandoning the structural clarity that Clean Architecture provides.
    - I chose IAppDbContext over the repository pattern deliberately — EF Core already implements repository and unit of work, so wrapping it in another abstraction would add indirection without value.
    - MediatR pipeline behaviors handle validation and logging at the application layer, keeping handlers focused on business logic.
    - CQRS is applied in its "lite" form — separate command and query models hitting the same database — because full CQRS with different database access and separate read stores would be overengineering for this scope.

    *Note: The DbContext is configured with `NoTracking` globally, which eliminates change-tracker overhead on the majority of requests (reads). Any command handler that fetches an entity and updates it, must call `.AsTracking()` on the query, so the cahnges will apply.

2. **RBAC (Role-Based Access Control)**

    **Single Role table, two scopes**  
    All roles live in one `Role` table which has `RoleScope` enum (`System` or `Workspace`). This avoids the overhead of separate tables while keeping the semantics clear.

    **System roles — stored on the User**  
    A user has at most one system role, held as a nullable FK (`User.SystemRoleId`). `null` means a regular user with no platform-level privileges. The only seeded system role is `SystemAdmin`. System-level endpoints are protected with `[Authorize(Policy = "SystemAdmin")]`, which checks the `role` claim in the JWT.

    **Workspace roles — stored in WorkspaceMember**  
    Workspace roles are scoped to a specific workspace via the `WorkspaceMember` table (`WorkspaceMember.RoleId`). This allows the same user to be an `Owner` in their personal workspace and a `Viewer` in a corporate one simultaneously.

    **Seeded roles**  
    EF Core seeds the following roles on first migration — no manual SQL required:

    | Name | Scope |
    |---|---|
    | SystemAdmin | System |
    | Owner | Workspace |
    | Admin | Workspace |
    | Editor | Workspace |
    | Viewer | Workspace |

    **Permissions — intentionally omitted**  
    The schema includes a `Permission` and `RolePermission` table for future use, but no permissions are seeded or enforced. This boilerplate contains no business-specific features that require fine-grained permission checks. Consumers of this template add permissions when their first custom feature needs them.

3. **Billing: Scoped to Workspace (The "Squarespace" Strategy)**  
    Unlike many templates that tie billing to the User object, this API ties subscriptions to the Workspace. This supports the agency-like workflow, where a single user may pay for "Client A's" Pro plan using one credit card, while "Client B" remains on a Free tier or uses a different payment method.  
    I combined Billing Profiles and Subscription Status into a single table "Subscription". This minimizes JOIN overhead for authorization checks while providing enough flexibility for 99% of SaaS use cases.

4. **Payment Provider Abstraction & Dependency Injection**  
    To avoid vendor lock-in, the system is decoupled from specific payment processors (like Stripe, Paddle, or LemonSqueezy).

    *Database level:* The schema uses a generic `PaymentProviderCustomerId` and `PaymentProvider` column instead of vendor-specific naming. This allows the business to pivot gateways without a destructive database migration.

    *Code level:* The API relies on a generic `IPaymentProvider` interface rather than a concrete implementation. Stripe is the included implementation, but swapping it means implementing the interface and changing a single DI registration.


5. **Reliable Email Delivery: Transactional Outbox Pattern**  
    For Email sending I impelmented an outbox pattern. Instead of calling the SMTP server/ sendimg an email in a request,   `IEmailQueue.Enqueue()` is caled, which adds a `PendingEmail` row in the same 'SaveChangesAsync` transaction that commits the business data (user creation, password reset token, etc.). This means email delivery and the business operations are atomic.

    A background `EmailSenderWorker` (`BackgroundService`)then polls the `PendingEmails` table every 30 seconds, batches up to 20 rows, and delivers them via SMTP. On failure it applies exponential backoff (`2^retryCount` minutes) up to 5 retries before marking the row `Failed`. An SMTP outage never causes a user-facing 500 error.

    *An SMTP server is still required* — the outbox defers delivery, it does not replace the transport.

