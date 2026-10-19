# View D: Sơ đồ Tuần tự các Luồng Nghiệp vụ và Kỹ thuật Trọng yếu (Key Sequence Diagrams)

- **Mã tài liệu:** `SSS-DIAG-04`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](../SSS_v1.0_DRAFT.md) — Mục 8, Mục 11, Mục 12, Mục 13 & Mục 15
- **Danh mục luồng (`SEQ-001` .. `SEQ-006`):**
  1. `SEQ-001`: Xác thực, Thiết lập Ngữ cảnh Tenant và Kiểm tra Quyền hạn (`Authentication & Tenant Context Resolution`)
  2. `SEQ-002`: Tiếp nhận Yêu cầu Nội bộ, Kiểm tra Form Động và Định tuyến Quy trình (`Request Intake, Validation & Routing`)
  3. `SEQ-003`: Hỗ trợ Xử lý bằng AI có Kiểm soát Con người (`AI-Assisted Request/Task Processing & Human-in-the-Loop`)
  4. `SEQ-004`: Giao việc Phòng ban, Nhân viên Nhận việc (`Claim`) và Xác nhận Hoàn thành (`Task Claim & Confirmation Lifecycle`)
  5. `SEQ-005`: Ngăn chặn Truy cập Trái phép Liên Tenant (`Cross-Tenant Resource Access Denial — Defense-in-Depth`)
  6. `SEQ-006`: Xử lý Lỗi Nhà cung cấp Bên ngoài và Chế độ Suy giảm An toàn (`External Provider Failure & Graceful Fallback`)

---

## 1. `SEQ-001`: Xác thực, Thiết lập Ngữ cảnh Tenant và Kiểm tra Quyền hạn

Luồng này mô tả cách người dùng đăng nhập, nhận JWT kèm `tenant_id` và `workspace_id`, và cách mọi request tiếp theo được kiểm tra qua chuỗi phòng vệ nhiều lớp (`sessionGuard` $\rightarrow$ `tenantGuard` $\rightarrow$ `JwtBearer` $\rightarrow$ `ClaimsTenantContext` $\rightarrow$ `TenantMembershipAuthorizer`).

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng (Staff / Admin)
    participant SPA as Angular 22 SPA (MOD-FE-001)
    participant API as BizFlow.Api (Auth & Middleware)
    participant AuthSvc as AuthService (MOD-001)
    participant MemAuth as TenantMembershipAuthorizer (MOD-002/003)
    participant DB as PostgreSQL 18 (BizFlowDbContext)

    User->>SPA: Nhập Email & Mật khẩu tại /login
    SPA->>API: POST /api/auth/login (Email, Password)
    API->>AuthSvc: LoginAsync(LoginRequest)
    AuthSvc->>DB: Truy vấn User, PasswordCredential, Tenant, Workspace, UserRoles (IgnoreQueryFilters cho bước Login)
    DB-->>AuthSvc: Trả về thông tin User & Tenant (Status = Active)
    AuthSvc->>DB: Tạo UserSession & RefreshToken + Ghi SecurityEvent (LoginSuccess)
    AuthSvc-->>API: AccessToken (JWT chứa sub, tenant_id, workspace_id, session_id) + RefreshToken
    API-->>SPA: 200 OK (AuthSessionResponse)
    SPA->>SPA: Lưu trạng thái phiên; kích hoạt sessionGuard, tenantGuard, permissionGuard

    Note over User,DB: Request nghiệp vụ tiếp theo có đính kèm Bearer JWT

    User->>SPA: Mở danh sách công việc (/tasks)
    SPA->>API: GET /api/tasks (Authorization: Bearer <JWT>)
    API->>API: Xác thực chữ ký JWT & trích xuất ClaimsTenantContext (CurrentUserId, CurrentTenantId)
    API->>MemAuth: AuthorizeRequestAsync(HttpContext)
    MemAuth->>DB: Kiểm tra Tenant Active, User Active, Session chưa bị thu hồi & Permission hợp lệ
    DB-->>MemAuth: Hợp lệ (Authorized)
    MemAuth-->>API: Cho phép đi tiếp vào TasksController
    API->>DB: Truy vấn WorkTasks (Tự động áp dụng HasQueryFilter: x.TenantId == CurrentTenantId)
    DB-->>API: Danh sách WorkTask trong phạm vi quyền (Scoped Read per ADR-0011)
    API-->>SPA: 200 OK (PagedResult<TaskSummaryDto>)
