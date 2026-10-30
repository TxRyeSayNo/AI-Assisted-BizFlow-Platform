import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DocumentSummarizerDialog } from './document-summarizer-dialog';
import { I18nService } from '../../core/i18n/i18n.service';

describe('DocumentSummarizerDialog - Master AI Document Suite', () => {
  let component: DocumentSummarizerDialog;
  let fixture: ComponentFixture<DocumentSummarizerDialog>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentSummarizerDialog],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        I18nService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentSummarizerDialog);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
  });

  it('renders modal closed by default and opens when isOpen is set to true', () => {
    expect(component.isOpen).toBe(false);
    expect(fixture.nativeElement.querySelector('.dialog-backdrop')).toBeNull();

    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.dialog-backdrop')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#summarizer-title')).not.toBeNull();
  });

  it('allows selecting summarization modes among all 4 operational types', () => {
    component.selectedMode = 'EXECUTIVE';
    expect(component.selectedMode).toBe('EXECUTIVE');

    component.selectedMode = 'ACTION_ITEMS';
    expect(component.selectedMode).toBe('ACTION_ITEMS');

    component.selectedMode = 'RISKS_COMPLIANCE';
    expect(component.selectedMode).toBe('RISKS_COMPLIANCE');

    component.selectedMode = 'COMPREHENSIVE';
    expect(component.selectedMode).toBe('COMPREHENSIVE');
  });

  it('summarizes raw document text and displays executive summary, key points, risks, and action items', () => {
    component.isOpen = true;
    component.rawText = 'Chỉ thị kiểm toán an ninh mạng: Phải vá lỗ hổng SSL trước ngày 20/11. Đội hạ tầng cấu hình tường lửa mới.';
    component.selectedMode = 'COMPREHENSIVE';
    fixture.detectChanges();

    component.summarize();
    expect(component.loading()).toBe(true);

    const req = http.expectOne('/api/v1/ai/documents/summarize');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      rawText: 'Chỉ thị kiểm toán an ninh mạng: Phải vá lỗ hổng SSL trước ngày 20/11. Đội hạ tầng cấu hình tường lửa mới.',
      attachmentId: null,
      mode: 'COMPREHENSIVE',
      language: 'vi',
    });

    req.flush({
      sourceTitle: 'Chỉ thị kiểm toán an ninh mạng',
      sourceType: 'DirectText',
      mode: 'COMPREHENSIVE',
      executiveSummary: 'Kế hoạch hành động khẩn cấp về an ninh mạng trong tháng 11.',
      keyPoints: ['Vá lỗ hổng SSL', 'Cấu hình tường lửa mới'],
      risksAndCompliance: ['Nguy cơ bị tấn công man-in-the-middle'],
      wordCount: 85,
      confidence: 0.96,
      tokensUsed: 220,
      actionItems: [
        {
          orderIndex: 1,
          title: 'Vá lỗ hổng chứng chỉ SSL',
          description: 'Cập nhật OpenSSL và gia hạn chứng chỉ Wildcard',
          suggestedDepartment: 'SecOps',
          suggestedPriority: 'CRITICAL',
          suggestedDurationDays: 2,
        },
        {
          orderIndex: 2,
          title: 'Cấu hình tường lửa thế hệ mới',
          description: 'Thiết lập rule chặn IP độc hại',
          suggestedDepartment: 'Network',
          suggestedPriority: 'HIGH',
          suggestedDurationDays: 3,
        },
      ],
      summarizedAt: '2026-10-31T00:00:00Z',
    });

    expect(component.loading()).toBe(false);
    expect(component.summaryResult()).not.toBeNull();
    expect(component.summaryResult()?.actionItems.length).toBe(2);
    expect(component.summaryResult()?.confidence).toBe(0.96);
  });

  it('selects action items and spawns them into database as DRAFT tasks', () => {
    component.isOpen = true;
    component.rawText = 'Văn bản dự án mẫu';
    component.summaryResult.set({
      sourceTitle: 'Hợp đồng dịch vụ',
      sourceType: 'Contract',
      mode: 'COMPREHENSIVE',
      executiveSummary: 'Tóm tắt điều khoản triển khai',
      keyPoints: ['Thời gian bàn giao'],
      risksAndCompliance: ['Phạt vi phạm tiến độ'],
      wordCount: 150,
      confidence: 0.95,
      tokensUsed: 300,
      actionItems: [
        {
          orderIndex: 1,
          title: 'Bàn giao tài liệu thiết kế kiến trúc',
          description: 'Bản vẽ CAD và sơ đồ mạng',
          suggestedDepartment: 'Architecture',
          suggestedPriority: 'HIGH',
          suggestedDurationDays: 5,
        },
        {
          orderIndex: 2,
          title: 'Cài đặt môi trường Staging',
          description: 'Triển khai Kubernetes trên AWS',
          suggestedDepartment: 'DevOps',
          suggestedPriority: 'MEDIUM',
          suggestedDurationDays: 3,
        },
      ],
      summarizedAt: '2026-10-31T00:00:00Z',
    });

    // Toggle selections
    expect(component.selectedActionIndexes.size).toBe(0);
    component.toggleSelectAll();
    expect(component.selectedActionIndexes.size).toBe(2);
    expect(component.allSelected()).toBe(true);

    // Create tasks from selected
    component.createTasksFromSelected();
    expect(component.creatingTasks()).toBe(true);

    const req = http.expectOne('/api/v1/ai/documents/create-tasks');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.sourceTitle).toBe('Hợp đồng dịch vụ');
    expect(req.request.body.items.length).toBe(2);

    req.flush({
      totalCreated: 2,
      createdTasks: [
        { taskId: 'task-01', title: 'Bàn giao tài liệu thiết kế kiến trúc', priority: 'HIGH', status: 'DRAFT' },
        { taskId: 'task-02', title: 'Cài đặt môi trường Staging', priority: 'MEDIUM', status: 'DRAFT' },
      ],
      createdAt: '2026-10-31T00:00:00Z',
    });

    expect(component.creatingTasks()).toBe(false);
    expect(component.createdTasksSuccess()).toBe(true);
    expect(component.createdTasksResult()?.totalCreated).toBe(2);
  });

  it('handles attachment-based summarization with fileName indicator', () => {
    component.isOpen = true;
    component.fileName = 'BienBanHopDong_Q4.pdf';
    component.attachmentId = 'att-uuid-777';
    fixture.detectChanges();

    component.summarize();

    const req = http.expectOne('/api/v1/ai/documents/summarize');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.attachmentId).toBe('att-uuid-777');
    expect(req.request.body.rawText).toBeNull();

    req.flush({
      sourceTitle: 'BienBanHopDong_Q4.pdf',
      sourceType: 'Attachment',
      mode: 'COMPREHENSIVE',
      executiveSummary: 'Tóm tắt nội dung file đính kèm',
      keyPoints: ['Điều khoản thanh toán đợt 1'],
      risksAndCompliance: [],
      wordCount: 200,
      confidence: 0.98,
      actionItems: [],
      summarizedAt: '2026-10-31T00:00:00Z',
    });

    expect(component.summaryResult()?.sourceTitle).toBe('BienBanHopDong_Q4.pdf');
  });

  it('emits close cleanly on closeDialog or backdrop click', () => {
    component.isOpen = true;
    let closed = false;
    component.close.subscribe(() => (closed = true));

    component.closeDialog();
    expect(closed).toBe(true);

    closed = false;
    const mockBackdropEvent = {
      target: {
        classList: {
          contains: (c: string) => c === 'dialog-backdrop',
        },
      },
    } as unknown as MouseEvent;

    component.onBackdropClick(mockBackdropEvent);
    expect(closed).toBe(true);
  });
});

