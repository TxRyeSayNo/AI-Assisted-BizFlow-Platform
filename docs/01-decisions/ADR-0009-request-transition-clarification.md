# ADR-0009: Request rework and overdue recovery

Status: Accepted by the project owner on 2026-10-04.

## Context

FR-REQ-008 permits rejected resolutions to return for rework, but §9.2 omits the corresponding edge. It also includes OVERDUE without any exit from it. The owner explicitly approved two additional Request transitions to resolve these gaps.

## Decision

- RESOLVED → IN_PROGRESS: requester-requested rework, preserving prior resolution and confirmation history.
- OVERDUE → IN_PROGRESS: resume processing.
- Accepted resolutions continue through RESOLVED → CONFIRMED → CLOSED; do not shortcut the confirmation milestone.

These edges must still pass the pinned workflow, current permission, tenant/resource scope and business-precondition checks. This decision adds no business states, changes no actors and does not authorize direct state writes. It does not reopen a REJECTED Request: BR-008 still requires a new linked revision.

The independent Task precedence decision in ADR-0003 remains unchanged. This ADR approves Request graph behavior, not completion of its runtime implementation.
