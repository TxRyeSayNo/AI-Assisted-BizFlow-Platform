-- 1. Company
INSERT INTO "Company" ("CompanyId", "Code", "Name", "ContactEmail", "Status", "CreatedAt", "UpdatedAt")
VALUES (
    '019f8000-0000-7000-8000-000000000001',
    'BIZFLOW',
    'BizFlow Corporation',
    'admin@bizflow.local',
    'ACTIVE',
    NOW(),
    NOW()
) ON CONFLICT ("CompanyId") DO UPDATE SET "Status" = 'ACTIVE';

-- 2. Tenant
INSERT INTO "Tenant" ("TenantId", "CompanyId", "TenantKey", "Name", "Status", "TimeZone", "CreatedAt", "UpdatedAt")
VALUES (
    '019f8000-0000-7000-8000-000000000002',
    '019f8000-0000-7000-8000-000000000001',
    'bizflow-corp',
    'BizFlow Corporation',
    'ACTIVE',
    'Asia/Ho_Chi_Minh',
    NOW(),
    NOW()
) ON CONFLICT ("TenantId") DO NOTHING;

-- 3. Department
INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt")
VALUES (
    '019f8000-0000-7000-8000-000000000003',
    '019f8000-0000-7000-8000-000000000002',
    'TECH',
    'Phòng Công nghệ & Chuyển đổi số',
    NULL,
    'ACTIVE',
    NOW(),
    NOW()
) ON CONFLICT ("DepartmentId") DO NOTHING;

-- 4. Admin User (ADMIN001 / Password123!)
INSERT INTO "User" (
    "UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode",
    "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName",
    "DepartmentId", "Status", "AccessFailedCount", "LockoutEnd", "MustChangePassword",
    "LastLoginAt", "CreatedAt", "UpdatedAt", "DeletedAt"
)
VALUES (
    '019f8000-0000-7000-8000-000000000010',
    '019f8000-0000-7000-8000-000000000002',
    FALSE,
    'ADMIN001',
    'ADMIN001',
    'admin@bizflow.local',
    'ADMIN@BIZFLOW.LOCAL',
    'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==',
    '019f8000000070008000000000000010',
    'Quản Trị Viên Hệ Thống',
    '019f8000-0000-7000-8000-000000000003',
    'ACTIVE',
    0,
    NULL,
    FALSE,
    NULL,
    NOW(),
    NOW(),
    NULL
) ON CONFLICT ("UserId") DO UPDATE SET
    "FullName" = EXCLUDED."FullName",
    "PasswordHash" = 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==',
    "Status" = 'ACTIVE',
    "AccessFailedCount" = 0,
    "LockoutEnd" = NULL;

-- 5. Employee User (EMP001 / Password123!)
INSERT INTO "User" (
    "UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode",
    "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName",
    "DepartmentId", "Status", "AccessFailedCount", "LockoutEnd", "MustChangePassword",
    "LastLoginAt", "CreatedAt", "UpdatedAt", "DeletedAt"
)
VALUES (
    '019f8000-0000-7000-8000-000000000011',
    '019f8000-0000-7000-8000-000000000002',
    FALSE,
    'EMP001',
    'EMP001',
    'employee@bizflow.local',
    'EMPLOYEE@BIZFLOW.LOCAL',
    'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==',
    '019f8000000070008000000000000011',
    'Nguyễn Văn Nhân Viên',
    '019f8000-0000-7000-8000-000000000003',
    'ACTIVE',
    0,
    NULL,
    FALSE,
    NULL,
    NOW(),
    NOW(),
    NULL
) ON CONFLICT ("UserId") DO UPDATE SET
    "FullName" = EXCLUDED."FullName",
    "PasswordHash" = 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==',
    "Status" = 'ACTIVE',
    "AccessFailedCount" = 0,
    "LockoutEnd" = NULL;

-- 5.1. Department Manager (MGR001 / Password123!)
INSERT INTO "User" (
    "UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode",
    "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName",
    "DepartmentId", "Status", "AccessFailedCount", "LockoutEnd", "MustChangePassword",
    "LastLoginAt", "CreatedAt", "UpdatedAt", "DeletedAt"
)
VALUES (
    '019f8000-0000-7000-8000-000000000012',
    '019f8000-0000-7000-8000-000000000002',
    FALSE,
    'MGR001',
    'MGR001',
    'manager@bizflow.local',
    'MANAGER@BIZFLOW.LOCAL',
    'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==',
    '019f8000000070008000000000000012',
    'Trần Thị Quản Lý',
    '019f8000-0000-7000-8000-000000000003',
    'ACTIVE',
    0,
    NULL,
    FALSE,
    NULL,
    NOW(),
    NOW(),
    NULL
) ON CONFLICT ("UserId") DO UPDATE SET
    "FullName" = EXCLUDED."FullName",
    "PasswordHash" = 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==',
    "Status" = 'ACTIVE',
    "AccessFailedCount" = 0,
    "LockoutEnd" = NULL;

