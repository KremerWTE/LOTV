# LOTV Privacy & Data Compliance

**Last Updated**: 2026-03-25
**Applicable Laws**: GDPR (EU), CCPA/CPRA (California), general US nonprofit PII guidelines
**Data Controller**: Lily of the Valley Ministry (LOTV)

---

## 1. PII Inventory

The following personal data is collected and stored by the LOTV platform.

### Families (service recipients)

| Field | Purpose | Retention |
|-------|---------|-----------|
| Parent names, email, phone | Case management, staff contact | Active + 7 years |
| City, State | Resource routing | Active + 7 years |
| Number of children, gestational age | Package assembly | Active + 3 years |
| Loss type (miscarriage, stillbirth) | Program reporting (aggregated) | Active + 3 years |

### Donors

| Field | Purpose | Retention |
|-------|---------|-----------|
| Name, email, phone | Receipts, correspondence | Active + 7 years |
| Mailing address | Tax receipts (IRS requirement) | Active + 7 years |
| Giving history | Receipts, year-end statements | Active + 7 years |
| Stripe Customer ID | Payment processing | Per Stripe's retention policy |
| IsAnonymous flag | Honor privacy preference | Permanent |

### Volunteers

| Field | Purpose | Retention |
|-------|---------|-----------|
| Name, email, phone | Scheduling, notifications | Active + 2 years post-departure |
| Role, chapter assignment | Authorization | Active |
| Case history | Performance metrics | Aggregated only after 2 years |

### Staff / Admin Users

| Field | Purpose | Retention |
|-------|---------|-----------|
| Name, email | Authentication, audit log | Active + 5 years |
| Hashed password | Authentication | Active only |
| Refresh tokens | Session management | 30 days (auto-expiry) |
| Audit log entries | Compliance, accountability | 7 years |

---

## 2. Data Minimization

LOTV collects only the minimum data necessary for each purpose:
- **Public intake form** (`/api/v1/public/apply`): collects only contact info and loss type — no SSN, DOB, financial data
- **Public donation form** (`/api/v1/public/give`): donor name, email, chapter assignment — credit card data handled entirely by Stripe (PCI-DSS scope is Stripe's)
- **Staff accounts**: no salary, personal address, or ID documents collected

---

## 3. Data Subject Rights

### Right to Access (GDPR Art. 15 / CCPA § 1798.110)

A data subject may request a copy of all data held about them.

**Procedure**:
1. Request received in writing (email to privacy contact)
2. Verify identity (match name + email against database)
3. Export via `GET /api/v1/donors/{id}` + `GET /api/v1/donors/{id}/contributions` for donors;
   `GET /api/v1/families/{id}` for families
4. Deliver within **30 days** (GDPR) / **45 days** (CCPA)

### Right to Deletion (GDPR Art. 17 / CCPA § 1798.105)

A data subject may request deletion of their personal data.

**Exceptions** (data that cannot be deleted):
- Donation records required for IRS compliance (7-year retention)
- Audit log entries for financial allocations (required for accountability)

**Procedure for donors**:
1. Set `IsAnonymous = true` immediately (anonymizes public displays)
2. Replace PII fields with `[REDACTED]` in `Donors` table after 7-year tax retention period
3. Retain `TotalGiven`, `GiftCount` as aggregated (non-PII) statistics
4. Delete `RefreshTokens` for associated user account immediately

**Procedure for families**:
1. Soft-delete the `Family` record (add `DeletedAt` timestamp — **TODO: implement**)
2. Retain `PackageRequest` records in anonymized form (replace FamilyId reference with a null/placeholder)
3. Hard delete after the applicable state law retention period

**Procedure for volunteers**:
1. Deactivate account (set `Status = Inactive`)
2. Anonymize personal fields after 2-year retention period
3. Retain `TotalCasesFulfilled` as aggregate statistics

### Right to Correction (GDPR Art. 16)

Any staff member with `ChapterAdmin` role can update donor, family, or volunteer records via the admin UI.

### Right to Opt Out of Sale (CCPA § 1798.120)

LOTV does **not** sell personal data. No data sharing agreements with third parties except:
- **Stripe** — payment processing (data processor, not data buyer)
- **Email provider** — notification delivery (data processor)

Both are covered by Data Processing Agreements (see Section 5).

### Right to Non-Discrimination (CCPA § 1798.125)

Exercising privacy rights does not affect the services a family, donor, or volunteer receives from LOTV.

---

## 4. Data Retention Schedule

| Category | Retention Period | Basis |
|----------|-----------------|-------|
| Donor giving records | 7 years from last gift | IRS charitable contribution records |
| Family case records | 7 years from case close | State nonprofit record-keeping |
| Audit log (financial) | 7 years | Internal financial controls |
| Volunteer case history | 2 years post-departure | Aggregated; non-PII retained |
| Auth tokens (refresh) | 30 days | Auto-expiry; no action needed |
| Staff accounts | 5 years post-departure | Audit trail integrity |
| Event attendee records | 3 years | Membership/ticketing records |

**Automated purge** — a scheduled job should be implemented to:
- Delete expired `RefreshTokens` (already auto-expires; periodic cleanup query)
- Anonymize `Donors` and `Families` after their retention window

---

## 5. Data Processor Agreements

Before launch, obtain signed DPAs with:

| Processor | Data Shared | DPA Status |
|-----------|------------|------------|
| Stripe | Donor name, email, card data (in their vault) | Stripe's standard DPA (accepted via ToS) |
| Email provider (SMTP relay) | Donor/volunteer name, email | **Required — obtain DPA before launch** |
| Cloud host (Azure / AWS) | All data at rest | **Required — standard DPA included in enterprise agreements** |
| Container registry | Source code only (no PII) | Not required |

---

## 6. Security Controls Summary

| Control | Implementation |
|---------|---------------|
| Encryption in transit | HTTPS enforced (HSTS); TLS 1.2+ |
| Encryption at rest | Host-level DB encryption (TDE) — configure at deployment |
| Access control | JWT + RBAC; chapter-scoped queries |
| Password security | Argon2id via ASP.NET Core Identity; 12-char minimum |
| Audit logging | Append-only `AuditEntry` table for all financial events |
| Anonymization | `IsAnonymous` flag on `Donor`; masking in exports |
| Breach detection | Serilog alerts + uptime monitoring (post-launch) |

---

## 7. Breach Response Plan

If a data breach is suspected:

1. **Contain**: revoke all active refresh tokens (`DELETE FROM RefreshTokens WHERE ExpiresAt > NOW()`)
2. **Rotate**: JWT signing key, database password (see disaster recovery runbook)
3. **Assess**: determine which records were accessed (audit log)
4. **Notify**:
   - **GDPR**: notify supervisory authority within **72 hours** if risk to data subjects
   - **CCPA**: notify affected California residents without unreasonable delay
   - **Individuals**: notify affected users if high risk to rights/freedoms
5. **Document**: record the incident in the post-mortem template (`docs/disaster-recovery-runbook.md`)

---

## 8. Pre-Launch Privacy Checklist

- [ ] Appoint a privacy contact and publish email address (privacy@lotvministry.org)
- [ ] Add privacy policy page to public website
- [ ] Obtain signed DPA from email provider
- [ ] Confirm cloud host DPA covers all regions where data subjects reside
- [ ] Implement soft-delete on `Family` and `Donor` records
- [ ] Implement automated `RefreshToken` cleanup job
- [ ] Confirm Stripe webhook does not log raw card data to application logs
- [ ] Add cookie consent banner to public pages (if using analytics)
- [ ] Verify no PII is logged in production log output
