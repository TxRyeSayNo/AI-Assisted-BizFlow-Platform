import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';
import { TaskConfirmationEditor } from './task-confirmation';

describe('Task confirmation', () => {
  const dialog = { close: vi.fn(), disableClose: false };
  let permission = true;

  beforeEach(() => {
    permission = true;
    dialog.close.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { taskId: 'task-1', title: 'Review Work' } },
        { provide: MatDialogRef, useValue: dialog },
        { provide: SessionService, useValue: { hasPermission: () => permission } },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('submits confirmation decision and closes dialog on success', () => {
    const c = TestBed.createComponent(TaskConfirmationEditor).componentInstance;
    const http = TestBed.inject(HttpTestingController);

    c.form.controls.decision.setValue('CONFIRMED');
    c.form.controls.note.setValue('Approved deliverables.');
    c.save();

    const req = http.expectOne('/api/v1/tasks/task-1/confirmation');
    expect(req.request.body).toEqual({ decision: 'CONFIRMED', note: 'Approved deliverables.' });
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    req.flush({
      taskId: 'task-1',
      confirmationId: 'conf-1',
      decision: 'CONFIRMED',
      status: 'COMPLETED',
      confirmedAt: new Date().toISOString(),
    });

    expect(dialog.close).toHaveBeenCalledWith(true);
  });

  it('requires note when decision is REWORK', () => {
    const c = TestBed.createComponent(TaskConfirmationEditor).componentInstance;
    c.form.controls.decision.setValue('REWORK');
    c.form.controls.note.setValue('');

    expect(c.form.invalid).toBe(true);

    c.form.controls.note.setValue('Please address the review comments.');
    expect(c.form.valid).toBe(true);
  });

  it('submits rework decision with reason note and closes dialog on success', () => {
    const c = TestBed.createComponent(TaskConfirmationEditor).componentInstance;
    const http = TestBed.inject(HttpTestingController);

    c.form.controls.decision.setValue('REWORK');
    c.form.controls.note.setValue('Needs revision.');
    c.save();

    const req = http.expectOne('/api/v1/tasks/task-1/confirmation');
    expect(req.request.body).toEqual({ decision: 'REWORK', note: 'Needs revision.' });
    req.flush({
      taskId: 'task-1',
      confirmationId: 'conf-2',
      decision: 'REWORK',
      status: 'IN_PROGRESS',
      confirmedAt: new Date().toISOString(),
    });

    expect(dialog.close).toHaveBeenCalledWith(true);
  });
});