-- 6. User Roles
-- Admin roles: COMPANY_ADMIN, MANAGER, EMPLOYEE
INSERT INTO "UserRole" ("UserId", "RoleId") VALUES
    ('019f8000-0000-7000-8000-000000000010', '019f7f8a-0000-7000-8000-000000000002'),
    ('019f8000-0000-7000-8000-000000000010', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-000000000010', '019f7f8a-0000-7000-8000-000000000004'),
-- Manager roles: MANAGER, EMPLOYEE
    ('019f8000-0000-7000-8000-000000000012', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-000000000012', '019f7f8a-0000-7000-8000-000000000004'),
-- Employee roles: EMPLOYEE
    ('019f8000-0000-7000-8000-000000000011', '019f7f8a-0000-7000-8000-000000000004')
ON CONFLICT ("UserId", "RoleId") DO NOTHING;

-- 7. ManagementScope for Admin & Manager
INSERT INTO "ManagementScope" ("ManagementScopeId", "TenantId", "UserId", "DepartmentId", "IncludeDescendants", "CreatedBy", "CreatedAt")
VALUES (
    '019f8000-0000-7000-8000-000000000020',
    '019f8000-0000-7000-8000-000000000002',
    '019f8000-0000-7000-8000-000000000010',
    '019f8000-0000-7000-8000-000000000003',
    TRUE,
    '019f8000-0000-7000-8000-000000000010',
    NOW()
) ON CONFLICT ("ManagementScopeId") DO NOTHING;

INSERT INTO "ManagementScope" ("ManagementScopeId", "TenantId", "UserId", "DepartmentId", "IncludeDescendants", "CreatedBy", "CreatedAt")
VALUES (
    '019f8000-0000-7000-8000-000000000021',
    '019f8000-0000-7000-8000-000000000002',
    '019f8000-0000-7000-8000-000000000012',
    '019f8000-0000-7000-8000-000000000003',
    TRUE,
    '019f8000-0000-7000-8000-000000000010',
    NOW()
) ON CONFLICT ("ManagementScopeId") DO NOTHING;

-- 8. Internal Service & Category
INSERT INTO "Service" ("ServiceId", "TenantId", "Code", "Name", "Description", "Status", "CreatedAt", "UpdatedAt")
VALUES (
    '019f8000-0000-7000-8000-000000000030',
    '019f8000-0000-7000-8000-000000000002',
    'IT-SUPPORT',
    'Hỗ trợ Kỹ thuật & Hạ tầng CNTT',
    'Dịch vụ tiếp nhận và xử lý sự cố CNTT nội bộ doanh nghiệp',
    'ACTIVE',
    NOW(),
    NOW()
) ON CONFLICT ("ServiceId") DO UPDATE SET "Name" = EXCLUDED."Name", "Description" = EXCLUDED."Description";

INSERT INTO "ServiceCategory" ("ServiceCategoryId", "ServiceId", "Code", "Name", "Status")
VALUES (
    '019f8000-0000-7000-8000-000000000031',
    '019f8000-0000-7000-8000-000000000030',
    'HARDWARE',
    'Sự cố thiết bị phần cứng & máy trạm',
    'ACTIVE'
) ON CONFLICT ("ServiceCategoryId") DO UPDATE SET "Name" = EXCLUDED."Name";

-- 9. Sample Work Task
INSERT INTO "Task" (
    "TaskId", "TenantId", "RequestId", "CreatorId", "Title", "Description",
    "Priority", "Deadline", "Status", "WorkflowVersionId", "SLAVersionId",
    "CompletedAt", "CreatedAt", "UpdatedAt", "DeletedAt"
)
VALUES (
    '019f8000-0000-7000-8000-000000000040',
    '019f8000-0000-7000-8000-000000000002',
    NULL,
    '019f8000-0000-7000-8000-000000000010',
    'Triển khai hạ tầng và thử nghiệm tính năng BizFlow AI',
    'Thử nghiệm các tính năng AI cốt lõi: Phân rã nhiệm vụ (Task Breakdown), Đánh giá rủi ro (Risk Assessment), Gợi ý nhân sự tối ưu (Assignment Recommendation) và Tóm tắt công việc (Summary).',
    'HIGH',
    NOW() + INTERVAL '7 days',
    'DRAFT',
    NULL,
    NULL,
    NULL,
    NOW(),
    NOW(),
    NULL
) ON CONFLICT ("TaskId") DO UPDATE SET "Title" = EXCLUDED."Title", "Description" = EXCLUDED."Description";
