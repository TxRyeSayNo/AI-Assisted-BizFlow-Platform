# Authentication slice

Requirements: FR-AUTH-001/002/003, BR-001/002/020, UI-01, TST-AUTH-001, ADR-0002. The email path of FR-AUTH-004 is covered separately in [password recovery](password-reset-slice.md); admin-issued temporary-password recovery and company provisioning remain required.

## Implementation map

| Layer | Evidence |
|---|---|
| Domain | UserAccount operational lockout/login methods; AuthenticationSession single-use rotation and absolute expiry |
| Application | AuthenticationService validates credentials/context, checks current account and company/tenant state, chooses rotation/replay behavior and commits audit with the mutation |
| Infrastructure | ASP.NET Core Identity password verification/rehashing; narrow identity lookup; per-user PostgreSQL row lock and transaction; persisted family revocation; signed JWT issuance |
| API | POST `/api/v1/auth/login`, POST `/api/v1/auth/refresh`; DTO validation; IP/user/tenant rate limits; JWT validation and live session/account checks |
| UI | GRID login, in-memory session, same-origin API bearer interceptor, shared refresh operation and identity-generation guards |
| Tests | Real PostgreSQL/API tests, JWT validation tests, Angular interceptor tests, disposable Kestrel/PostgreSQL browser harness |

Controllers never access DbContext. Authentication identity resolution is a narrow Infrastructure lookup, not a general tenant bypass. Resource operations still require Application authorization and persisted grants.

## Security behavior

- Login accepts `{ identifier, password, tenantKey? }`; identifiers are normalized, password whitespace is preserved. Ambiguous identifiers require workspace context (409). Only company display names are returned as ambiguity metadata, following A-01; no user records or IDs are exposed.
- Wrong credentials, inactive/locked users and suspended tenant/company contexts cannot issue a session. The error is generic. Unknown identifiers receive a dummy Identity password verification; timing equivalence is not claimed.
- Password hashes use Identity's versioned format. Successful verification upgrades older supported hashes when needed. No raw password is persisted or logged.
- Refresh secrets have 512 random bits; only their SHA-256 hashes are stored. The refresh secret itself authenticates `/auth/refresh`; a valid access token is deliberately not required to refresh an expired one. This is credential authentication, not anonymous business access.
- A user-row lock serializes login and refresh operations. Rotation, account metadata and audit commit together. Replaying a consumed token revokes all remaining active sessions in that family. A simultaneous duplicate refresh therefore yields one successful response followed by family revocation; the client must share a single refresh operation.
- Access JWTs validate signature, issuer, audience, algorithm, token type and lifetime. Each authenticated API request also checks the current user/security stamp, active tenant/company and an active session in the token's family. Permissions are resolved from current role grants, not trusted from JWT claims.
- Authentication audit events are system observations linked to the affected User. A failed attempt is not falsely attributed to an authenticated human. Audit metadata contains family IDs, never tokens/password hashes.
- The browser keeps tokens only in memory. Reload requires sign-in. A refresh failure clears the session; stale refresh responses cannot restore it. Tokens are attached only to relative `/api/v1/` business requests, never external URLs or credential endpoints. Old requests cannot be retried as a subsequently signed-in identity.

## Configuration

Required: `ConnectionStrings__BizFlow`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKeyBase64` (base64-encoded cryptographically random key of at least 32 bytes). Do not commit values. JWT configuration is validated at startup. Migrations are an explicit deployment step, never silently applied on startup.

Configurable deployment-policy defaults (not business states):

| Setting | Default |
|---|---|
| `Jwt__AccessLifetimeMinutes` | 15 |
| `Authentication__RefreshLifetimeDays` | 14, absolute family lifetime |
| `Authentication__FailedAttemptLimit` | 5 |
| `Authentication__LockoutMinutes` | 15 |
| `Authentication__RateLimits__PerIpPerMinute` | 30 |
| `Authentication__RateLimits__PerUserPerMinute` | 30 |
| `Authentication__RateLimits__PerTenantPerMinute` | 300 |

Rate limits are process-local for the initial single-host monolith. Scale-out deployment must coordinate quotas; it must not assume these counters are distributed. Forwarded headers are not trusted implicitly. Production TLS and trusted-proxy configuration remain deployment gates.

## Remaining gates

- Administrator-issued temporary-password/change-required flow. Email reset now atomically invalidates all sessions. Currently MustChangePassword accounts fail closed at ordinary login; they cannot receive ordinary access tokens before completing a password change.
- Company registration/approval/provisioning, operational user lifecycle and system permission/role seeds.
- Full tenant/platform application shells and permission-scoped navigation; `/workspace` is a minimal authenticated landing page.
- Full business-endpoint tenant-isolation suite (TST-SEC-001). Authenticated test-only probes verify middleware/Application wiring but do not stand in for the future business API suite.
- Broader security, accessibility, observability and production deployment audits.

## References

Implementation follows Microsoft's [Identity password verification contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1.verifyhashedpassword?view=aspnetcore-10.0), [JWT validation guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0) and [rate limiting guidance](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0). The browser harness uses [WebApplicationFactory's Kestrel support](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1.usekestrel?view=aspnetcore-10.0).
