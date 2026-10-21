# View D: Sơ đồ Tuần tự các Luồng Nghiệp vụ và Kỹ thuật Trọng yếu (Key Sequence Diagrams)

- **Mã tài liệu:** `SSS-DIAG-04`
- **Phiên bản:** `1.2-DRAFT`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](../SSS_v1.0_DRAFT.md) — Mục 8, Mục 11, Mục 12, Mục 13 & Mục 15
- **Ngày lập:** `2026-10-18` (Cập nhật đồng bộ v1.2: `2026-10-21`)
- **Danh mục luồng (`SEQ-001` .. `SEQ-010`):**
  1. `SEQ-001`: Xác thực, Thiết lập Ngữ cảnh Tenant và Kiểm tra Quyền hạn (`Authentication & Tenant Context Resolution`)
  2. `SEQ-002`: Tiếp nhận Yêu cầu Nội bộ, Kiểm tra Form Động và Định tuyến Quy trình (`Request Intake, Validation & Routing`)
  3. `SEQ-003`: Hỗ trợ Xử lý bằng AI có Kiểm soát Con người (`AI-Assisted Request/Task Processing & Human-in-the-Loop`)
  4. `SEQ-004`: Giao việc Phòng ban, Nhân viên Nhận việc (`Claim`) và Xác nhận Hoàn thành (`Task Claim & Confirmation Lifecycle`)
  5. `SEQ-005`: Ngăn chặn Truy cập Trái phép Liên Tenant (`Cross-Tenant Resource Access Denial — Defense-in-Depth`)
  6. `SEQ-006`: Xử lý Lỗi Nhà cung cấp Bên ngoài và Chế độ Suy giảm An toàn (`External Provider Failure & Graceful Fallback`)
  7. `SEQ-007`: Đăng ký Doanh nghiệp, Xác minh Email và Khởi tạo Nguyên tử Primary Tenant kèm Rollback (`Company Registration, Email Verification & Atomic Provisioning Workflow`)
  8. `SEQ-008`: Đăng nhập Hợp nhất & Đăng nhập Không Mật khẩu qua Email Link kèm Bộ chọn Workspace (`Unified Login & Passwordless Email-Link Flow with Workspace Selector Modal`)
  9. `SEQ-009`: Trợ lý Robot Thông minh Nhúng UI & Quy trình Bóc tách Việc AI với Xác nhận Bắt buộc từ Con người (`Interactive Embedded Cute Robot Chatbot & AI Task Decomposition with Mandatory Human Confirmation Workflow`)
  10. `SEQ-010`: Vòng đời Cấu hình & Phát Thông báo Nhắc Hạn Đa Kênh Tự động (`Deadline Reminder Lifecycle, Web Push Delivery & Deduplicated Multi-Channel Dispatching`)

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

---

## 7. `SEQ-007`: Đăng ký Doanh nghiệp, Xác minh Email và Khởi tạo Nguyên tử Primary Tenant kèm Rollback

Luồng này đặc tả toàn diện chuỗi tương tác từ điểm vào công khai (`/register-company`, liên kết CTA từ `/login`), kiểm tra trùng lặp, gửi token xác minh email, xử lý đăng nhập khi chưa xác minh, và giao dịch nguyên tử cấp phát Workspace chính (`Primary Tenant` duy nhất theo `DEC-WS-001`, `ADR-SSS-007`) kèm điều kiện kiểm tra bất biến sau khởi tạo (`Post-Provisioning Invariant Guard`) và cơ chế phục hồi khi cấp quyền `COMPANY_ADMIN` thất bại.

