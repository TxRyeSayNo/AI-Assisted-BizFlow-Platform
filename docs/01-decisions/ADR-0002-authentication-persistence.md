# ADR-0002 — Authentication sessions and platform identity

Status: accepted by the project owner in this implementation session (2026-09-29).

## Context

FR-AUTH-003 requires persisted, rotating and revocable refresh tokens. The approved 42-table model has no authentication-session entity. Appendix C requires User.TenantId, while A01 operates above tenant boundaries.

## Decision

The owner explicitly approved one authentication-session table and a nullable User.TenantId only for Platform Administrator accounts. This is an approved amendment to the prior physical-table baseline: 42 + 1 = 43 application tables. Provider-owned Hangfire storage is infrastructure, not additional domain entities.

Sessions will store only a cryptographic token hash, expiry, revocation and rotation metadata, bound to a user. Rotation must be atomic, single-use and replay-tested. No plaintext refresh tokens are persisted or logged. User deactivation, password reset and tenant suspension must prevent session use.

Tenantless identity must never imply a tenant-query-filter bypass. Platform operations require explicit platform permission. Tenant-owned writes still require a real TenantId. Tenantless accounts must not be assignable as employees, managers, requesters or task owners.

## Verification required before M1 is complete

- Database constraint and application validation for platform-only tenantless accounts.
- Rotation, concurrent refresh, revoked/expired token and replay tests.
- Inactive user/tenant denial, password-reset session revocation.
- Tenant A/B isolation tests plus platform permission boundary tests.

This ADR approves the change; it does not claim the persistence implementation is complete.
