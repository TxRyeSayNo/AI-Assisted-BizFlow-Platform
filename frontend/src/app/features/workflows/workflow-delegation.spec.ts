import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { WorkflowDelegation } from './workflow-delegation';

describe('workflow step delegation', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function setup() {
    const fixture = TestBed.createComponent(WorkflowDelegation);
    fixture.componentRef.setInput('requestId', 'request');
    fixture.componentRef.setInput('stepId', 'step');
    fixture.componentRef.setInput('revision', 4);
    fixture.componentRef.setInput('candidates', [
      { userId: 'principal', name: 'Người gốc', decided: false },
    ]);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/workflows/lookup/users')
      .flush([
        { id: 'principal', fullName: 'Người gốc', employeeCode: 'P' },
        { id: 'backup', fullName: 'Người thay', employeeCode: 'B' },
      ]);
    fixture.detectChanges();
    return fixture;
  }
  it('offers a different recipient and sends revision plus the original accountable slot', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelectorAll('select')[1].textContent).not.toContain(
      'Người gốc',
    );
    component.principalId = 'principal';
    component.delegateId = 'backup';
    component.reason = 'Nghỉ phép';
    const expiry = new Date(Date.now() + 86400000);
    component.expiresAt = expiry.toISOString();
    const updated = vi.fn();
    component.updated.subscribe(updated);
    component.save();
    component.save();
    const request = TestBed.inject(HttpTestingController).expectOne(
      '/api/v1/requests/request/workflow/steps/step/delegations',
    );
    expect(request.request.body).toEqual({
      revision: 4,
      principalId: 'principal',
      delegateId: 'backup',
      expiresAt: expiry.toISOString(),
      reason: 'Nghỉ phép',
    });
    request.flush({});
    expect(updated).toHaveBeenCalledOnce();
    expect(component.busy()).toBe(false);
  });
  it('refuses expired dates and requests a refresh when a concurrent decision changes revision', () => {
    const fixture = setup();
    const component = fixture.componentInstance;
    component.principalId = 'principal';
    component.delegateId = 'backup';
    component.reason = 'Vắng mặt';
    component.expiresAt = '2000-01-01T00:00';
    component.save();
    expect(component.error()).toContain('tương lai');
    component.expiresAt = new Date(Date.now() + 86400000).toISOString();
    const updated = vi.fn();
    component.updated.subscribe(updated);
    component.save();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/requests/request/workflow/steps/step/delegations')
      .flush({ message: 'Hãy tải lại' }, { status: 409, statusText: 'Conflict' });
    expect(updated).toHaveBeenCalledOnce();
    expect(component.error()).toBe('Hãy tải lại');
    expect(component.busy()).toBe(false);
  });
});