```mermaid
sequenceDiagram
    autonumber
    actor Owner as Đại diện Doanh nghiệp (Initial Owner)
    participant SPA as Angular 22 SPA (MOD-FE-001)
    participant API as BizFlow.Api (Public & Middleware)
    participant OnbSvc as TenantOnboardingService (MOD-002)
    participant DB as PostgreSQL 18 (BizFlowDbContext)
    participant Outbox as EmailQueue / Outbox

    Note over Owner,SPA: Giai đoạn 1: Điểm vào Công khai & Gửi Hồ sơ Đăng ký
    Owner->>SPA: Truy cập /login -> Bấm liên kết CTA Register your company -> Chuyển đến /register-company
    Owner->>SPA: Nhập thông tin công ty (Code, Name, TenantKey, TimeZone) & Tài khoản đại diện (Email, FullName, Password)
    SPA->>API: POST /api/v1/auth/register-company (Idempotency-Key: UUID)
    API->>OnbSvc: RegisterCompanyAsync(RegisterCompanyDto)

    OnbSvc->>DB: Kiểm tra trùng lặp: Code, TenantKey hoặc Email (IgnoreQueryFilters)
    alt Trùng lặp với hồ sơ ACTIVE hoặc PENDING đã xác minh
        DB-->>OnbSvc: Tồn tại bản ghi trùng
        OnbSvc-->>API: ConflictException (409 Conflict: COMPANY.DUPLICATE_CODE_OR_KEY)
        API-->>SPA: 409 Conflict ProblemDetails
        SPA-->>Owner: Báo lỗi trùng thông tin, yêu cầu chọn mã khác
    else Trùng đơn cũ đã REJECTED hoặc EXPIRED (Re-registration) hoặc Hồ sơ Mới
        OnbSvc->>DB: Tạo Company (Status = Pending, EmailVerified = false)
        OnbSvc->>DB: Tạo EmailVerificationToken (TokenHash SHA-256, TTL = 24 giờ)
        OnbSvc->>Outbox: Lưu bản ghi gửi email xác minh vào EmailQueueItems (Transactional Outbox)
        DB-->>OnbSvc: Lưu thành công
        OnbSvc-->>API: 202 Accepted (RegistrationPendingDto)
        API-->>SPA: 202 Accepted (Please check your email to verify your registration)
        SPA-->>Owner: Hiển thị màn hình chờ xác minh email
    end

    Note over Owner,SPA: Giai đoạn 2: Tình huống Cố gắng Đăng nhập khi Chưa Xác minh Email
    Owner->>SPA: Cố gắng đăng nhập tại /login khi chưa bấm link xác minh
    SPA->>API: POST /api/v1/auth/login (Email, Password)
    API->>DB: Kiểm tra tài khoản & EmailVerified flag
    DB-->>API: UserAccount / Company tồn tại nhưng EmailVerified = false
    API-->>SPA: 403 Forbidden (AUTH.EMAIL_NOT_VERIFIED)
    SPA-->>Owner: Thông báo email chưa xác minh, cung cấp nút Resend verification email

    Note over Owner,DB: Giai đoạn 3: Xác minh Email & Khởi tạo Nguyên tử Primary Tenant
    Owner->>SPA: Nhấp liên kết xác minh trong Email (/verify-email?token=raw_token)
    SPA->>API: POST /api/v1/auth/verify-email (Token)
    API->>OnbSvc: VerifyEmailAndProvisionAsync(Token)
    OnbSvc->>DB: Kiểm tra token còn hạn (TTL <= 24h) & băm SHA-256 khớp
    DB-->>OnbSvc: Token hợp lệ

    Note over OnbSvc,DB: Khởi chạy Giao dịch ACID Duy nhất (All-or-Nothing Unit of Work)
    OnbSvc->>DB: BEGIN TRANSACTION (IsolationLevel.ReadCommitted)
    OnbSvc->>DB: (1) Cập nhật Company.Status = Active, EmailVerified = true
    OnbSvc->>DB: (2) Khởi tạo đúng 01 Primary Tenant (Unique CompanyId per DEC-WS-001, Status = Active)
    OnbSvc->>DB: (3) Khởi tạo TenantSettings & BusinessCalendar mặc định
    OnbSvc->>DB: (4) Khởi tạo UserAccount đại diện (TenantId = PrimaryTenant.Id, Status = Active)
    OnbSvc->>DB: (5) Gán vai trò hệ thống COMPANY_ADMIN qua UserRole (IsSystem = true) + Ghi AuditLog

    Note over OnbSvc,DB: Post-Provisioning Invariant Check (Kiểm tra điều kiện bất biến)
    alt Nếu bước 5 gán quyền COMPANY_ADMIN thất bại HOẶC Invariant Check vi phạm
        OnbSvc->>DB: ROLLBACK TRANSACTION (Hủy bỏ 100% mọi thay đổi)
        OnbSvc->>DB: Đánh dấu Company.Status = Pending (cho phép thử lại, không sinh workspace mồ côi)
        OnbSvc-->>API: ProvisioningException (500 ONBOARDING.PROVISIONING_ROLLED_BACK)
        API-->>SPA: 500 Internal Server Error kèm CorrelationId
        SPA-->>Owner: Thông báo lỗi khởi tạo hệ thống, dữ liệu an toàn, vui lòng thử lại
    else Mọi bước thành công và Invariant Check thỏa mãn (1 Primary Tenant + >=1 Company Admin)
        OnbSvc->>DB: COMMIT TRANSACTION
        OnbSvc->>Outbox: Thêm thông báo chào mừng và hướng dẫn vào EmailQueueItems
        OnbSvc-->>API: 200 OK (ProvisioningSuccessDto: TenantId, WorkspaceUrl, AccessToken)
        API-->>SPA: 200 OK kèm phiên đăng nhập tự động
        SPA-->>Owner: Chuyển hướng vào Không gian làm việc chính (/tasks) với vai trò COMPANY_ADMIN
    end
```

