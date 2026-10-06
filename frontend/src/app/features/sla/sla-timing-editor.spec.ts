import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { SessionService } from '../../core/auth/session';
import { SlaTimingData, SlaTimingEditor } from './sla-timing-editor';

describe('SLA timing snapshot editor', () => {
  let allowed: boolean;
  let data: SlaTimingData;
  let dialog: { close: ReturnType<typeof vi.fn>; disableClose: boolean };
  beforeEach(() => {
    allowed = true;
    dialog = { close: vi.fn(), disableClose: false };
    data = {
      profileId: 'profile',
      profileName: '<script>profile</script>',
      source: {
        slaVersionId: 'original',
        versionNo: 3,
        targetMinutes: 60,
        warningMinutes: 45,
        calendar: {
          calendarId: 'calendar',
          timeZone: 'UTC',
          workingHours: { monday: [{ start: '08:00', end: '17:30' }] },
          holidays: ['2026-10-03'],
        },
        escalationConfig: {
          levels: [{ level: 1, afterMinutes: 0, recipientUserIds: ['recipient'] }],
        },
      },
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialog },
        { provide: SessionService, useValue: { hasPermission: () => allowed } },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const pending = () =>
    TestBed.inject(HttpTestingController).expectOne('/api/v1/sla-profiles/profile/versions');
  const saved = {
    slaVersionId: 'new',
    slaProfileId: 'profile',
    versionNo: 4,
    targetMinutes: 90,
    warningMinutes: 50,
    calendarId: 'calendar',
  };
  it('creates one snapshot preserving calendar and escalation without mutating its source', () => {
    const fixture = TestBed.createComponent(SlaTimingEditor);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
    const editor = fixture.componentInstance;
    editor.form.setValue({ targetMinutes: 90, warningMinutes: 50 });
    editor.save();
    editor.save();
    editor.close();
    expect(dialog.disableClose).toBe(true);
    expect(dialog.close).not.toHaveBeenCalled();
    const request = pending();
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      targetMinutes: 90,
      warningMinutes: 50,
      calendarId: 'calendar',
      escalationConfig: data.source.escalationConfig,
    });
    request.flush(saved);
    editor.save();
    TestBed.inject(HttpTestingController).expectNone(() => true);
    expect(dialog.close).toHaveBeenCalledWith({ versionNo: 4, reviewHistory: true });
    expect(data.source.targetMinutes).toBe(60);
    expect(data.source.warningMinutes).toBe(45);
    expect(editor.data.source.escalationConfig).not.toBe(data.source.escalationConfig);
  });
  it.each([
    [0, 0],
    [60, 60],
    [60, -1],
    [60.5, 45],
    [60, 1.5],
    [2147483648, 45],
  ])('rejects invalid timing %i / %i without a request', (targetMinutes, warningMinutes) => {
    const editor = TestBed.createComponent(SlaTimingEditor).componentInstance;
    editor.form.setValue({ targetMinutes, warningMinutes });
    editor.save();
    expect(editor.form.invalid).toBe(true);
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });
  it('rechecks revoked configuration permission before sending', () => {
    const editor = TestBed.createComponent(SlaTimingEditor).componentInstance;
    allowed = false;
    editor.save();
    expect(editor.terminal()).toBe(true);
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });
  it('permits corrected submission after a definite validation rejection', () => {
    const editor = TestBed.createComponent(SlaTimingEditor).componentInstance;
    editor.save();
    pending().flush({ detail: 'private-server-detail' }, { status: 422, statusText: 'Invalid' });
    expect(editor.uncertain()).toBe(false);
    expect(editor.error()).not.toContain('private-server-detail');
    editor.form.setValue({ targetMinutes: 90, warningMinutes: 50 });
    editor.save();
    pending().flush(saved);
    expect(dialog.close).toHaveBeenCalledWith({ versionNo: 4, reviewHistory: true });
  });
  it.each([0, 503])(
    'blocks duplicate POST after uncertain status %i and requests history review',
    (status) => {
      const editor = TestBed.createComponent(SlaTimingEditor).componentInstance;
      editor.save();
      const request = pending();
      if (status === 0) request.error(new ProgressEvent('error'));
      else request.flush({}, { status, statusText: 'Unavailable' });
      expect(editor.uncertain()).toBe(true);
      expect(dialog.disableClose).toBe(true);
      editor.save();
      TestBed.inject(HttpTestingController).expectNone(() => true);
      editor.close();
      expect(dialog.close).toHaveBeenCalledWith({ reviewHistory: true });
    },
  );
  it('treats an inconsistent success response as unknown, not proof of persistence', () => {
    const editor = TestBed.createComponent(SlaTimingEditor).componentInstance;
    editor.save();
    pending().flush({ ...saved, slaProfileId: 'foreign' });
    expect(editor.uncertain()).toBe(true);
    expect(dialog.close).not.toHaveBeenCalled();
  });
  it.each([401, 403, 404])('stops resubmission after terminal status %i', (status) => {
    const editor = TestBed.createComponent(SlaTimingEditor).componentInstance;
    editor.save();
    pending().flush({}, { status, statusText: 'Unavailable' });
    editor.save();
    expect(editor.terminal()).toBe(true);
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });
  it('cancels its outstanding subscription on destruction without reporting success', () => {
    const fixture = TestBed.createComponent(SlaTimingEditor);
    fixture.componentInstance.save();
    const request = pending();
    fixture.destroy();
    expect(request.cancelled).toBe(true);
    expect(dialog.close).not.toHaveBeenCalled();
  });
});
