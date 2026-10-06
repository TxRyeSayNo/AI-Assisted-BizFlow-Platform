# SLA timing snapshot editor

Scope: the timing-authoring portion of FR-SLA-001 / UI-03, using canonical API-SLA-03 and the history endpoint approved in ADR-0007. ADR-0004/0005/0006 remain unchanged. This is not full calendar/escalation authoring or SLA runtime acceptance.

## Behavior and boundaries

Settings → SLA profiles → saved version → Adjust timing opens a GRID drawer only for `sla.configure`. The user chooses target and elapsed-from-start warning working minutes. Both must be integers within the API's signed integer range, with `0 ≤ warning < target`. The source version's frozen calendar ID and complete escalation configuration are copied unchanged into `POST /api/v1/sla-profiles/{id}/versions`. The source is cloned locally, never modified, and no currently pinned work is rebound. An earlier version may be used as the source; the server assigns the next profile-wide version number under its existing lock.

The existing Application service rechecks live tenant configuration authority, validates working time and active same-tenant escalation recipients, and atomically persists the new immutable version and `SLA.VERSION_CREATED` audit. Controllers, API contracts, permissions, tables and migrations are unchanged by this UI increment. Frontend permission checks are usability controls, not an authorization boundary.

The drawer disables duplicate submission and dismissal while saving. A matching successful response returns to fresh history. Network failures, unexpected server failures or inconsistent success bodies are explicitly uncertain: the form cannot POST again and offers history review. History is not an idempotency guarantee and cannot identify which concurrent client created a matching snapshot. Definite validation failures allow correction; missing resources or lost authority stop resubmission. Server/provider details are never rendered. The existing authentication interceptor may retry after an unauthenticated 401 and successful token refresh; there is no retry of an uncertain mutation.

An existing escalation policy with an inactive recipient is rejected by the backend, even though it was valid when its original version was saved. The timing editor reports this limitation rather than dropping or rewriting recipients. Full escalation editing remains separate. Empty profiles cannot yet create their first version through this drawer because calendar administration/default selection awaits its owner decision.

## Verification

`sla-timing-editor.spec.ts` covers immutable source handling, exact payload preservation, integer/threshold bounds, revoked authority, duplicate prevention, definite rejection recovery, unknown outcomes, inconsistent responses and destroyed subscriptions. History tests cover configure gating, current-row membership, saved/uncertain outcomes and stale navigation. `real-specs/sla-timing.spec.ts` uses separate disposable desktop/mobile tenant fixtures to create a real version, reread persistence, and compare historical/new calendar and escalation details. Existing backend SLA integration tests cover tenant isolation, concurrent numbering, immutable snapshots, recipient eligibility and atomic audit failure.

Run results are recorded in [implementation status](implementation-status.md). No complete-platform or production-readiness claim follows from this slice.
