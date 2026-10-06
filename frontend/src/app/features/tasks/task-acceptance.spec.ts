import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';
import { TaskAcceptanceEditor } from './task-acceptance';

describe('Task acceptance', () => {
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
  it('keeps exact unknown-outcome intent/key and prevents duplicate acceptance', () => {
    const c = TestBed.createComponent(TaskAcceptanceEditor).componentInstance;
    const http = TestBed.inject(HttpTestingController);
    c.note.setValue('Ready');
    c.save();
    c.save();
    const first = http.expectOne('/api/v1/tasks/task/accept');
    const key = first.request.headers.get('Idempotency-Key');
    first.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(c.uncertain()).toBe(true);
    expect(c.note.disabled).toBe(true);
    c.note.setValue('Changed');
    c.save();
    const retry = http.expectOne('/api/v1/tasks/task/accept');
    expect(retry.request.body).toEqual({ note: 'Ready' });
    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    retry.flush({
      taskId: 'task',
      assignmentId: 'assignment',
      confirmationId: 'confirmation',
      status: 'ACCEPTED',
    });
    c.save();
    http.expectNone('/api/v1/tasks/task/accept');
    expect(dialog.close).toHaveBeenCalledWith(true);
  });
  it('requires review after another claimant or revoked access and cannot resubmit', () => {
    const c = TestBed.createComponent(TaskAcceptanceEditor).componentInstance;
    c.save();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/v1/tasks/task/accept').flush({}, { status: 404, statusText: 'Not found' });
    expect(c.terminal()).toBe(true);
    c.save();
    http.expectNone('/api/v1/tasks/task/accept');
    c.close();
    expect(dialog.close).toHaveBeenCalledWith(true);
  });
});
