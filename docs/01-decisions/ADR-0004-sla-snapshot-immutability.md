# ADR-0004 — Immutable SLA and referenced calendar snapshots

Status: accepted by the project owner on 2026-10-02.

## Context

The approved SLAVersion dictionary has no publication status/timestamp, while architecture §4.2 requires immutable published SLA versions and the canonical API catalog provides version creation rather than a separate publication operation. Calendar changes must not retroactively alter historical SLA calculations.

## Owner decision

The owner explicitly confirmed: saved SLA versions are immutable snapshots from creation; every SLA change creates a new version; the referenced calendar configuration remains frozen for that version so historical calculations remain deterministic and reproducible.

## Implementation consequences

- Preserve the existing SLAVersion fields; do not add publication status or timestamp columns.
- Never edit a persisted SLA version's duration, warning, calendar, escalation configuration, profile ownership or version number. Changes create the next version under the same logical profile.
- Freeze a BusinessCalendar once referenced by a saved SLA version. A changed calendar requires a new calendar record referenced by the new SLA version; old references and calendar contents remain intact.
- Version creation still requires tenant ownership, application authorization, full configuration validation, concurrency protection and atomic audit. Immutability does not make an invalid configuration valid or automatically activate it for a service/object.
- Application/EF/database mutation boundaries and concurrent calendar-edit/version-create behavior must be tested. Stored runtime objects continue to pin their version; no silent rebinding.

This decision does not approve unrelated pending owner decisions, new tables, extra SLA states or P2 calendar scope. The profile catalog increment alone does not implement SLA versions or timing.