---

## 8. `SEQ-008`: Đăng nhập Hợp nhất & Đăng nhập Không Mật khẩu qua Email Link kèm Bộ chọn Workspace

Luồng này mô tả hai phương thức đăng nhập hợp nhất trên cùng một giao diện `/login` (`UI-01`):
1. **Phương thức 1:** Đăng nhập bằng ID bắt buộc 3 trường (`ID`, `Mật khẩu`, `Mã Workspace`).
2. **Phương thức 2:** Đăng nhập không mật khẩu qua email link, kiểm tra số lượng workspace và hiển thị `Workspace Selector Modal` (`IR-UI-005`) khi người dùng thuộc nhiều doanh nghiệp.

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng (Staff / Manager)
    participant SPA as Angular 22 SPA (MOD-FE-001)
    participant Modal as Workspace Selector Modal (IR-UI-005)
    participant API as BizFlow.Api (AuthController / EmailLinkAuthController)
    participant AuthSvc as AuthenticationService (MOD-001)
    participant DB as PostgreSQL 18 (BizFlowDbContext)
    participant SMTP as Transactional Email Service (IR-EXT-002)

    Note over User,API: Phương thức 1: Đăng nhập Định danh 3 Trường Bắt buộc (FR-AUTH-001, BR-025)
    User->>SPA: Nhập ID/Code, Password và Workspace Key tại /login
    SPA->>API: POST /api/v1/auth/login (Identifier, Password, WorkspaceKey)
    API->>AuthSvc: ValidateCredentialsAsync(Identifier, Password, WorkspaceKey)
    AuthSvc->>DB: Truy vấn User + PasswordCredential + Tenant (Status = Active) đồng thời cả 3 trường
    alt Thiếu hoặc sai bất kỳ trường nào trong 3 trường
        DB-->>AuthSvc: Không tìm thấy hoặc mật khẩu/mã sai
        AuthSvc-->>API: 401 Unauthorized (AUTH.INVALID_CREDENTIALS)
        API-->>SPA: 401 Unauthorized (Thông tin đăng nhập không hợp lệ)
        SPA-->>User: Hiển thị thông báo lỗi chung, bảo vệ chống dò quét
    else Cả 3 trường khớp chính xác
        DB-->>AuthSvc: Hợp lệ (UserAccount, Tenant, Roles)
        AuthSvc->>DB: Tạo UserSession & RefreshToken
        AuthSvc-->>API: JWT AccessToken + RefreshToken
        API-->>SPA: 200 OK (AuthSessionResponse)
        SPA-->>User: Đăng nhập thành công, chuyển hướng vào Workspace chính
    end

    Note over User,SMTP: Phương thức 2: Đăng nhập Không Mật khẩu qua Email Link (FR-AUTH-005, ADR-SSS-008)
    User->>SPA: Chọn tab "Đăng nhập qua Email" & chỉ nhập Email
    SPA->>API: POST /api/v1/auth/email-link/request (Email)
    API->>AuthSvc: GenerateEmailLoginLinkAsync(Email)
    AuthSvc->>DB: Tạo EmailLoginToken (HMAC-SHA256, TTL = 15m, Single-Use)
    AuthSvc->>SMTP: Gửi email chứa liên kết đăng nhập (/auth/verify-link?token=raw_token)
    SMTP-->>User: Nhận email có nút "Đăng nhập ngay vào BizFlow"
    API-->>SPA: 200 OK (Email sent)
    SPA-->>User: "Vui lòng kiểm tra hộp thư email của bạn"

    Note over User,API: Giai đoạn Xác thực Liên kết Email & Định tuyến Workspace
    User->>SPA: Nhấp vào liên kết trong email
    SPA->>API: POST /api/v1/auth/email-link/verify (Token)
    API->>AuthSvc: VerifyEmailLinkTokenAsync(Token)
    AuthSvc->>DB: Kiểm tra token hợp lệ, chưa sử dụng, còn hạn (<= 15m)
    AuthSvc->>DB: Truy vấn danh sách Tenant/Workspace mà Email thuộc về (Status = Active)

    alt Trường hợp 0 Workspace (Chưa từng tham gia hoặc đã bị xóa)
        DB-->>AuthSvc: Count == 0
        AuthSvc-->>API: 404 Not Found (AUTH.USER_NOT_FOUND)
        API-->>SPA: 404 Not Found kèm gợi ý
        SPA-->>User: "Email chưa thuộc doanh nghiệp nào" + Nút điều hướng sang /register-company
    else Trường hợp Đúng 1 Workspace
        DB-->>AuthSvc: Count == 1 (WorkspaceId, TenantKey)
        AuthSvc->>DB: Hủy token đã dùng + Tạo UserSession
        AuthSvc-->>API: 200 OK (JWT AccessToken + RefreshToken)
        API-->>SPA: 200 OK (AuthSessionResponse)
        SPA-->>User: Vào thẳng Workspace chính
    else Trường hợp >= 2 Workspaces (Thuộc nhiều doanh nghiệp)
        DB-->>AuthSvc: Count >= 2 (Danh sách WorkspaceInfo)
        AuthSvc->>DB: Tạo TemporarySessionToken (TTL = 5m, mục đích chọn workspace)
        AuthSvc-->>API: 200 OK (TemporaryToken, WorkspacesList)
        API-->>SPA: 200 OK (WorkspacesList)
        SPA->>Modal: Mở Workspace Selector Modal (IR-UI-005)
        User->>Modal: Chọn Workspace mong muốn làm việc
        Modal->>API: POST /api/v1/auth/email-link/select-workspace (TemporaryToken, SelectedWorkspaceId)
        API->>AuthSvc: CompleteWorkspaceSelectionAsync(...)
        AuthSvc->>DB: Xác thực TemporaryToken & cấp JWT AccessToken chính thức
        API-->>SPA: 200 OK (AuthSessionResponse)
        SPA-->>User: Đăng nhập thành công vào Workspace đã chọn
    end
