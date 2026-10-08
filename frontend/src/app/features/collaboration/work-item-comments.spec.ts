import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionService } from '../../core/auth/session';
import { CommentItem, WorkItemComments } from './work-item-comments';

describe('WorkItemComments component', () => {
  const mockComments: CommentItem[] = [
    {
      commentId: 'c-1',
      objectType: 'TASK',
      objectId: 'task-100',
      authorId: 'user-1',
      authorName: 'Alice Developer',
      authorEmail: 'alice@example.com',
      content: 'Initial discussion comment',
      createdAt: '2026-10-07T12:00:00Z',
      editedAt: null,
      isOwner: true,
      canDelete: true,
    },
    {
      commentId: 'c-2',
      objectType: 'TASK',
      objectId: 'task-100',
      authorId: 'user-2',
      authorName: 'Bob Reviewer',
      authorEmail: 'bob@example.com',
      content: 'Looks good to me',
      createdAt: '2026-10-07T12:05:00Z',
      editedAt: null,
      isOwner: false,
      canDelete: false,
    },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [WorkItemComments],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: SessionService,
          useValue: {
            hasPermission: (perm: string) => perm === 'comments.create' || perm === 'comments.read',
            identity: () => ({
              userId: 'user-1',
              fullName: 'Alice Developer',
              tenantId: 'tenant-1',
            }),
          },
        },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('loads and renders comments for the target object', () => {
    const fixture = TestBed.createComponent(WorkItemComments);
    fixture.componentRef.setInput('objectType', 'TASK');
    fixture.componentRef.setInput('objectId', 'task-100');
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    const req = http.expectOne((r) => r.url === '/api/v1/comments');
    expect(req.request.params.get('objectType')).toBe('TASK');
    expect(req.request.params.get('objectId')).toBe('task-100');
    req.flush(mockComments);

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Discussion & Collaboration');
    expect(fixture.nativeElement.textContent).toContain('Alice Developer');
    expect(fixture.nativeElement.textContent).toContain('Initial discussion comment');
    expect(fixture.nativeElement.textContent).toContain('Bob Reviewer');
    expect(fixture.nativeElement.textContent).toContain('Looks good to me');
  });

  it('posts a new comment and appends to the list', () => {
    const fixture = TestBed.createComponent(WorkItemComments);
    fixture.componentRef.setInput('objectType', 'TASK');
    fixture.componentRef.setInput('objectId', 'task-100');
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    http.expectOne((r) => r.url === '/api/v1/comments').flush([]);
    fixture.detectChanges();

    fixture.componentInstance.newCommentText = 'A fresh comment';
    fixture.componentInstance.postComment();

    const postReq = http.expectOne('/api/v1/comments');
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body).toEqual({
      objectType: 'TASK',
      objectId: 'task-100',
      content: 'A fresh comment',
    });

    const newComment: CommentItem = {
      commentId: 'c-3',
      objectType: 'TASK',
      objectId: 'task-100',
      authorId: 'user-1',
      authorName: 'Alice Developer',
      authorEmail: 'alice@example.com',
      content: 'A fresh comment',
      createdAt: '2026-10-07T12:10:00Z',
      editedAt: null,
      isOwner: true,
      canDelete: true,
    };
    postReq.flush(newComment);
    fixture.detectChanges();

    expect(fixture.componentInstance.comments().length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('A fresh comment');
    expect(fixture.componentInstance.newCommentText).toBe('');
  });

  it('allows owner to edit comment and saves changes', () => {
    const fixture = TestBed.createComponent(WorkItemComments);
    fixture.componentRef.setInput('objectType', 'TASK');
    fixture.componentRef.setInput('objectId', 'task-100');
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    http.expectOne((r) => r.url === '/api/v1/comments').flush(mockComments);
    fixture.detectChanges();

    fixture.componentInstance.startEdit(mockComments[0]);
    expect(fixture.componentInstance.editingCommentId).toBe('c-1');
    expect(fixture.componentInstance.editingText).toBe('Initial discussion comment');

    fixture.componentInstance.editingText = 'Edited discussion comment';
    fixture.componentInstance.saveEdit('c-1');

    const putReq = http.expectOne('/api/v1/comments/c-1');
    expect(putReq.request.method).toBe('PUT');
    expect(putReq.request.body).toEqual({ content: 'Edited discussion comment' });

    putReq.flush({
      ...mockComments[0],
      content: 'Edited discussion comment',
      editedAt: '2026-10-07T12:15:00Z',
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.editingCommentId).toBeNull();
    expect(fixture.componentInstance.comments()[0].content).toBe('Edited discussion comment');
  });

  it('deletes comment when user confirms', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const fixture = TestBed.createComponent(WorkItemComments);
    fixture.componentRef.setInput('objectType', 'TASK');
    fixture.componentRef.setInput('objectId', 'task-100');
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    http.expectOne((r) => r.url === '/api/v1/comments').flush(mockComments);
    fixture.detectChanges();

    fixture.componentInstance.deleteComment('c-1');

    const delReq = http.expectOne('/api/v1/comments/c-1');
    expect(delReq.request.method).toBe('DELETE');
    delReq.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(fixture.componentInstance.comments().length).toBe(1);
    expect(fixture.componentInstance.comments()[0].commentId).toBe('c-2');
  });
});