```

---

## 2. `SEQ-002`: Tiếp nhận Yêu cầu Nội bộ, Kiểm tra Form Động và Định tuyến Quy trình

Luồng này minh họa quá trình nhân viên tạo và gửi một yêu cầu dịch vụ nội bộ (ví dụ: Yêu cầu cấp quyền GitLab), bao gồm cả phần đã triển khai (`RequestRecord`, `RequestFieldValue`, `WorkTask` liên kết) và phần mục tiêu (`RoutingRule`, `ApprovalInstance`, `SLAState`).

```mermaid
sequenceDiagram
    autonumber
    actor Requester as Nhân viên gửi yêu cầu (Requester)
    participant SPA as Angular 22 SPA (/requests/new)
    participant ReqCtrl as RequestsController (BizFlow.Api)
    participant ReqSvc as RequestService (MOD-006)
    participant SvcCat as ServiceCatalog (MOD-004)
    participant WfEng as Workflow & Approval (MOD-007)
    participant SlaSvc as SLA Engine (MOD-008)
    participant DB as PostgreSQL 18 (BizFlowDbContext)
    participant Notif as NotificationService & Hub (MOD-009)

    Requester->>SPA: Chọn loại dịch vụ (VD: Cấp quyền GitLab)
    SPA->>ReqCtrl: GET /api/services/{serviceTypeId}/form
    ReqCtrl->>SvcCat: Lấy FormDefinition & FormFields đang Active
    SvcCat-->>SPA: Schema các trường dữ liệu động (Repository, Role, Lý do...)

    Requester->>SPA: Điền thông tin form (hoặc dùng AI Intake) & bấm Submit
    SPA->>ReqCtrl: POST /api/requests (Create & Submit Request)
    ReqCtrl->>ReqSvc: CreateAndSubmitRequestAsync(dto)
    ReqSvc->>SvcCat: Kiểm tra tính hợp lệ của các trường bắt buộc theo FormDefinition
    ReqSvc->>DB: Lưu RequestRecord (Status = Submitted) + RequestFieldValues + RequestHistory

    rect rgb(235, 245, 255)
        Note over ReqSvc,SlaSvc: Nhánh điều phối tự động (Target Architecture vs Current Partial Implementation)
        ReqSvc->>WfEng: Đánh giá RoutingRule & khởi tạo WorkflowInstance / ApprovalInstance (Target)
        WfEng->>DB: Tạo WorkTask thực thi / bước phê duyệt liên kết qua RequestTaskLink
        ReqSvc->>SlaSvc: Tính ResponseDueAt & ResolutionDueAt theo SlaProfile + WorkingHours + Holidays
        SlaSvc->>DB: Khởi tạo mốc theo dõi SLA (Target: SLAState & SLAEvent)
    end

    ReqSvc->>DB: Ghi AuditLog (Action = request.submitted) trong cùng Transaction
    ReqSvc->>Notif: Xếp hàng NotificationQueueItem & đẩy thông báo thời gian thực qua SignalR
    Notif-->>SPA: Sự kiện SignalR (RequestSubmitted / TaskAssigned)
    ReqCtrl-->>SPA: 201 Created (RequestDetailDto)
