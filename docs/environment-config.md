# LOTV Environment Configuration Reference

All sensitive values are injected as environment variables at runtime.
Never commit real secrets to source control — use the templates below as a guide.

---

## Runtime environments

| Environment | `ASPNETCORE_ENVIRONMENT` | appsettings file loaded |
|-------------|--------------------------|-------------------------|
| Local dev   | `Development`            | `appsettings.Development.json` |
| Staging     | `Staging`                | `appsettings.Staging.json`     |
| Production  | `Production`             | `appsettings.Production.json`  |

ASP.NET Core loads `appsettings.json` first, then the environment-specific overlay, then environment variables (highest priority).

---

## Required environment variables

### Database

| Variable | Description | Example |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | Full ADO.NET connection string | `Server=…;Database=lotv;User Id=…;Password=…` |

For SQLite (local dev only):
```
ConnectionStrings__DefaultConnection=Data Source=lotv.db
```

---

### JWT Authentication

| Variable | Description | Notes |
|----------|-------------|-------|
| `Jwt__Key` | HMAC-SHA256 signing secret | ≥ 32 chars; generate with `openssl rand -base64 48` |
| `Jwt__Issuer` | Token issuer claim | `lotv-api` |
| `Jwt__Audience` | Token audience claim | `lotv-web` |

---

### CORS

| Variable | Description | Example |
|----------|-------------|---------|
| `AllowedOrigins__0` | First allowed origin | `https://app.lotvministry.org` |
| `AllowedOrigins__1` | Second allowed origin (optional) | `https://staging.lotvministry.org` |

---

### Stripe Payments

| Variable | Description | Notes |
|----------|-------------|-------|
| `Stripe__SecretKey` | Stripe secret API key | `sk_live_…` (prod) / `sk_test_…` (staging) |
| `Stripe__WebhookSecret` | Webhook signing secret from Stripe dashboard | `whsec_…` |

---

### Email (future — required before launch)

| Variable | Description |
|----------|-------------|
| `Email__SmtpHost` | SMTP relay hostname |
| `Email__SmtpPort` | SMTP port (typically 587) |
| `Email__Username` | SMTP auth username |
| `Email__Password` | SMTP auth password |
| `Email__FromAddress` | Sender address shown to recipients |
| `Email__FromName` | Sender display name |

---

## GitHub Actions secrets

Configure these in **Settings → Secrets and variables → Actions** on the GitHub repo.

### Shared (used by both staging and production workflows)

| Secret name | Maps to | Notes |
|-------------|---------|-------|
| `REGISTRY_HOST` | Container registry hostname | e.g. `ghcr.io/KremerWTE` or `myacr.azurecr.io` |
| `REGISTRY_USER` | Registry login username | GitHub username or service principal |
| `REGISTRY_PASSWORD` | Registry login password / PAT | GitHub PAT with `write:packages` scope |

### GitHub environment: `staging`

Configure these under **Environments → staging → Environment secrets**.

| Secret name | Description |
|-------------|-------------|
| `DB_CONNECTION_STRING` | Staging database connection string |
| `JWT_KEY` | JWT signing key (staging) |
| `STRIPE_SECRET_KEY` | Stripe test-mode secret key (`sk_test_…`) |
| `STRIPE_WEBHOOK_SECRET` | Stripe webhook secret for staging endpoint |
| `ALLOWED_ORIGINS` | Comma-separated list of allowed CORS origins |

### GitHub environment: `production`

Configure these under **Environments → production → Environment secrets**.

| Secret name | Description |
|-------------|-------------|
| `DB_CONNECTION_STRING` | Production database connection string |
| `JWT_KEY` | JWT signing key (production — rotate every 90 days) |
| `STRIPE_SECRET_KEY` | Stripe live-mode secret key (`sk_live_…`) |
| `STRIPE_WEBHOOK_SECRET` | Stripe webhook secret for production endpoint |
| `ALLOWED_ORIGINS` | Production CORS origins only |

> **Tip**: GitHub environment secrets are only available to workflows that target that specific environment (`environment: staging` / `environment: production`). Staging secrets cannot leak into production jobs.

---

## How secrets flow into the container

The deploy workflows pass secrets as environment variables to the running container.
In the deploy step (swap in your hosting action), map like this:

```yaml
env:
  ASPNETCORE_ENVIRONMENT: Production
  ConnectionStrings__DefaultConnection: ${{ secrets.DB_CONNECTION_STRING }}
  Jwt__Key: ${{ secrets.JWT_KEY }}
  Stripe__SecretKey: ${{ secrets.STRIPE_SECRET_KEY }}
  Stripe__WebhookSecret: ${{ secrets.STRIPE_WEBHOOK_SECRET }}
```

ASP.NET Core's configuration system automatically translates `__` to `:` so `Jwt__Key` maps to `Jwt:Key` in `appsettings`.

---

## Local development setup

1. Copy `.env.example` (if provided) or set environment variables in your shell:

```bash
export ConnectionStrings__DefaultConnection="Data Source=lotv-dev.db"
export Jwt__Key="local-dev-secret-at-least-32-characters-long"
export Jwt__Issuer="lotv-api"
export Jwt__Audience="lotv-web"
export AllowedOrigins__0="https://localhost:7200"
```

2. Or use `appsettings.Development.json` locally (git-ignored — do not commit secrets).

3. Run:
```bash
dotnet run --project src/Lotv.Api
```

The development environment uses SQLite (`lotv.db`) auto-created at startup via `EnsureCreated`.

---

## Secret rotation procedure

1. Generate a new value (e.g. `openssl rand -base64 48` for JWT keys)
2. Add the new secret to GitHub environment secrets
3. Update the variable name or trigger a re-deploy
4. Verify the new secret works by watching the `/health` endpoint
5. Revoke the old secret in the issuing system (Stripe dashboard, etc.)
