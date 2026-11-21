import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { ToastService } from '../../core/toast/toast.service';
import { WorkItemWatch } from './work-item-watch';

describe('work item follow button', () => {
  const url = '/api/v1/work-items/TASK/task-1/watch';

  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const enable = () => {
    const flags = TestBed.inject(FeatureFlagsService);
    flags.load();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/features')
      .flush({ kanban: true, calendar: true, mentions: true });
    return flags;
  };

  const create = () => {
    const fixture = TestBed.createComponent(WorkItemWatch);
    fixture.componentRef.setInput('objectType', 'TASK');
    fixture.componentRef.setInput('objectId', 'task-1');
    fixture.detectChanges();
    return fixture;
  };

  it('stays completely inert while the mentions feature is off', () => {
    const fixture = create();
    // No request and no button: a deployment with the flag off looks untouched.
    TestBed.inject(HttpTestingController).verify();
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });

  it('reads the status and follows, then unfollows, through the work item endpoints', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();

    http.expectOne({ method: 'GET', url }).flush({
      objectType: 'TASK',
      objectId: 'task-1',
      watching: false,
      watcherCount: 0,
    });
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.textContent).toContain('Follow');
    expect(button.getAttribute('aria-pressed')).toBe('false');

    button.click();
    http.expectOne({ method: 'PUT', url }).flush({
      objectType: 'TASK',
      objectId: 'task-1',
      watching: true,
      watcherCount: 2,
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Unfollow');
    expect(fixture.nativeElement.textContent).toContain('(2)');

    fixture.nativeElement.querySelector('button').click();
    http.expectOne({ method: 'DELETE', url }).flush({
      objectType: 'TASK',
      objectId: 'task-1',
      watching: false,
      watcherCount: 1,
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Follow');
  });

  it('shows no button at all when the item is out of scope', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();

    http.expectOne({ method: 'GET', url }).flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });

  it('keeps the previous state and reports a failed toggle without leaking server text', () => {
    enable();
    const http = TestBed.inject(HttpTestingController);
    const fixture = create();

    http.expectOne({ method: 'GET', url }).flush({
      objectType: 'TASK',
      objectId: 'task-1',
      watching: false,
      watcherCount: 0,
    });
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();
    http.expectOne({ method: 'PUT', url }).flush(null, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();

    expect(fixture.componentInstance.status()!.watching).toBe(false);
    expect(fixture.componentInstance.busy()).toBe(false);
    const toasts = TestBed.inject(ToastService).toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('error');
    // A second click is possible again after the failure.
    expect((fixture.nativeElement.querySelector('button') as HTMLButtonElement).disabled).toBe(
      false,
    );
  });
});
