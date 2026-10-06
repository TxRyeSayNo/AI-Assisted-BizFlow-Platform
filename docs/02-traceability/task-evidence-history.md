# Task progress and result history

Scope: DEV-D5 persistence prerequisite for FR-TASK-005/006/007/008, Appendix C TaskProgressReport/TaskResult, BR-001/015/016 and ADR-0003. This does not implement progress/result APIs or execute Task lifecycle transitions.

## Approved model

Both entities retain their six specified fields. TaskProgressReport has ProgressReportId, TaskId, AuthorId, Percent, nullable Content and SubmittedAt. TaskResult has TaskResultId, TaskId, AuthorId, nullable Content, RevisionNo and SubmittedAt. No tenant column, report-type discriminator, correction table or additional business state is invented. Ownership derives from the persisted Task. This increment brought the model to 28 of the approved 43 application tables; subsequent [assignment history](task-assignment-history.md) brings it to 29.

Percent is SMALLINT in 0–100 with default 0; RevisionNo is positive with default 1. Both have restrictive Task/author foreign keys and indexes supporting parent history plus author lookup. Submission timestamps normalize to UTC. Text is plain data, not trusted HTML; unsupported control characters are rejected and optional blank text becomes null.

The separate formal-report factory requires nonblank content (FR-TASK-006), while ordinary progress permits an absent note. The result snapshot factory preserves the approved nullable-content column. A row's existence alone is **not** sufficient result evidence: the future submission use case must validate required result/evidence, files and performer authority before the workflow can enter SUBMITTED. There is no result submission endpoint yet.

## Persistence and history

Default EF reads inherit authenticated tenant and soft-delete scope from Task. Added records require a persisted visible Task and same-tenant author; tracked forged parents cannot establish ownership. Result inserts additionally require IN_PROGRESS. Existing-record updates and hard deletes are rejected.

PostgreSQL locks the parent before inserting either evidence type, verifies live parent/author tenancy and plain-text/range constraints, and requires IN_PROGRESS for TaskResult. This preserves ADR-0003: OVERDUE cannot submit a result before resuming. Concurrent parent changes cannot be overlooked by an old snapshot. Submitted results and reports are append-only, including no-op updates, delete and TRUNCATE. Rework/correction appends records; it does not overwrite prior content or timestamps.

Progress state eligibility and non-decreasing percentage/correction-reason validation remain Application use-case gates. They are not silently defined by these storage factories/guards. The future use case must resolve the prior report under the parent lock, enforce the business rule and persist the correction reason in audit evidence. Server-side result revision allocation, actor permissions and workflow conditions likewise remain required. Raw-SQL lifecycle changes in tests are fixtures, not an implementation of those commands.

Empty rollback/reapplication is supported. The migration refuses rollback if either evidence table contains history, without discarding the other table.

## Verification and remaining work

Tests cover Domain ranges/text/identity/formal-report validation; nullable fields and defaults; immutable earlier revisions; tenant/anonymous/platform isolation; forged parents; raw-SQL references; soft-deleted parent visibility; all non-IN_PROGRESS result states; append-only protection; rollback with either table populated; and lock-observed overdue races at READ COMMITTED, REPEATABLE READ and SERIALIZABLE. Execution results are recorded in [implementation status](implementation-status.md).

The final full backend suite passed 222 unit and 215 PostgreSQL integration tests; EF reported no pending model changes. No frontend or public API contract changed, and no Task UI acceptance is claimed.

Still required: assignment/performer resolution, current-progress correction checks, formal-report/result Application orchestration, revision numbering, file evidence validation, submission/review/confirmation and closure integration, audit/notification production, canonical APIs, GRID UI and end-to-end acceptance.
