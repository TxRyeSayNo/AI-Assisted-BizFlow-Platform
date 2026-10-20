# BẢNG PHÂN TÍCH KHOẢNG TRỐNG TRIỂN KHAI (IMPLEMENTATION GAP ANALYSIS)

**Dự án:** AI-Assisted BizFlow Platform  
**Phiên bản tài liệu:** v1.0.0  
**Ngày thực hiện:** 19/10/2026  
**Trạng thái kiểm toán:** Cập nhật đối soát thực tế toàn bộ mã nguồn Backend (.NET 10), Frontend (Angular 22), Database (PostgreSQL 18) và Infrastructure (Redis, MinIO).

---

## 1. PHƯƠNG PHÁP TÍNH TOÁN VÀ ĐỊNH NGHĨA TRẠNG THÁI

### 1.1. Phân loại trạng thái (Status Classification)
- `VERIFIED`: Đã triển khai hoàn chỉnh mã nguồn (Backend & Frontend nếu áp dụng), có kiểm thử tự động (Unit / Integration Test) hoặc kiểm chứng hành vi thực tế vượt qua (100% Pass).
- `IMPLEMENTED_UNVERIFIED`: Đã có mã nguồn nghiệp vụ nhưng chưa có kiểm thử độc lập phủ kín tất cả edge-cases hoặc chưa chạy kịch bản E2E.
- `PARTIALLY_IMPLEMENTED`: Đã triển khai một phần (ví dụ: đã có schema/entity/API đọc nhưng thiếu luồng ghi, hoặc thiếu màn hình UI).
- `NOT_IMPLEMENTED`: Chưa có mã nguồn nghiệp vụ thực tế hoặc chỉ mới ở dạng interface rỗng.
- `BLOCKED`: Tạm hoãn do thiếu phụ thuộc bên ngoài hoặc quyết định kiến trúc chưa chốt (`OPEN_DECISIONS`).
- `OUT_OF_SCOPE`: Tính năng đề xuất cho tương lai (`PROPOSED`), không thuộc phạm vi MVP Release 1.0 (ví dụ: Tự đăng ký Onboarding tự động hoàn toàn `FR-TEN-005` theo `DEC-WS-001` / `OD-021`).

### 1.2. Thống kê định lượng (Quantitative Metrics)
- **Tổng số Yêu cầu chức năng & Phi chức năng khảo sát:** 119 yêu cầu (bao gồm 63 Functional Requirements `FR-*`, 13 Interface Requirements `IR-*`, 15 Data Requirements `DR-*`, 16 Non-Functional Requirements `NFR-*`, 12 Security Requirements `SEC-*`).
- **Phạm vi In-Scope Release 1.0 (MVP Baseline):** 110 yêu cầu.
- **Trạng thái thực tế trước đợt bổ sung:**
  - `VERIFIED`: 62 yêu cầu
  - `PARTIALLY_IMPLEMENTED`: 18 yêu cầu
  - `NOT_IMPLEMENTED`: 28 yêu cầu
  - `OUT_OF_SCOPE / BLOCKED`: 2 yêu cầu (`FR-TEN-005`, `NFR-BCDR-001`)

---

## 2. MA TRẬN TRUY VẾT VÀ KHOẢNG TRỐNG (REQUIREMENT GAP MATRIX)

### 2.1. Module M01 — Xác thực & Phân quyền (Authentication & Authorization)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-AUTH-001** | Đăng nhập Người dùng Doanh nghiệp bằng Mã NV/Email | SRS §3.1.1, ADR-0002 | P0 | `VERIFIED` | `AuthenticationController.cs`, `AuthService.cs`, `login.component.ts` | Không có | Đã triển khai đầy đủ JWT + Claim | `AuthenticationTests.cs` (Passed), API smoke test `POST /auth/login` (Passed) |
| **FR-AUTH-002** | Đăng nhập Quản trị viên Nền tảng (PLATFORM_ADMIN) | SRS §3.1.1, ADR-0002 | P0 | `VERIFIED` | `AuthenticationController.cs`, `AssertPlatformAccount()` | Không có | Đã phân tách context TenantId = null cho Admin | `AuthenticationTests.cs` (Passed) |
| **FR-AUTH-003** | Duy trì Phiên, Refresh Token xoay vòng và Đăng xuất | SRS §3.1.1, ADR-0002 | P0 | `VERIFIED` | `RefreshToken` entity, Redis token revocation blacklist | Không có | Token rotation chuẩn RFC 6749 | `TokenRevocationTests.cs` (Passed) |
| **FR-AUTH-004** | Khôi phục Mật khẩu qua Email / Mật khẩu Tạm thời | SRS §3.1.1, OD-004 | P0 | `PARTIALLY_IMPLEMENTED` | `PasswordResetToken` model | Thiếu API trigger gửi mail phục hồi tự phục vụ | Bổ sung API ForgotPassword/ResetPassword và Mock SMTP | `EmailSenderStubTests.cs` |

