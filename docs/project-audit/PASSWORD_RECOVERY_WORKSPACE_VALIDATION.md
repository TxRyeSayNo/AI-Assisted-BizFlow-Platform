# Workspace-scoped email recovery — 24 November 2026

The public company recovery flow now requires workspace-key and employee code or
linked email on both request and reset. API validation rejects missing/blank or
malformed workspace keys before queueing. Identity resolution remains tenant
scoped; mail is delivered only to the address stored on the matching account.
Generic acknowledgements do not disclose whether an account exists.

The frontend now shows required workspace hints, visible validation toasts and an
accurate queued-request acknowledgement. A success toast is no longer immediately
cleared. Reset messages are Vietnamese and explain how to open the single-use link.

Deployment now forwards ResetEmail settings into the API container instead of
hardcoding delivery off. Defaults remain disabled. Gmail STARTTLS and the sender
selected by the user are prepared in the ignored local .env, without storing
sender credentials in tracked source files. A Vietnamese configuration/handoff
guide and a no-email configuration preflight script are included.

Validation:

- 21 backend integration checks passed: actual isolated SMTP transport, token
  validation, Hangfire/PostgreSQL delivery, workspace validation, linked-email
  lookup, tenant isolation, one-time reset, account state, password changes and
  invalidation of old sessions. Database tests use disposable PostgreSQL.
- 5 frontend recovery tests passed.
- 2 browser evaluations passed on desktop/mobile with intercepted email/reset
  APIs. No real account password is changed and no external email is sent.
- Final production frontend build and Compose configuration validation passed.
- Live disabled API rejects an absent workspace with 422 and correctly reports
  AUTH.RESET_UNAVAILABLE with 503 when valid-shaped recovery is requested.
- Initially the preflight script reported disabled sending and the missing SMTP
  password. After the user supplied it in the ignored .env, the check passed
  without exposing secret values.

Gmail TLS connection and SMTP authentication succeeded with the user-supplied
credential; no email was sent by that probe. Local delivery is now enabled and
the API restarted successfully with its PostgreSQL/Hangfire email workers active.
An enabled-API probe using a randomly generated nonexistent workspace returned
202 and queued successfully, without selecting an email recipient.

Remaining evidence: a specified test account with a receiving linked mailbox is
needed for a real request-to-inbox verification. Inbox receipt has not been
verified. The password reset tests above did not change a real user's password.

See [setup and handoff instructions](../02-traceability/password-reset-setup.vi.md).
