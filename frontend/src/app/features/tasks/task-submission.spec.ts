import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';
import { TaskSubmissionEditor } from './task-submission';

describe('Task submission', () => {
  const dialog = { close: vi.fn(), disableClose: false };
  let permission = true;

  beforeEach(() => {
    permission = true;
    dialog.close.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { taskId: 'task-1', title: 'Deliverable Work' } },
        { provide: MatDialogRef, useValue: dialog },
        { provide: SessionService, useValue: { hasPermission: () => permission } },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('submits result deliverables with idempotency key and closes on success', () => {
    const c = TestBed.createComponent(TaskSubmissionEditor).componentInstance;
    const http = TestBed.inject(HttpTestingController);
    c.content.setValue('Completed research draft.');
    c.save();
    c.save(); // double-submit guarded

    const req = http.expectOne('/api/v1/tasks/task-1/result');
    expect(req.request.body).toEqual({ content: 'Completed research draft.' });
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    req.flush({
      taskId: 'task-1',
      taskResultId: 'result-1',
      revisionNo: 1,
      status: 'SUBMITTED',
      submittedAt: new Date().toISOString(),
    });

    expect(dialog.close).toHaveBeenCalledWith(true);
  });

  it('retries with same key on transient failure and blocks unauthorized attempt', () => {
    const c = TestBed.createComponent(TaskSubmissionEditor).componentInstance;
    const http = TestBed.inject(HttpTestingController);

    permission = false;
    c.save();
    http.expectNone('/api/v1/tasks/task-1/result');

    permission = true;
    c.content.setValue('Draft 1');
    c.save();

    const first = http.expectOne('/api/v1/tasks/task-1/result');
    const key = first.request.headers.get('Idempotency-Key');
    first.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(c.uncertain()).toBe(true);

    c.save();
    const retry = http.expectOne('/api/v1/tasks/task-1/result');
    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    retry.flush({
      taskId: 'task-1',
      taskResultId: 'result-1',
      revisionNo: 1,
      status: 'SUBMITTED',
      submittedAt: new Date().toISOString(),
    });

    expect(dialog.close).toHaveBeenCalledWith(true);
  });
});