```

---

## 3. `SEQ-003`: Hỗ trợ Xử lý bằng AI có Kiểm soát Con người (`ADR-0008`)

Luồng này thể hiện kiến trúc tích hợp AI an toàn: mọi dữ liệu đưa vào prompt đều phải đi qua `AiContextReader` để kiểm tra quyền đọc của người dùng hiện tại, phản hồi được chuẩn hóa thành đề xuất (`AiSuggestion`), ghi nhật ký đầy đủ vào `AiExecutionLogs`, và **bắt buộc người dùng xác nhận** trước khi thay đổi dữ liệu nghiệp vụ.

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng (Requester / Assignee)
    participant SPA as Angular 22 SPA
    participant AiCtrl as AiAssistantController (BizFlow.Api)
    participant AiSvc as AiAssistantService (MOD-012)
    participant CtxReader as AiContextReader (MOD-012)
    participant OpenAIClient as OpenAiModelClient (Infrastructure)
    participant OpenAI as OpenAI API (External)
    participant Dispatcher as AiActionDispatcher (MOD-012)
    participant DB as PostgreSQL 18

    User->>SPA: Bấm "Phân tích / Gợi ý bằng AI" trên Request hoặc Task
    SPA->>AiCtrl: POST /api/ai/assist (FeatureType, TargetEntityId, UserInput)
    AiCtrl->>AiSvc: ExecuteAssistAsync(request)
    AiSvc->>CtxReader: LoadAuthorizedContextAsync(CurrentUserId, CurrentTenantId, TargetEntityId)
    CtxReader->>DB: Truy vấn Request/Task/ServiceCatalog với HasQueryFilter & kiểm tra quyền đọc (ADR-0011)
    alt Người dùng không có quyền đọc bản ghi mục tiêu
        DB-->>CtxReader: Null / Forbidden
        CtxReader-->>AiCtrl: Trả về 404 Not Found (ADR-0012 - Không tiết lộ dữ liệu)
    else Người dùng có quyền hợp lệ
        DB-->>CtxReader: Ngữ cảnh đã lọc bỏ thông tin nhạy cảm ngoài phạm vi
        CtxReader-->>AiSvc: AuthorizedAiContext
        AiSvc->>OpenAIClient: GenerateStructuredSuggestionAsync(prompt, toolDefinitions, timeout=45s)
        OpenAIClient->>OpenAI: POST /v1/responses (TLS 1.2+, JSON Schema)
        OpenAI-->>OpenAIClient: Structured JSON Output (Classification, MissingFields, Summary, SuggestedActions)
        OpenAIClient-->>AiSvc: AiModelResult
        AiSvc->>DB: Lưu AiSuggestion + AiExecutionLog (Model, LatencyMs, TokenUsage, Status=Succeeded)
        AiSvc-->>AiCtrl: AiAssistResponseDto (Chỉ là đề xuất, chưa tự động sửa Request/Task)
        AiCtrl-->>SPA: 200 OK (Hiển thị gợi ý kèm nhãn "Đề xuất bởi AI - Cần bạn xác nhận")

        User->>SPA: Xem xét, chỉnh sửa (nếu cần) và bấm "Áp dụng Đề xuất"
        SPA->>AiCtrl: POST /api/ai/suggestions/{suggestionId}/apply
        AiCtrl->>Dispatcher: ApplyConfirmedSuggestionAsync(suggestionId, userOverrides)
        Dispatcher->>DB: Cập nhật trạng thái AiSuggestion (Accepted) & gọi Use Case nghiệp vụ tương ứng kèm AuditLog
        Dispatcher-->>SPA: 200 OK (Đã áp dụng dưới quyền của User)
    end
```

---

## 4. `SEQ-004`: Giao việc Phòng ban, Nhân viên Nhận việc (`Claim`) và Xác nhận Hoàn thành (`ADR-0001..0004`)

Luồng này mô tả chính xác các quy tắc bất biến của `TaskWorkflowEngine` đã được triển khai và kiểm thử trong codebase: giao việc cho phòng ban (`Department Pool`), nhân viên nhận việc (`Claim`), hoàn thành công việc với bằng chứng (`CompletionEvidence`), và bước xác nhận hoàn thành (`Confirm` / `Reject`).

