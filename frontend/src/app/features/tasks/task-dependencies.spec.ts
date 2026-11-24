import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { FeatureFlagsService } from '../../core/features/feature-flags.service';
import { ToastService } from '../../core/toast/toast.service';
import { TaskDependencies, TaskDependencyView } from './task-dependencies';

describe('task dependencies', () => {
  const taskId = 'task-1';
  const url = `/api/v1/tasks/${taskId}/dependencies`;

  const configure = (enabled: boolean) =>
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: FeatureFlagsService,
          useValue: { taskDependencies: () => enabled },
        },
      ],
    });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const view = (over: Partial<TaskDependencyView> = {}): TaskDependencyView => ({
    taskId,
    dependsOn: [
      {
        taskId: 'task-0',
        title: 'Việc trước',
        status: 'IN_PROGRESS',
        settled: false,
        deadline: '2026-12-01T00:00:00Z',
      },
    ],
    blocks: [],
    blockedBy: 1,
    ...over,
  });

  const create = () => {
    const fixture = TestBed.createComponent(TaskDependencies);
    fixture.componentRef.setInput('objectId', taskId);
    fixture.detectChanges();
    return fixture;
  };

  it('shows both directions and warns only while a predecessor is open', () => {
    configure(true);
    const fixture = create();
    TestBed.inject(HttpTestingController).expectOne(url).flush(view());
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Việc trước');
    expect(fixture.nativeElement.querySelector('.dependency-warning')).not.toBeNull();
    // The open predecessor is marked structurally, not by colour alone.
    expect(fixture.nativeElement.querySelectorAll('.dependency-open').length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('No other task is waiting');
  });

  it('drops the warning once every predecessor has settled', () => {
    configure(true);
    const fixture = create();
    TestBed.inject(HttpTestingController)
      .expectOne(url)
      .flush(
        view({
          dependsOn: [
            {
              taskId: 'task-0',
              title: 'Việc trước',
              status: 'COMPLETED',
              settled: true,
              deadline: null,
            },
          ],
          blockedBy: 0,
        }),
      );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.dependency-warning')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('.dependency-open').length).toBe(0);
  });

  it('searches through the ordinary task list and leaves out what is already linked', () => {
    configure(true);
    const fixture = create();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(url).flush(view());
    fixture.detectChanges();

    fixture.componentInstance.search.setValue('  chuẩn bị  ');
    fixture.componentInstance.find();

    const search = http.expectOne((r) => r.url === '/api/v1/tasks');
    expect(search.request.params.get('search')).toBe('chuẩn bị');
    expect(search.request.params.get('pageSize')).toBe('10');
    search.flush({
      items: [
        { taskId: 'task-0', title: 'Việc trước', status: 'IN_PROGRESS' },
        { taskId: taskId, title: 'Chính nó', status: 'DRAFT' },
        { taskId: 'task-9', title: 'Việc khác', status: 'DRAFT' },
      ],
    });
    fixture.detectChanges();

    // Neither the task itself nor an existing predecessor is offered again.
    expect(fixture.componentInstance.candidates().map((c) => c.taskId)).toEqual(['task-9']);
  });

  it('posts a new link and replaces the view with the server answer', () => {
    configure(true);
    const fixture = create();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(url).flush(view({ dependsOn: [], blockedBy: 0 }));

    fixture.componentInstance.add('task-9');

    const posted = http.expectOne((r) => r.method === 'POST' && r.url === url);
    expect(posted.request.body).toEqual({ dependsOnTaskId: 'task-9' });
    posted.flush(view());
    fixture.detectChanges();

    expect(fixture.componentInstance.view()?.blockedBy).toBe(1);
    expect(TestBed.inject(ToastService).toasts()).toEqual([]);
  });

  it('deletes a link and reports a refusal with the server message', () => {
    configure(true);
    const fixture = create();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(url).flush(view());

    fixture.componentInstance.remove('task-0');
    const deleted = http.expectOne((r) => r.method === 'DELETE' && r.url === `${url}/task-0`);
    deleted.flush(view({ dependsOn: [], blockedBy: 0 }));
    expect(fixture.componentInstance.view()?.dependsOn).toEqual([]);

    fixture.componentInstance.add('task-9');
    http
      .expectOne((r) => r.method === 'POST')
      .flush(
        { message: 'That link would make two tasks wait for each other.' },
        { status: 422, statusText: 'Unprocessable' },
      );
    const toasts = TestBed.inject(ToastService).toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].message).toContain('wait for each other');
  });

  it('never shows a server message from a 5xx', () => {
    configure(true);
    const fixture = create();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(url).flush(view());

    fixture.componentInstance.add('task-9');
    http
      .expectOne((r) => r.method === 'POST')
      .flush({ message: 'internal detail' }, { status: 500, statusText: 'Server Error' });

    const toasts = TestBed.inject(ToastService).toasts();
    expect(toasts[0].message).not.toContain('internal detail');
    expect(toasts[0].message).toContain('phụ thuộc');
  });

  it('renders nothing and asks for nothing while the feature is off', () => {
    configure(false);
    const fixture = create();

    TestBed.inject(HttpTestingController).expectNone(url);
    expect(fixture.nativeElement.querySelector('.task-dependencies')).toBeNull();
  });

  it('stays hidden when the task is out of scope', () => {
    configure(true);
    const fixture = create();
    TestBed.inject(HttpTestingController)
      .expectOne(url)
      .flush({ message: 'private detail' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.componentInstance.available()).toBe(false);
    expect(fixture.nativeElement.textContent).not.toContain('private detail');
  });

  it('cancels an outstanding read when the view is destroyed', () => {
    configure(true);
    const fixture = create();
    const request = TestBed.inject(HttpTestingController).expectOne(url);
    fixture.destroy();
    expect(request.cancelled).toBe(true);
  });
});
