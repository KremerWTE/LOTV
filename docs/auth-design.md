# LOTV Authentication & Authorization Design
**Version**: 1.0
**Status**: Approved — Phase 1
**Last Updated**: 2026-03-25

---

## 1. Authentication Strategy

**Decision**: ASP.NET Core Identity + JWT Bearer tokens

### Rationale
- Native to .NET 9; no external vendor dependency or ongoing licensing cost (critical for a nonprofit)
- Full control over user store, password policy, and token issuance
- JWT Bearer is stateless — fits Blazor WebAssembly well (token stored in memory, sent as Authorization header)
- Supports future MFA additions without architectural changes
- Refresh token pattern provides secure session extension without re-login

### Token Design
| Property | Value |
|---|---|
| Access token lifetime | 60 minutes |
| Refresh token lifetime | 14 days (rolling) |
| Algorithm | HS256 (internal); upgrade to RS256 when multi-service |
| Claims | `sub` (userId), `email`, `role`, `chapterId` (nullable), `jti` |
| Storage | Memory only in Blazor WASM — never localStorage (XSS risk) |

### Login Flow
```
POST /api/v1/auth/login  { email, password }
  → 200 { accessToken, refreshToken, expiresIn, user: { id, name, role, chapterId } }

POST /api/v1/auth/refresh  { refreshToken }
  → 200 { accessToken, refreshToken, expiresIn }

POST /api/v1/auth/logout   (invalidates refresh token server-side)
```

### Password Policy
- Minimum 10 characters
- At least 1 uppercase, 1 digit, 1 special character
- Bcrypt hashing via ASP.NET Identity default (10+ rounds)
- Account lockout: 5 failed attempts → 15-minute lockout

---

## 2. Role Hierarchy

Roles are hierarchical — higher roles implicitly have all permissions of lower roles within their scope.

```
HQAdmin
  └── ChapterAdmin  (scoped to one Chapter)
        └── ChapterStaff  (scoped to one Chapter)
              └── Volunteer  (scoped to own assignments)
                    └── Donor  (scoped to own giving history)
                          └── PublicUser  (unauthenticated)
```

### Role Definitions

| Role | ChapterId Required | Description |
|---|---|---|
| `HQAdmin` | No (null) | Full read/write access to all chapters; can manage chapters, roles, and global settings |
| `ChapterAdmin` | Yes | Full admin within their chapter: manages staff, volunteers, cases, donations, exports |
| `ChapterStaff` | Yes | Manages cases, families, volunteers within their chapter; read access to donations |
| `Volunteer` | Yes | Views and updates their own assigned cases; cannot access donor/financial data |
| `Donor` | Optional | Views their own giving history and tax receipts; can update own profile |
| `PublicUser` | N/A | Unauthenticated; can submit intake form (/apply) and volunteer signup (/volunteer) |

---

## 3. Permission Matrix

Legend: `C` = Create, `R` = Read, `U` = Update, `D` = Delete, `-` = No access, `Own` = Own records only

### Families & Cases

| Resource | HQAdmin | ChapterAdmin | ChapterStaff | Volunteer | Donor |
|---|---|---|---|---|---|
| Family records | CRUD | CRUD | CRU | R (assigned) | - |
| PackageRequest (cases) | CRUD | CRUD | CRU | RU (assigned, limited fields) | - |
| Case status change | CRUD | CRUD | CRU | U (assigned only) | - |
| Case assignment | CRUD | CRUD | CRU | - | - |
| Internal notes (staff) | CRUD | CRUD | CRUD | - | - |
| Internal notes (volunteer) | CRUD | CRUD | CRUD | CRU (own) | - |
| Request activity log | R | R | R | R (own) | - |

### Volunteers

| Resource | HQAdmin | ChapterAdmin | ChapterStaff | Volunteer | Donor |
|---|---|---|---|---|---|
| Volunteer roster | CRUD | CRUD | CR | R (own profile) | - |
| Volunteer profile | CRUD | CRUD | RU | U (own) | - |
| Volunteer workload | R | R | R | - | - |

### Donors & Donations

| Resource | HQAdmin | ChapterAdmin | ChapterStaff | Volunteer | Donor |
|---|---|---|---|---|---|
| Donor records | CRUD | CRUD | R | - | R (own) |
| Donations | CRUD | CRUD | R | - | R (own) |
| Fund allocations | CRUD | CRUD | R | - | - |
| Allocation approval | CRUD | CRU | - | - | - |
| Export donor data | Yes | Yes | No | No | No |

### Events

