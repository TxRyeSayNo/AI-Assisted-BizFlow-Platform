import { Pipe, PipeTransform } from '@angular/core';

// Keep the source wall-clock time and its offset. This is display-only: API values
// and datetime attributes retain their original precision and timezone.
export const ISO_DATE_TIME_PATTERN =
  /\b\d{4}-\d{2}-\d{2}[Tt]\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:[Zz]|[+-]\d{2}:\d{2})\b/g;

export function readableIsoDate(
  value: string,
  weekday = false,
): { text: string; dateTime: string; zone: string } | null {
  const match =
    /^(\d{4})-(\d{2})-(\d{2})[Tt](\d{2}):(\d{2})(?::\d{2}(?:\.\d+)?)?([Zz]|[+-]\d{2}:\d{2})$/.exec(
      value,
    );
  if (!match || !Number.isFinite(Date.parse(value))) return null;
  const [, year, month, day, hour, minute, offset] = match;
  const date = new Date(Date.UTC(+year, +month - 1, +day));
  if (date.getUTCMonth() !== +month - 1 || date.getUTCDate() !== +day || +hour > 23 || +minute > 59)
    return null;
  const dayName = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', timeZone: 'UTC' }).format(
    date,
  );
  return {
    text: weekday
      ? `${hour}:${minute} · ${dayName[0].toUpperCase()}${dayName.slice(1)}, ${day}/${month}/${year}`
      : `${hour}:${minute}, ${day}/${month}/${year}`,
    dateTime: value,
    zone: offset.toUpperCase() === 'Z' ? 'UTC' : `UTC${offset}`,
  };
}

@Pipe({ name: 'readableDates', standalone: true })
export class ReadableDatesPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return (value ?? '').replace(ISO_DATE_TIME_PATTERN, (iso) => {
      const date = readableIsoDate(iso);
      // Vietnam's standard offset is familiar in this application. Keep other
      // explicit offsets visible so a remote meeting's time is never ambiguous.
      return date ? date.text + (date.zone === 'UTC+07:00' ? '' : ` (${date.zone})`) : iso;
    });
  }
}
