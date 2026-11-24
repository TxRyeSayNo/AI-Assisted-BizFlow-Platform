import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionService } from '../../core/auth/session';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { ToastService } from '../../core/toast/toast.service';
import { LabelView, WorkItemLabels } from './work-item-labels';

describe('work item labels', () => {
  const url = '/api/v1/work-items/TASK/task-1/labels';

  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: { hasPermission: () => true, identity: () => null } },
      ],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const enable = () => {
    TestBed.inject(FeatureFlagsService).load();
    TestBed.inject(HttpTestingController).expectOne('/api/v1/features').flush({
      kanban: true,
      calendar: true,
      mentions: true,
      deadlineReminders: true,
      reportExport: true,
      labels: true,
    });
  };

  const label = (id: string, name: string, archived = false): LabelView => ({
    labelId: id,
    name,
    colour: '#1A73E8',
    archived,
    usageCount: 0,
  });

  const create = () => {
    const fixture = TestBed.createComponent(WorkItemLabels);
    fixture.componentRef.setInput('objectType', 'TASK');
    fixture.componentRef.setInput('objectId', 'task-1');
    fixture.detectChanges();
    return fixture;
  };

  it('stays completely inert while the labels feature is off', () => {
    const fixture = create();
    TestBed.inject(HttpTestingController).verify();
    expect(fixture.nativeElement.querySelector('.work-item-labels')).toBeNull();
  });

  it('shows the labels on the item and offers the rest of the catalogue', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();

    http.expectOne({ method: 'GET', url }).flush({
      objectType: 'TASK',
      objectId: 'task-1',
      labels: [label('a', 'Khẩn')],
    });
    http
      .expectOne('/api/v1/labels')
      .flush([label('a', 'Khẩn'), label('b', 'Chiến dịch'), label('c', 'Hết dùng', true)]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Khẩn');
    // Only the label not already applied, and never a retired one, is offered.
    expect(fixture.componentInstance.addable().map((l) => l.labelId)).toEqual(['b']);
  });

  it('applies and removes a label through the work item endpoints', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();
    http
      .expectOne({ method: 'GET', url })
      .flush({ objectType: 'TASK', objectId: 'task-1', labels: [] });
    http.expectOne('/api/v1/labels').flush([label('b', 'Chiến dịch')]);
    fixture.detectChanges();

    fixture.componentInstance.attach('b');
    http.expectOne({ method: 'PUT', url: `${url}/b` }).flush({
      objectType: 'TASK',
      objectId: 'task-1',
      labels: [label('b', 'Chiến dịch')],
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.attached().map((l) => l.labelId)).toEqual(['b']);
    expect(fixture.componentInstance.addable()).toEqual([]);

    fixture.componentInstance.detach('b');
    http
      .expectOne({ method: 'DELETE', url: `${url}/b` })
      .flush({ objectType: 'TASK', objectId: 'task-1', labels: [] });
    fixture.detectChanges();
    expect(fixture.componentInstance.attached()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('No labels yet');
  });

  it('reports a refused change with the server wording and a failure with its own', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();
    http
      .expectOne({ method: 'GET', url })
      .flush({ objectType: 'TASK', objectId: 'task-1', labels: [] });
    http.expectOne('/api/v1/labels').flush([label('b', 'Chiến dịch')]);

    fixture.componentInstance.attach('b');
    http
      .expectOne({ method: 'PUT', url: `${url}/b` })
      .flush(
        { message: 'Nhãn này đã bị thu hồi.' },
        { status: 422, statusText: 'Unprocessable Content' },
      );
    // A curated 4xx fault is shown as-is.
    expect(TestBed.inject(ToastService).toasts()[0].message).toContain('đã bị thu hồi');

    TestBed.inject(ToastService).clear();
    fixture.componentInstance.attach('b');
    http
      .expectOne({ method: 'PUT', url: `${url}/b` })
      .flush({ message: 'private detail' }, { status: 503, statusText: 'Unavailable' });
    // A 5xx body never reaches the screen.
    expect(TestBed.inject(ToastService).toasts()[0].message).not.toContain('private detail');
    expect(fixture.componentInstance.busy()).toBe(false);
  });

  it('renders nothing when the item is outside the callers scope', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();
    http.expectOne({ method: 'GET', url }).flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    // No catalogue request follows a hidden item, and nothing is drawn.
    http.verify();
    expect(fixture.componentInstance.available()).toBe(false);
    expect(fixture.nativeElement.querySelector('.work-item-labels')).toBeNull();
  });
});
