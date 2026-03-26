# ADR-005: Real-Time Strategy
**Status**: Accepted
**Date**: 2026-03-25
**Deciders**: Chris Kremer

---

## Context

LOTV staff need a live operations board (Kanban view) that reflects case state changes made by other staff or volunteers in real time. Without real-time updates, staff would need to manually refresh to see reassignments, status changes, and new requests — creating a risk of duplicate actions on the same case.

---

## Decision

**ASP.NET Core SignalR** for real-time push notifications to the Blazor WASM client.

### Hub: `RequestsHub`
Single hub at `/hubs/requests`. Clients connect on loading any admin page. The hub broadcasts the following server → client messages:

| Event | Payload | Trigger |
|---|---|---|
| `CaseStatusChanged` | `{ caseId, newStatus, updatedBy }` | Any status update via API |
| `CaseAssigned` | `{ caseId, volunteerId, volunteerName }` | Assignment or reassignment |
| `CaseCreated` | `{ caseId, familyName, reason }` | New request submitted |
| `CaseEscalated` | `{ caseId, reason }` | Escalation action |
| `OverdueAlert` | `{ caseId, ageInDays }` | Background job detects overdue |
| `DonationReceived` | `{ donationId, amount, channel }` | Stripe webhook processed |

### Group Strategy
```
"chapter-{chapterId}"   — all staff and volunteers in a chapter receive chapter events
"hq"                    — HQAdmin receives all chapter events (subscribed to all chapter groups)
```

On connect, the hub adds the user to their chapter group (from JWT claim) or the HQ group.

### Client Reconnect
On reconnect, the Blazor client:
1. Re-calls `GET /api/v1/dashboard/stats` and `GET /api/v1/cases?status=New,InProgress` to re-sync current state
2. Re-joins hub groups
3. Does **not** replay missed events from the hub — API poll on reconnect is the authoritative state source

This avoids the complexity of event replay / durable event log on the SignalR side.

---

## Consequences

**Positive**
- SignalR is built into ASP.NET Core — no external infrastructure required for a single server
- Blazor WASM has first-class SignalR client support (`Microsoft.AspNetCore.SignalR.Client`)
- Group-per-chapter isolates broadcast scope cleanly — chapter A staff don't receive chapter B events
- Degraded gracefully: if SignalR connection drops, the page still works via normal HTTP; updates appear on next user action or reconnect

**Negative**
- Single-server SignalR does not scale horizontally without a backplane (Redis or Azure SignalR Service)
- If/when the API is deployed as multiple instances, the Redis SignalR backplane must be added — this is a known future migration step
- Adds a persistent connection per browser tab — minor infrastructure cost at scale

---

## Scaling Path

When horizontal scaling is needed:
1. Add `Microsoft.AspNetCore.SignalR.StackExchangeRedis` backplane
2. Point all API instances at the same Redis instance
3. No code changes required — just configuration

Azure SignalR Service is an alternative managed backplane if Redis is not already in the stack.

---

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| Polling (setInterval) | Creates unnecessary load; introduces stale-data window between polls (5–30s); poor UX for operations board |
| Server-Sent Events (SSE) | One-way only; cannot support future client → server real-time events; less .NET ecosystem support |
| WebSockets (raw) | SignalR abstracts WebSockets + fallbacks (SSE, long-polling); no benefit to going raw |
| Azure SignalR Service (managed) | Additional cost and external dependency; adds complexity before needed; easy to migrate to if/when scaling requires it |
