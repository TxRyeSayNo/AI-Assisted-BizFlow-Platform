# Date/time input selectors — 24 November 2026

Replaced the combined datetime-local fields in task creation and the request's
create-task dialog with one shared form control: calendar date, 24-hour selector
and minute selector. Date-only filters in reports, audit and task boards use the
same Vietnamese calendar when the user clicks the field or calendar button.

The shared control implements both reactive and template-driven Angular forms.
Its value remains a local `yyyy-MM-ddTHH:mm` string; existing submission code
converts it to UTC. Partial selections invalidate the form instead of dropping
an intended deadline. Optional values can be cleared. Disabled state and external
value updates mark the view for change detection, including after a failed save.

Validation:

- Focused frontend checks passed for shared selectors, calendar, task board,
  reports and audit. Shared selector tests also cover partial input, saved values,
  midnight, strict Vietnamese date parsing, invalid input clearing,
  touched/disabled/readonly state and asynchronous re-enable.
- Production frontend build passed.
- Vietnamese calendar browser checks passed at 1440px and 390px, including month
  navigation labels, selecting a calendar cell and the date-only filter popup.
- Playwright desktop/mobile checks cover date-field calendar opening, hour/minute
  selection, partial-input submit blocking, clearing after a rejected save,
  date-only filters and overflow. A selected 09:05 Vietnam deadline is submitted
  as 02:05 UTC on the same date.
- Submission is intercepted with a validation response; no real task or business
  notification is created by browser evaluation.
- Shared layout adapts to narrow containers. The adjacent priority field is not
  stretched to the date/time control's height.

The shared `bf-date-picker` now uses an Angular Material calendar with a Vietnamese
date adapter and translated navigation/accessibility labels. Its language is
independent of the browser/operating system language. Weeks start on Monday;
dates display and accept `dd/MM/yyyy`, while form values remain `yyyy-MM-dd`.
The same picker is used by deadline date/time fields and the date-only filters
in reports, audit and the task board. Hour/minute selectors keep 24-hour time.

Strict parsing rejects impossible dates and ambiguous month/day input. Clearing
an invalid date resets form validation. Saved values, disabled/readonly fields,
template-driven forms and UTC deadline conversion are covered by regression checks.

Screenshots:

- [Desktop](../../artifacts/date-time-picker-desktop.png)
- [Mobile](../../artifacts/date-time-picker-mobile.png)
