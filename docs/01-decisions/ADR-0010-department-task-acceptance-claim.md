# ADR-0010: One-time department Task assignment claim

Status: Accepted by the project owner on 2026-10-04.

## Context

BR-004 makes a department assignment a queue/team responsibility until a user accepts or is assigned. TaskAssignment has nullable UserId and AcceptedAt but no AcceptedBy field. FR-TASK-002 requires preserving assignment history.

## Decision

For a department-only Task assignment, acceptance may atomically fill the initially-null UserId with the accepting active member of that department. The user must belong to the same tenant. DepartmentId, AssignedBy, AssignedAt and assignment identity remain unchanged. After this claim, UserId and the acceptance decision are immutable. Audit/confirmation must identify the actual accepting actor.

The decision does not permit replacing an existing target, claiming a different department, accepting an ended/rejected/already-accepted assignment, bypassing current authority/workflow checks or adding a table/column. The acceptance use case must perform claim, confirmation, Task transition and audit within its authorized concurrency-protected transaction. This approval is not evidence that the complete acceptance API is implemented.
