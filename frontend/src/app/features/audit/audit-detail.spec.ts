import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { AuditDetail } from './audit-detail';

describe('audit event details', () => {
  it('renders stored JSON as escaped text and keeps missing system attribution explicit', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: MatDialogRef, useValue: { close: () => undefined } },
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            scopeLabel: 'Platform event',
            event: {
              auditLogId: 'event',
              actorType: 'SYSTEM',
              actorId: null,
              action: 'AUTH.LOGIN_SUCCEEDED',
              objectType: 'User',
              objectId: 'user',
              before: null,
              after: { name: '<img src=x onerror=alert(1)>' },
              metadata: { note: '<script>unsafe</script>' },
              createdAt: '2026-10-01T09:00:00Z',
            },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(AuditDetail);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('<img src=x onerror=alert(1)>');
    expect(fixture.nativeElement.textContent).toContain('No actor ID recorded');
    expect(fixture.nativeElement.textContent).toContain('No previous values recorded');
    expect(fixture.nativeElement.textContent).toContain('2026-10-01 09:00:00');
  });
});
