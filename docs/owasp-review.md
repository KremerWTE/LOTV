# OWASP Top 10 Security Review — LOTV Platform

**Review Date**: 2026-03-25
**Reviewer**: Development team (pre-launch review)
**Standard**: OWASP Top 10 (2021 edition)

---

## Summary

| # | Risk | Status | Priority |
|---|------|--------|----------|
| A01 | Broken Access Control | Mitigated | Complete |
| A02 | Cryptographic Failures | Mitigated | Complete |
| A03 | Injection | Mitigated | Complete |
| A04 | Insecure Design | Mitigated | Complete |
| A05 | Security Misconfiguration | Mitigated | Complete |
| A06 | Vulnerable & Outdated Components | Mitigated | Ongoing |
| A07 | Identification & Authentication Failures | Mitigated | Complete |
| A08 | Software & Data Integrity Failures | Mitigated | Complete |
| A09 | Security Logging & Monitoring Failures | Partially Mitigated | Pre-launch |
| A10 | Server-Side Request Forgery (SSRF) | Mitigated | Complete |

---

## A01 — Broken Access Control

**Risk**: Users acting outside their intended permissions.

### Mitigations in place

- **Role-based authorization policies** defined in `Program.cs` (lines 106–148):
  - `Authenticated` — any valid JWT
  - `Staff` — `ChapterStaff` or higher
  - `Volunteer` — `Volunteer` role
  - `ChapterAdmin` — `ChapterAdmin` or `HQAdmin`
  - `HQAdmin` — HQ-only superadmin operations
- **All API groups** use `.RequireAuthorization(policy)` — there is no unauthenticated-by-default route; every non-public endpoint explicitly names a policy
- **Public endpoints** (`/api/v1/public/*`, `/api/v1/auth/*`, `/health`) are explicitly opted into `.AllowAnonymous()`
- **Chapter scoping**: queries filter by `ChapterId` derived from the JWT claim — a `ChapterStaff` user cannot read another chapter's data by guessing IDs
- **HQ-only endpoints** (`/api/v1/chapters`, HQ dashboard rollup) require `HQAdmin` role; `ChapterStaff` receives 403
- **Integration tests** in `ControllerAuthorizationTests.cs` verify 401 for unauthenticated, 403 for insufficient role, 200 for correct role across all endpoint groups

### Remaining items