| Resource | HQAdmin | ChapterAdmin | ChapterStaff | Volunteer | Donor |
|---|---|---|---|---|---|
| Events | CRUD | CRUD | CRU | R | R |
| Attendees | CRUD | CRUD | CRU | - | R (own) |
| Auction items | CRUD | CRUD | CRU | - | - |
| Auction bids | CRUD | CRUD | R | - | CRU (own, while open) |

### Diocese, Parish, Chapters

| Resource | HQAdmin | ChapterAdmin | ChapterStaff | Volunteer | Donor |
|---|---|---|---|---|---|
| Diocese data | CRUD | R | R | - | - |
| Parish data | CRUD | CRU | R | - | - |
| Chapter management | CRUD | R (own) | - | - | - |

### Reports & Administration

| Resource | HQAdmin | ChapterAdmin | ChapterStaff | Volunteer | Donor |
|---|---|---|---|---|---|
| Impact report | R (all) | R (own chapter) | R (own chapter) | - | - |
| Audit log | R (all) | R (own chapter) | - | - | - |
| Data export | Yes (all) | Yes (own chapter) | No | No | No |
| User management | CRUD | CRU (own chapter) | - | - | - |
| System settings | CRUD | R | - | - | - |

---

## 4. Chapter-Scoping Enforcement

All API endpoints that access chapter-scoped data **must** apply the chapter filter. The pattern:

```csharp
// In every chapter-scoped service method:
var chapterId = _httpContextAccessor.HttpContext?.User
    .FindFirstValue("chapterId");

if (chapterId == null && !User.IsInRole("HQAdmin"))
    throw new UnauthorizedException();

var query = _db.PackageRequests.AsQueryable();
if (chapterId != null)
    query = query.Where(r => r.ChapterId == int.Parse(chapterId));

// HQAdmin: no filter applied → sees all chapters
```

This is implemented as a middleware-injected base repository method, not per-controller. A `IChapterScopeService` returns the active `chapterId?` from the current user's claims.

---

## 5. Secrets Management

| Secret | Storage |
|---|---|
| JWT signing key | Azure Key Vault (production); .NET User Secrets (local dev) |
| Database connection string | Azure Key Vault / environment variable |
| Stripe API keys | Azure Key Vault |
| SendGrid API key | Azure Key Vault |
| ASP.NET Identity data protection keys | Azure Blob Storage (key ring) |

**Rules**:
- No secrets in `appsettings.json` or source control — ever
- All secrets accessed via `IConfiguration` backed by Key Vault in production
- Local dev uses `dotnet user-secrets` with the project ID
- Secret rotation: JWT key rotated every 90 days; overlap period of 15 minutes for token validity

---

## 6. PII Handling Policy

### What is PII in this system
- Family: full name, email, phone, address, story, children's initials, faith tradition
- Donor: full name, email, phone, address
- Volunteer: full name, email, phone, address

### Retention & Anonymization
| Data class | Retention | Anonymization action |
|---|---|---|
| Fulfilled family records | 3 years post-fulfillment | Name → "Anonymous Family", email/phone/address cleared, story cleared |
| Open/active family records | Indefinitely while active | No anonymization |
| Donor records | 7 years (IRS charitable contribution records) | N/A — legal hold |
| Volunteer records | 2 years post-inactivity | Email anonymized, phone cleared |
| Audit log | 7 years (compliance) | No anonymization — system entries only |

### Access Controls for PII
- Family contact data (email, phone, address): hidden from `Volunteer` role in API responses — volunteers see name and case details only
- Donor data: never exposed to Volunteer or ChapterStaff roles via API
- Story field: ChapterStaff+ only; not included in any export unless explicitly requested

### Data in Transit & at Rest
- All API traffic: HTTPS/TLS 1.2+ enforced; HTTP redirected
- Database: encryption at rest via Azure managed disk encryption
- Backups: encrypted, stored in separate Azure region
- CSV exports: contain warning header; all export events logged in audit trail

---

## 7. Public Intake Forms (Unauthenticated)

The following routes are publicly accessible without authentication:

| Route | Purpose | Rate Limited? |
|---|---|---|
| `GET /` | Public home page | No |
| `GET /apply` | Family intake form | Yes — 5 submissions/IP/hour |
| `POST /api/v1/intake/family` | Family intake submission | Yes — 5/IP/hour |
| `GET /volunteer` | Volunteer signup | No |
| `POST /api/v1/intake/volunteer` | Volunteer signup submission | Yes — 10/IP/hour |
| `GET /give` | Donation form | No |
| `POST /api/v1/intake/donation` | Public donation submission | Yes — 20/IP/hour |

Rate limiting implemented via `AspNetCoreRateLimit` or .NET 8+ built-in rate limiter middleware.