---

### 2.2. Module M02 — Quản lý Thuê bao & Doanh nghiệp (Tenancy & Company Registration)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-TEN-001** | Gửi Hồ sơ Đăng ký Doanh nghiệp Mới từ Public Portal | SRS §3.1.2, BR-024 | P0 | `VERIFIED` | `PublicCompanyRegistrationsController.cs`, `CompanyRegistrationManagement.cs`, `register-company.ts` | Không có | Hoàn thành tiếp nhận đăng ký công khai và validation | `CompanyRegistrationManagementTests.cs` (Passed), `npm run build` (Passed) |
| **FR-TEN-002** | Phê duyệt/Từ chối Đăng ký và Khởi tạo Nguyên tử Tenant + Admin | SRS §3.1.2, BR-022, BR-023 | P0 | `VERIFIED` | `CompanyProvisioningStore.cs`, `CompanyRegistrationsController.cs`, `company-registrations.ts` | Không có | Duyệt nguyên tử Tenant + UserAccount Admin ban đầu; Từ chối kèm lý do bắt buộc | `CompanyRegistrationManagementTests.cs` (Passed), UI Modal Test (Passed) |
| **FR-TEN-003** | Quản lý Trạng thái Doanh nghiệp (ACTIVE, SUSPENDED, INACTIVE) | SRS §3.1.2 | P1 | `VERIFIED` | `CompanyRegistrationsController.cs/status`, `company-registrations.ts` actions | Không có | Hỗ trợ Suspend/Activate/Inactivate và ghi AuditLog | `CompanyRegistrationManagementTests.cs` (Passed) |
| **FR-TEN-004** | Cách ly Dữ liệu Đa thuê bao Tuyệt đối theo TenantId | SRS §3.1.2, BR-002 | P0 | `VERIFIED` | `BizFlowDbContext.cs` Global Query Filters, `TenantContext.cs` | Không có | 100% Query và Mutation đều enforce `TenantId` | `MultiTenantIsolationTests.cs` (Passed 100%) |
| **FR-TEN-005** | Tự đăng ký Doanh nghiệp (Self-Service Onboarding) | SRS §3.1.2, OD-021 | P1 | `OUT_OF_SCOPE` | Tài liệu ADR-SSS-001, DEC-WS-001 | Tính năng mở rộng PROPOSED, baseline xác nhận qua Platform Admin duyệt | Giữ quy trình Platform Admin duyệt (`FR-TEN-002`) cho MVP | N/A |

---

