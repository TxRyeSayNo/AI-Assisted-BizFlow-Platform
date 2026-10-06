# ADR-0007: SLA version history reader

Status: Accepted by the owner on 2026-10-03 ("Approve SLA version-history endpoint").

The canonical catalog includes API-SLA-03 creation but lacks the configuration-history reader needed by FR-SLA-001's UI and uncertain-save reconciliation. Add tenant-authorized `GET /api/v1/sla-profiles/{id}/versions` with pagination and frozen calendar/configuration details.

The implementation uses the existing live `sla.read` permission; `sla.configure` alone does not grant read access. Pages default to 25, allow 1–100 rows, and sort by descending version number. Foreign and nonexistent profiles share a safe 404. Responses are not cacheable. Calendar data comes from each immutable version's referenced calendar, never a current/default calendar. There are no new states, tables or mutation operations.

This approval does not approve the separate default-calendar administration proposal or any unrelated pending owner decision.
