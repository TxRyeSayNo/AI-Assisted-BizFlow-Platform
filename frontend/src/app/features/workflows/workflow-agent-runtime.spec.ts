import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { WorkflowRuntime } from './workflow-runtime';

describe('workflow AI results', () => {
  const path = '/api/v1/requests/request-1/workflow';
  const step = {
    id: 'step-1',
    nodeType: 'DEPARTMENT',
    name: 'AI prepare',
    status: 'PENDING',
    policy: 'SINGLE',
    dueAt: null,
    outcome: null,
    candidates: ['Owner'],
    decisions: [],
    candidateDetails: [],
    delegations: [],
    canAct: true,
    agent: {
      id: 'run-1',
      status: 'REVIEW_REQUIRED',
      attempts: 1,
      errorCode: null,
      model: 'test',
      result: {
        summary: 'Review budget',
        deliverable: '<img src=x onerror=alert(1)>',
        needsHumanReview: true,
        findings: ['Missing receipt'],
        evidence: [{ source: 'fields.budget', quote: '1200' }],
      },
    },
  };
  const run = {
    id: 'workflow-1',
    name: 'Onboarding',
    versionNo: 1,
    status: 'RUNNING',
    revision: 4,
    canDelegate: false,
    steps: [step],
    formFields: [],
    formData: {},
  };
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const create = () => {
    const fixture = TestBed.createComponent(WorkflowRuntime);
    fixture.componentRef.setInput('requestId', 'request-1');
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne(path).flush(run);
    fixture.detectChanges();
    return fixture;
  };
  it('shows a grounded deliverable as text and loads immutable history on demand', () => {
    const fixture = create();
    const http = TestBed.inject(HttpTestingController);
    expect(fixture.nativeElement.textContent).toContain('1200');
    expect(fixture.nativeElement.textContent).toContain('<img src=x');
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    fixture.componentInstance.loadAgentHistory(step);
    http.expectOne(`${path}/steps/step-1/agent/history`).flush([
      {
        attempt: 1,
        completedAt: '2026-11-25T01:00:00Z',
        trace: [
          { round: 1, tool: 'read_inputs' },
          {
            round: 2,
            action: 'prepare_deliverable',
            result: { summary: 'Old result', deliverable: 'Preserved result' },
          },
        ],
      },
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Preserved result');
  });
  it('requires a reason to retry and submits the current workflow revision once', () => {
    const fixture = create();
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.retryAgent(step);
    http.expectNone(`${path}/steps/step-1/agent/retry`);
    fixture.componentInstance.agentReasons['step-1'] = 'Model recovered';
    fixture.componentInstance.retryAgent(step);
    fixture.componentInstance.retryAgent(step);
    const request = http.expectOne(`${path}/steps/step-1/agent/retry`);
    expect(request.request.body).toEqual({ revision: 4, reason: 'Model recovered' });
    request.flush({
      ...run,
      revision: 5,
      steps: [{ ...step, agent: { ...step.agent, status: 'QUEUED' } }],
    });
    expect(fixture.componentInstance.runtime()?.revision).toBe(5);
  });
});