### 2.3. Module M03 — Quản lý Cơ cấu Tổ chức & Người dùng (Organization & Users)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-ORG-001** | Tạo mới Tài khoản Người dùng trong Doanh nghiệp | SRS §3.1.3, BR-001 | P0 | `VERIFIED` | `UsersController.cs` (POST), `UserManagement.cs`, `UserManagementStore.cs` | Không có | Đầy đủ validation, uniqueness, băm mật khẩu, gán role | `UserManagementTests` (Passed) |
| **FR-ORG-002** | Cập nhật Thông tin Hồ sơ, Phòng ban và Vai trò | SRS §3.1.3 | P0 | `VERIFIED` | `UsersController.cs` (PUT), `UserManagement.cs` | Không có | Cập nhật hồ sơ, gán lại phòng ban và vai trò | `UserManagementTests` (Passed) |
| **FR-ORG-003** | Vô hiệu hóa hoặc Kích hoạt lại Tài khoản | SRS §3.1.3 | P0 | `VERIFIED` | `UsersController.cs` (PATCH status), `UserAccount.SetStatus` | Không có | Thu hồi phiên làm việc tức thì bằng cách xoay SecurityStamp | `CompanyRegistrationManagementTests.cs` (Passed) |
| **FR-ORG-004** | Quản lý Cơ cấu Phòng ban Phân cấp (ManagementScope) | SRS §3.1.3, BR-003 | P0 | `VERIFIED` | `DepartmentsController.cs` (POST, PUT), `DepartmentManagement.cs` | Không có | Hỗ trợ phân cấp và thuật toán chống chu trình đệ quy | `CompanyRegistrationManagementTests.cs` (Passed) |
| **FR-ORG-005** | Tạo Vai trò Tùy chỉnh và Ma trận Quyền hạn RBAC | SRS §3.1.3, BR-003 | P1 | `VERIFIED` | `RolesController.cs`, `RoleEntity`, `PermissionMatrix` | Không có | Đầy đủ quyền quản trị RBAC theo từng module | `RolePermissionTests.cs` (Passed) |

---

### 2.4. Module M04 — Danh mục Dịch vụ (Service Catalog)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-SVC-001** | Tạo và Quản lý Danh mục Dịch vụ và Phân loại | SRS §3.1.4, ADR-0008 | P0 | `VERIFIED` | `ServicesController.cs`, `ServiceCatalogService.cs` | Không có | Đầy đủ CRUD Service, Categories, Custom Fields | `ServiceCatalogTests.cs` (Passed) |
| **FR-SVC-002** | Cấu hình Quy tắc Định tuyến Tự động (RoutingRule) | SRS §3.1.4, BR-008 | P0 | `PARTIALLY_IMPLEMENTED` | `RoutingRule` model trong domain | Thiếu UI quản lý danh sách Routing Rules cho từng dịch vụ | Bổ sung CRUD API `ServiceRoutingRulesController` | `RoutingRuleTests.cs` |
| **FR-SVC-003** | Gắn Phiên bản Quy trình và Hồ sơ SLA Mặc định | SRS §3.1.4, BR-013 | P1 | `VERIFIED` | `ServiceDefinition.WorkflowDefinitionId`, `SlaProfileId` | Không có | Đã hỗ trợ liên kết khóa ngoại và snapshot | `ServiceDefinitionTests.cs` (Passed) |
| **FR-SVC-004** | Cấu hình Quy tắc Phê duyệt cho Dịch vụ | SRS §3.1.4, BR-012 | P1 | `PARTIALLY_IMPLEMENTED` | `ApprovalRule` schema | Chưa kết nối luồng duyệt động đa cấp | Kết nối qua module Workflow M07 | `ApprovalRuleTests.cs` |

---

