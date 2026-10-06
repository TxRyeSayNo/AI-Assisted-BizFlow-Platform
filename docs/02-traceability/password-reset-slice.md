# Password recovery — email path

Date: 2026-09-30. Implements the email path of FR-AUTH-004, UI-01 recovery screens, BR-001/002/020 and ADR-0002 session invalidation. **FR-AUTH-004 remains partial:** the A-12 administrator-issued temporary-password and forced-change flow is not implemented.

## Traceability

| Layer | Evidence |
|---|---|
| Domain | `UserAccount.ResetPassword`: replace hash/security stamp, clear operational lockout and change-required flag; inactive, deleted and administratively locked users are ineligible |
| Application | `PasswordResetService` validates signed identity, current tenant/company/account, identifier, expiry, email binding and password policy under the shared user lock; revokes all sessions and audits atomically |
| Delivery | `PasswordResetDeliveryService` resolves an unambiguous account, checks eligibility and quotas, issues the token and audits before sending outside the database lock |
| Infrastructure | Purpose-separated signed reset JWT, Identity hashing, PostgreSQL transaction/session revocation, Hangfire PostgreSQL email queue, TLS SMTP adapter |
| API | POST `/api/v1/auth/forgot-password` and `/api/v1/auth/reset-password`; safe DTO errors, no-store responses and authentication IP quota |
| UI | GRID `/forgot-password` and `/reset-password`; email fragment token removed from address/history immediately and held only in memory; local safe error messages |
| Tests | PostgreSQL/API single-use, concurrent reset/refresh, live account-state, replay, session invalidation and enumeration regressions; real Hangfire queue; loopback SMTP delivery; Angular tests; real desktop/mobile browser reset/login |

No new application table or migration is needed. The existing user security stamp makes all outstanding links single-use together; the existing authentication-session table records revocation. Hangfire owns its separate `hangfire` infrastructure schema, as allowed by ADR-0002. Old planning references to a `PasswordResetToken` table are not an approved addition to the physical model.

## API and security behavior

- Forgot-password input: `{ identifier, tenantKey? }`. Every well-formed request is queued, including unknown and ambiguous identities. The public result is the same generic 202 acknowledgement; actual identity resolution occurs in the worker. Disabled/unavailable delivery returns a uniform 503, not account-dependent success/failure. This does not claim exact timing equivalence.
- Reset input: `{ identifier, tenantKey?, resetToken, newPassword }`. A signed reset credential is required; an access JWT is neither required nor accepted as a reset token. The token has a distinct audience and type, short expiry and bindings to user, tenant, current email and security stamp. A supplied workspace key must match that identity.
- A per-user PostgreSQL row lock serializes login, refresh and reset. Token expiry and current eligibility are rechecked after acquiring it. Password update, stamp replacement, revocation of every session family and `AUTH.PASSWORD_RESET_SUCCEEDED` audit commit together. Concurrent reset has one winner; refresh racing reset cannot leave a usable old session.
- Successful reset invalidates the old password, all earlier reset links, access JWTs and refresh tokens. Operational failed-login lockout can be recovered by email; business `LOCKED`, `INACTIVE`, deletion or inactive tenant/company cannot be bypassed.
- Password whitespace is preserved. Server policy defaults to 12–1024 characters with at least four distinct characters; validation errors do not echo credentials. These are deployment policy defaults, not new business states.
- The link uses `/reset-password#token=...`; URL fragments do not reach the HTTP server. The UI removes the fragment immediately. No tokens are stored in browser web storage. Reload requires reopening the email link. Real-auth test tracing/video is disabled.
- No password, reset token or message body is serialized into Hangfire arguments. Arguments do contain the normalized identifier, workspace key and request timestamp, so the job database is sensitive operational storage. No Hangfire dashboard is exposed. SMTP errors are sanitized before retry state is stored.
- Delivery is at-least-once, not exactly-once. Retry may send another link within the original request's expiry; a successful password change invalidates previously issued links. `AUTH.PASSWORD_RESET_REQUESTED` records issuance, not proof of SMTP receipt. There is no general notification subsystem in this slice.

## Deployment configuration

Email recovery is disabled by default. Set values through environment variables or a secret provider, never committed credentials:

| Setting | Purpose/default |
|---|---|
| `ResetEmail__Enabled` | `true` to enable the email queue and its two-worker hosted server |
| `ResetEmail__Host`, `ResetEmail__Port` | SMTP host; port defaults to 587 |
| `ResetEmail__SslOnConnect` | `false` uses required STARTTLS; `true` uses TLS on connect |
| `ResetEmail__Username`, `ResetEmail__Password` | Provider credentials when required |
| `ResetEmail__FromAddress` | Valid sender mailbox |
| `ResetEmail__FrontendOrigin` | HTTPS origin only, without path/query/fragment; loopback HTTP allowed for local testing |
| `ResetEmail__AllowInsecureLoopback` | Defaults `false`; plaintext SMTP only for explicitly enabled localhost/loopback tests |
| `ConnectionStrings__Hangfire` | Optional separate PostgreSQL connection; defaults to BizFlow connection |
| `PasswordReset__TokenLifetimeMinutes` | 15; configurable 1–120 |
| `PasswordReset__MinimumPasswordLength` | 12; configurable 12–128, maximum accepted length always 1024 |
| `PasswordReset__MinimumDistinctCharacters` | 4; configurable 1–10 |

Enabled settings are validated at startup. Hangfire initializes its provider-owned schema on startup and fails startup if storage is unavailable; provision database/schema permissions accordingly. This is distinct from EF application migrations, which remain an explicit deployment step. Jobs retry after 15, 60 and 300 seconds and do not extend the original link lifetime. Normal authentication rate limits also apply. Production SMTP delivery requires operator configuration and has not been tested against a real external recipient; automated transport testing uses an isolated loopback SMTP listener.

## Open requirement decision

FR-AUTH-004 allows employees without email, but Appendix C marks `User.Email` required. Owner confirmation has been requested to allow nullable email for tenant users only, retain mandatory platform-account email and enforce per-tenant uniqueness for non-null addresses. **This amendment is not approved yet; the current schema remains unchanged.** The earlier owner approval covered session persistence and platform-only nullable TenantId, not nullable Email. Admin reset, forced-change login, admin authorization/UI and this schema decision remain required before FR-AUTH-004 is complete.
