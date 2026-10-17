# Open Questions and Decisions — BizFlow

Version 0.1 · 2026-10-16 · Draft

Questions are not marked resolved merely because a default is recommended. The source SSS v1.2 states that no critical business questions remained at that baseline date; the current list records new evidence/access gaps and future-scope questions surfaced in this SRS review.

## OQ-001 — Which repository is the actual BizFlow application repository (Angular 22 + ASP.NET Core .NET 10 + PostgreSQL)?

|Field|Detail|
|---|---|
|Why it matters|Without it, current routes, entities, migrations, permissions, tests, Git branch/status and implementation coverage cannot be verified.|
|Options|Provide repository URL or a project archive; confirm branch to inspect.|
|Recommended default|Provide the correct repo before marking any implementation or test as verified.|
|Decision owner|Project owner|
|Status|Open — critical evidence blocker|
|Blocking impact|Blocks implementation-readiness assessment; does not block a source-derived draft SRS.|

## OQ-002 — Is SSS v1.2 FINAL still the current canonical baseline, or has it been superseded?

|Field|Detail|
|---|---|
|Why it matters|Older/newer documents could conflict with the working specification.|
|Options|Confirm v1.2 remains canonical; provide any later SSS/ADR/change log.|
|Recommended default|Retain v1.2 plus conversation decisions dated 2026-10-16 until superseding evidence is supplied.|
|Decision owner|Project owner|
|Status|Open — high|
|Blocking impact|Blocks final baseline approval.|

## OQ-003 — What numeric performance target and measurement workload should be approved?

|Field|Detail|
|---|---|
|Why it matters|SSS NFR-PERF-001 proposes p95 <2 s at demo scale but lacks workload and measurement setup.|
|Options|Approve source proposal with explicit workload; choose another target; defer numeric SLO.|
|Recommended default|Treat current value as a proposal, not acceptance gate, until workload and measurement conditions are agreed.|
|Decision owner|Product owner / QA|
|Status|Open — medium|
|Blocking impact|Does not block draft SRS; blocks numeric performance acceptance.|

## OQ-004 — What are the approved data retention, hard-delete, export and backup/restore policies?

|Field|Detail|
|---|---|
|Why it matters|Soft-delete/audit immutability are specified, but retention durations, RPO and RTO are not.|
|Options|Define durations/targets; keep these as explicit operational decisions for demo; obtain legal input if required.|
|Recommended default|Do not invent a retention duration or production recovery commitment; define demo-safe handling and record production values as open.|
|Decision owner|Product owner / Operations|
|Status|Open — medium|
|Blocking impact|Non-blocking for MVP draft; must be addressed before production.|

## OQ-005 — What evidence, if any, must be used to verify the requester’s authority to register a company?

|Field|Detail|
|---|---|
|Why it matters|Initial admin assignment and legal representation are different concepts. SSS describes an initial Company Administrator but does not establish a legal verification procedure.|
|Options|Manual project-team review; contact-email verification; formal legal-representative verification; other approved process.|
|Recommended default|For the demo, treat approval as platform-admin approval of the registration request and do not claim legal representative verification unless actually performed.|
|Decision owner|Product owner / Platform Admin|
|Status|Open — medium|
|Blocking impact|Does not block demo if claims are accurate; blocks stronger verification claims.|

## OQ-006 — How should user identity and membership work when Future Enterprise allows multiple independent workspaces?

|Field|Detail|
|---|---|
|Why it matters|SSS assumes one user belongs to one tenant/company for MVP. Future upgrade may need membership per workspace or organization-level users.|
|Options|Single tenant membership retained; organization-level identity with many workspace memberships; separate users per workspace.|
|Recommended default|Defer implementation decision to Future Enterprise design; avoid MVP changes unless required.|
|Decision owner|Product owner / Architect|
|Status|Deferred — Future Enterprise|
|Blocking impact|Non-blocking for MVP; blocks future multi-workspace design.|

## OQ-007 — What are the package patch versions, model ID, email provider and cloud region?

