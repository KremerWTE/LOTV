# LOTV Smoke Test Checklist

Run this checklist against each environment (staging and production) after every deployment.

---

## 1. Infrastructure

- [ ] `GET /health` returns `200 {"status":"Healthy"}` and database check passes
- [ ] HTTPS redirect active — `http://` request redirects to `https://`
- [ ] `Strict-Transport-Security` header present in response
- [ ] `X-Content-Type-Options: nosniff` header present
- [ ] `X-Frame-Options: DENY` header present

---

## 2. Authentication

- [ ] `POST /api/v1/auth/register` — register new user, expect `201`
- [ ] `POST /api/v1/auth/login` — login with that user, expect `200` + `accessToken` + `refreshToken` in body
- [ ] `POST /api/v1/auth/login` with wrong password — expect `401`
- [ ] `GET /api/v1/requests` with no token — expect `401`
- [ ] `GET /api/v1/requests` with valid token — expect `200`
- [ ] `POST /api/v1/auth/refresh` with valid refresh token — expect `200` + new tokens
- [ ] `POST /api/v1/auth/logout` — expect `200`, refresh token revoked

---

## 3. Request Lifecycle

- [ ] `POST /api/v1/families` — create a test family, expect `201`
- [ ] `POST /api/v1/requests` — create a request for that family, expect `201`
- [ ] `GET /api/v1/requests/{id}` — retrieve it, expect `200` with `"status":"New"`
- [ ] `PUT /api/v1/requests/{id}/status` `{"Status":"InProgress"}` — expect `200`
- [ ] `GET /api/v1/requests/{id}` — confirm status is now `InProgress`
- [ ] `POST /api/v1/requests/{id}/notes` — add a note, expect `201`
- [ ] `GET /api/v1/requests/{id}/notes` — confirm note appears

---

## 4. Volunteers & Donors

- [ ] `POST /api/v1/volunteers` — create volunteer, expect `201`
- [ ] `GET /api/v1/volunteers` — list volunteers, expect `200` + array
- [ ] `POST /api/v1/donors` — create donor, expect `201`
- [ ] `POST /api/v1/donations` — create donation for that donor, expect `200` or `201`

---

## 5. Dashboard

- [ ] `GET /api/v1/dashboard/stats` — expect `200` with all fields: `openCases`, `overdue`, `donationsThisMonth`, `donationsLastMonth`, `activeVolunteers`
- [ ] Stats reflect the volunteer and donation created above (counts / amounts > 0)

---

## 6. Real-time (SignalR)

- [ ] Connect to `/hubs/requests` with a valid token — connection established (no error)
- [ ] Status change on a request triggers a `CaseStatusChanged` event on connected clients

---

## 7. Rate Limiting

- [ ] Send 11+ rapid requests to `POST /api/v1/auth/login` from same IP — 11th returns `429`

---

## 8. Frontend (Blazor WASM)

- [ ] App loads at root URL, login page renders
- [ ] Login with valid credentials redirects to dashboard
- [ ] Dashboard displays stats
- [ ] Kanban board loads with requests in correct columns
- [ ] Direct-navigate to `/admin/dashboard` while logged in — loads (no 404 from SPA routing)
- [ ] Refresh page while logged in — session restored from `sessionStorage`, no re-login required

---

## Sign-off

| Environment | Date | Tester | Result |
|---|---|---|---|
| Staging    |      |        |        |
| Production |      |        |        |
