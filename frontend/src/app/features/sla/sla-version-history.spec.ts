import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Subject } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { SlaVersionHistory, SlaHistoryPage } from './sla-version-history';
import { SlaTimingEditor, SlaTimingOutcome } from './sla-timing-editor';

describe('SLA immutable version history', () => {
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let canConfigure: WritableSignal<boolean>;
  beforeEach(() => {
    params = new BehaviorSubject(convertToParamMap({ id: 'profile' }));
    canConfigure = signal(false);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: { identity: () => null, hasPermission: () => canConfigure() },
        },
        { provide: ActivatedRoute, useValue: { paramMap: params } },
      ],
    });
  });
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    TestBed.inject(MatDialog).closeAll();
  });
  const pending = (id = 'profile') =>
    TestBed.inject(HttpTestingController).expectOne(
      (r) => r.url === `/api/v1/sla-profiles/${id}/versions`,
    );
  const data: SlaHistoryPage = {
    slaProfileId: 'profile',
    profileName: '<script>unsafe</script>',
    page: 1,
    pageSize: 25,
    total: 26,
    items: [
      {
        slaVersionId: 'snapshot',
        versionNo: 26,
        targetMinutes: 60,
        warningMinutes: 45,
        escalationConfig: {
          levels: [{ level: 1, afterMinutes: 0, recipientUserIds: ['recipient-id'] }],
        },
        calendar: {
          calendarId: 'calendar-id',
          timeZone: 'UTC',
          workingHours: { monday: [{ start: '08:00', end: '17:30' }] },
          holidays: ['2026-10-03'],
        },
      },
    ],
  };
  it('loads safe read-only history, pages explicitly and clears stale data', () => {
    const fixture = TestBed.createComponent(SlaVersionHistory);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading saved versions');
    const request = pending();
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageSize')).toBe('25');
    request.flush(data);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('<script>unsafe</script>');
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
    fixture.componentInstance.load(2);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('unsafe');
    const second = pending();
    expect(second.request.params.get('page')).toBe('2');
    second.flush({ ...data, page: 2, items: [] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No saved versions on this page');
    TestBed.inject(HttpTestingController).expectNone((r) => r.method !== 'GET');
  });
  it('distinguishes an empty owned profile from a missing profile', () => {
    const fixture = TestBed.createComponent(SlaVersionHistory);
    pending().flush({ ...data, total: 0, items: [] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('This profile has no saved versions');
    expect(fixture.nativeElement.querySelector('button:last-child').disabled).toBe(true);
  });
  it.each([403, 404, 503])(
    'handles %i without exposing provider details or stale configuration',
    (status) => {
      const fixture = TestBed.createComponent(SlaVersionHistory);
      pending().flush(data);
      fixture.componentInstance.load(1);
      pending().flush({ detail: 'private-details' }, { status, statusText: 'Failure' });
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent;
      expect(text).not.toContain('unsafe');
      expect(text).not.toContain('private-details');
      expect(text).toContain(
        status === 403
          ? 'no longer have access'
          : status === 404
            ? 'not found'
            : 'could not be loaded',
      );
      expect(text.includes('Retry')).toBe(status === 503);
    },
  );
  it('cancels the old route request and rejects a mismatched profile response', () => {
    const fixture = TestBed.createComponent(SlaVersionHistory);
    const old = pending();
    params.next(convertToParamMap({ id: 'other' }));
    expect(old.cancelled).toBe(true);
    pending('other').flush(data);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('not found');
    expect(fixture.componentInstance.result()).toBeNull();
  });
  it('shows exact frozen dates, intervals and escalation recipients without edit controls', () => {
    const fixture = TestBed.createComponent(SlaVersionHistory);
    pending().flush(data);
    fixture.detectChanges();
    fixture.componentInstance.inspect(fixture.componentInstance.result()!.items[0]);
    fixture.detectChanges();
    const dialog = document.querySelector('mat-dialog-container')!;
    expect(dialog.textContent).toContain('Immutable snapshot');
    expect(dialog.textContent).toContain('08:00–17:30');
    expect(dialog.textContent).toContain('2026-10-03');
    expect(dialog.textContent).toContain('No working time');
    expect(dialog.textContent).toContain('0 working minutes after breach');
    expect(dialog.textContent).toContain('recipient-id');
    expect(dialog.querySelectorAll('input,select,textarea').length).toBe(0);
    fixture.componentInstance.load(1);
    pending().flush({ ...data, items: [], total: 0 });
  });
  it('gates timing controls on configure permission and current visible row membership', () => {
    const fixture = TestBed.createComponent(SlaVersionHistory);
    pending().flush(data);
    fixture.detectChanges();
    const open = vi.spyOn(TestBed.inject(MatDialog), 'open');
    fixture.componentInstance.createTimingVersion(data.items[0]);
    expect(open).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain('Adjust timing');
    canConfigure.set(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Adjust timing');
    fixture.componentInstance.createTimingVersion({ ...data.items[0] });
    expect(open).not.toHaveBeenCalled();
  });
  it.each([true, false])('reloads history after saved/uncertain editor outcome: %s', (saved) => {
    vi.spyOn(TestBed.inject(SessionService), 'hasPermission').mockReturnValue(true);
    const fixture = TestBed.createComponent(SlaVersionHistory);
    pending().flush(data);
    const closed = new Subject<SlaTimingOutcome>();
    const open = vi
      .spyOn(TestBed.inject(MatDialog), 'open')
      .mockReturnValue({ close: vi.fn(), afterClosed: () => closed } as never);
    fixture.componentInstance.createTimingVersion(fixture.componentInstance.result()!.items[0]);
    expect(open).toHaveBeenCalledWith(
      SlaTimingEditor,
      expect.objectContaining({
        data: { profileId: 'profile', profileName: data.profileName, source: data.items[0] },
      }),
    );
    closed.next(saved ? { versionNo: 27, reviewHistory: true } : { reviewHistory: true });
    const reload = pending();
    expect(reload.request.params.get('page')).toBe('1');
    reload.flush(data);
    expect(fixture.componentInstance.notice()).toContain(
      saved ? 'version 27 created' : 'Review saved versions',
    );
    closed.complete();
  });
  it('ignores a closed editor outcome after navigation to another profile', () => {
    vi.spyOn(TestBed.inject(SessionService), 'hasPermission').mockReturnValue(true);
    const fixture = TestBed.createComponent(SlaVersionHistory);
    pending().flush(data);
    const closed = new Subject<SlaTimingOutcome>();
    vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({
      close: vi.fn(),
      afterClosed: () => closed,
    } as never);
    fixture.componentInstance.createTimingVersion(fixture.componentInstance.result()!.items[0]);
    params.next(convertToParamMap({ id: 'other' }));
    pending('other').flush({ ...data, slaProfileId: 'other' });
    closed.next({ versionNo: 27, reviewHistory: true });
    closed.complete();
    expect(fixture.componentInstance.notice()).toBeNull();
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });
});
