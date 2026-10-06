# Pending owner decisions

Task queue rejection authority (2026-10-06): ADR-0010 approves one-time department acceptance claims but does not say whether any unclaimed queue member may reject the whole assignment. Asked whether rejection must be limited to specifically assigned users or may be exercised by any active department member with `tasks.accept`. Acceptance implementation proceeds under the approved contract; no department rejection authority is inferred.

The remaining requests below are unanswered as of 2026-10-06 and are **not approved amendments**. The owner explicitly approved the four Task decisions as TD-01–04, now recorded in ADR-0012. No unrelated approval is inferred from the time limit.

| Decision | Conflicting/missing requirement | Proposed resolution awaiting confirmation |
|---|---|---|
| Request active-state sets | §9.2 uses “Any active”/“Active” for cancellation/overdue without enumerating source states | Cancellation from DRAFT/SUBMITTED/ROUTED/RECEIVED/IN_PROGRESS/WAITING_FOR_INFORMATION/RESOLVED/CONFIRMED/OVERDUE; system overdue only from SUBMITTED/ROUTED/RECEIVED/IN_PROGRESS/WAITING_FOR_INFORMATION with a breached applicable running SLA/deadline. CLOSED/REJECTED/CANCELLED stay terminal. Owner confirmation requested; not implemented |
| Request action permissions | Rejection, cancellation and additional-information actions lack named permission codes | Separate `requests.reject`, `requests.cancel`, `requests.information`; preserve requester decisions under `requests.confirm` and assigned processing under `requests.process`. Default grants to be reviewed separately. Owner confirmation requested; no grants added |
| Tenant-user email | FR-AUTH-004 supports employees without email; Appendix C requires User.Email | Permit nullable email for tenant users only; require email for platform accounts; preserve per-tenant uniqueness for non-null addresses |
| Company approval evidence | API-TEN-03 approval and API-TEN-04 provisioning are separate; FR-TEN-002 requires prior approval; Company has no APPROVED state | Record immutable COMPANY.APPROVED audit evidence, keep Company PENDING until successful provisioning sets ACTIVE |
| Department name uniqueness | FR-ORG-004 requires unique name/code per tenant; Appendix C marks Name non-unique | Owner to choose unique name and code, or unique code only; no choice inferred |
| Forced password-change API | A-12 requires an administrator-issued temporary password and forced change, but does not define the completion endpoint/response contract | Valid temporary-password login returns AUTH.PASSWORD_CHANGE_REQUIRED without session tokens; POST /api/v1/auth/change-temporary-password verifies the temporary credential and atomically saves the new password; existing normal login/email-reset contracts remain unchanged |
| Workflow draft-update API | FR-WF-001 requires editing drafts; the canonical catalog defines creation/version creation/read/publish/runtime endpoints, but no update endpoint | Add PUT /api/v1/workflow-versions/{id} with required If-Match, replacing definition/steps/transitions only while DRAFT; preserve published immutability. Owner confirmation requested; no update endpoint implemented |
| Platform notifications | UI-14 includes platform administrators; approved Notification.TenantId is required, while approved platform accounts are tenantless | Permit null Notification.TenantId only for a platform-account recipient; platform event dispatch and the platform inbox remain unimplemented pending confirmation. The tenant inbox retains required TenantId and recipient ownership |
| Default calendar selection/API | SSS §3.1 requires one configurable default per tenant; frozen historical calendars have no current-default marker, and the canonical catalog lacks calendar administration | Add `sla.default_calendar_id` tenant setting validated against a same-tenant calendar; GET/PUT `/api/v1/business-calendar` with If-Match concurrency. Updates create a new calendar and switch the setting; saved versions keep old references. No extra table. Owner confirmation requested; not implemented |

The task-transition conflict was resolved on 2026-10-01: the owner chose exact §9.1 transitions, requiring resume before result submission and IN_PROGRESS for result rework. See [ADR-0003](../01-decisions/ADR-0003-task-transition-precedence.md). This does not approve the unrelated decisions above.

The earlier explicit owner approval ("Oke") covered the authentication-session table and platform-only nullable User.TenantId, recorded in ADR-0002. It does not approve any of the above changes. The project goal and full SSS scope remain active and incomplete.

The owner approved immutable saved SLA versions and frozen referenced calendar configuration on 2026-10-02; [ADR-0004](../01-decisions/ADR-0004-sla-snapshot-immutability.md) records this accepted decision. It is not a pending approval and does not resolve the unrelated items above.

The paginated SLA version-history endpoint was approved on 2026-10-03 and is recorded in [ADR-0007](../01-decisions/ADR-0007-sla-version-history.md). It is no longer pending. This does not approve default-calendar administration.

The owner also approved elapsed-from-start WarningMinutes and the weekday-interval/ISO-holiday calendar format ([ADR-0005](../01-decisions/ADR-0005-sla-warning-calendar-contract.md)), and ordered escalation levels targeting active same-tenant users ([ADR-0006](../01-decisions/ADR-0006-sla-escalation-contract.md)) on 2026-10-03. Those SLA contract choices are accepted, not blockers.

On 2026-10-04, the owner approved separate tenant-scoped `service.update` authority, default COMPANY_ADMIN only, for If-Match-protected metadata changes preserving categories and bindings. [ADR-0008](../01-decisions/ADR-0008-service-update-permission.md) records it.

The owner also approved RESOLVED → IN_PROGRESS for requester-requested rework and OVERDUE → IN_PROGRESS for Request resume, while retaining RESOLVED → CONFIRMED → CLOSED on acceptance. [ADR-0009](../01-decisions/ADR-0009-request-transition-clarification.md) records this resolved graph ambiguity. Rejected Request revision remains a separate immutable-history rule.

On 2026-10-04, the owner approved a one-time UserId claim when an active same-department user accepts a department-only Task assignment. Original assignment metadata is preserved and the accepting user/decision then becomes immutable. [ADR-0010](../01-decisions/ADR-0010-department-task-acceptance-claim.md) records this accepted decision; it does not resolve the unrelated pending questions above.

The owner also approved the tenant/managed/own/assigned Task-read capabilities and their explicit default grants on 2026-10-04. [ADR-0011](../01-decisions/ADR-0011-scoped-task-read-policy.md) records the accepted union-of-grants policy; platform inspection remains separate.