|Field|Detail|
|---|---|
|Why it matters|These are implementation-time configuration choices documented by SSS, but exact repository configurations were not inspected.|
|Options|Read from repository environment/manifests once available; choose model/provider by tested availability/cost.|
|Recommended default|Do not embed speculative values in requirements; keep environment-specific and review at implementation.|
|Decision owner|Technical lead|
|Status|Open — implementation-time|
|Blocking impact|Not a product-scope blocker; blocks exact environment verification.|

## OQ-008 — What are the actual statuses of P0/P1 features and automated tests in the application?

|Field|Detail|
|---|---|
|Why it matters|SSS defines intended requirements but does not prove code is present or passing.|
|Options|Inspect source, migrations, tests, CI and perform safe checks when repo becomes available.|
|Recommended default|Create an implementation coverage matrix only after exact repo access.|
|Decision owner|Technical lead / QA|
|Status|Open — critical evidence blocker|
|Blocking impact|Blocks implementation readiness and release acceptance.|

## OQ-009 — What pricing, quota and payment terms govern the future paid multi-workspace upgrade?

|Field|Detail|
|---|---|
|Why it matters|Only the product direction for a paid upgrade is confirmed; price, plan names, billing provider and limits are not.|
|Options|Per-workspace fee; tier-based limit; negotiated enterprise contract; defer commercial design.|
|Recommended default|Keep all commercial values undecided; do not implement billing during MVP.|
|Decision owner|Product owner|
|Status|Deferred — Future Enterprise|
|Blocking impact|Non-blocking for MVP; required before launch of paid upgrade.|

## OQ-010 — How will access be handled if a user is invited to multiple workspaces in the future?

|Field|Detail|
|---|---|
|Why it matters|The future enterprise model needs explicit switching, membership, owner/admin and cross-workspace reporting rules.|
|Options|Each workspace membership independently assigned; company-level shared membership; selective delegation.|
|Recommended default|Decide with OQ-006 before feature design; no hidden cross-workspace access.|
|Decision owner|Product owner / Architect|
|Status|Deferred — Future Enterprise|
|Blocking impact|Non-blocking for MVP.|

## OQ-011 — Which capabilities and successful end-to-end paths are required for the 2026-10-19 demo?

|Field|Detail|
|---|---|
|Why it matters|The date is confirmed but there is no verified implementation coverage or stakeholder-defined demo script.|
|Options|Prioritize onboarding + task flow + request flow; include AI and tenant-negative tests only if implemented and stable.|
|Recommended default|Use Section 14.3 recommended demo slice and compare it to the repo once provided.|
|Decision owner|Project owner / Team|
|Status|Open — high|
|Blocking impact|Blocks final demo sign-off; not the product SRS itself.|

## OQ-012 — Should the SSS entity count be reconciled?

|Field|Detail|
|---|---|
|Why it matters|The source scorecard says 39 entities, while 38 distinct “Entity – ...” headings were extracted from Appendix C. This may reflect an entity without a heading or a count error.|
|Options|Inspect the full data dictionary and confirm whether an entity is missing or the scorecard is stale.|
|Recommended default|Do not silently add or remove a domain entity; reconcile when the source repository/spec owner reviews the SSS.|
|Decision owner|BA / Technical lead|
|Status|Open — medium documentation consistency issue|
|Blocking impact|Does not block this draft; blocks declaring the source data dictionary fully internally consistent.|

## Confirmed decisions retained

|Decision ID|Decision|Status|Impact|
|---|---|---|---|
|DEC-WS-001|MVP uses one primary workspace per company (Option C).|Confirmed by user on 2026-10-16.|No MVP multi-workspace implementation.|
|DEC-WS-002|Future paid upgrade may enable multiple independent workspaces per company (Option A).|Confirmed product direction; commercial terms unapproved.|Future Enterprise only; require separate specification/change control.|
|DEC-ONB-001|Company registration must be manually approved before provisioning.|Confirmed by user and SSS.|MVP onboarding.|
|DEC-MEMBER-001|Company Administrator adds users directly.|Confirmed by user on 2026-10-16.|MVP membership/onboarding.|
|DEC-TEN-001|Each MVP user belongs to one tenant/company unless changed by approved future decision.|Baseline assumption in SSS v1.2.|Must revisit before multi-workspace upgrade.|