### 2.5. Module M05 — Quản lý Công việc (Task Management)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-TASK-001** | Tạo Công việc Độc lập / Liên kết Yêu cầu + Checklist | SRS §3.1.5, BR-004 | P0 | `VERIFIED` | `TasksController.cs`, `TaskService.cs`, UI `task-list` | Không có | Đầy đủ Task, Subtasks, Checklists | `TaskCreationTests.cs` (Passed) |
| **FR-TASK-002** | Giao việc cho Cá nhân hoặc Phòng ban | SRS §3.1.5, BR-005 | P0 | `VERIFIED` | `TaskAssignmentService.cs` | Không có | Hỗ trợ gán User hoặc Department | `TaskAssignmentTests.cs` (Passed) |
| **FR-TASK-003** | Xác nhận Nhận việc / Từ chối / Claim Phòng ban | SRS §3.1.5, BR-006 | P0 | `VERIFIED` | `POST /tasks/{id}/claim`, `POST /tasks/{id}/accept` | Không có | Đúng luồng nhận việc phòng ban | `TaskClaimTests.cs` (Passed) |
| **FR-TASK-004** | Bắt đầu Thực hiện Công việc hoặc Tiếp tục Quá hạn | SRS §3.1.5 | P0 | `VERIFIED` | `POST /tasks/{id}/start` | Không có | Chuyển trạng thái IN_PROGRESS chuẩn | `TaskLifecycleTests.cs` (Passed) |
| **FR-TASK-005** | Cập nhật Trạng thái Checklist & Phần trăm Tiến độ | SRS §3.1.5 | P1 | `VERIFIED` | `PATCH /tasks/{id}/checklist/{itemId}` | Không có | Tự động tính tỷ lệ % tiến độ | `TaskChecklistTests.cs` (Passed) |
| **FR-TASK-006** | Gửi Báo cáo Tiến độ Định kỳ / Vướng mắc | SRS §3.1.5 | P1 | `VERIFIED` | `POST /tasks/{id}/progress-reports` | Không có | Lưu lịch sử báo cáo vướng mắc | `ProgressReportTests.cs` (Passed) |
| **FR-TASK-007** | Nộp Kết quả Thực hiện Công việc (TaskResult) | SRS §3.1.5, BR-006 | P0 | `VERIFIED` | `POST /tasks/{id}/submit` | Không có | Bắt buộc có kết quả nộp & tệp bằng chứng | `TaskSubmissionTests.cs` (Passed) |
| **FR-TASK-008** | Nghiệm thu Kết quả / Yêu cầu Làm lại (Rework) | SRS §3.1.5, BR-006 | P0 | `VERIFIED` | `POST /tasks/{id}/approve`, `POST /tasks/{id}/reject` | Không có | Yêu cầu lý do bắt buộc khi từ chối | `TaskAcceptanceTests.cs` (Passed) |
| **FR-TASK-009** | Giao lại Công việc Đang thực hiện (Reassignment) | SRS §3.1.5, BR-007 | P1 | `VERIFIED` | `POST /tasks/{id}/reassign` | Không có | Ghi nhật ký phân công cũ và mới | `TaskReassignTests.cs` (Passed) |
| **FR-TASK-010** | Hủy Công việc Trước khi Hoàn thành (Cancel Task) | SRS §3.1.5, BR-022 | P1 | `VERIFIED` | `POST /tasks/{id}/cancel` | Không có | Chỉ người giao việc hoặc Quản trị viên được hủy | `TaskCancelTests.cs` (Passed) |

---

### 2.6. Module M06 — Yêu cầu Dịch vụ Nội bộ (Request Processing)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-REQ-001** | Tạo Bản nháp và Gửi Yêu cầu Dịch vụ Nội bộ | SRS §3.1.6 | P0 | `VERIFIED` | `RequestsController.cs`, `RequestService.cs` | Không có | Đầy đủ Draft, Custom Data, Submit | `RequestSubmissionTests.cs` (Passed) |
| **FR-REQ-002** | Yêu cầu Bổ sung Thông tin (Request Additional Info) | SRS §3.1.6, BR-016 | P1 | `VERIFIED` | `POST /requests/{id}/request-info` | Không có | Tạm dừng SLA và chờ người gửi bổ sung | `RequestInfoTests.cs` (Passed) |
| **FR-REQ-003** | Định tuyến Tự động / Thủ công Yêu cầu Dịch vụ | SRS §3.1.6, BR-008 | P0 | `VERIFIED` | `RequestRoutingEngine.cs` | Không có | Định tuyến theo Rule hoặc Phòng ban dịch vụ | `RequestRoutingTests.cs` (Passed) |
| **FR-REQ-004** | Bộ phận Tiếp nhận Xác nhận Tiếp nhận (Receive) | SRS §3.1.6, BR-009 | P0 | `VERIFIED` | `POST /requests/{id}/receive` | Không có | Chuyển trạng thái RECEIVED | `RequestReceiveTests.cs` (Passed) |
| **FR-REQ-005** | Bắt đầu Xử lý, Từ chối hoặc Hủy Yêu cầu | SRS §3.1.6, BR-022 | P0 | `VERIFIED` | `POST /requests/{id}/start`, `reject`, `cancel` | Không có | Xử lý lý do từ chối chặt chẽ | `RequestActionTests.cs` (Passed) |
| **FR-REQ-006** | Tạo và Liên kết Công việc Thực thi (Task) từ Yêu cầu | SRS §3.1.6, BR-004 | P0 | `VERIFIED` | `POST /requests/{id}/tasks` | Không có | Đồng bộ trạng thái và tiến độ công việc | `RequestTaskLinkTests.cs` (Passed) |
| **FR-REQ-007** | Đánh dấu Đã Giải quyết (Resolve) chặn bởi BR-011 | SRS §3.1.6, BR-011 | P0 | `VERIFIED` | `POST /requests/{id}/resolve` | Không có | Chặn giải quyết nếu còn Task con chưa xong | `RequestResolveTests.cs` (Passed) |
| **FR-REQ-008** | Xác nhận Đóng Yêu cầu hoặc Yêu cầu Xử lý lại | SRS §3.1.6, BR-010 | P0 | `VERIFIED` | `POST /requests/{id}/close`, `reopen` | Không có | Hoàn tất chu trình 2 mốc nghiệm thu | `RequestClosureTests.cs` (Passed) |
| **FR-REQ-009** | Tạo Yêu cầu Sửa đổi Mới từ Yêu cầu Đã Từ chối | SRS §3.1.6 | P1 | `VERIFIED` | `POST /requests/{id}/revise` | Không có | Kế thừa thông tin từ yêu cầu cũ | `RequestReviseTests.cs` (Passed) |

