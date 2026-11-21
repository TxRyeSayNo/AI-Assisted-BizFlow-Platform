import { TestBed } from '@angular/core/testing';
import { ReportChart } from './report-chart';

describe('report chart', () => {
  beforeEach(() => TestBed.configureTestingModule({}));

  const create = (
    data: { label: string; value: number }[],
    kind: 'bar' | 'line' | 'pie' = 'bar',
  ) => {
    const fixture = TestBed.createComponent(ReportChart);
    fixture.componentRef.setInput('title', 'Nhiệm vụ theo phòng ban');
    fixture.componentRef.setInput('kind', kind);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();
    return fixture;
  };

  it('draws one bar per slice, scaled to the largest value', () => {
    const fixture = create([
      { label: 'Kỹ thuật', value: 10 },
      { label: 'Vận hành', value: 5 },
    ]);

    const bars = fixture.componentInstance.bars();
    expect(bars.length).toBe(2);
    // The tallest bar sets the scale, so the half-size value is about half as tall.
    expect(bars[0].height).toBeGreaterThan(bars[1].height);
    expect(Math.abs(bars[1].height / bars[0].height - 0.5)).toBeLessThan(0.1);
    expect(fixture.nativeElement.querySelectorAll('rect').length).toBe(2);
    // The drawing is labelled, not anonymous.
    expect(fixture.nativeElement.querySelector('svg').getAttribute('role')).toBe('img');
    expect(fixture.nativeElement.querySelector('svg').getAttribute('aria-label')).toBe(
      'Nhiệm vụ theo phòng ban',
    );
  });

  it('always exposes the numbers as a table for assistive technology', () => {
    const fixture = create([{ label: 'Kỹ thuật', value: 10 }]);
    const rows = fixture.nativeElement.querySelectorAll('.chart-table tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Kỹ thuật');
    expect(rows[0].textContent).toContain('10');
  });

  it('shows an empty message rather than an empty drawing', () => {
    const fixture = create([]);
    expect(fixture.nativeElement.querySelector('svg')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Không có dữ liệu');
  });

  it('ignores slices that are negative or not finite', () => {
    const fixture = create([
      { label: 'Tốt', value: 4 },
      { label: 'Âm', value: -2 },
      { label: 'Không số', value: Number.NaN },
    ]);
    expect(fixture.componentInstance.visible().map((s) => s.label)).toEqual(['Tốt']);
  });

  it('charts only the largest slices when the list is long', () => {
    const many = Array.from({ length: 20 }, (_, index) => ({ label: `P${index}`, value: index }));
    const fixture = create(many);
    const visible = fixture.componentInstance.visible();
    expect(visible.length).toBe(12);
    // The biggest value survives the cut and the smallest does not.
    expect(visible.some((s) => s.value === 19)).toBe(true);
    expect(visible.some((s) => s.value === 0)).toBe(false);
  });

  it('builds a polyline through every point in line mode', () => {
    const fixture = create(
      [
        { label: 'A', value: 1 },
        { label: 'B', value: 3 },
        { label: 'C', value: 2 },
      ],
      'line',
    );
    expect(fixture.componentInstance.linePoints().split(' ').length).toBe(3);
    expect(fixture.nativeElement.querySelector('polyline')).not.toBeNull();
  });

  it('splits a pie by share and reports whole percentages', () => {
    const fixture = create(
      [
        { label: 'Đúng hạn', value: 3 },
        { label: 'Vi phạm', value: 1 },
      ],
      'pie',
    );
    const wedges = fixture.componentInstance.wedges();
    expect(wedges.map((w) => w.percent)).toEqual([75, 25]);
    expect(fixture.nativeElement.querySelectorAll('path').length).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('.chart-legend li').length).toBe(2);
  });

  it('draws a full circle when one slice holds everything', () => {
    const fixture = create([{ label: 'Tất cả', value: 7 }], 'pie');
    const wedge = fixture.componentInstance.wedges()[0];
    expect(wedge.percent).toBe(100);
    // A single full slice cannot be an arc from a point to itself, so it is a closed circle.
    expect(wedge.path).toContain('A');
    expect(fixture.nativeElement.querySelectorAll('path').length).toBe(1);
  });
});
