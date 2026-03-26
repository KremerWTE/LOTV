# ADR-002: Database and ORM Choice
**Status**: Accepted
**Date**: 2026-03-25
**Deciders**: Chris Kremer

---

## Context

LOTV needs a relational database to store families, cases, donors, donations, volunteers, events, and audit data. The system is multi-tenant by chapter. The team is .NET-native. The org is a nonprofit with cost sensitivity.

---

## Decision

**PostgreSQL (production) + SQLite (local development)**
**EF Core 9 with Code-First Migrations**
**Repository pattern over direct DbContext injection**

### Database
- **PostgreSQL** is the production database. It is free, open-source, highly capable, and has excellent EF Core provider support (`Npgsql.EntityFrameworkCore.PostgreSQL`). It avoids the SQL Server licensing cost.
- **SQLite** is used for local development and integration tests — zero setup, file-based, EF Core provider identical.
- Connection string switches between providers based on `ASPNETCORE_ENVIRONMENT`.

### ORM
- **EF Core 9** (code-first) generates and applies all schema migrations. No raw SQL schema management.
- All entities defined in `Lotv.Core` as plain C# classes; EF configuration via `IEntityTypeConfiguration<T>` classes in `Lotv.Api`.
- Migrations stored in `Lotv.Api/Migrations/`.

### Access Pattern
- **Generic `IRepository<T>` with a `IUnitOfWork`** wraps EF Core DbContext.
- Chapter-scoped queries are enforced by a `IChapterScopedRepository<T>` that automatically appends `WHERE ChapterId = @id` based on the current user's JWT claim.
- HQAdmin uses the base `IRepository<T>` with no chapter filter.

---

## Consequences

**Positive**
- PostgreSQL is free and widely hosted (Azure Database for PostgreSQL Flexible Server, Railway, Supabase)
- EF Core migrations provide a reproducible, version-controlled schema history
- SQLite for local dev means no Docker or cloud dependency for a developer onboarding
- Repository pattern isolates data access, making unit testing possible without hitting a real DB
- Code-first keeps entity definitions as the source of truth

**Negative**
- PostgreSQL and SQL Server have some EF Core behavioral differences — team must test migrations against Postgres specifically, not just SQLite
- EF Core can produce suboptimal queries for complex aggregates; may need raw SQL or compiled queries for dashboard roll-ups
- Repository/UoW adds boilerplate compared to direct DbContext injection

---

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| SQL Server | Licensing cost; Azure SQL pricing unsuitable for nonprofit budget |
| Dapper (micro-ORM) | No migrations; more manual schema management; acceptable for performance-critical reads but not as primary ORM |
| MongoDB | Schema-less adds complexity without benefit for this relational domain |
| Direct DbContext injection | Reduces testability; harder to enforce chapter scoping consistently |

---

## Multi-Tenant Strategy

**Single database, ChapterId column on all tenant-scoped entities.** This is the simplest, most cost-effective approach for the current scale. All tables that represent chapter-owned data have a `ChapterId INT NOT NULL` column indexed.

DB-per-tenant was evaluated and rejected: the number of chapters is small and adding a new chapter should not require database provisioning.