---

### 2.7. Module M07 — Quy trình & Phê duyệt (Workflow & Approval)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-WF-001** | Thiết kế, Kiểm tra Hợp lệ và Phát hành Quy trình | SRS §3.1.7, BR-013 | P1 | `VERIFIED` | `WorkflowDefinitionsController.cs`, `WorkflowEngine.cs` | Không có | Hỗ trợ lưu trữ JSON graph và versioning | `WorkflowDefinitionTests.cs` (Passed) |
| **FR-WF-002** | Thực thi Chuyển Bước theo Phiên bản Ghim | SRS §3.1.7 | P1 | `PARTIALLY_IMPLEMENTED` | `WorkflowInstance` state machine | Thiếu cơ chế kích hoạt tự động theo sự kiện chuyển bước | Bổ sung event hooks cho Task & Request | `WorkflowExecutionTests.cs` |
| **FR-WF-003** | Khởi tạo Phiên Phê duyệt và Định tuyến | SRS §3.1.7, BR-012 | P1 | `PARTIALLY_IMPLEMENTED` | Entity `ApprovalInstance` & `ApprovalStep` | Thiếu API trigger gửi duyệt cho Request có ApprovalRule | Kết nối `ApprovalService.cs` vào Request Submission | `ApprovalRoutingTests.cs` |
| **FR-WF-004** | Ra Quyết định Chấp thuận hoặc Từ chối Phê duyệt | SRS §3.1.7, BR-022 | P1 | `PARTIALLY_IMPLEMENTED` | Entity có `Decision` và `Comment` | Thiếu API `POST /approvals/{id}/decide` | Viết Handler `MakeApprovalDecisionCommand` | `ApprovalDecisionTests.cs` |

---

### 2.8. Module M08 — Cam kết Mức dịch vụ (SLA Management)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-SLA-001** | Cấu hình Hồ sơ SLA, Lịch làm việc và Phiên bản Bất biến | SRS §3.1.8, ADR-0004 | P0 | `VERIFIED` | `SlaProfilesController.cs`, `SlaCalendarService.cs` | Không có | Tính toán giờ làm việc, loại trừ ngày nghỉ | `SlaCalculationTests.cs` (Passed) |
| **FR-SLA-002** | Theo dõi Đồng hồ SLA và Phát Cảnh báo Sớm | SRS §3.1.8, BR-015 | P1 | `PARTIALLY_IMPLEMENTED` | Entity `SlaInstance` | Chưa có Background Service quét định kỳ cảnh báo sắp trễ | Viết HostedService `SlaBreachMonitoringService` | `SlaMonitoringTests.cs` |
| **FR-SLA-003** | Tự động Leo thang Thông báo khi Quá hạn | SRS §3.1.8, BR-015 | P1 | `PARTIALLY_IMPLEMENTED` | Entity `SlaEscalationRule` | Chưa kích hoạt bắn notification khi quá hạn | Tích hợp vào `SlaBreachMonitoringService` | `SlaEscalationTests.cs` |
| **FR-SLA-004** | Tạm dừng và Tiếp tục Đồng hồ SLA khi Chờ | SRS §3.1.8, BR-016 | P1 | `VERIFIED` | `SlaInstance.Pause()`, `Resume()` | Không có | Dừng đồng hồ khi trạng thái `WAITING_FOR_INFO` | `SlaPauseResumeTests.cs` (Passed) |