- [ ] Add integration tests for chapter-scoping enforcement (a ChapterStaff user cannot read another chapter's requests by ID)
- [ ] Audit `mock` route group (`/api/mock/*`) — ensure it is disabled in non-Development environments

---

## A02 — Cryptographic Failures

**Risk**: Sensitive data exposed due to weak or missing encryption.

### Mitigations in place

- **HTTPS enforced**: `app.UseHttpsRedirection()` redirects all HTTP traffic; HSTS enabled in production via `UseHsts()`
- **JWT signing**: HMAC-SHA256 (`SecurityAlgorithms.HmacSha256`); key is a long random secret injected from environment variables, never committed to source
- **JWT key length**: enforced at runtime — short keys are rejected by `Microsoft.IdentityModel.Tokens`
- **Passwords**: hashed via ASP.NET Core Identity (`PasswordHasher<T>`) — Argon2id/PBKDF2 with per-user salt; plaintext passwords never stored
- **Refresh tokens**: generated via `RandomNumberGenerator.GetBytes(64)` (CSPRNG); stored as SHA-256 hash in the database
- **Database at rest**: production deployments must enable encryption at the hosting layer (Azure SQL TDE, AWS RDS encryption)
- **No sensitive data logged**: `appsettings.Production.json` sets EF Core logging to Warning — queries with bind parameters are not logged in production

### Remaining items

- [ ] Confirm production database provider has TDE/encryption at rest enabled (hosting setup)
- [ ] Confirm Stripe keys are never written to application logs (audit log sinks before launch)

---

## A03 — Injection

**Risk**: Hostile data sent to an interpreter (SQL, command, LDAP, etc.).

### Mitigations in place

- **EF Core parameterized queries**: all database access uses EF Core LINQ — no raw SQL string concatenation in application code; bind parameters are always used
- **No shell execution**: the application does not invoke shell commands or external processes
- **No LDAP**: authentication is ASP.NET Core Identity backed by the application database
- **Input binding via `[FromBody]`**: ASP.NET Core model binding handles deserialization; structured types are used rather than raw strings where possible
- **Stripe webhook body**: the raw request body is read for signature verification before parsing — Stripe-Signature header is verified against the shared webhook secret

### Remaining items

- [ ] Add `[MaxLength]` and validation attributes to all string model properties (prevents oversized inputs reaching the database)
- [ ] Consider adding FluentValidation for complex request DTOs

---

## A04 — Insecure Design

**Risk**: Missing or ineffective security controls by design.

### Mitigations in place

- **Principle of least privilege**: each role has exactly the permissions it needs — volunteers cannot read donor financials; chapter staff cannot see other chapters; HQ admin is a separate role
- **Public intake endpoints** (`/api/v1/public/apply`, `/api/v1/public/give`) are narrow — they accept only the minimal fields required for intake; families/donors cannot self-assign roles or chapters
- **Audit log endpoint** (`/api/v1/audit`) is append-only and requires ChapterAdmin+
- **Allocation approval**: fund allocations require an `ApprovedBy` field and go through `PendingReview` before `Allocated` — no single user can move money unilaterally
- **Rate limiting** on all public auth and payment endpoints prevents automated abuse

### Remaining items

- [ ] Implement dual-approval for allocations above a configurable dollar threshold (business rule not yet enforced server-side)
- [ ] Add explicit data retention / PII deletion policy and implement soft-delete with scheduled purge

---

## A05 — Security Misconfiguration

**Risk**: Insecure defaults, unnecessary features, or error messages exposing internals.

### Mitigations in place

- **Security headers** added in `Program.cs` (lines 186–198):
  - `X-Frame-Options: DENY` — prevents clickjacking
  - `X-Content-Type-Options: nosniff` — prevents MIME sniffing
  - `X-XSS-Protection: 1; mode=block` — legacy XSS filter for older browsers
  - `Referrer-Policy: strict-origin-when-cross-origin`
- **Swagger/OpenAPI**: disabled in production (only available in Development environment)
- **Detailed error responses**: `UseExceptionHandler("/error")` in production; stack traces not returned to clients
- **CORS**: `AllowedOrigins` is explicitly configured — no wildcard `*` in production
- **Mock data route group** (`/api/mock/*`): must be disabled or removed for production — see remaining items

### Remaining items

- [ ] Add `Content-Security-Policy` header (current setup has `X-XSS-Protection` but not CSP)
- [ ] Verify mock route group is gated to Development environment only: `if (app.Environment.IsDevelopment()) { ... mock routes ... }`
- [ ] Confirm Swagger UI is not reachable in staging or production

---

## A06 — Vulnerable & Outdated Components

**Risk**: Known vulnerabilities in libraries or frameworks.

### Mitigations in place

- **Vulnerability scan** run on 2026-03-25: `dotnet list package --vulnerable` returned **0 vulnerabilities** across all projects
- **Packages are current**: .NET 9.0, Entity Framework Core 9.0, Serilog 4.x, Moq 4.20, Stripe.net (latest)
- **Dependabot** (or equivalent): recommended to enable on the GitHub repository to receive automated PRs for vulnerable dependency updates

### Remaining items

- [ ] Enable GitHub Dependabot alerts on the repository
- [ ] Add `dotnet list package --vulnerable` as a step in `ci.yml` to block merges with known-vulnerable packages
- [ ] Schedule quarterly dependency review

---

## A07 — Identification & Authentication Failures

**Risk**: Broken authentication, weak credentials, session management issues.

### Mitigations in place

- **ASP.NET Core Identity** with default password complexity (uppercase, number, special character, min 6 chars — enforce longer minimum for production)
- **JWT access tokens**: short-lived (configured expiry); stateless; signed with HS256
- **Refresh tokens**: long-lived but stored as hashes; single-use rotation on each refresh
- **Invalid tokens return 401**: middleware rejects expired, tampered, or wrong-issuer tokens — verified by `ControllerAuthorizationTests`
- **Rate limiting on auth endpoints**: 10 requests/minute/IP prevents brute-force attacks on `/api/v1/auth/login`
- **Email uniqueness**: ASP.NET Core Identity enforces unique email per user

### Remaining items

- [ ] Enforce stronger password minimum length (12+ characters) for production
- [ ] Add account lockout after N failed attempts (`LockoutEnabled = true` in Identity options)
- [ ] Implement refresh token revocation on explicit logout
- [ ] Add MFA option for `HQAdmin` and `ChapterAdmin` roles

---

## A08 — Software & Data Integrity Failures

**Risk**: Code or data that is not integrity-checked (deserialization attacks, untrusted CI/CD pipeline).

### Mitigations in place

- **Stripe webhook signature verification**: raw body is read and verified against the `Stripe-Signature` header using the `Stripe__WebhookSecret` before any processing — prevents forged webhook events
- **JWT signature verification**: all tokens are verified against the issuer's signing key before any claim is trusted
- **GitHub Actions CI pipeline**: test gate must pass before staging or production deploy proceeds — no unreviewed code reaches production
- **Docker images built from source**: images are built in CI from the tagged commit — no pre-built binaries from external sources

### Remaining items

- [ ] Pin GitHub Actions runner versions and third-party action versions (`@v4` → `@sha256:…`) for supply-chain integrity
- [ ] Add `dotnet nuget verify` for signed packages in CI (if using internal feed)

---

## A09 — Security Logging & Monitoring Failures

**Risk**: Attacks go undetected due to insufficient logging or alerting.

### Mitigations in place

- **Serilog structured logging**: all requests are logged with correlation IDs; configuration in `appsettings.json` per environment
- **Audit log endpoint** (`GET /api/v1/audit`): returns `RequestActivity` records for admin review; requires `ChapterAdmin` role
- **Health check endpoint** (`GET /health`): database connectivity verified; can be polled by external uptime monitors

### Remaining items (require cloud setup)

- [ ] Forward Serilog output to a cloud logging sink (Azure Monitor / AWS CloudWatch / Datadog) in staging and production
- [ ] Set up alerts: error rate spike, 5xx rate > 1%, authentication failure rate > 10/min
- [ ] Set up uptime monitoring with external probe (UptimeRobot, Pingdom, or cloud-native)
- [ ] Alert on Stripe payment failure rate (> 5% failure in a 5-minute window)
- [ ] Retain logs for 90 days minimum (compliance and forensic investigation)

---

## A10 — Server-Side Request Forgery (SSRF)

**Risk**: Application fetches resources from attacker-controlled URLs.

### Mitigations in place

- **No user-controlled URL fetching**: the application does not accept URLs from user input and use them to make outbound HTTP requests
- **External HTTP calls are to fixed endpoints only**: Stripe API (`api.stripe.com`), SMTP relay — both use hardcoded hostnames from configuration, not user input
- **No S3/blob URL redirect**: file upload is not yet implemented; when added, signed URLs should be generated server-side with fixed bucket/prefix constraints

### Remaining items

- [ ] When implementing document/receipt PDF storage, ensure blob storage URLs are generated from fixed bucket names + server-generated paths — never derived from user input
- [ ] If any webhook callback URL is ever stored per-donor or per-chapter, validate it against an allowlist of known-safe domains

---

## Pre-Launch Checklist

Before going live, confirm all items marked **[ ]** above are addressed or have an accepted risk decision documented. Items that require cloud infrastructure are tracked separately in `MASTER_TODO.md` Phase 6.

High-priority pre-launch fixes:
1. Gate mock route group to Development only (`A05`)
2. Add account lockout (`A07`)
3. Enforce 12-character password minimum (`A07`)
4. Add `Content-Security-Policy` header (`A05`)
5. Enable Dependabot (`A06`)
