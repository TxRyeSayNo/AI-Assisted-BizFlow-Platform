import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { TaskDetail } from './task-detail';

describe('scoped task detail', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'task' })) } },
      ],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('renders nonrevealing unavailable errors and clears stale data during reload', () => {
    const fixture = TestBed.createComponent(TaskDetail);
    const http = TestBed.inject(HttpTestingController);
    http
      .expectOne((r) => r.url === '/api/v1/tasks/task')
      .flush({}, { status: 404, statusText: 'Not found' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('unavailable or outside your access scope');
    fixture.componentInstance.page('reports', 2);
    const request = http.expectOne((r) => r.url === '/api/v1/tasks/task');
    expect(request.request.params.get('reportPage')).toBe('2');
    expect(request.request.params.get('checklistPage')).toBe('1');
    request.flush({}, { status: 503, statusText: 'Unavailable' });
    expect(fixture.componentInstance.data()).toBeNull();
  });
});