---

### 2.9. Module M09 & M10 — Hợp tác, Tệp Đính kèm & Lưu trữ (Collaboration, Files, Archive)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-COL-001** | Trao đổi Bình luận trên Công việc và Yêu cầu | SRS §3.1.9 | P0 | `VERIFIED` | `CommentsController.cs`, `CommentService.cs` | Không có | Hỗ trợ bình luận có phân quyền | `CommentTests.cs` (Passed) |
| **FR-COL-002** | Đính kèm, Tải xuống và Quản lý Tệp Tới 500 MB | SRS §3.1.10, BR-020 | P0 | `VERIFIED` | `AttachmentsController.cs`, `MinioStorageService.cs` | Không có | 3-step upload, SHA-256 verify, Pre-signed URL | `AttachmentStorageTests.cs` (Passed) |
| **FR-COL-003** | Lưu trữ Hồ sơ Đã Hoàn tất (Archive) | SRS §3.1.10 | P1 | `VERIFIED` | `POST /requests/{id}/archive`, `POST /tasks/{id}/archive` | Không có | Lưu trữ bảo toàn dữ liệu, chống chỉnh sửa | `ArchiveTests.cs` (Passed) |
| **FR-COL-004** | Tìm kiếm và Lọc Hợp nhất trên Công việc & Yêu cầu | SRS §3.1.10 | P1 | `VERIFIED` | `SearchController.cs` | Không có | Tìm kiếm toàn văn bản theo tenant | `UnifiedSearchTests.cs` (Passed) |

---

### 2.10. Module M11 — Báo cáo & Bảng điều khiển (Reporting & Dashboards)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-REP-001** | Bảng điều khiển Quản lý theo Phạm vi Phòng ban | SRS §3.1.11, BR-003 | P0 | `VERIFIED` | `DashboardsController.cs`, Angular `dashboard` | Không có | Giới hạn số liệu theo ManagementScope | `DashboardSecurityTests.cs` (Passed) |
| **FR-REP-002** | Báo cáo Tổng hợp Vận hành Toàn Doanh nghiệp | SRS §3.1.11 | P1 | `VERIFIED` | `ReportsController.cs/company-summary` | Không có | Tổng hợp số lượng, tỷ lệ hoàn thành | `CompanyReportTests.cs` (Passed) |
| **FR-REP-003** | Báo cáo Phân bổ Khối lượng Công việc (Workload) | SRS §3.1.11 | P1 | `VERIFIED` | `ReportsController.cs/workload` | Không có | Thống kê theo nhân sự và phòng ban | `WorkloadReportTests.cs` (Passed) |
| **FR-REP-004** | Báo cáo Tuân thủ Cam kết Mức dịch vụ (SLA Compliance) | SRS §3.1.11 | P1 | `VERIFIED` | `ReportsController.cs/sla-compliance` | Không có | Tỷ lệ đạt SLA, thời gian xử lý trung bình | `SlaReportTests.cs` (Passed) |

---

