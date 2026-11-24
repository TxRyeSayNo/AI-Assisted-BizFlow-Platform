import { ReadableDatesPipe, readableIsoDate } from './readable-date';

describe('readable business dates', () => {
  const pipe = new ReadableDatesPipe();

  it('formats historical meeting content without changing the source or losing surrounding text', () => {
    const source = 'Thời gian: 2026-11-25T09:00:00+07:00\nĐịa điểm: Phòng họp tầng 3';
    expect(pipe.transform(source)).toBe('Thời gian: 09:00, 25/11/2026\nĐịa điểm: Phòng họp tầng 3');
    expect(source).toContain('2026-11-25T09:00:00+07:00');
  });

  it('keeps foreign timezone wall-clock times explicit, including near midnight', () => {
    expect(pipe.transform('2026-11-25T00:15:00-05:00')).toBe('00:15, 25/11/2026 (UTC-05:00)');
    expect(pipe.transform('2026-11-24T23:30:00.123456Z')).toBe('23:30, 24/11/2026 (UTC)');
    expect(readableIsoDate('2026-11-25T09:00:00+07:00', true)?.text).toContain('Thứ Tư');
  });

  it('handles multiple timestamps, empty values and invalid dates without inventing a correction', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('2026-02-30T09:00:00+07:00')).toBe('2026-02-30T09:00:00+07:00');
    expect(pipe.transform('2026-11-25T25:00:00+07:00')).toBe('2026-11-25T25:00:00+07:00');
    expect(pipe.transform('2026-11-25T09:00+07:00 — 2026-11-25T10:30+07:00')).toBe(
      '09:00, 25/11/2026 — 10:30, 25/11/2026',
    );
    expect(pipe.transform('Mã hồ sơ 2026-11-25 / thời hạn chưa xác định')).toBe(
      'Mã hồ sơ 2026-11-25 / thời hạn chưa xác định',
    );
  });
});
