import { Component, Input, computed, signal } from '@angular/core';

export interface ChartSlice {
  label: string;
  value: number;
}

/**
 * Bar, line and pie charts drawn as inline SVG (F5). No charting library is added: the shapes are
 * simple, inline SVG scales crisply, and avoiding a dependency keeps the bundle and the lockfile
 * untouched.
 *
 * The chart is accessible rather than decorative: it carries a role and a label, and the same
 * numbers are also exposed as a visually hidden table so a screen reader never meets a bare
 * drawing.
 */
@Component({
  selector: 'bf-report-chart',
  template: `
    <figure class="chart" [class.chart-pie]="kind === 'pie'">
      <figcaption>{{ title }}</figcaption>
      @if (total() === 0) {
        <p class="chart-empty">{{ emptyLabel }}</p>
      } @else {
        <svg
          [attr.viewBox]="'0 0 ' + width + ' ' + height"
          role="img"
          [attr.aria-label]="title"
          preserveAspectRatio="xMidYMid meet"
        >
          @switch (kind) {
            @case ('bar') {
              @for (bar of bars(); track bar.label) {
                <rect
                  [attr.x]="bar.x"
                  [attr.y]="bar.y"
                  [attr.width]="bar.width"
                  [attr.height]="bar.height"
                  [attr.fill]="bar.colour"
                  rx="2"
                />
                <text
                  [attr.x]="bar.labelX"
                  [attr.y]="height - 4"
                  text-anchor="middle"
                  class="chart-tick"
                >
                  {{ bar.short }}
                </text>
                <text
                  [attr.x]="bar.labelX"
                  [attr.y]="bar.y - 3"
                  text-anchor="middle"
                  class="chart-value"
                >
                  {{ bar.value }}
                </text>
              }
            }
            @case ('line') {
              <polyline
                [attr.points]="linePoints()"
                fill="none"
                stroke="#1a73e8"
                stroke-width="2"
              />
              @for (point of linePointList(); track point.label) {
                <circle [attr.cx]="point.x" [attr.cy]="point.y" r="2.5" fill="#1a73e8" />
                <text
                  [attr.x]="point.x"
                  [attr.y]="height - 4"
                  text-anchor="middle"
                  class="chart-tick"
                >
                  {{ point.short }}
                </text>
              }
            }
            @default {
              @for (wedge of wedges(); track wedge.label) {
                <path [attr.d]="wedge.path" [attr.fill]="wedge.colour" />
              }
            }
          }
        </svg>
        @if (kind === 'pie') {
          <ul class="chart-legend">
            @for (wedge of wedges(); track wedge.label) {
              <li>
                <span
                  class="chart-swatch"
                  [style.background]="wedge.colour"
                  aria-hidden="true"
                ></span>
                {{ wedge.label }} — {{ wedge.value }} ({{ wedge.percent }}%)
              </li>
            }
          </ul>
        }
      }
      <table class="chart-table">
        <caption>
          {{
            title
          }}
        </caption>
        <tbody>
          @for (slice of visible(); track slice.label) {
            <tr>
              <th scope="row">{{ slice.label }}</th>
              <td>{{ slice.value }}</td>
            </tr>
          }
        </tbody>
      </table>
    </figure>
  `,
  styleUrls: ['./report-chart.scss'],
})
export class ReportChart {
  /** Slices in the order they should be read. */
  @Input({ required: true }) set data(value: ChartSlice[] | null | undefined) {
    this.slices.set(
      (value ?? []).filter((slice) => Number.isFinite(slice.value) && slice.value >= 0),
    );
  }
  @Input({ required: true }) title = '';
  @Input() kind: 'bar' | 'line' | 'pie' = 'bar';
  @Input() emptyLabel = 'Không có dữ liệu.';
  /** Keeps a long category list readable by charting only the largest entries. */
  @Input() maxSlices = 12;

  readonly width = 320;
  readonly height = 160;

  private readonly slices = signal<ChartSlice[]>([]);

  readonly visible = computed(() => {
    const all = this.slices();
    if (all.length <= this.maxSlices) return all;
    return [...all].sort((a, b) => b.value - a.value).slice(0, this.maxSlices);
  });

  readonly total = computed(() => this.visible().reduce((sum, slice) => sum + slice.value, 0));

  private readonly peak = computed(() =>
    Math.max(1, ...this.visible().map((slice) => slice.value)),
  );

  private readonly palette = [
    '#1a73e8',
    '#e8710a',
    '#188038',
    '#b3261e',
    '#9334e6',
    '#007b83',
    '#8c6d1f',
    '#5f6368',
  ];

  readonly bars = computed(() => {
    const slices = this.visible();
    const plot = this.height - 24;
    const step = this.width / Math.max(1, slices.length);
    const barWidth = Math.max(4, step * 0.6);
    return slices.map((slice, index) => {
      const height = Math.max(1, Math.round((slice.value / this.peak()) * (plot - 14)));
      return {
        label: slice.label,
        short: this.short(slice.label),
        value: slice.value,
        x: Math.round(index * step + (step - barWidth) / 2),
        y: plot - height,
        width: Math.round(barWidth),
        height,
        labelX: Math.round(index * step + step / 2),
        colour: this.palette[index % this.palette.length],
      };
    });
  });

  readonly linePointList = computed(() => {
    const slices = this.visible();
    const plot = this.height - 24;
    const step = slices.length > 1 ? this.width / (slices.length - 1) : 0;
    return slices.map((slice, index) => ({
      label: slice.label,
      short: this.short(slice.label),
      x: Math.round(slices.length > 1 ? index * step : this.width / 2),
      y: Math.round(plot - (slice.value / this.peak()) * (plot - 14)),
    }));
  });

  readonly linePoints = computed(() =>
    this.linePointList()
      .map((point) => `${point.x},${point.y}`)
      .join(' '),
  );

  readonly wedges = computed(() => {
    const slices = this.visible();
    const total = this.total();
    const cx = this.width / 2;
    const cy = (this.height - 10) / 2;
    const radius = Math.min(cx, cy) - 4;
    let angle = -Math.PI / 2;
    return slices.map((slice, index) => {
      const sweep = total === 0 ? 0 : (slice.value / total) * Math.PI * 2;
      const end = angle + sweep;
      const large = sweep > Math.PI ? 1 : 0;
      const start = { x: cx + radius * Math.cos(angle), y: cy + radius * Math.sin(angle) };
      const finish = { x: cx + radius * Math.cos(end), y: cy + radius * Math.sin(end) };
      // A single slice covering everything cannot be drawn as an arc; draw the whole circle.
      const path =
        sweep >= Math.PI * 2 - 1e-6
          ? `M ${cx} ${cy - radius} A ${radius} ${radius} 0 1 1 ${cx - 0.01} ${cy - radius} Z`
          : `M ${cx} ${cy} L ${start.x.toFixed(2)} ${start.y.toFixed(2)} ` +
            `A ${radius} ${radius} 0 ${large} 1 ${finish.x.toFixed(2)} ${finish.y.toFixed(2)} Z`;
      angle = end;
      return {
        label: slice.label,
        value: slice.value,
        percent: total === 0 ? 0 : Math.round((slice.value / total) * 100),
        colour: this.palette[index % this.palette.length],
        path,
      };
    });
  });

  private short(label: string) {
    return label.length <= 6 ? label : label.slice(0, 5) + '…';
  }
}
