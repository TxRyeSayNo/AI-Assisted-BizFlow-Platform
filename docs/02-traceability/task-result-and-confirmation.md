# Task result submission and confirmation/rework — implementation contract

Scope: FR-TASK-007, FR-TASK-008, POST `/api/v1/tasks/{id}/result`, POST `/api/v1/tasks/{id}/confirmation`, BR-001/002/004/012/016/020, SSS §9.1, ADR-0003/0012.

- **Result submission (performer)**:
  - Input: `{ content: string }`.
  - Permission: `tasks.submit` (Employee, Manager, Admin).
  - Preconditions: Active tenant performer matching current accepted individual assignment, `IN_PROGRESS` task state, unpinned workflow/SLA.
  - Lifecycle transition: `IN_PROGRESS` → `SUBMITTED`.
  - Persistence & Evidence: Append-only `TaskResult` snapshot record with auto-incrementing `RevisionNo`, updated `Task.Status = 'SUBMITTED'` and `Task.UpdatedAt = now`. Trigger `bizflow_task_evidence_guard()` enforces that `TaskResult` can only be appended for in-progress work or within the atomic transaction transitioning to `SUBMITTED`.
  - Audit & Idempotency: Append-only `AuditLog` action `TASK.RESULT_SUBMITTED`, tenant+actor advisory lock, idempotency key hash uniqueness index `UX_AuditLog_TaskResultSubmissionReplay`.
  - Notification: In-app `Notification` of event `TaskSubmitted` sent to task assigner / creator.

- **Confirmation / Review (reviewer)**:
  - Input: `{ decision: "CONFIRMED" | "REWORK", note?: string }`.
  - Permission: `tasks.confirm` (Manager, Admin).
  - Preconditions: Reviewer in task tenant, `SUBMITTED` task state, existing submitted result evidence, required review note if requesting rework.
  - Lifecycle transitions:
    - Approve (`CONFIRMED`): `SUBMITTED` → `COMPLETED` (with milestone confirmation satisfied and `Task.CompletedAt = now`).
    - Rework (`REWORK`): `SUBMITTED` → `IN_PROGRESS` (returns task to performer for revised deliverables).
  - Persistence & Evidence: Append-only `Confirmation` milestone record with `MilestoneType = "RESULT"`, `Decision = "CONFIRMED"` or `"REJECTED"`, actor ID and reviewer note.
  - Audit & Idempotency: Append-only `AuditLog` action `TASK.RESULT_CONFIRMED` or `TASK.RESULT_REWORK`, tenant+actor advisory lock, idempotency key hash uniqueness index `UX_AuditLog_TaskResultConfirmationReplay`.
  - Notification: In-app `Notification` of event `TaskConfirmed` sent to assigned performer.

- **UI & Frontend**:
  - `TaskSubmissionEditor` dialog: Plain-text deliverables editor, idempotency key preservation across retry, validation and error handling.
  - `TaskConfirmationEditor` dialog: Decision selection (`CONFIRMED` / `REWORK`), mandatory note on rework, error handling.
  - `TaskDetailComponent`: Action buttons "Submit Deliverable" and "Review & Confirm" wired to user permissions (`tasks.submit`, `tasks.confirm`) and matching state (`IN_PROGRESS`, `SUBMITTED`), with deliverables history list rendering all revisions and confirmation decisions.

- **Verification evidence**:
  - Unit tests: `TaskSubmissionTests.cs`, `TaskConfirmationTests.cs` (300 passed).
  - Integration tests: `TaskResultAndConfirmationApiTests.cs`, `TaskEvidencePersistenceTests.cs` (273 passed against PostgreSQL 18).
  - Angular frontend tests: `task-submission.spec.ts`, `task-confirmation.spec.ts` (151 passed).
  - Production build: Angular compilation clean.
