import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';
import { TaskExecutionAction } from './task-execution';

describe('Task execution', () => {
  const dialog = { close: vi.fn(), disableClose: false };
  let permission = true;
  beforeEach(() => {
    permission = true;
    dialog.close.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { taskId: 'task', title: 'Work', resume: false } },
        { provide: MatDialogRef, useValue: dialog },
        { provide: SessionService, useValue: { hasPermission: () => permission } },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('retries the identical key with no caller-controlled state and cannot double-submit', () => {
    const c = TestBed.createComponent(TaskExecutionAction).componentInstance;
    const http = TestBed.inject(HttpTestingController);
    c.save();
    c.save();
    const first = http.expectOne('/api/v1/tasks/task/start');
    expect(first.request.body).toEqual({});
    const key = first.request.headers.get('Idempotency-Key');
    first.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(c.uncertain()).toBe(true);
    c.save();
    const retry = http.expectOne('/api/v1/tasks/task/start');
    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    retry.flush({ taskId: 'task', assignmentId: 'assignment', status: 'IN_PROGRESS' });
    c.save();
    http.expectNone('/api/v1/tasks/task/start');
    expect(dialog.close).toHaveBeenCalledWith(true);
  });
  it('blocks missing capability and requires review after a state conflict', () => {
    const c = TestBed.createComponent(TaskExecutionAction).componentInstance;
    const http = TestBed.inject(HttpTestingController);
    permission = false;
    c.save();
    http.expectNone('/api/v1/tasks/task/start');
    permission = true;
    c.save();
    http.expectOne('/api/v1/tasks/task/start').flush({}, { status: 409, statusText: 'Conflict' });
    expect(c.terminal()).toBe(true);
    c.save();
    http.expectNone('/api/v1/tasks/task/start');
    c.close();
    expect(dialog.close).toHaveBeenCalledWith(true);
  });
});
