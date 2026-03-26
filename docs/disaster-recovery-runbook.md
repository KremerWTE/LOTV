# LOTV Disaster Recovery Runbook

**Last Updated**: 2026-03-25
**Owner**: Engineering / On-Call
**Severity Levels**: P1 = site down | P2 = degraded | P3 = minor

---

## 1. Incident Response Overview

```
Detect → Triage → Contain → Restore → Post-mortem
```

1. **Detect**: Alert fires (uptime monitor, Stripe failure spike, logs) or user report received
2. **Triage**: Identify scope — is it DB, API, Web, auth, payments, or infra?
3. **Contain**: Stop the bleeding — rollback, disable broken feature, scale down
4. **Restore**: Bring service back to known-good state
5. **Post-mortem**: Write a blameless post-mortem within 48 hours; update runbook if needed

---

## 2. Severity Classification

| Severity | Condition | Response Time | Escalate To |
|----------|-----------|---------------|-------------|
| P1 | API/Web unreachable, auth broken, data loss | Immediate (< 15 min) | Engineering lead + stakeholders |
| P2 | Degraded performance, partial feature failure, payment errors | < 1 hour | On-call engineer |
| P3 | Non-critical feature broken, cosmetic, slow query | < 4 hours | Next business day |

---

## 3. Rollback Procedure

### Container rollback (API or Web)

1. Identify the last known-good image tag from the container registry:
   ```bash
   # Azure Container Registry example
   az acr repository show-tags --name <acr-name> --repository lotv-api --orderby time_desc
   ```

2. Re-deploy the previous tag:
   ```bash
   # Azure Container Apps example
   az containerapp update \
     --name lotv-api-prod \
     --resource-group <rg> \
     --image <registry>/lotv-api:<previous-sha>
   ```

3. Verify health:
   ```bash
   curl -s https://api.lotvministry.org/health | jq .
   # Expected: {"status":"Healthy"}
   ```

4. Confirm traffic is flowing — check logs for 5xx rate dropping.

### GitHub Actions re-deploy

To re-deploy a specific commit without a new code change:
1. Go to **Actions → Deploy — Staging** (or Production)
2. Click **Re-run jobs** on the last successful run for the target commit
3. Or push a new tag: `git tag v1.2.1-hotfix && git push origin v1.2.1-hotfix`

---

## 4. Database Recovery

### Restore from backup

1. Identify the target recovery point (timestamp of last known-good state)
2. Restore to a new database instance — **do not overwrite production in-place**:
   ```bash
   # Azure SQL example — point-in-time restore
   az sql db restore \
     --dest-name lotv-restored-YYYYMMDD \
     --edition Standard \
     --name lotv-prod \
     --resource-group <rg> \
     --server <server-name> \
     --time "2026-03-24T18:00:00Z"
   ```
3. Validate the restored database: spot-check key tables (`Families`, `Donations`, `FundAllocations`)
4. Update the API connection string to point to the restored DB
5. Run `dotnet ef database update` to apply any migrations that ran after the restore point
6. Verify via `/health` and a manual smoke test

### Schema migration failure

If a deployment runs `dotnet ef database update` and it fails mid-migration:

1. Do **not** re-run migrations against the broken state
2. Restore the last backup (see above)
3. Fix the migration script locally and test against a copy of the backup
4. Re-deploy with the corrected migration

### Data integrity check

Run after any restore to verify referential integrity:
```sql
-- Donations without a valid Donor
SELECT COUNT(*) FROM Donations d LEFT JOIN Donors dr ON d.DonorId = dr.Id WHERE dr.Id IS NULL;

-- FundAllocations without a valid Donation
SELECT COUNT(*) FROM FundAllocations a LEFT JOIN Donations d ON a.DonationId = d.Id WHERE d.Id IS NULL;

-- Requests without a valid Family
SELECT COUNT(*) FROM Requests r LEFT JOIN Families f ON r.FamilyId = f.Id WHERE f.Id IS NULL;
```
All queries should return 0.

---

## 5. Secret Rotation

Use this procedure any time a secret is compromised or during scheduled rotation.

### JWT signing key

1. Generate a new key:
   ```bash
   openssl rand -base64 48
   ```
2. Update `Jwt__Key` in GitHub environment secrets (staging, then production)
3. Deploy — new tokens will be signed with the new key; existing tokens will be **invalidated immediately** (users must log in again)
4. Communicate to ops team if this is a scheduled rotation (not an emergency)

### Database password

1. Rotate password in the database server (Azure SQL / RDS)
2. Update `ConnectionStrings__DefaultConnection` in GitHub environment secrets
3. Re-deploy API — the new connection string takes effect on startup
4. Verify via `/health`

### Stripe keys

1. Roll the key in the Stripe dashboard (mark old key as inactive)
2. Update `Stripe__SecretKey` and `Stripe__WebhookSecret` in GitHub environment secrets
3. Re-deploy
4. Verify the webhook by triggering a test event from the Stripe dashboard

---

## 6. Payment Failure Incident

If Stripe payments start failing:

1. Check Stripe dashboard → **Events** tab for error rate
2. Check API logs for `stripe_error` log entries
3. If it is a Stripe-side outage: communicate to stakeholders, disable the donation intake form via a feature flag until Stripe recovers
4. If it is a key/signature mismatch: rotate keys (see Section 5)
5. Failed donations are **not automatically retried** — review the Stripe **Events** tab for `payment_intent.payment_failed` and follow up with donors manually

---

## 7. SignalR / Real-Time Failure

If the Kanban board or HQ Operations Board stops updating in real time:

1. Clients auto-reconnect every 30 seconds — most brief outages self-heal
2. If the hub is persistently down, check API logs for `SignalR` exceptions
3. Check memory/CPU on the API pod — SignalR is CPU/memory sensitive under load
4. Restart the API pod: `az containerapp revision restart ...`
5. The frontend falls back to polling gracefully — no data is lost

---

## 8. Post-Mortem Template

```markdown
## Incident Post-Mortem — [Date] [Title]

**Duration**: [start] → [end] ([total duration])
**Severity**: P1 / P2 / P3
**Impact**: [who was affected, what was broken]

### Timeline

| Time (UTC) | Event |
|------------|-------|
| HH:MM | Alert fired / first report |
| HH:MM | On-call acknowledged |
| HH:MM | Root cause identified |
| HH:MM | Fix deployed |
| HH:MM | All clear |

### Root Cause

[One paragraph description of what went wrong and why]

### Contributing Factors

- [e.g., no automated rollback, migration ran without a backup, etc.]

### Resolution

[What was done to restore service]

### Action Items

| Item | Owner | Due |
|------|-------|-----|
| Add test for X | Engineering | YYYY-MM-DD |
| Add alert for Y | Engineering | YYYY-MM-DD |
```

---

## 9. Key Contacts & Resources

| Resource | Location |
|----------|----------|
| GitHub repo | https://github.com/KremerWTE/LOTV |
| Smoke test checklist | `docs/smoke-test-checklist.md` |
| Environment config | `docs/environment-config.md` |
| OWASP security review | `docs/owasp-review.md` |
| API health endpoint | `https://api.lotvministry.org/health` |
| Stripe dashboard | https://dashboard.stripe.com |
| Container registry | Configured in `REGISTRY_HOST` GitHub secret |
