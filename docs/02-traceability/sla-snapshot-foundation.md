# SLA/calendar snapshot persistence

Scope: approved BusinessCalendar and SLAVersion entities, BR-010, A-08, [ADR-0004](../01-decisions/ADR-0004-sla-snapshot-immutability.md), [ADR-0005](../01-decisions/ADR-0005-sla-warning-calendar-contract.md) and [ADR-0006](../01-decisions/ADR-0006-sla-escalation-contract.md). These are prerequisites for configuration and service/runtime version pinning, not complete FR-SLA-001…004 acceptance.

## Data model and validation

`20261002182914_SlaSnapshotFoundation` adds two already-approved tables, bringing the implemented application model to 20 of 43. BusinessCalendar has its five approved business fields; SLAVersion retains its seven fields including positive per-profile VersionNo. Both use PostgreSQL xmin as a technical concurrency token. No publication fields, convenience table, actor grant, default configuration or runtime clock is added.

- Versions require `target > 0` and `0 <= warning < target` (A-08). WarningMinutes means elapsed working minutes from start (ADR-0005); no clock calculation is implemented by these storage fields.
- Calendar validation accepts lowercase weekday arrays of nonoverlapping, positive same-day intervals, minute-precision HH:mm and an end-only 24:00 boundary. Adjacent-day intervals represent overnight work. Holidays are unique, real YYYY-MM-DD dates. Unknown fields, duplicate input keys, malformed times, overlaps and invalid dates are rejected. PostgreSQL JSONB cannot retain duplicate object keys; duplicate raw input detection occurs in Domain before JSONB conversion.
- Calendar timezone is an IANA identifier. Domain follows the existing tenant timezone validation; SQL additionally checks the database timezone catalog. Empty hours are valid unconfigured calendar storage but cannot be referenced by an SLA version.
- EscalationConfigJson uses the approved ordered levels with strictly increasing nonnegative offsets after breach and nonempty unique recipient UUID lists. Domain and SQL enforce the schema; EF and SQL require active, non-deleted recipients in the owning tenant. SQL locks recipient rows in UUID order and rechecks eligibility after concurrent deactivation. This is configuration validation, not delivery/scheduler implementation. Null or an empty levels array means no escalation.

## Tenant and immutability boundaries

Queries derive version tenancy through SLAProfile and require current user/tenant context. EF rejects foreign references and forged attached parent/calendar identities by resolving persisted ownership (or genuinely Added parents in the same unit of work). SQL also enforces matching profile/calendar tenancy; a version cannot be reparented after creation.

Saved SLA versions reject all updates and deletes in EF and SQL. Domain exposes construction, not mutation. Calendars referenced by any saved version reject business-value changes and deletion in SQL. The current EF boundary allows only calendar creation; an authorized calendar-edit/default-calendar administration use case is not yet exposed. A changed configuration can be represented by a new calendar record without altering history.

Reference creation performs a value-preserving update of the calendar row inside the same transaction, changing only its technical MVCC version. This serializes concurrent configuration writes and causes stale repeatable-read/serializable editors to fail instead of overlooking a newly committed reference. It never changes timezone, hours, holidays or identity. A simple row lock without a row update would not cover the older-snapshot case; see [PostgreSQL's consistency guidance](https://www.postgresql.org/docs/18/applevel-consistency.html). Before exposing configuration writes, Application must resolve version-number conflicts, compare any required expected calendar version, and audit actual business changes atomically.

Rollback refuses to discard any stored version or calendar, including unconfigured calendars. Empty rollback/reapplication is supported. No application database is migrated automatically; tests use disposable PostgreSQL.

## Required follow-through

- The later [SLA version creation API](sla-version-creation.md) implements Application authorization, typed validation, safe numbering and atomic configuration audit. This storage increment's tests alone do not prove that API boundary.
- Default calendar provisioning/selection/administration, version history and configuration UI, service binding and runtime version pinning.
- Calendar-aware UTC calculations, DST-boundary policy where relevant, pause/resume, warnings/overdue/escalation idempotency and tenant-policy consumers.
- Calendar-content immutability does not pin the operating system's future timezone-rule database. Runtime calculations must persist authoritative computed UTC instants in the approved runtime state and define reproducibility across timezone-rule upgrades; frozen JSON alone is not proof of full historical runtime determinism.
- Current verification results and outstanding gates are recorded in [implementation status](implementation-status.md). Full SLA acceptance is not inferred from storage tests.