```mermaid
sequenceDiagram
    autonumber
    actor Manager as Trưởng phòng / Người giao việc (Manager)
    actor Staff as Nhân viên phòng ban (Assignee)
    participant API as TasksController (BizFlow.Api)
    participant WfEng as TaskWorkflowEngine (MOD-007)
    participant TaskSvc as TaskService (MOD-005)
    participant Storage as IObjectStorageProvider (MinIO/Local)
    participant DB as PostgreSQL 18 (BizFlowDbContext)

    Manager->>API: POST /api/tasks/{id}/assign (OwnerDepartmentId = DeptA, AssigneeUserId = null)
    API->>WfEng: AssignTaskAsync(taskId, deptA, null)
    WfEng->>DB: Tạo TaskAssignment (Department Pool) + WorkTask.MarkWorkflowMutation() (Status = Assigned)
    DB-->>API: 200 OK (Task ở hàng đợi phòng ban DeptA)

    Staff->>API: POST /api/tasks/{id}/claim (Nhân viên thuộc DeptA nhận việc)
    API->>WfEng: ClaimTaskAsync(taskId, staffUserId)
    WfEng->>DB: Kiểm tra Staff thuộc DeptA & chưa có Active User Assignment (ADR-0002)
    WfEng->>DB: Ghi nhận TaskAssignment cho Staff + TaskHistory (Action = Claimed)
    DB-->>API: 200 OK (Staff trở thành người xử lý chính)

    Staff->>API: POST /api/tasks/{id}/start
    API->>WfEng: TransitionTaskAsync(taskId, InProgress)
    WfEng->>DB: Cập nhật WorkTask.Status = InProgress (qua MarkWorkflowMutation)

    Staff->>API: POST /api/tasks/{id}/evidence (Upload file minh chứng hoàn thành)
    API->>Storage: UploadAsync(tenantId/tasks/{id}/evidence.pdf)
    Storage-->>API: ObjectKey & Checksum
    API->>TaskSvc: RecordCompletionEvidenceAsync(taskId, metadata)
    TaskSvc->>DB: Lưu CompletionEvidence

    Staff->>API: POST /api/tasks/{id}/submit-for-confirmation (Khi RequiresConfirmation = true per ADR-0004)
    API->>WfEng: SubmitForConfirmationAsync(taskId)
    WfEng->>DB: Chuyển WorkTask.Status = PendingConfirmation + Tạo bản ghi Confirmation (Status = Pending)
    DB-->>API: 200 OK (Chờ Manager xác nhận)

    Manager->>API: POST /api/tasks/{id}/confirm (Approve = true, Comment = "Đã kiểm tra quyền GitLab")
    API->>WfEng: ConfirmTaskCompletionAsync(taskId, managerUserId)
    WfEng->>DB: Cập nhật Confirmation (Approved) + WorkTask.Status = Completed + CompletedAt = UtcNow
    Note over WfEng,DB: SaveChangesAsync kiểm tra ValidateConfirmationInvariants & ValidateTaskWorkflowMutations
    DB-->>API: 200 OK (Hoàn tất chu trình Task)
```

---

## 5. `SEQ-005`: Ngăn chặn Truy cập Trái phép Liên Tenant (`Defense-in-Depth` & `ADR-0012`)

Luồng này minh họa cách hệ thống bảo vệ cô lập dữ liệu giữa các doanh nghiệp (`Tenant A` và `Tenant B`) ở cả 4 lớp bảo vệ, đảm bảo trả về `404 Not Found` không rò rỉ thông tin tồn tại của bản ghi (`ADR-0012`).

