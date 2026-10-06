# SLA version history read slice

Scope: FR-SLA-001 / UI-03 configuration inspection, BR-010 historical integrity, and the additive GET contract explicitly approved in [ADR-0007](../01-decisions/ADR-0007-sla-version-history.md). This document records the original read-only inspection slice, not full SLA configuration or runtime acceptance. The subsequent [timing snapshot editor](sla-timing-editor.md) adds permission-controlled version creation; the inspection drawer remains read-only.

## Contract and layers

`GET /api/v1/sla-profiles/{id}/versions?page=1&pageSize=25` requires a live tenant-scoped `sla.read` grant. Page size is 1–100; page must be positive and its offset fit a signed 32-bit integer. Invalid pagination returns 422. Missing and foreign profiles both return safe 404 with caller-scoped denial evidence. An owned profile without versions returns 200 with an empty list. The existing catalog and POST contracts are unchanged.

The response is `{slaProfileId,profileName,items,page,pageSize,total}`. Each row contains `slaVersionId`, `versionNo`, `targetMinutes`, `warningMinutes`, nullable structured `escalationConfig`, and `calendar:{calendarId,timeZone,workingHours,holidays}`. Versions sort newest first by their unique profile-local number. No publication state, timestamp, current default calendar, recipient display name or runtime SLA state is invented. Responses use no-store.

The controller delegates to Application `SlaVersionHistory`, which authorizes before validation/storage and records missing-resource denials. Infrastructure `SlaVersionHistoryStore` checks its tenant context and retains EF tenant filters on profile, version and calendar reads. A Repeatable Read transaction keeps metadata, count and page consistent within a request; separate pages are separate snapshots and a later save can shift their offsets. Calendar details come from each version's immutable reference, not a current configuration. Reads do not create business audit entries or mutate configuration.

## GRID UI

Settings → SLA profiles → profile name opens paginated version history. A read-only detail drawer shows exact frozen working intervals, non-working days, timezone, ISO holiday dates, elapsed-from-start warning minutes and escalation offsets after breach. Recipient UUIDs are labeled as IDs; current names/eligibility are not fabricated as historical facts. Text interpolation escapes data. Route and backend authorization are separate controls; only the backend is a security boundary.

Loading, empty, missing, denied and retryable failure states clear previous configuration. Route changes cancel old requests and returned profile IDs must match the requested profile. Changing pages or leaving the view closes its drawer. The drawer restores keyboard focus and uses the existing GRID/Material layout. No editing, activation, publish or automatic POST retry is introduced. History supports manual inspection after an uncertain save; it does not establish idempotency or prove which concurrent client created an otherwise identical snapshot.

## Verification and limits

`SlaProfileCatalogTests` covers frozen old/new calendar details, typed escalation values, descending pages, response allowlisting, no read-side mutation, empty profiles, bounds, forged tenant inputs, foreign/missing resource denial, custom-role-name non-authority and live read-grant removal. Angular tests cover paging, safe text, cancellation, cross-route responses, error clearing and exact detail rendering. Real desktop/mobile browser cases inspect seeded immutable history and drawer contents using Kestrel and disposable PostgreSQL. Run outcomes are tracked in [implementation status](implementation-status.md).

No migration is needed: the model remains 20 of 43 approved application tables. Default-calendar administration/provisioning, version editing forms, service selection/pinning, UTC runtime calculation, pause/resume, warning/overdue/escalation jobs and notification delivery remain incomplete. The default-calendar API proposal still requires its separate owner decision.
