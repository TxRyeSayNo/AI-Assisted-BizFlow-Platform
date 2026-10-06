# SLA version creation API

Scope: API-SLA-03, the save-version portion of FR-SLA-001, BR-010 and accepted ADR-0004/0005/0006. This uses the approved profile/calendar/version model and does not introduce a table, state or publication endpoint.

## Contract

`POST /api/v1/sla-profiles/{id}/versions` accepts `targetMinutes`, `warningMinutes`, an explicit existing `calendarId`, and optional `escalationConfig`. Omitted/null escalation means no levels. Unknown top-level or configuration fields, caller-selected version numbers, tenant IDs and status/publication fields are rejected. Target must be positive and warning must be nonnegative and strictly below target; warning is elapsed working minutes from start. The approved typed escalation contract is in ADR-0006.

Success is 201 with `slaVersionId`, `slaProfileId`, `versionNo`, `targetMinutes`, `warningMinutes`, `calendarId` and structured nullable `escalationConfig`. Responses use no-store. No unsupported item-GET endpoint/Location URI is invented. Creation does not activate a profile, select a default calendar, bind/rebind a service or start a runtime clock.

The explicit calendar reference pins that same-tenant configuration. Empty weekly schedules are invalid for version creation. Calendar administration/provisioning is still absent; the endpoint does not silently create a demo/default calendar or infer one from historical records. The proposed default-calendar setting/API remains subject to its separate owner decision.

## Layers and transaction

`SlaProfilesController` delegates to Application `SlaVersionCreation`. Application authorizes live `sla.configure` before storage, validates the payload using Domain rules, controls the next version number and constructs the actual actor's `SLA.VERSION_CREATED` audit. A read grant, matching role name or platform account is not configuration authority.

Infrastructure `SlaVersionStore` opens a transaction and locks the tenant-owned profile then calendar. Only after acquiring the parent lock does it read the latest version number. Concurrent API requests receive successive numbers; overflow returns `409 SLA.VERSION_LIMIT`. Missing and foreign profile/calendar references share a safe 404 and caller-scoped denial audit without revealing foreign IDs.

Recipient rows are locked in UUID order and checked for active, non-deleted same-tenant membership. Application rechecks authorization after configuration locks and again after recipient locks, so an operation that waited while grants were revoked cannot proceed with its earlier authorization result. The database snapshot/recipient guards remain defense in depth.

Version and mutation audit commit together. Audit stores the profile, server number, thresholds, calendar reference and structured escalation configuration. A failed audit insert rolls back the version and does not consume a number. Security-denial audits use their existing independent context, so rollback of an attempted business mutation does not erase denial evidence. No automatic POST retry/idempotency contract is introduced. The owner-approved [history reader](sla-version-history.md) supports manual inspection after uncertain saves, but cannot prove which concurrent client created an identical snapshot. Do not automatically retry ambiguous creates or claim the configuration UI is complete.

## Evidence and remaining work

`SlaVersionCreationTests` checks Application authorization order, disposal, missing-reference evidence, server numbering and actor attribution. `SlaProfileCatalogTests` exercises the real endpoint for concurrency, foreign references, unsupported input, actor/permission boundaries, exact saved configuration, immutable history, atomic audit rollback and permission revocation during an observed database lock wait. Full run results are in [implementation status](implementation-status.md).

The subsequent [timing editor](sla-timing-editor.md) exposes this API for new snapshots based on an existing version, preserving its calendar and escalation policy. Still required: first-version/calendar/escalation authoring, default-calendar configuration/provisioning, profile/service selection, calendar-aware UTC runtime state, pause/resume, warning/overdue/escalation scheduling, notification delivery/idempotency and final SLA acceptance. This does not complete FR-SLA-001…004.