```mermaid
sequenceDiagram
    autonumber
    actor Attacker as Người dùng thuộc Tenant A
    participant API as BizFlow.Api
    participant MemAuth as TenantMembershipAuthorizer
    participant AppSvc as TaskService / RequestService
    participant DbCtx as BizFlowDbContext (EF Core)
    participant DB as PostgreSQL 18

    Note over Attacker,DB: Kịch bản 1: Giả mạo Header X-Tenant-Id của Tenant B trong khi cầm JWT của Tenant A
    Attacker->>API: GET /api/tasks/{taskOfTenantB} (JWT: tenant_id = TenantA, Header: X-Tenant-Id = TenantB)
    API->>MemAuth: Kiểm tra tính nhất quán giữa JWT Claim và Tenant Context
    MemAuth-->>API: Phát hiện xung đột Tenant Context / Không phải thành viên Tenant B
    API->>DbCtx: Ghi SecurityEvent (CrossTenantAccessAttempt)
    API-->>Attacker: 403 Forbidden (TENANT_ACCESS_DENIED)

    Note over Attacker,DB: Kịch bản 2: Dùng đúng JWT của Tenant A nhưng đoán UUID bản ghi thuộc Tenant B
    Attacker->>API: GET /api/tasks/{uuidOfTenantBTask} (JWT: tenant_id = TenantA)
    API->>MemAuth: Xác nhận thành viên hợp lệ của Tenant A
    MemAuth->>AppSvc: GetTaskByIdAsync(uuidOfTenantBTask)
    AppSvc->>DbCtx: WorkTasks.FirstOrDefaultAsync(t => t.Id == uuidOfTenantBTask)
    DbCtx->>DB: SELECT ... FROM work_tasks WHERE id = @id AND tenant_id = @TenantA (Do HasQueryFilter tự động gắn)
    DB-->>DbCtx: 0 rows returned (Null)
    DbCtx-->>AppSvc: Null
    AppSvc-->>API: Ném NotFoundException (Không tiết lộ bản ghi có tồn tại ở Tenant B hay không per ADR-0012)
    API-->>Attacker: 404 Not Found (ProblemDetails chuẩn)
```

---

## 6. `SEQ-006`: Xử lý Lỗi Nhà cung cấp Bên ngoài và Chế độ Suy giảm An toàn (`Graceful Degradation`)

Luồng này mô tả cách hệ thống duy trì hoạt động của các quy trình nghiệp vụ cốt lõi (`Request`, `Task`, `Approval`) ngay cả khi **OpenAI API**, **Redis** hoặc **SMTP** gặp sự cố hoặc hết thời gian chờ (`Timeout`).

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng (Staff)
    participant SPA as Angular 22 SPA
    participant API as BizFlow.Api
    participant AiSvc as AiAssistantService (MOD-012)
    participant OpenAIClient as OpenAiModelClient
    participant OpenAI as OpenAI API
    participant DB as PostgreSQL 18

    User->>SPA: Gửi yêu cầu gợi ý phân loại Request bằng AI
    SPA->>API: POST /api/ai/assist
    API->>AiSvc: ExecuteAssistAsync(request)
    AiSvc->>OpenAIClient: CallOpenAiAsync(payload, timeout = 45s)
    OpenAIClient->>OpenAI: POST /v1/responses
    Note over OpenAIClient,OpenAI: OpenAI bị quá tải (HTTP 429/503) hoặc Timeout > 45 giây
    OpenAI--xOpenAIClient: Timeout / HttpRequestException / 503 Service Unavailable
    OpenAIClient-->>AiSvc: Trả về kết quả lỗi có kiểm soát (AiProviderFailure)

    AiSvc->>DB: Ghi AiExecutionLog (Status = Failed / Fallback, ErrorCode, LatencyMs)
    AiSvc->>AiSvc: Kích hoạt Deterministic Fallback (Gợi ý theo từ khóa/quy tắc cục bộ hoặc trả về cảnh báo rõ ràng)
    AiSvc-->>API: 200 OK (AiAssistResponseDto với IsFallback=true) hoặc 503 ProblemDetails thân thiện
    API-->>SPA: Hiển thị thông báo: "AI tạm thời gián đoạn — Bạn vẫn có thể điền và gửi Request thủ công"

    User->>SPA: Điền thủ công các trường bắt buộc và bấm Submit Request
    SPA->>API: POST /api/requests
    API->>DB: Lưu RequestRecord & khởi tạo quy trình bình thường (Không phụ thuộc vào trạng thái của OpenAI)
    DB-->>API: 201 Created
    API-->>SPA: Gửi yêu cầu thành công
```