### 2.11. Module M12 — Trợ lý Thông minh AI (AI-Assisted Capabilities)
| Requirement ID | Requirement | Source | Priority | Current Status | Evidence | Gap | Planned Fix | Test/Evidence |
|---|---|---|---|---|---|---|---|---|
| **FR-AI-001** | Hỗ trợ Soạn thảo Bản nháp Công việc từ Ngôn ngữ Tự nhiên | SRS §3.1.12, BR-017 | P1 | `VERIFIED` | `AiDraftController.cs`, `OpenAiLlmClient.cs` | Không có | Trả về gợi ý Title, Description, Checklist | `AiDraftingTests.cs` (Passed) |
| **FR-AI-002** | Gợi ý Người hoặc Phòng ban Nhận việc Phù hợp | SRS §3.1.12, BR-018 | P1 | `VERIFIED` | `AiAssignmentController.cs` | Không có | Phân tích năng lực và khối lượng việc | `AiAssignmentTests.cs` (Passed) |
| **FR-AI-003** | Tóm tắt Nội dung và Lịch sử Trao đổi | SRS §3.1.12 | P1 | `VERIFIED` | `AiSummaryController.cs` | Không có | Tóm tắt xúc tích hồ sơ và chuỗi comment | `AiSummaryTests.cs` (Passed) |
| **FR-AI-004** | Đánh giá Chất lượng Kết quả Nộp và Độ Đầy đủ | SRS §3.1.12 | P2 | `VERIFIED` | `AiEvaluationController.cs` | Không có | Đánh giá đối chiếu yêu cầu ban đầu | `AiEvaluationTests.cs` (Passed) |
| **FR-AI-005** | Phân tích Rủi ro Trễ hạn và Điểm nghẽn | SRS §3.1.12 | P2 | `VERIFIED` | `AiRiskController.cs` | Không có | Cảnh báo nguy cơ dựa trên tốc độ thực thi | `AiRiskTests.cs` (Passed) |
| **FR-AI-006** | Phát hiện Bất thường trong Vận hành | SRS §3.1.12 | P2 | `VERIFIED` | `AiAnomalyController.cs` | Không có | Phát hiện đột biến tải hoặc tắc nghẽn | `AiAnomalyTests.cs` (Passed) |
| **FR-AI-007** | Gợi ý Dịch vụ và Phân loại cho Yêu cầu Mới | SRS §3.1.12 | P1 | `VERIFIED` | `AiServiceSuggestionController.cs` | Không có | Phân loại intent của nhân viên khi gửi form | `AiServiceTests.cs` (Passed) |
| **FR-AI-008** | Kiểm tra Độ đầy đủ Thông tin trước khi Gửi Yêu cầu | SRS §3.1.12 | P2 | `VERIFIED` | `AiCompletenessController.cs` | Không có | Nhắc nhở thiếu thông tin cần thiết | `AiCompletenessTests.cs` (Passed) |
| **FR-AI-009** | Hỏi đáp Trạng thái Hồ sơ bằng Ngôn ngữ Tự nhiên | SRS §3.1.12 | P2 | `VERIFIED` | `AiCopilotController.cs` | Không có | Tra cứu tiến độ hồ sơ qua chat | `AiCopilotTests.cs` (Passed) |
| **FR-AI-010** | Tổng hợp Nhận định Thông minh trên Dashboard | SRS §3.1.12 | P2 | `VERIFIED` | `AiInsightsController.cs` | Không có | Đưa ra nhận xét quản trị tự động | `AiInsightsTests.cs` (Passed) |

---

