# ADR-0005 — SLA warning meaning and calendar JSON

Status: accepted by the project owner on 2026-10-03.

The owner approved both decisions explicitly:

- WarningMinutes is elapsed working minutes from SLA start, not an offset backwards from the deadline. A target of 60 and warning of 45 warns after 45 working minutes. Approved A-08 still requires `0 <= warning < target` and `target > 0`.
- WorkingHoursJson is a weekday-to-time-range-array object, for example `{"monday":[{"start":"08:00","end":"17:30"}]}`. HolidaysJson is an array of YYYY-MM-DD calendar dates. Empty weekly schedules mean no working time, never 24/7, and cannot be used for an executable SLA. Overnight hours are represented by intervals on adjacent days.

Technical representation: lowercase English weekday keys; omitted days or empty arrays are nonworking days. Intervals use minute-precision local `HH:mm` times, start-inclusive and end-exclusive; `24:00` is allowed only as an end boundary. End must follow start within that day, intervals must not overlap, and duplicate dates/keys or unknown fields are rejected. The IANA timezone belongs to the stored calendar. These are basic weekly hours/holidays, not P2 advanced calendars.

An empty calendar may exist as unconfigured storage, but version creation must reject it. The accepted snapshot/immutability decision in ADR-0004 still applies. This decision does not approve an escalation JSON schema, default-calendar administration API, daylight-saving boundary policy or unrelated pending owner decisions. Runtime warning/pause calculations and configuration API/UI remain implementation work, not claims established by this ADR.
