# Branch task creation fix — 24 November 2026

The local API log confirmed HTTP 500 on `POST /api/v1/tasks/ai-decomposition/confirm`:
`Task creation audit requires a draft and its initial checklist.` The store constructed
an already-assigned task and then attempted a draft creation audit. Persistence also
requires new tasks to be drafts, so changing the audit alone would not fix the workflow.

The store now saves the draft, initial checklist and `TASK.CREATED` snapshot first,
then checks live assignment permission/department scope and uses the workflow engine
to assign it. `TASK.ASSIGNED`, the assignment and inbox notifications are saved in the
same transaction. Any failure rolls back the entire batch, including earlier saves.
Timestamps are normalized to PostgreSQL microsecond precision for workflow checks.
Invalid/ambiguous departments and invalid checklist/duration/priority inputs return
validation errors. A support request without an available service/category returns a
clear error instead of silently turning into a task.

The frontend displays creation errors inside the modal, preserves failed proposals,
blocks concurrent submission and omits created branches from subsequent bulk creation.
Escape closes the modal even when a failed submission has replaced the focused control.

Validation on the final code:

- 671 backend unit tests passed.
- 39 related AI/assignment integration tests passed before final validation refinements.
- All 6 final creation regression cases passed against disposable PostgreSQL: one assigned
  task, ten assigned tasks, draft creation plus whole-batch rollback, invalid checklist and
  revoked scope, mixed task/request creation with department-name resolution, and missing
  support service. These tests inspect persisted records, checklist, assignments, creation
  and assignment audits, notifications and task listing where applicable.
- 47 frontend AI/widget tests passed, including visible errors, retry and duplicate-click
  prevention. Four final browser tests passed at desktop/laptop/mobile/short viewport sizes,
  exercising modal errors and Escape. Browser submission errors are mocked; actual writes
  are verified by the PostgreSQL integration cases.
- Frontend production build passed. Updated local backend was restarted; `/health/live`
  returned HTTP 200 both directly on port 5154 and through the frontend on port 4200.

No test tasks or assignment notifications were sent to the development company's users.
Database writes for validation were confined to the disposable integration database.
The full repository integration suite was not run. This fix prevents concurrent clicks and
resubmitting known-created branches; it does not add durable idempotency for ambiguous
network failures after a server commit.