```

---

## 9. `SEQ-009`: Trợ lý Robot Thông minh Nhúng UI & Quy trình Bóc tách Việc AI với Xác nhận Bắt buộc từ Con người

Luồng này mô tả vòng đời của widget robot trợ lý (`sessionStorage`), cơ chế vấn tin tự nhiên có kiểm soát quyền hạn (`AiContextReader`), và quy trình bóc tách đoạn văn bản thành việc cần làm với **xác nhận bắt buộc của con người** (`BR-028`).

```mermaid
sequenceDiagram
    autonumber
    actor User as Nhân viên / Quản trị viên
    participant Widget as Robot Widget UI (MOD-014 / MOD-FE-001)
    participant PreviewUI as Interactive Preview Checklist (IR-API-006)
    participant API as BizFlow.Api (AiAssistantController / TasksController)
    participant Reader as AiContextReader (MOD-012)
    participant OpenAI as OpenAI API (IR-EXT-001)
    participant Engine as TaskWorkflowEngine (MOD-005 / MOD-007)
    participant DB as PostgreSQL 18 (BizFlowDbContext)

    Note over User,Widget: Vòng đời Widget Robot: Mặc định Mở & Đóng theo Phiên (FR-AI-011, BR-029)
    User->>Widget: Sau khi đăng nhập, Widget tự động hiển thị mở chào mừng
    alt Người dùng muốn thu nhỏ
        User->>Widget: Nhấp nút [X] ở góc trên bên phải
        Widget->>Widget: sessionStorage.setItem('bizflow_robot_dismissed', 'true')
        Widget->>Widget: Thu nhỏ / Ẩn widget trong phiên làm việc hiện tại
        Note over User,Widget: Đăng xuất & đăng nhập lại hoặc mở tab mới -> Widget tự động mở lại
    end

    Note over User,OpenAI: Vấn tin Tiến độ & Báo cáo Tự nhiên Ràng buộc Quyền Hạn (FR-AI-012, BR-027)
    User->>Widget: Nhập câu hỏi: "Có công việc nào thuộc phòng IT của tôi sắp trễ hạn không?"
    Widget->>API: POST /api/v1/ai/assistant/query (Message, CurrentTenantId)
    API->>Reader: BuildScopedContextAsync(CurrentUserId, CurrentTenantId, "tasks,deadlines")
    Reader->>DB: Truy vấn WorkTasks lọc nghiêm ngặt theo TenantId, RBAC và ManagementScope (BR-003)
    DB-->>Reader: Chỉ trả về các bản ghi thuộc quyền quản lý của người dùng
    Reader-->>API: ScopedContextJson (Không chứa bất kỳ dữ liệu trái phép nào)
    API->>OpenAI: POST /v1/chat/completions (SystemPrompt + ScopedContext + Query)
    Note over OpenAI: Anti-Hallucination Guardrail (BR-027): Chỉ trả lời dựa trên bằng chứng ngữ cảnh
    OpenAI-->>API: Phản hồi tổng hợp tiến độ và hạn chót
    API->>DB: Lưu lịch sử hội thoại (RobotConversation, RobotMessage per DR-016)
    API-->>Widget: 200 OK (AnswerText, RelevantRecordLinks)
    Widget-->>User: Hiển thị câu trả lời thông minh kèm liên kết tới công việc

    Note over User,DB: Quy trình Bóc tách Việc AI & Xác nhận Bắt buộc từ Con người (FR-AI-013, BR-028)
    User->>Widget: Gõ lệnh: /task Chuẩn bị tài liệu kỹ thuật, họp phòng IT và kiểm thử hệ thống trước thứ 6
    Widget->>API: POST /api/v1/ai/tasks/decompose (ParagraphText)
    API->>OpenAI: Prompt bóc tách cấu trúc (Type, Title, TargetDept, Description, Checklist)
    OpenAI-->>API: Danh sách 3 đầu việc đề xuất (JSON có cấu trúc)
    Note over API: TUYỆT ĐỐI CHƯA GHI VÀO DB (BR-028: AI Non-Autonomy)
    API-->>Widget: 200 OK (ProposedTasksList)
    Widget->>PreviewUI: Mở Bảng Xem Trước Danh Sách Việc Tương Tác
    PreviewUI-->>User: Hiển thị 3 việc kèm Checkbox, Tiêu đề chỉnh sửa được, Phòng ban nhận việc

    User->>PreviewUI: Bỏ chọn 1 việc không cần thiết (chỉ giữ 2 việc)
    User->>PreviewUI: Sửa lại hạn chót và tiêu đề của việc số 1
    User->>PreviewUI: Nhấp nút "Xác nhận tạo việc" (Confirm Create Tasks)

    PreviewUI->>API: POST /api/v1/tasks/ai-decomposition/confirm (ConfirmedTasksList)
    API->>API: Xác thực quyền tạo việc (tasks.create / requests.create) trên từng phòng ban đích
    API->>DB: BEGIN TRANSACTION
    loop Với từng việc được xác nhận
        API->>Engine: CreateTaskAsync(TaskPayload)
        Engine->>DB: Thêm WorkTask, TaskAssignment, Checklist (Status = OPEN)
        API->>DB: Ghi AuditLog (Action = "AI_DECOMPOSED_TASK_CONFIRMED")
    end
    API->>DB: COMMIT TRANSACTION
    API-->>PreviewUI: 201 Created (CreatedTasksResult)
    PreviewUI-->>User: Thông báo "Đã tạo thành công 02 công việc mới!", cập nhật danh sách công việc
