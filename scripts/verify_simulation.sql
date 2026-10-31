SET client_encoding = 'UTF8';

SELECT '=== DEPARTMENTS ===' AS section;
SELECT "DepartmentId", "Code", "Name" FROM "Department" WHERE "TenantId" = '019f8000-0000-7000-8000-000000000002' ORDER BY "Code";

SELECT '=== USERS & ROLES ===' AS section;
SELECT u."EmployeeCode", u."FullName", u."Email", d."Code" AS dept, r."Name" AS role
FROM "User" u
LEFT JOIN "Department" d ON u."DepartmentId" = d."DepartmentId"
JOIN "UserRole" ur ON u."UserId" = ur."UserId"
JOIN "Role" r ON ur."RoleId" = r."RoleId"
WHERE u."TenantId" = '019f8000-0000-7000-8000-000000000002'
ORDER BY u."EmployeeCode", r."Name";

SELECT '=== TASKS & ASSIGNMENTS ===' AS section;
SELECT t."Title", t."Priority", t."Status", d."Code" AS dept, u."FullName" AS assignee,
       (SELECT COUNT(*) FROM "TaskChecklistItem" c WHERE c."TaskId" = t."TaskId") AS total_checklists,
       (SELECT COUNT(*) FROM "TaskChecklistItem" c WHERE c."TaskId" = t."TaskId" AND c."IsCompleted") AS completed_checklists
FROM "Task" t
LEFT JOIN "TaskAssignment" ta ON t."TaskId" = ta."TaskId" AND ta."EndedAt" IS NULL
LEFT JOIN "Department" d ON ta."DepartmentId" = d."DepartmentId"
LEFT JOIN "User" u ON ta."UserId" = u."UserId"
WHERE t."TenantId" = '019f8000-0000-7000-8000-000000000002'
ORDER BY t."CreatedAt";

SELECT '=== REQUESTS ===' AS section;
SELECT r."Title", r."Priority", r."Status", s."Name" AS service_name, sc."Name" AS cat_name, u."FullName" AS requester
FROM "Request" r
JOIN "Service" s ON r."ServiceId" = s."ServiceId"
JOIN "ServiceCategory" sc ON r."CategoryId" = sc."ServiceCategoryId"
JOIN "User" u ON r."RequesterId" = u."UserId"
WHERE r."TenantId" = '019f8000-0000-7000-8000-000000000002';

SELECT '=== AI VECTOR MEMORIES ===' AS section;
SELECT "MemoryId", "MemoryScope", "Title", jsonb_array_length("EmbeddingJson") AS emb_dim
FROM "AIVectorMemory"
WHERE "TenantId" = '019f8000-0000-7000-8000-000000000002';

