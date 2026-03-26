# ADR-004: Org Hierarchy and Data Scoping
**Status**: Accepted
**Date**: 2026-03-25
**Deciders**: Chris Kremer

---

## Context

LOTV operates as a single national nonprofit with local chapters. Every chapter manages its own families, cases, volunteers, and donations. HQ staff need visibility into all chapters for reporting and oversight. The system must prevent chapter A from seeing chapter B's data while giving HQ a unified view.

---

## Decision

**Two-tier hierarchy: National HQ → Local Chapters**
**Single-database, ChapterId column isolation**
**HQAdmin bypasses chapter filter; all other roles are strictly scoped**

### Hierarchy
```
National HQ (no ChapterId)
  ├── Chicago Chapter     (ChapterId = 1)
  ├── Milwaukee Chapter   (ChapterId = 2)
  └── Detroit Chapter     (ChapterId = 3)
```

There is no middle tier (Region, Diocese, etc.) between HQ and Chapter for access control purposes. Diocese is a data classification concept for donor and parish tracking, not an authorization boundary.

### Data Isolation
Every entity that belongs to a chapter carries `ChapterId INT NOT NULL`. The `IChapterScopedRepository<T>` enforces this at the data access layer:

```csharp
public class ChapterScopedRepository<T> : IRepository<T> where T : IChapterOwned
{
    private readonly int? _chapterId; // null for HQAdmin

    public IQueryable<T> Query()
    {
        var q = _db.Set<T>().AsQueryable();
        if (_chapterId.HasValue)
            q = q.Where(e => e.ChapterId == _chapterId.Value);
        return q;
    }
}
```

`_chapterId` is resolved from the current user's JWT `chapterId` claim at request startup via a scoped `IChapterContextService`.

### HQ Roll-Up
For HQ reporting, `IDashboardService` exposes:
- Chapter-scoped methods (for ChapterAdmin/Staff): filter by ClaimChapterId
- Cross-chapter roll-up methods (for HQAdmin): `GROUP BY ChapterId` with no WHERE filter
- `ChapterSummaryRow` DTO aggregates key KPIs per chapter for the HQ dashboard

---

## Consequences

**Positive**
- Simple mental model: one column enforces the entire tenancy boundary
- HQ roll-up queries are standard SQL GROUP BY — no complex cross-DB joins
- Adding a new chapter is a single INSERT to the Chapters table
- No operational overhead of managing multiple databases or schemas
- Chapter scoping is enforced at the repository layer, not the controller layer — harder to accidentally bypass

**Negative**
- A bug in `IChapterScopedRepository` could expose cross-chapter data — must be thoroughly tested
- Very large chapters could create table hotspots; mitigated by indexing `ChapterId` on all tenant tables
- If chapters ever need completely isolated data residency (GDPR, state law), a schema-per-tenant or DB-per-tenant migration would be required

---

## Role-to-Scope Matrix

| Role | ChapterId in JWT | Data Scope |
|---|---|---|
| HQAdmin | null | All chapters — no filter |
| ChapterAdmin | Required | Own chapter only |
| ChapterStaff | Required | Own chapter only |
| Volunteer | Required | Own chapter; further filtered to assigned cases |
| Donor | Required (or null for public) | Own giving records only |
| PublicUser | N/A | No authenticated data |

---

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| Three-tier (HQ → Region → Chapter) | Over-engineered for current size; LOTV operates without a regional layer |
| Schema-per-tenant | Significant operational complexity; no benefit at current scale |
| DB-per-tenant | Cost and provisioning overhead; adding a chapter requires database setup |
| Row-Level Security (Postgres RLS) | Adds defense in depth but complex to manage alongside EF Core; can be added later as a secondary control |
