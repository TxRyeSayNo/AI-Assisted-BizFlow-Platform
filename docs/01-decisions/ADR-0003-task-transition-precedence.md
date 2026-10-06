# ADR-0003 — Exact canonical task transition graph

Status: Accepted by project owner on 2026-10-01.

## Conflict

FR-TASK-007 includes OVERDUE among result-submission preconditions, but the canonical SSS §9.1 table does not contain OVERDUE → SUBMITTED. FR-TASK-008 mentions rejected results entering REJECTED or IN_PROGRESS according to workflow, but §9.1 only defines SUBMITTED → IN_PROGRESS for rework.

## Owner decision

The owner answered: “Keep §9.1 exact: resume work before submission; use IN_PROGRESS for rework”.

- Preserve the exact §9.1 task transition graph.
- An OVERDUE task must resume through OVERDUE → IN_PROGRESS before IN_PROGRESS → SUBMITTED.
- A rejected submitted result uses SUBMITTED → IN_PROGRESS, subject to the configured workflow and reviewer authorization. Do not add SUBMITTED → REJECTED.
- ASSIGNED → REJECTED remains the existing employee assignment-rejection path, with required reason; REJECTED → ASSIGNED remains available to an authorized manager.
- Published workflow configuration cannot authorize either excluded edge. This does not create a new task state or alter Request rejection/revision semantics.

This narrowly resolves the two conflicting task FR statements against the canonical table. It does not approve any other pending schema, onboarding, authentication or organization decision. The original SSS is preserved; this decision records implementation precedence explicitly.

## Evidence

`TaskLifecyclePolicy` rejects both excluded direct edges even when other evidence and grants would permit an action. `TaskLifecyclePolicyTests` exercises the explicit resume/submission and rework paths, assignment rejection, and every user/system state pair. Persistence, workflow-engine integration, task APIs/UI and end-to-end acceptance remain required.
