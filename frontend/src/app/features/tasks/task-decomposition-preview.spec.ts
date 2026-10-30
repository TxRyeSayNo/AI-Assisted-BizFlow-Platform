import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { SessionService } from '../../core/auth/session';
import { TaskDecompositionPreview } from './task-decomposition-preview';

describe('TaskDecompositionPreview Component', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SessionService, useValue: { hasPermission: () => true } },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('loads departments on init and requests preview with zero database persistence (BR-028)', () => {
    const fixture = TestBed.createComponent(TaskDecompositionPreview);
    const component = fixture.componentInstance;
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // Initial departments load
    const deptReq = http.expectOne('/api/v1/departments');
    expect(deptReq.request.method).toBe('GET');
    deptReq.flush([{ id: 'd1111111-1111-1111-1111-111111111111', name: 'Phòng IT' }]);

    expect(component.departments().length).toBe(1);

    // Enter prompt and click preview
    component.prompt.set('1. Cài đặt hệ điều hành\n2. Cấu hình VPN');
    component.previewDecomposition();

    expect(component.previewBusy()).toBe(true);

    const previewReq = http.expectOne('/api/v1/tasks/ai-decomposition/preview');
    expect(previewReq.request.method).toBe('POST');
    expect(previewReq.request.body).toEqual({ prompt: '1. Cài đặt hệ điều hành\n2. Cấu hình VPN' });

    previewReq.flush({
      originalPrompt: '1. Cài đặt hệ điều hành\n2. Cấu hình VPN',
      proposals: [
        {
          title: 'Cài đặt hệ điều hành',
          description: 'Cài Windows 11',
          type: 'Assignment',
          priority: 'MEDIUM',
          checklistItems: ['Tải ISO', 'Cài đặt'],
        },
        {
          title: 'Cấu hình VPN',
          description: 'Cấp quyền truy cập',
          type: 'SupportRequest',
          priority: 'HIGH',
          checklistItems: [],
        },
      ],
    });

    expect(component.previewBusy()).toBe(false);
    expect(component.proposals().length).toBe(2);
    expect(component.selectedCount).toBe(2);
    expect(component.proposals()[0].title).toBe('Cài đặt hệ điều hành');
    expect(component.proposals()[1].type).toBe('SupportRequest');
  });

  it('supports selecting, deselecting, removing proposals and adding subtasks', () => {
    const fixture = TestBed.createComponent(TaskDecompositionPreview);
    const component = fixture.componentInstance;
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne('/api/v1/departments').flush([]);

    // Populate proposals manually
    component.proposals.set([
      {
        selected: true,
        title: 'Task A',
        description: 'Desc A',
        type: 'Assignment',
        departmentId: null,
        departmentName: null,
        priority: 'MEDIUM',
        checklistItems: ['Step 1'],
      },
      {
        selected: true,
        title: 'Task B',
        description: 'Desc B',
        type: 'Assignment',
        departmentId: null,
        departmentName: null,
        priority: 'LOW',
        checklistItems: [],
      },
    ]);

    expect(component.selectedCount).toBe(2);

    // Toggle select all off
    component.toggleSelectAll(false);
    expect(component.selectedCount).toBe(0);

    // Toggle select all on
    component.toggleSelectAll(true);
    expect(component.selectedCount).toBe(2);

    // Add subtask
    const first = component.proposals()[0];
    first.newChecklistInput = 'Step 2';
    component.addChecklistItem(first);
    expect(first.checklistItems).toEqual(['Step 1', 'Step 2']);

    // Remove subtask
    component.removeChecklistItem(first, 0);
    expect(first.checklistItems).toEqual(['Step 2']);

    // Remove item B
    component.removeItem(1);
    expect(component.proposals().length).toBe(1);
    expect(component.proposals()[0].title).toBe('Task A');
  });

  it('confirms decomposition by sending selected items to batch confirm API', () => {
    const fixture = TestBed.createComponent(TaskDecompositionPreview);
    const component = fixture.componentInstance;
    const http = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();

    http.expectOne('/api/v1/departments').flush([]);

    component.proposals.set([
      {
        selected: true,
        title: 'Thực hiện kiểm thử',
        description: 'Chạy automated test suite',
        type: 'Assignment',
        departmentId: 'd1111111-1111-1111-1111-111111111111',
        departmentName: 'IT',
        priority: 'HIGH',
        checklistItems: ['Unit tests', 'E2E tests'],
      },
      {
        selected: false, // unselected proposal
        title: 'Đề xuất bị bỏ qua',
        description: '',
        type: 'SupportRequest',
        departmentId: null,
        departmentName: null,
        priority: 'LOW',
        checklistItems: [],
      },
    ]);

    component.confirmDecomposition();

    expect(component.confirmBusy()).toBe(true);

    const confirmReq = http.expectOne('/api/v1/tasks/ai-decomposition/confirm');
    expect(confirmReq.request.method).toBe('POST');
    // Only selected item was included!
    expect(confirmReq.request.body).toEqual({
      items: [
        {
          title: 'Thực hiện kiểm thử',
          description: 'Chạy automated test suite',
          type: 'Assignment',
          departmentId: 'd1111111-1111-1111-1111-111111111111',
          priority: 'HIGH',
          checklistItems: ['Unit tests', 'E2E tests'],
        },
      ],
    });

    confirmReq.flush({
      totalCreated: 1,
      items: [
        {
          id: 't1111111-1111-1111-1111-111111111111',
          type: 'Assignment',
          title: 'Thực hiện kiểm thử',
          status: 'Draft',
          departmentId: 'd1111111-1111-1111-1111-111111111111',
        },
      ],
    });

    expect(component.confirmBusy()).toBe(false);
    expect(component.successMessage()).toContain('1');
  });

  it('does not send confirmation if no items are selected', () => {
    const fixture = TestBed.createComponent(TaskDecompositionPreview);
    const component = fixture.componentInstance;
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne('/api/v1/departments').flush([]);

    component.proposals.set([
      {
        selected: false,
        title: 'Task A',
        description: '',
        type: 'Assignment',
        departmentId: null,
        departmentName: null,
        priority: 'MEDIUM',
        checklistItems: [],
      },
    ]);

    component.confirmDecomposition();

    http.expectNone('/api/v1/tasks/ai-decomposition/confirm');
    expect(component.confirmBusy()).toBe(false);
  });

  it('auto-initializes prompt and triggers preview when loaded with prompt query parameter', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SessionService, useValue: { hasPermission: () => true } },
        {
          provide: ActivatedRoute,
          useValue: {
            queryParams: of({ prompt: '1. Cài đặt database\n2. Cấu hình backup' }),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(TaskDecompositionPreview);
    const component = fixture.componentInstance;
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http.expectOne('/api/v1/departments').flush([]);

    expect(component.prompt()).toBe('1. Cài đặt database\n2. Cấu hình backup');
    const previewReq = http.expectOne('/api/v1/tasks/ai-decomposition/preview');
    expect(previewReq.request.method).toBe('POST');
    expect(previewReq.request.body).toEqual({ prompt: '1. Cài đặt database\n2. Cấu hình backup' });
    previewReq.flush({
      originalPrompt: '1. Cài đặt database\n2. Cấu hình backup',
      proposals: [
        { title: 'Cài đặt database', description: '', type: 'Assignment', priority: 'HIGH', checklistItems: [] }
      ]
    });

    expect(component.proposals().length).toBe(1);
    expect(component.proposals()[0].title).toBe('Cài đặt database');
  });
});