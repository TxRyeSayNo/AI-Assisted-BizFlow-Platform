# TD-01–04 acceptance cases (approved contract, written before implementation)

These are required test cases, not passing-test claims. They complement the existing exhaustive canonical TaskLifecyclePolicy and scoped Task-list tests.

| Decision | Automated evidence required |
|---|---|
| TD-01 | Exact SSS §9.1 state pairs remain enforced; no direct OVERDUE → SUBMITTED or SUBMITTED → REJECTED. Creation remains DRAFT and nullable pins do not introduce another graph. Existing Domain transition tests must remain green. |
| TD-02 | Reject local/date-only, malformed, equal-now and past deadlines; accept future Z and explicit offsets; persist UTC; omission remains null. An identical replay after the original deadline passes still returns the original result without a new Task. |
| TD-03 | Identical replay yields original id/body and one Task/checklist/audit set; changed title, checklist/order, priority or deadline with the same key gives 409; parallel identical requests create once; conflicting parallel payloads produce one winner; tenant/user keys are isolated; current revoked permission denies replay; fingerprint/key hashes persist but raw key does not; audit failure rolls back all rows; populated replay-index rollback is protected. UI retains the key after unknown outcomes and prevents double-click submission. |
| TD-04 | Creator, direct assignee, unclaimed department queue, managed target and tenant-read cases follow ADR-0011; ended/claimed unrelated assignments do not grant access; missing/foreign/out-of-scope all 404; anonymous 401; denied reads audited; related checklist/report/result rows cannot escape the authorized parent; pagination validates bounds and stable ordering; no linked Request body, credentials or unrelated tenant/user data in the response. |
| Vertical slice | Manager creates a real Task with checklist and future explicit-offset deadline, sees it in own scoped list and reopens persisted detail on desktop/mobile. Unauthorized actors cannot create; UI permission hiding is not relied upon for server security. Build, Angular/backend tests and relevant real-browser checks must pass. |
