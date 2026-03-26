# ADR-003: Payment Processor
**Status**: Accepted
**Date**: 2026-03-25
**Deciders**: Chris Kremer

---

## Context

LOTV needs to process one-time and recurring donations online, issue tax receipts, and handle failed payment retries. The organization is a registered 501(c)(3). Security (PCI-DSS compliance), reliability, and webhook support are critical.

---

## Decision

**Stripe**

- Stripe Checkout or Stripe Elements for the public `/give` donation form (card details never touch LOTV servers — handled entirely by Stripe JS, maintaining PCI-DSS SAQ-A compliance)
- `Stripe.net` (.NET SDK) for server-side charge confirmation, subscription creation, and webhook handling
- Recurring gifts use Stripe Subscriptions; cancellation/update handled via Stripe Customer Portal
- Webhook endpoint `POST /api/v1/webhooks/stripe` receives and processes: `payment_intent.succeeded`, `payment_intent.payment_failed`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.payment_failed`
- Stripe provides built-in tax receipt generation and email delivery

### Nonprofit Pricing
Stripe offers reduced processing fees for verified 501(c)(3) organizations through Stripe's nonprofit discount program (~1.5% + 30¢ vs. standard 2.9% + 30¢). LOTV should apply during implementation.

---

## Consequences

**Positive**
- PCI-DSS SAQ-A compliance: card data never stored or transmitted by LOTV's servers
- Stripe Subscriptions handles the full recurring billing lifecycle (retries, dunning, failures) automatically
- Excellent .NET SDK (`Stripe.net`) and webhook tooling
- Stripe Dashboard provides reconciliation without custom reporting
- Stripe Connect available if chapters need separate payout accounts in the future
- Nonprofit fee discount reduces cost burden

**Negative**
- Processing fee on every transaction (unavoidable with any hosted processor)
- Vendor dependency; migration to another processor requires significant rework
- Requires Stripe account verification and 501(c)(3) documentation for nonprofit pricing

---

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| PayPal | Higher complexity for subscriptions; less developer-friendly API; Giving Fund alternative requires more integration work |
| Square | Less mature recurring billing support; fewer .NET examples |
| Braintree | Owned by PayPal; similar concerns; less ecosystem momentum |
| Manual / check-only | Cannot support online giving from the public-facing form |

---

## Email Provider (Bundled Decision)

**SendGrid** for transactional email (case notifications, volunteer assignments, receipts, digest reports).

- Free tier: 100 emails/day (sufficient for initial launch)
- `SendGrid` NuGet package for .NET
- Template-based emails managed in SendGrid dashboard
- Nonprofit pricing available for higher volumes

Alternative (rejected): Mailgun — similar capability but SendGrid has better .NET SDK documentation and a more generous free tier.
