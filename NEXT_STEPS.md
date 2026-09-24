# Next Steps — LOTV

**Updated:** 2026-09-24 | **Branch:** kremer-dev | **Phase:** Phase 6 — Deployment & Launch (QA readiness)

---

## 🎯 Current Focus: Prayer Request process QA on production

**Status:** Workflow, email, security and QA-data code is built and pushed. 615 unit/integration + 132 browser tests pass, 0 build warnings. Production has everything up to `bb4d3f9`; `e4d9124` + `545f46d` + the missing-column fix + docs still need a PR (kremer-dev → stage → main).

**Next Tasks (Priority Order):**
1. **PR kremer-dev → stage → main** to deploy the load diagnostics (`545f46d`), the 39-family sample data and `MissingColumnBootstrap`
2. **Production QA sample Load** — cause found: `Invalid column name 'Level'` (production DB predates `Volunteers.Level` / `Requests.ProcessStage`). `MissingColumnBootstrap` adds missing model columns at startup; after deploy check the API log for "Added missing column …" warnings, then click Load (System Admin → QA Sample Data)
3. **Set GitHub secrets:** `APP_SOCKETLABS_SERVER_ID`/`_API_KEY`, `APP_EMAIL_FROM`, `APP_TEAM_EMAILS`, `APP_SUSAN_INITIAL_PASSWORD`; then "Send test" on Request Emails
4. **Review production Users list** — registration was public until the security fix deployed
5. **Group training** — still undefined; get the requirement, then scope
6. Walk each request type through the pipeline with Susan using the sample data; log enhancements

**Completed This Session (2026-09-24):**
- ✅ Kanban/queue/case workflow, assignment rules, accurate workloads, Confirmed on accept
- ✅ Data-quality alerts, grief support list, one bereavement tracker per family, reminders
- ✅ Mother's/Father's Day lists (all submissions since last holiday), SocketLabs email, 15 email previews
- ✅ Registration security hole closed; Susan Harper provisioned (HQAdmin + volunteer)

---

## 🔗 Quick Links

- Full TODO: `MASTER_TODO.md` | Last session: `sessions/2026-09-24-prayer-request-workflow-email-security-and-qa-data.md`
- Directives: `.claude/CRITICAL_RULES_CONSOLIDATED.md`, `.claude/SESSION_STARTUP_DIRECTIVE.md`
- PM plan / risks: `docs/LOTV-PM-Plan.md` (R-32 to R-37)

---

## ⚠️ Critical Rules

**Protected Branches:** NO commits to main, master, dev, *stage*, *prod*, *deploy* — flow is kremer-dev → stage → main via PR
**Current Branch:** kremer-dev (✅ Safe) | **Commits:** `type(scope): description`, no AI co-author
**JotForm:** HIPAA-BAA account — builder UI only | **Prod DB:** `10.100.1.87` — no direct AI access
**Secrets:** never commit passwords/keys; starting passwords go through GitHub secrets