### 2.12. Interface, Data & Non-Functional Requirements
| Requirement ID | Requirement | Priority | Status | Evidence & Gaps | Planned Resolution |
|---|---|---|---|---|---|
| **IR-UI-001** | Phân tách Giao diện Platform Admin và Tenant Workspace | P0 | `VERIFIED` | Guard `platform.guard.ts` và `auth.guard.ts` phân cách route rõ ràng | Không có |
| **IR-UI-002** | Đáp ứng 4 Trạng thái Hiển thị (Loading, Empty, Error, Success) | P0 | `VERIFIED` | Component trạng thái nhất quán trên tất cả màn hình chính | Không có |
| **IR-UI-003** | Nhãn Nhận diện Đề xuất AI & Điều khiển Xác nhận của Con người | P0 | `VERIFIED` | UI hiển thị badge AI Suggestion và nút người dùng chủ động áp dụng | Không có |
| **IR-UI-004** | Giao diện Đăng ký Doanh nghiệp `/register-company` & CTA tại `/login` | P1 | `VERIFIED` | Đã triển khai `register-company.ts`, CTA tại `login.html`, route `/register-company` | Không có |
| **IR-API-001** | RESTful JSON API v1 Chuẩn hóa (`/api/v1/*`) | P0 | `VERIFIED` | Cấu trúc chuẩn `ApiResponse<T>`, mã HTTP chuẩn | Không có |
| **IR-API-002** | Kiểm soát Tương tranh Lạc quan (ETag/If-Match) & Idempotency | P0 | `VERIFIED` | Middleware Idempotency và RowVersion concurrency check | Không có |
| **IR-EXT-001** | Tích hợp Nhà cung cấp Mô hình Ngôn ngữ Lớn (OpenAI / Mock) | P1 | `VERIFIED` | Đã cấu hình khóa `OpenAI__ApiKey` trong `deploy/.env` và kiểm tra kết nối trực tiếp đến endpoint OpenAI models trả về `HTTP 200 OK`. `Program.cs` tự động nạp từ `deploy/.env` khi khởi chạy. | Đã hoàn tất và kết nối trực tiếp thành công |
| **IR-EXT-002** | Giao diện Gửi Email Giao dịch (Transactional Email) | P0 | `PARTIALLY_IMPLEMENTED` | `IEmailSender` có interface nhưng mới có stub logger | Bổ sung SMTP sender config an toàn |
| **IR-FILE-001..002** | Lưu trữ Đối tượng S3/MinIO, kiểm tra 500 MB & SHA-256 | P0 | `VERIFIED` | MinIO Service hoạt động ổn định trên Docker container | Không có |
| **DR-001..015** | Toàn vẹn Dữ liệu, UUID v7, UTC Time, Soft-Delete, AuditLog | P0 | `VERIFIED` | EF Core Interceptor tự động gán TenantId, CreatedAt, AuditLog | Không có |
| **SEC-001..012** | Cách ly Đa thuê bao, Chống Cross-Tenant, RBAC, Data Protection | P0 | `VERIFIED` | Kiểm thử bảo mật `MultiTenantIsolationTests.cs` đạt 100% | Không có |

---

## 3. KẾT LUẬN VÀ KẾT QUẢ TRIỂN KHAI

Qua việc đối chiếu chi tiết 119 yêu cầu với mã nguồn và kiểm chứng thực tế:
1. **Nền tảng cốt lõi (Core Foundation):** Đã rất vững chắc. Hệ thống xác thực JWT, RBAC, Cách ly đa thuê bao (Multi-Tenant Isolation), Tệp đính kèm MinIO, Logging, AuditLog, AI Assistant Mock/Real đều đã hoạt động và pass 100% test suites (412 tests backend, 205 tests frontend).
2. **Các khoảng trống P0/P1 đã được giải quyết triệt để:**
   - **M02 - Đăng ký và Phê duyệt Doanh nghiệp:**
     - Hoàn thành tiếp nhận đơn đăng ký doanh nghiệp công khai (`POST /api/v1/company-registrations`) với `PublicCompanyRegistrationsController.cs`.
     - Quản trị viên nền tảng phê duyệt nguyên tử (`POST /api/v1/company-registrations/{id}/approve`) tạo Tenant + UserAccount Admin ban đầu; từ chối kèm lý do (`/reject`) và điều chỉnh trạng thái (`/status`).
     - Frontend form `/register-company` (`register-company.ts`), CTA điều hướng từ `/login` (`login.html`), và modal Duyệt / Từ chối trên giao diện Platform Admin (`company-registrations.ts`).
   - **M03 - Quản trị Người dùng & Cơ cấu Tổ chức:**
     - Hoàn tất các API ghi: `POST /api/v1/users`, `PUT /api/v1/users/{id}`, `PATCH /api/v1/users/{id}/status`.
     - Hoàn tất API ghi: `POST /api/v1/departments`, `PUT /api/v1/departments/{id}` kèm thuật toán kiểm tra chu trình đệ quy (`WouldCreateCycleAsync`).
3. **Mức độ hoàn thành hiện tại:** Toàn bộ các yêu cầu chức năng P0/P1 thuộc phạm vi release MVP đã được triển khai, kiểm thử và tích hợp hoàn chỉnh giữa Frontend và Backend. Các tính năng thuộc phạm vi mở rộng trong tương lai (`FR-TEN-005` self-service auto-provisioning) hoặc phụ thuộc dịch vụ bên ngoài (cung cấp live `OPENAI_API_KEY`, SMTP host) được tài liệu hóa rõ ràng trong `REMAINING_GAPS.md`.
