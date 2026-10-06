# ADR-0006 — MVP SLA escalation configuration

Status: accepted by the project owner on 2026-10-03.

The owner approved ordered levels with `level`, `afterMinutes` and `recipientUserIds`. Levels are numbered 1…N. Offsets are nonnegative working minutes after breach and strictly increase between levels. Recipients must be active users in the same tenant. Each level notifies once and never changes the work owner.

JSON representation: `{"levels":[{"level":1,"afterMinutes":0,"recipientUserIds":["00000000-0000-7000-8000-000000000001"]}]}`. The example UUID illustrates the shape only, not a seeded account. SQL null or `{"levels":[]}` means no escalation levels. Each configured level requires at least one recipient; duplicate recipients within a level, unknown properties, malformed/empty UUIDs and noninteger offsets are rejected. A recipient may appear in different levels because idempotency is per object/version/level.

Eligibility is checked against live tenant users when saving the version, not inferred from client-supplied names or IDs. Saved configuration remains immutable under ADR-0004. Runtime must recheck delivery eligibility and tenant state, persist escalation/idempotency evidence and preserve owner/assignee relationships. Structural configuration validation is not proof that runtime escalation or notification delivery exists.

Dynamic role/department/manager selectors are not implied by this approved UUID-recipient contract. No unrelated pending decision or extra physical table is approved by this ADR.