```

---

## 10. `SEQ-010`: Vòng đời Cấu hình & Phát Thông báo Nhắc Hạn Đa Kênh Tự động

Luồng này mô tả việc người dùng cấu hình mốc nhắc hạn (`FR-NOTIF-003`), tiến trình nền quét lịch nhắc, cơ chế điều phối đa kênh thông minh (`In-App SignalR` $\rightarrow$ `Web Push VAPID` $\rightarrow$ `Fallback Email`), bộ lọc chống trùng lặp (`OD-032`), và tự động hủy nhắc hạn khi công việc hoàn thành.

```mermaid
sequenceDiagram
    autonumber
    actor User as Nhân sự Nhận việc
    participant SPA as Angular 22 SPA (MOD-FE-001)
    participant API as BizFlow.Api (DeadlineReminderController)
    participant Worker as DeadlineReminderWorker (MOD-015 / MOD-BG-001)
    participant Hub as NotificationHub SignalR (INT-012)
    participant WebPush as W3C Browser Push Service (RFC 8292 VAPID)
    participant SMTP as Transactional Email Service (IR-EXT-002)
    participant DB as PostgreSQL 18 (BizFlowDbContext)

    Note over User,API: Giai đoạn 1: Cấu hình Mốc Nhắc Hạn Cá nhân hóa (FR-NOTIF-003, BR-030)
    User->>SPA: Cấu hình nhắc hạn qua /notifications hoặc giao diện Task (Trước 24h, 4h, 1h, Overdue)
    SPA->>API: POST /api/v1/notifications/reminders/config (TaskId, Triggers: [24h, 4h, 1h, Overdue])
    API->>DB: Lưu các mốc vào DeadlineReminderSchedule (Status = Pending, ScheduledAt UTC)
    API-->>SPA: 200 OK (ScheduleConfigured)

    Note over User,API: Đăng ký Web Push Notification (Một lần khi người dùng cho phép)
    User->>SPA: Cho phép nhận thông báo trình duyệt ("Allow Notifications")
    SPA->>WebPush: Đăng ký PushSubscription với Browser Push Manager
    WebPush-->>SPA: PushSubscription Object (Endpoint, P256DH Key, Auth Secret)
    SPA->>API: POST /api/v1/notifications/push-subscriptions (PushSubscriptionDto)
    API->>DB: Lưu vào PushSubscriptions (UserId, Endpoint, Keys)

    Note over Worker,SMTP: Giai đoạn 2: Quét Định kỳ & Điều Phối Đa Kênh Thông minh (FR-NOTIF-004, OD-032)
    Worker->>DB: Quét DeadlineReminderSchedule (ScheduledAt <= UtcNow AND Status = Pending)
    DB-->>Worker: Danh sách lịch nhắc đến hạn kích hoạt

    loop Với từng bản ghi nhắc hạn đến hạn
        Worker->>DB: Kiểm tra NotificationLog (UNIQUE: TenantId, TaskId, UserId, TriggerType, ScheduledAt)
        alt Đã có bản ghi gửi thành công trong NotificationLog (Chống trùng lặp tuyệt đối)
            Worker->>Worker: Bỏ qua (Deduplication Guard)
        else Chưa phát thông báo
            Note over Worker,Hub: Kênh 1: Kiểm tra Người dùng Trực tuyến trên In-App SignalR
            Worker->>Hub: IsUserConnected(UserId)
            alt Người dùng ĐANG ONLINE trên ứng dụng
                Hub-->>Worker: True (User is connected)
                Worker->>Hub: SendAsync("ReceiveDeadlineReminder", ReminderPayload)
                Hub-->>SPA: Toast thông báo nổi & cập nhật badge Notification Center
                Worker->>DB: Ghi NotificationLog (Channel = "InAppSignalR", Status = "Sent")
            else Người dùng OFFLINE (Không mở ứng dụng)
                Hub-->>Worker: False (User is offline)
                Note over Worker,WebPush: Kênh 2: Thử gửi Web Push Notification qua VAPID
                Worker->>DB: Lấy PushSubscription của UserId
                alt Có Web Push Subscription hợp lệ
                    Worker->>WebPush: Gửi VAPID Web Push Payload có mã hóa ECE
                    alt Gửi Web Push thành công
                        WebPush-->>User: Hiển thị Native Browser Notification trên màn hình
                        Worker->>DB: Ghi NotificationLog (Channel = "WebPush", Status = "Sent")
                    else Gửi Web Push lỗi (410 Gone / Network Error)
                        Note over Worker,SMTP: Kênh 3: Fallback Tự động sang Gửi Email HTML
                        Worker->>SMTP: Gửi Email nhắc hạn HTML chuyên nghiệp có nút CTA "Xem công việc"
                        SMTP-->>User: Nhận Email thông báo nhắc việc trong hộp thư
                        Worker->>DB: Ghi NotificationLog (Channel = "EmailFallback", Status = "Sent")
                    end
                else Không có Web Push Subscription
                    Note over Worker,SMTP: Kênh 3: Fallback Tự động sang Gửi Email HTML
                    Worker->>SMTP: Gửi Email nhắc hạn HTML có nút CTA "Xem công việc"
                    SMTP-->>User: Nhận Email thông báo nhắc việc trong hộp thư
                    Worker->>DB: Ghi NotificationLog (Channel = "EmailDirect", Status = "Sent")
                end
            end
            Worker->>DB: Cập nhật DeadlineReminderSchedule.Status = "Completed"
        end
    end

    Note over User,DB: Giai đoạn 3: Tự động Hủy Nhắc Hạn khi Công việc Hoàn tất (OD-032)
    User->>SPA: Nộp kết quả hoàn thành công việc (Submit Task Result)
    SPA->>API: POST /api/v1/tasks/{id}/submit-result
    API->>DB: Chuyển WorkTask sang trạng thái SUBMITTED
    API->>DB: Cập nhật DeadlineReminderSchedule SET Status = 'CANCELLED' WHERE TaskId = {id} AND Status = 'Pending'
    Note over DB: Toàn bộ các mốc nhắc hạn trong tương lai của công việc này bị hủy ngay lập tức
    API-->>SPA: 200 OK (Task submitted, pending reminders cancelled)
```


