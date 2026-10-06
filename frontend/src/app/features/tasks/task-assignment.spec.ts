import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';
import { TaskAssignmentEditor } from './task-assignment';

describe('Task assignment editor', () => {
  const dialog = { close: vi.fn(), disableClose: false };
  beforeEach(() => {
    dialog.close.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { taskId: 'task', title: 'Work' } },
        { provide: MatDialogRef, useValue: dialog },
        { provide: SessionService, useValue: { hasPermission: () => true } },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function setup() {
    const component = TestBed.createComponent(TaskAssignmentEditor).componentInstance;
    TestBed.inject(HttpTestingController)
      .expectOne((r) => r.url === '/api/v1/users')
      .flush({ items: [{ userId: 'user', fullName: 'Person', employeeCode: 'EMP' }], total: 1 });
    component.form.patchValue({ targetId: 'user' });
    return component;
  }
  it('retains the exact intent/key on unknown results and cannot double-submit', () => {
    const component = setup();
    const http = TestBed.inject(HttpTestingController);
    component.save();
    component.save();
    const first = http.expectOne('/api/v1/tasks/task/assign');
    const key = first.request.headers.get('Idempotency-Key');
    const body = first.request.body;
    first.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(component.uncertain()).toBe(true);
    component.form.patchValue({ targetId: 'changed' });
    component.save();
    const retry = http.expectOne('/api/v1/tasks/task/assign');
    expect(retry.request.body).toEqual(body);
    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    retry.flush({ taskId: 'task', assignmentId: 'assignment', status: 'ASSIGNED' });
    component.save();
    http.expectNone('/api/v1/tasks/task/assign');
    expect(dialog.close).toHaveBeenCalledWith(true);
  });
  it('shows scope rejection without relying on directory visibility as authorization', () => {
    const component = setup();
    component.save();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/tasks/task/assign')
      .flush({ code: 'TASK.TARGET_OUT_OF_SCOPE' }, { status: 403, statusText: 'Forbidden' });
    expect(component.error()).toContain('management scope');
    expect(component.form.enabled).toBe(true);
  });
});
