import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SessionService } from '../../core/auth/session';
import { TaskCreate } from './task-create';

describe('task creation replay safety', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SessionService, useValue: { hasPermission: () => true } },
      ],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('prevents double submit and retains the exact payload and key on unknown outcomes', () => {
    const component = TestBed.createComponent(TaskCreate).componentInstance;
    component.form.patchValue({ title: 'Original', checklist: 'One\nTwo' });
    component.save();
    component.save();
    const http = TestBed.inject(HttpTestingController);
    const first = http.expectOne('/api/v1/tasks');
    const key = first.request.headers.get('Idempotency-Key');
    const body = first.request.body;
    first.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(component.uncertain()).toBe(true);
    expect(component.form.disabled).toBe(true);
    component.form.patchValue({ title: 'Changed programmatically' });
    component.save();
    const retry = http.expectOne('/api/v1/tasks');
    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    expect(retry.request.body).toEqual(body);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    retry.flush({ taskId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' });
    component.save();
    http.expectNone('/api/v1/tasks');
  });
  it('allows correction after known validation failure and normalizes a future local deadline', () => {
    const component = TestBed.createComponent(TaskCreate).componentInstance;
    component.form.patchValue({ title: 'Work', deadline: '2099-01-01T12:00' });
    component.save();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/tasks');
    expect(request.request.body.deadline).toBe(new Date('2099-01-01T12:00').toISOString());
    request.flush({}, { status: 422, statusText: 'Invalid' });
    expect(component.form.enabled).toBe(true);
  });
  it('does not send denied or past-deadline submissions', () => {
    const component = TestBed.createComponent(TaskCreate).componentInstance;
    component.form.patchValue({ title: 'Work', deadline: '2000-01-01T12:00' });
    component.save();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/tasks');
    vi.spyOn(component.session, 'hasPermission').mockReturnValue(false);
    component.form.patchValue({ deadline: '' });
    component.save();
    TestBed.inject(HttpTestingController).expectNone('/api/v1/tasks');
  });
});
