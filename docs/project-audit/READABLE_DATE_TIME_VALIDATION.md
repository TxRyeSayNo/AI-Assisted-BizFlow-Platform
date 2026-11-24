# Readable date/time display — 24 November 2026

Date/time fields use `HH:mm, dd/MM/yyyy`; date-only fields retain `dd/MM/yyyy`
and time-only fields use 24-hour `HH:mm`. Audit detail retains seconds and explicit
UTC; the audit list now also respects its existing UTC label.

Updated DatePipe formats across task lists/details/boards/calendar, requests,
notifications, approvals, workflows, audit, search, attachments, comments, company
registrations and AI conversation history. Chat timestamps now use a 24-hour clock.

The shared readable-date parser handles ISO timestamps in old notification content,
task/request descriptions, comments, search snippets, AI summaries and assistant
text. AI chat reuses its validation while retaining its date chips and weekday.
It preserves the source wall-clock time; offsets outside UTC+07 remain visible.
Invalid calendar dates remain unchanged rather than silently rolling over.
All prose remains escaped Angular text. JSON audit payloads and form/API date
values remain untouched.

New meeting notifications are generated as readable Vietnamese local time, with
an explicit “giờ Việt Nam” label, even when the tool receives a UTC instant.

Validation:

- 20 focused frontend tests passed: shared formatting, AI message dates, notification
  inbox and audit views. Coverage includes old content, multiple timestamps,
  malformed dates, foreign offsets, midnight and escaped HTML.
- 8 PersonalAgentWorkflow integration tests passed using disposable PostgreSQL;
  meeting delivery verifies equivalent UTC and UTC+07 inputs display the same hour.
- Production frontend build passed, compiling all affected templates.
- 2 Playwright checks passed on desktop and mobile using a read-only historical
  notification fixture. Screenshots were visually inspected for readable time
  and text wrapping. No evaluation notification was delivered to actual users.
- API and frontend proxy health returned HTTP 200 after restarting both services.

The initial full frontend suite exhausted the available machine memory. A broader
isolated retry was stopped after making no progress under memory pressure. The
successful frontend run was limited to the focused tests above; this is not a
claim that the entire frontend suite passed.

Visual evidence:

- [Desktop](../../artifacts/readable-times-desktop.png)
- [Mobile](../../artifacts/readable-times-mobile.png)
