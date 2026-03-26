# ADR-001: Authentication Strategy
**Status**: Accepted
**Date**: 2026-03-25
**Deciders**: Chris Kremer

---

## Context

LOTV is a multi-role SaaS platform (HQAdmin, ChapterAdmin, ChapterStaff, Volunteer, Donor, PublicUser). It requires secure, stateless authentication compatible with Blazor WebAssembly on the frontend and ASP.NET Core Web API on the backend. The organization is a nonprofit with limited budget for ongoing vendor fees.

---

## Decision

**ASP.NET Core Identity + JWT Bearer tokens**

- ASP.NET Core Identity manages the user store (credentials, roles, claims) in the application database
- On login, the API issues a short-lived JWT access token (60 min) and a long-lived refresh token (14 days, rolling)
- The Blazor WASM client holds the access token in memory only (never localStorage) and attaches it as `Authorization: Bearer <token>` on all API calls
- Refresh tokens are stored server-side and invalidated on logout or rotation
- Role and `chapterId` are embedded as JWT claims, enabling stateless authorization on the API side

---

## Consequences

**Positive**
- Zero vendor cost — fully self-hosted with standard .NET libraries
- Full control over password policy, token lifetime, and user schema
- JWT statelessness fits WASM architecture cleanly — no cookie complexity
- Standard pattern with excellent community resources and .NET 9 support
- Easy to extend with 2FA (TOTP) later without architectural change

**Negative**
- Refresh token revocation requires a server-side revocation list (one DB table) — not purely stateless
- We own the security implementation; any misconfiguration is our responsibility
- No built-in SSO/social login — would require adding OpenIddict or similar if needed later

---

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| Azure AD B2C | Monthly cost per MAU is prohibitive for a nonprofit; overkill for internal staff tool |
| Auth0 | Same cost concern; adds vendor dependency for a small user base |
| Cookie-based sessions | Poor fit for Blazor WASM + API architecture; CSRF complexity |
| OpenIddict (OIDC) | Adds complexity without benefit at this scale; can add later if SSO is needed |
