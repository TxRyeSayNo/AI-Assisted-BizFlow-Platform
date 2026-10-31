import json
import math
import re
import subprocess
import os

VECTOR_DIMENSIONS = 384

def fnv1a_32(text: str) -> int:
    h = 2166136261
    for c in text:
        h ^= ord(c)
        h = (h * 16777619) & 0xFFFFFFFF
    if h >= 0x80000000:
        h -= 0x100000000
    return h

def generate_embedding(text: str) -> list[float]:
    vector = [0.0] * VECTOR_DIMENSIONS
    if not text or not text.strip():
        return vector

    normalized = text.lower().strip()
    words = [w for w in re.split(r'[\s\t\r\n,.;:!?\-_()\[\]{}]+', normalized) if w]

    for i, word in enumerate(words):
        hash1 = abs(fnv1a_32(word)) % VECTOR_DIMENSIONS
        vector[hash1] += 2.0

        if i < len(words) - 1:
            bigram = f"{word}_{words[i+1]}"
            hash_bi = abs(fnv1a_32(bigram)) % VECTOR_DIMENSIONS
            vector[hash_bi] += 1.5

        if len(word) >= 3:
            for c in range(len(word) - 2):
                trigram = word[c:c+3]
                hash_tri = abs(fnv1a_32(trigram)) % VECTOR_DIMENSIONS
                vector[hash_tri] += 0.5

    sum_sq = sum(v * v for v in vector)
    if sum_sq > 0.0:
        norm = math.sqrt(sum_sq)
        vector = [round(v / norm, 6) for v in vector]

    return vector

# Documents for corporate knowledge base
doc1_title = "Chính sách tạm ứng, thanh toán và quyết toán công tác phí"
doc1_content = "Quy định tạm ứng và quyết toán công tác phí tại BizFlow Corporation: 1. Nhân viên được tạm ứng tối đa 80% chi phí dự kiến trước ngày khởi hành ít nhất 3 ngày làm việc. 2. Định mức lưu trú khách sạn: Cán bộ quản lý tối đa 1.500.000 VNĐ/đêm, nhân viên tối đa 1.000.000 VNĐ/đêm tại Hà Nội và TP.HCM; các tỉnh thành khác tối đa 800.000 VNĐ/đêm. 3. Vé máy bay: Mặc định hạng Phổ thông (Economy), ưu tiên đặt trước ít nhất 7 ngày. 4. Thời hạn hoàn ứng: Nộp đầy đủ hóa đơn GTGT hợp lệ và bảng kê chi phí trong vòng 7 ngày làm việc kể từ ngày kết thúc công tác về Phòng Tài chính - Kế toán."
emb1 = json.dumps(generate_embedding(f"{doc1_title}\n{doc1_content}"))

doc2_title = "Quy định làm việc từ xa và văn hóa công sở BizFlow"
doc2_content = "Quy định làm việc từ xa (Remote Work) tại BizFlow Corporation: 1. Nhân sự đã qua thử việc được đăng ký tối đa 2 ngày làm việc từ xa mỗi tuần với sự phê duyệt của Quản lý phòng ban. 2. Khung giờ làm việc cốt lõi bắt buộc phải trực tuyến trên hệ thống và kênh chat nội bộ: Sáng 08:30 - 12:00, Chiều 13:30 - 17:30. 3. Tuân thủ bảo mật: Không sử dụng mạng Wi-Fi công cộng không có bảo mật để truy cập cơ sở dữ liệu nội bộ. Phải sử dụng VPN công ty khi cần truy cập hạ tầng backend."
emb2 = json.dumps(generate_embedding(f"{doc2_title}\n{doc2_content}"))

doc3_title = "Quy trình tiếp nhận và xử lý yêu cầu CNTT (IT Service Desk SLA)"
doc3_content = "Cam kết chất lượng dịch vụ CNTT (IT Service Level Agreement - SLA): 1. Sự cố mức P1 (Hệ thống ngừng hoạt động, ảnh hưởng toàn bộ doanh nghiệp): Thời gian phản hồi dưới 15 phút, thời gian khắc phục dưới 2 giờ. 2. Sự cố mức P2 (Suy giảm tính năng quan trọng của phòng ban): Phản hồi dưới 30 phút, giải quyết dưới 4 giờ. 3. Yêu cầu cấp phát phần cứng, tài khoản mới (P3): Xử lý trong vòng 24 giờ làm việc. Tất cả yêu cầu cần được tạo trên hệ thống BizFlow Service Desk."
emb3 = json.dumps(generate_embedding(f"{doc3_title}\n{doc3_content}"))

doc4_title = "Chính sách an toàn thông tin và bảo mật tài khoản người dùng"
doc4_content = "Chính sách an toàn thông tin BizFlow: 1. Mật khẩu tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt. Phải thay đổi mật khẩu định kỳ tối thiểu 90 ngày một lần. 2. Tuyệt đối không chia sẻ tài khoản, mã xác thực 2FA hoặc thông tin đăng nhập cho người khác. 3. Khóa màn hình máy tính khi rời khỏi bàn làm việc (Windows + L). 4. Nghiêm cấm lưu trữ dữ liệu khách hàng hoặc tài liệu mật trên thiết bị cá nhân chưa được phòng CNTT mã hóa."
emb4 = json.dumps(generate_embedding(f"{doc4_title}\n{doc4_content}"))

doc5_title = "Quy trình Onboarding và cấp phát tài sản cho nhân sự mới"
doc5_content = "Quy trình tiếp nhận và đào tạo nhân viên mới (Onboarding Flow): 1. Trước ngày gia nhập 3 ngày: Phòng Nhân sự gửi yêu cầu cấp phát laptop, tạo email doanh nghiệp và thẻ ra vào cho Phòng CNTT. 2. Ngày đầu tiên (Day 1): Gặp gỡ người hướng dẫn (Buddy), tham quan văn phòng, ký kết hợp đồng thử việc và nhận bàn giao tài sản. 3. Tuần đầu tiên: Hoàn thành khóa học trực tuyến về Quy chế văn hóa doanh nghiệp và Hướng dẫn sử dụng nền tảng BizFlow. 4. Sau 60 ngày: Phòng Nhân sự tổ chức đánh giá kết quả thử việc cùng Trưởng phòng ban."
emb5 = json.dumps(generate_embedding(f"{doc5_title}\n{doc5_content}"))

sql = f"""-- =========================================================================
-- BIZFLOW CORPORATION - ENTERPRISE DATA SIMULATION SEED SCRIPT
-- Simulated Multi-Department Enterprise Dataset with Realistic Operations
-- =========================================================================
SET client_encoding = 'UTF8';

-- 1. Ensure Target Company & Tenant Exist and Active
INSERT INTO "Company" ("CompanyId", "Code", "Name", "ContactEmail", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000001', 'BIZFLOW', 'BizFlow Corporation', 'admin@bizflow.local', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("CompanyId") DO UPDATE SET "Name" = EXCLUDED."Name", "Status" = 'ACTIVE';

INSERT INTO "Tenant" ("TenantId", "CompanyId", "TenantKey", "Name", "Status", "TimeZone", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-000000000001', 'bizflow-corp', 'BizFlow Corporation', 'ACTIVE', 'Asia/Ho_Chi_Minh', NOW(), NOW())
ON CONFLICT ("TenantId") DO UPDATE SET "Name" = EXCLUDED."Name", "Status" = 'ACTIVE';

-- 2. Departments
-- EXEC: Ban Giám đốc & Điều hành
INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000004', '019f8000-0000-7000-8000-000000000002', 'EXEC', 'Ban Giám đốc & Điều hành', NULL, 'ACTIVE', NOW(), NOW())
ON CONFLICT ("DepartmentId") DO UPDATE SET "Name" = EXCLUDED."Name", "Status" = 'ACTIVE';

-- TECH: Phòng Công nghệ & Chuyển đổi số
INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000003', '019f8000-0000-7000-8000-000000000002', 'TECH', 'Phòng Công nghệ & Chuyển đổi số', '019f8000-0000-7000-8000-000000000004', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("DepartmentId") DO UPDATE SET "Name" = EXCLUDED."Name", "ParentDepartmentId" = '019f8000-0000-7000-8000-000000000004', "Status" = 'ACTIVE';

-- SALES: Phòng Kinh doanh B2B & Đối tác
INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000005', '019f8000-0000-7000-8000-000000000002', 'SALES', 'Phòng Kinh doanh B2B & Đối tác', '019f8000-0000-7000-8000-000000000004', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("DepartmentId") DO UPDATE SET "Name" = EXCLUDED."Name", "ParentDepartmentId" = '019f8000-0000-7000-8000-000000000004', "Status" = 'ACTIVE';

-- MKT: Phòng Tiếp thị & Truyền thông Thương hiệu
INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000006', '019f8000-0000-7000-8000-000000000002', 'MKT', 'Phòng Tiếp thị & Truyền thông Thương hiệu', '019f8000-0000-7000-8000-000000000004', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("DepartmentId") DO UPDATE SET "Name" = EXCLUDED."Name", "ParentDepartmentId" = '019f8000-0000-7000-8000-000000000004', "Status" = 'ACTIVE';

-- FIN: Phòng Tài chính - Kế toán
INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000007', '019f8000-0000-7000-8000-000000000002', 'FIN', 'Phòng Tài chính - Kế toán', '019f8000-0000-7000-8000-000000000004', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("DepartmentId") DO UPDATE SET "Name" = EXCLUDED."Name", "ParentDepartmentId" = '019f8000-0000-7000-8000-000000000004', "Status" = 'ACTIVE';

-- HR: Phòng Hành chính - Nhân sự
INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000008', '019f8000-0000-7000-8000-000000000002', 'HR', 'Phòng Hành chính - Nhân sự', '019f8000-0000-7000-8000-000000000004', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("DepartmentId") DO UPDATE SET "Name" = EXCLUDED."Name", "ParentDepartmentId" = '019f8000-0000-7000-8000-000000000004', "Status" = 'ACTIVE';

-- CS: Phòng Dịch vụ Khách hàng & Vận hành
INSERT INTO "Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000009', '019f8000-0000-7000-8000-000000000002', 'CS', 'Phòng Dịch vụ Khách hàng & Vận hành', '019f8000-0000-7000-8000-000000000004', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("DepartmentId") DO UPDATE SET "Name" = EXCLUDED."Name", "ParentDepartmentId" = '019f8000-0000-7000-8000-000000000004', "Status" = 'ACTIVE';

-- 3. Users Directory (All Password123!)
-- Hash: AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==

-- 3.1 CEO / General Director
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000010', '019f8000-0000-7000-8000-000000000002', FALSE, 'ADMIN001', 'ADMIN001', 'admin@bizflow.local', 'ADMIN@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_ADMIN001', 'Phạm Minh Đức (CEO)', '019f8000-0000-7000-8000-000000000004', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Phạm Minh Đức (CEO)', "DepartmentId" = '019f8000-0000-7000-8000-000000000004', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.2 COO / Deputy General Director
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000013', '019f8000-0000-7000-8000-000000000002', FALSE, 'EXEC001', 'EXEC001', 'huong.nguyen@bizflow.local', 'HUONG.NGUYEN@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_EXEC001', 'Nguyễn Thị Mai Hương (COO)', '019f8000-0000-7000-8000-000000000004', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Nguyễn Thị Mai Hương (COO)', "DepartmentId" = '019f8000-0000-7000-8000-000000000004', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.3 CTO / Head of Tech
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000012', '019f8000-0000-7000-8000-000000000002', FALSE, 'MGR001', 'MGR001', 'manager@bizflow.local', 'MANAGER@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_MGR001', 'Lê Hoàng Nam (CTO)', '019f8000-0000-7000-8000-000000000003', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Lê Hoàng Nam (CTO)', "DepartmentId" = '019f8000-0000-7000-8000-000000000003', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.4 DevOps Engineer (Tech)
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-00000000001e', '019f8000-0000-7000-8000-000000000002', FALSE, 'TECH002', 'TECH002', 'huy.do@bizflow.local', 'HUY.DO@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_TECH002', 'Đỗ Quang Huy (DevOps)', '019f8000-0000-7000-8000-000000000003', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Đỗ Quang Huy (DevOps)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.5 FullStack Engineer (Tech)
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000011', '019f8000-0000-7000-8000-000000000002', FALSE, 'EMP001', 'EMP001', 'employee@bizflow.local', 'EMPLOYEE@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_EMP001', 'Nguyễn Văn Nhân Viên (Dev)', '019f8000-0000-7000-8000-000000000003', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Nguyễn Văn Nhân Viên (Dev)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.6 Sales Manager
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000014', '019f8000-0000-7000-8000-000000000002', FALSE, 'SALES_MGR', 'SALES_MGR', 'dat.nguyen@bizflow.local', 'DAT.NGUYEN@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_SALES_MGR', 'Nguyễn Tiến Đạt (Trưởng phòng Sales)', '019f8000-0000-7000-8000-000000000005', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Nguyễn Tiến Đạt (Trưởng phòng Sales)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.7 Sales Executive
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000015', '019f8000-0000-7000-8000-000000000002', FALSE, 'SALES001', 'SALES001', 'kiet.hoang@bizflow.local', 'KIET.HOANG@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_SALES001', 'Hoàng Tuấn Kiệt (Chuyên viên B2B)', '019f8000-0000-7000-8000-000000000005', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Hoàng Tuấn Kiệt (Chuyên viên B2B)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.8 Marketing Manager
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000016', '019f8000-0000-7000-8000-000000000002', FALSE, 'MKT_MGR', 'MKT_MGR', 'thao.vu@bizflow.local', 'THAO.VU@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_MKT_MGR', 'Vũ Phương Thảo (Trưởng phòng Marketing)', '019f8000-0000-7000-8000-000000000006', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Vũ Phương Thảo (Trưởng phòng Marketing)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.9 Marketing Content & Event
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000017', '019f8000-0000-7000-8000-000000000002', FALSE, 'MKT001', 'MKT001', 'linh.le@bizflow.local', 'LINH.LE@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_MKT001', 'Lê Mỹ Linh (Chuyên viên Content)', '019f8000-0000-7000-8000-000000000006', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Lê Mỹ Linh (Chuyên viên Content)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.10 Chief Accountant / CFO
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000018', '019f8000-0000-7000-8000-000000000002', FALSE, 'FIN_MGR', 'FIN_MGR', 'ha.dang@bizflow.local', 'HA.DANG@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_FIN_MGR', 'Đặng Thu Hà (Kế toán trưởng)', '019f8000-0000-7000-8000-000000000007', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Đặng Thu Hà (Kế toán trưởng)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.11 Accountant
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000019', '019f8000-0000-7000-8000-000000000002', FALSE, 'ACC001', 'ACC001', 'anh.phan@bizflow.local', 'ANH.PHAN@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_ACC001', 'Phan Ngọc Ánh (Kế toán viên)', '019f8000-0000-7000-8000-000000000007', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Phan Ngọc Ánh (Kế toán viên)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.12 HR Manager
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-00000000001a', '019f8000-0000-7000-8000-000000000002', FALSE, 'HR_MGR', 'HR_MGR', 'lan.bui@bizflow.local', 'LAN.BUI@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_HR_MGR', 'Bùi Mai Lan (Trưởng phòng Nhân sự)', '019f8000-0000-7000-8000-000000000008', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Bùi Mai Lan (Trưởng phòng Nhân sự)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.13 HR Specialist
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-00000000001b', '019f8000-0000-7000-8000-000000000002', FALSE, 'HR001', 'HR001', 'thanh.ngo@bizflow.local', 'THANH.NGO@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_HR001', 'Ngô Thị Thanh (Chuyên viên Tuyển dụng)', '019f8000-0000-7000-8000-000000000008', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Ngô Thị Thanh (Chuyên viên Tuyển dụng)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.14 Customer Success Manager
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-00000000001c', '019f8000-0000-7000-8000-000000000002', FALSE, 'CS_MGR', 'CS_MGR', 'bao.trinh@bizflow.local', 'BAO.TRINH@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_CS_MGR', 'Trịnh Quốc Bảo (Trưởng phòng CSKH)', '019f8000-0000-7000-8000-000000000009', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Trịnh Quốc Bảo (Trưởng phòng CSKH)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 3.15 Customer Success Specialist
INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "MustChangePassword", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-00000000001d', '019f8000-0000-7000-8000-000000000002', FALSE, 'CS001', 'CS001', 'binh.tran@bizflow.local', 'BINH.TRAN@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_CS001', 'Trần Văn Bình (Chuyên viên CSKH VIP)', '019f8000-0000-7000-8000-000000000009', 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = 'Trần Văn Bình (Chuyên viên CSKH VIP)', "Status" = 'ACTIVE', "AccessFailedCount" = 0;

-- 4. User Roles Mappings
INSERT INTO "UserRole" ("UserId", "RoleId") VALUES
    ('019f8000-0000-7000-8000-000000000010', '019f7f8a-0000-7000-8000-000000000002'),
    ('019f8000-0000-7000-8000-000000000010', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-000000000010', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000013', '019f7f8a-0000-7000-8000-000000000002'),
    ('019f8000-0000-7000-8000-000000000013', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-000000000013', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000012', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-000000000012', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000014', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-000000000014', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000016', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-000000000016', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000018', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-000000000018', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-00000000001a', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-00000000001a', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-00000000001c', '019f7f8a-0000-7000-8000-000000000003'),
    ('019f8000-0000-7000-8000-00000000001c', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000011', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-00000000001e', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000015', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000017', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-000000000019', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-00000000001b', '019f7f8a-0000-7000-8000-000000000004'),
    ('019f8000-0000-7000-8000-00000000001d', '019f7f8a-0000-7000-8000-000000000004')
ON CONFLICT ("UserId", "RoleId") DO NOTHING;

-- 5. Management Scopes for Leadership
INSERT INTO "ManagementScope" ("ManagementScopeId", "TenantId", "UserId", "DepartmentId", "IncludeDescendants", "CreatedBy", "CreatedAt")
VALUES
    ('019f8000-0000-7000-8000-000000000020', '019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-000000000010', '019f8000-0000-7000-8000-000000000004', TRUE, '019f8000-0000-7000-8000-000000000010', NOW())
ON CONFLICT ("ManagementScopeId") DO NOTHING;

INSERT INTO "ManagementScope" ("ManagementScopeId", "TenantId", "UserId", "DepartmentId", "IncludeDescendants", "CreatedBy", "CreatedAt")
VALUES
    ('019f8000-0000-7000-8000-000000000022', '019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-000000000013', '019f8000-0000-7000-8000-000000000004', TRUE, '019f8000-0000-7000-8000-000000000010', NOW())
ON CONFLICT ("ManagementScopeId") DO NOTHING;

INSERT INTO "ManagementScope" ("ManagementScopeId", "TenantId", "UserId", "DepartmentId", "IncludeDescendants", "CreatedBy", "CreatedAt")
VALUES
    ('019f8000-0000-7000-8000-000000000021', '019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-000000000012', '019f8000-0000-7000-8000-000000000003', TRUE, '019f8000-0000-7000-8000-000000000010', NOW()),
    ('019f8000-0000-7000-8000-000000000023', '019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-000000000014', '019f8000-0000-7000-8000-000000000005', TRUE, '019f8000-0000-7000-8000-000000000010', NOW()),
    ('019f8000-0000-7000-8000-000000000024', '019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-000000000016', '019f8000-0000-7000-8000-000000000006', TRUE, '019f8000-0000-7000-8000-000000000010', NOW()),
    ('019f8000-0000-7000-8000-000000000025', '019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-000000000018', '019f8000-0000-7000-8000-000000000007', TRUE, '019f8000-0000-7000-8000-000000000010', NOW()),
    ('019f8000-0000-7000-8000-000000000026', '019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-00000000001a', '019f8000-0000-7000-8000-000000000008', TRUE, '019f8000-0000-7000-8000-000000000010', NOW()),
    ('019f8000-0000-7000-8000-000000000027', '019f8000-0000-7000-8000-000000000002', '019f8000-0000-7000-8000-00000000001c', '019f8000-0000-7000-8000-000000000009', TRUE, '019f8000-0000-7000-8000-000000000010', NOW())
ON CONFLICT ("ManagementScopeId") DO NOTHING;

-- 6. Services & Service Categories
INSERT INTO "Service" ("ServiceId", "TenantId", "Code", "Name", "Description", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000030', '019f8000-0000-7000-8000-000000000002', 'IT-SUPPORT', 'Hỗ trợ Kỹ thuật & Hạ tầng CNTT', 'Dịch vụ tiếp nhận và xử lý sự cố CNTT nội bộ doanh nghiệp', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("ServiceId") DO UPDATE SET "Name" = EXCLUDED."Name", "Description" = EXCLUDED."Description";

INSERT INTO "ServiceCategory" ("ServiceCategoryId", "ServiceId", "Code", "Name", "Status")
VALUES
    ('019f8000-0000-7000-8000-000000000031', '019f8000-0000-7000-8000-000000000030', 'HARDWARE', 'Sự cố thiết bị phần cứng & máy trạm', 'ACTIVE'),
    ('019f8000-0000-7000-8000-00000000003b', '019f8000-0000-7000-8000-000000000030', 'SOFTWARE', 'Cài đặt phần mềm, cấp quyền tài khoản hệ thống', 'ACTIVE')
ON CONFLICT ("ServiceCategoryId") DO UPDATE SET "Name" = EXCLUDED."Name";

INSERT INTO "Service" ("ServiceId", "TenantId", "Code", "Name", "Description", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000032', '019f8000-0000-7000-8000-000000000002', 'HR-PORTAL', 'Dịch vụ Hành chính - Nhân sự (HR Portal)', 'Cổng tiếp nhận đề xuất nhân sự, phúc lợi và trang thiết bị', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("ServiceId") DO UPDATE SET "Name" = EXCLUDED."Name", "Description" = EXCLUDED."Description";

INSERT INTO "ServiceCategory" ("ServiceCategoryId", "ServiceId", "Code", "Name", "Status")
VALUES
    ('019f8000-0000-7000-8000-000000000033', '019f8000-0000-7000-8000-000000000032', 'ONBOARDING', 'Cấp phát trang thiết bị & Onboarding nhân sự mới', 'ACTIVE'),
    ('019f8000-0000-7000-8000-000000000034', '019f8000-0000-7000-8000-000000000032', 'LEAVE_REQ', 'Nghỉ phép, chế độ bảo hiểm và phúc lợi lao động', 'ACTIVE')
ON CONFLICT ("ServiceCategoryId") DO UPDATE SET "Name" = EXCLUDED."Name";

INSERT INTO "Service" ("ServiceId", "TenantId", "Code", "Name", "Description", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000035', '019f8000-0000-7000-8000-000000000002', 'FIN-EXPENSE', 'Dịch vụ Tài chính - Kế toán & Mua sắm', 'Tiếp nhận tạm ứng công tác phí và đề xuất mua sắm tài sản doanh nghiệp', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("ServiceId") DO UPDATE SET "Name" = EXCLUDED."Name", "Description" = EXCLUDED."Description";

INSERT INTO "ServiceCategory" ("ServiceCategoryId", "ServiceId", "Code", "Name", "Status")
VALUES
    ('019f8000-0000-7000-8000-000000000036', '019f8000-0000-7000-8000-000000000035', 'TRAVEL_ADVANCE', 'Tạm ứng & Quyết toán công tác phí', 'ACTIVE'),
    ('019f8000-0000-7000-8000-000000000037', '019f8000-0000-7000-8000-000000000035', 'PROCUREMENT', 'Đề xuất mua sắm công cụ, vật tư và bản quyền phần mềm', 'ACTIVE')
ON CONFLICT ("ServiceCategoryId") DO UPDATE SET "Name" = EXCLUDED."Name";

INSERT INTO "Service" ("ServiceId", "TenantId", "Code", "Name", "Description", "Status", "CreatedAt", "UpdatedAt")
VALUES ('019f8000-0000-7000-8000-000000000038', '019f8000-0000-7000-8000-000000000002', 'MKT-DESIGN', 'Dịch vụ Truyền thông & Thiết kế Tiếp thị', 'Yêu cầu ấn phẩm thiết kế, banner sự kiện và truyền thông báo chí', 'ACTIVE', NOW(), NOW())
ON CONFLICT ("ServiceId") DO UPDATE SET "Name" = EXCLUDED."Name", "Description" = EXCLUDED."Description";

INSERT INTO "ServiceCategory" ("ServiceCategoryId", "ServiceId", "Code", "Name", "Status")
VALUES
    ('019f8000-0000-7000-8000-000000000039', '019f8000-0000-7000-8000-000000000038', 'BRAND_ASSET', 'Yêu cầu ấn phẩm thiết kế, banner sự kiện', 'ACTIVE'),
    ('019f8000-0000-7000-8000-00000000003a', '019f8000-0000-7000-8000-000000000038', 'PRESS_RELEASE', 'Soạn thảo & Đăng tải thông cáo báo chí, tin tức', 'ACTIVE')
ON CONFLICT ("ServiceCategoryId") DO UPDATE SET "Name" = EXCLUDED."Name";

-- 7. Cross-Department Requests
INSERT INTO "Request" (
    "RequestId", "TenantId", "RequesterId", "ServiceId", "CategoryId", "Title", "Description",
    "Priority", "Status", "WorkflowVersionId", "SLAVersionId", "ResolvedAt", "ClosedAt", "CreatedAt", "UpdatedAt", "DeletedAt"
)
VALUES (
    '019f8000-0000-7000-8000-000000000050',
    '019f8000-0000-7000-8000-000000000002',
    '019f8000-0000-7000-8000-00000000001b',
    '019f8000-0000-7000-8000-000000000032',
    '019f8000-0000-7000-8000-000000000033',
    'Yêu cầu cấp phát laptop cấu hình cao cho Kỹ sư Full-Stack mới',
    'Yêu cầu cấp phát 01 laptop Dell XPS 15 (32GB RAM, 1TB SSD) cho Kỹ sư Full-stack chuẩn bị gia nhập công ty vào thứ Hai tuần tới.',
    'HIGH',
    'IN_PROGRESS',
    NULL, NULL, NULL, NULL,
    NOW() - INTERVAL '2 days', NOW(), NULL
) ON CONFLICT ("RequestId") DO UPDATE SET "Title" = EXCLUDED."Title", "Description" = EXCLUDED."Description", "Status" = 'IN_PROGRESS';

INSERT INTO "Request" (
    "RequestId", "TenantId", "RequesterId", "ServiceId", "CategoryId", "Title", "Description",
    "Priority", "Status", "WorkflowVersionId", "SLAVersionId", "ResolvedAt", "ClosedAt", "CreatedAt", "UpdatedAt", "DeletedAt"
)
VALUES (
    '019f8000-0000-7000-8000-000000000051',
    '019f8000-0000-7000-8000-000000000002',
    '019f8000-0000-7000-8000-000000000015',
    '019f8000-0000-7000-8000-000000000035',
    '019f8000-0000-7000-8000-000000000036',
    'Tạm ứng công tác phí hội nghị Chuyển đổi số Quốc gia tại Đà Nẵng',
    'Đề nghị tạm ứng số tiền 15.000.000 VNĐ phục vụ chuyến công tác 3 ngày 2 đêm gặp gỡ đối tác và đại diện gian hàng triển lãm BizFlow.',
    'MEDIUM',
    'SUBMITTED',
    NULL, NULL, NULL, NULL,
    NOW() - INTERVAL '1 day', NOW(), NULL
) ON CONFLICT ("RequestId") DO UPDATE SET "Title" = EXCLUDED."Title", "Description" = EXCLUDED."Description", "Status" = 'SUBMITTED';

INSERT INTO "Request" (
    "RequestId", "TenantId", "RequesterId", "ServiceId", "CategoryId", "Title", "Description",
    "Priority", "Status", "WorkflowVersionId", "SLAVersionId", "ResolvedAt", "ClosedAt", "CreatedAt", "UpdatedAt", "DeletedAt"
)
VALUES (
    '019f8000-0000-7000-8000-000000000052',
    '019f8000-0000-7000-8000-000000000002',
    '019f8000-0000-7000-8000-00000000001a',
    '019f8000-0000-7000-8000-000000000038',
    '019f8000-0000-7000-8000-000000000039',
    'Thiết kế bộ banner và standee cho sự kiện Kick-off Q4',
    'Cần thiết kế bộ ấn phẩm gồm 01 backdrop sân khấu chính (6m x 3m) và 02 standee đứng chào đón nhân viên tại sảnh chính tòa nhà.',
    'MEDIUM',
    'RESOLVED',
    NULL, NULL, NOW() - INTERVAL '3 hours', NULL,
    NOW() - INTERVAL '3 days', NOW(), NULL
) ON CONFLICT ("RequestId") DO UPDATE SET "Title" = EXCLUDED."Title", "Description" = EXCLUDED."Description", "Status" = 'RESOLVED';

-- 8. Enterprise Work Tasks Lifecycle
-- First step: Ensure tasks exist in DRAFT state so Checklists can be updated / inserted
INSERT INTO "Task" (
    "TaskId", "TenantId", "RequestId", "CreatorId", "Title", "Description",
    "Priority", "Deadline", "Status", "WorkflowVersionId", "SLAVersionId",
    "CompletedAt", "CreatedAt", "UpdatedAt", "DeletedAt"
)
VALUES
    ('019f8000-0000-7000-8000-000000000040', '019f8000-0000-7000-8000-000000000002', NULL, '019f8000-0000-7000-8000-000000000010', 'Triển khai hạ tầng và thử nghiệm tính năng BizFlow AI', 'Thử nghiệm các tính năng AI cốt lõi: Phân rã nhiệm vụ (Task Breakdown), Đánh giá rủi ro (Risk Assessment), Gợi ý nhân sự tối ưu (Assignment Recommendation) và Tóm tắt công việc (Summary).', 'HIGH', NOW() + INTERVAL '3 days', 'DRAFT', NULL, NULL, NULL, NOW() - INTERVAL '2 days', NOW(), NULL),
    ('019f8000-0000-7000-8000-000000000041', '019f8000-0000-7000-8000-000000000002', NULL, '019f8000-0000-7000-8000-000000000010', 'Tổ chức chiến dịch ra mắt sản phẩm Q4/2026 - BizFlow Enterprise', 'Chuẩn bị toàn diện tài liệu truyền thông, bộ nhận diện chiến dịch và landing page bán hàng cho giải pháp quản trị luồng công việc doanh nghiệp.', 'CRITICAL', NOW() + INTERVAL '1 day', 'DRAFT', NULL, NULL, NULL, NOW() - INTERVAL '1 day', NOW(), NULL),
    ('019f8000-0000-7000-8000-000000000042', '019f8000-0000-7000-8000-000000000002', NULL, '019f8000-0000-7000-8000-000000000013', 'Đàm phán hợp đồng cung cấp giải pháp chuyển đổi số với Tập đoàn ABC', 'Hoàn thiện báo giá chi tiết, kế hoạch triển khai giai đoạn 1 và thống nhất điều khoản bảo mật dữ liệu khách hàng với ban Pháp chế Tập đoàn ABC.', 'HIGH', NOW() + INTERVAL '7 days', 'DRAFT', NULL, NULL, NULL, NOW() - INTERVAL '3 days', NOW(), NULL),
    ('019f8000-0000-7000-8000-000000000043', '019f8000-0000-7000-8000-000000000002', NULL, '019f8000-0000-7000-8000-000000000010', 'Quyết toán báo cáo tài chính quý 3 và lập kế hoạch ngân sách năm 2027', 'Đối soát số dư công nợ khách hàng, kiểm toán chi phí vận hành các phòng ban và dự thảo hạn mức chi tiêu hoạt động cho năm tài chính 2027.', 'HIGH', NOW() + INTERVAL '10 days', 'DRAFT', NULL, NULL, NULL, NOW() - INTERVAL '2 days', NOW(), NULL),
    ('019f8000-0000-7000-8000-000000000044', '019f8000-0000-7000-8000-000000000002', NULL, '019f8000-0000-7000-8000-000000000010', 'Tuyển dụng 03 Kỹ sư Full-Stack Senior và Onboarding đợt 2 tháng 11', 'Tiến hành sàn lọc hồ sơ ứng viên, điều phối phỏng vấn kỹ thuật cùng phòng Công nghệ và chuẩn bị hợp đồng lao động thử việc.', 'MEDIUM', NOW() + INTERVAL '5 days', 'DRAFT', NULL, NULL, NULL, NOW() - INTERVAL '4 days', NOW(), NULL),
    ('019f8000-0000-7000-8000-000000000045', '019f8000-0000-7000-8000-000000000002', NULL, '019f8000-0000-7000-8000-000000000013', 'Thiết lập quy trình chăm sóc khách hàng VIP và khảo sát CSAT kỳ 2', 'Tổng hợp danh sách 50 khách hàng doanh nghiệp chiến lược, gửi thư tri ân và tiến hành khảo sát mức độ hài lòng về chất lượng hỗ trợ.', 'MEDIUM', NOW() - INTERVAL '1 day', 'DRAFT', NULL, NULL, NULL, NOW() - INTERVAL '7 days', NOW() - INTERVAL '1 day', NULL)
ON CONFLICT ("TaskId") DO UPDATE SET "Title" = EXCLUDED."Title", "Description" = EXCLUDED."Description", "Priority" = EXCLUDED."Priority";

-- Temporarily set all to DRAFT for checklist insertion
UPDATE "Task" SET "Status" = 'DRAFT' WHERE "TaskId" IN (
    '019f8000-0000-7000-8000-000000000040',
    '019f8000-0000-7000-8000-000000000041',
    '019f8000-0000-7000-8000-000000000042',
    '019f8000-0000-7000-8000-000000000043',
    '019f8000-0000-7000-8000-000000000044',
    '019f8000-0000-7000-8000-000000000045'
);

-- 9. Checklists
INSERT INTO "TaskChecklistItem" ("ChecklistItemId", "TaskId", "Title", "SortOrder", "IsCompleted", "CompletedBy", "CompletedAt")
VALUES
    ('019f8000-0000-7000-8000-000000000070', '019f8000-0000-7000-8000-000000000040', 'Thiết lập cấu hình PostgreSQL & pgvector schema', 1, FALSE, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000071', '019f8000-0000-7000-8000-000000000040', 'Triển khai kiểm thử giao diện Trợ lý Robot AI', 2, FALSE, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000072', '019f8000-0000-7000-8000-000000000040', 'Đánh giá hiệu năng Vector Memory và độ trễ phản hồi', 3, FALSE, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000073', '019f8000-0000-7000-8000-000000000041', 'Hoàn thiện Landing Page và video demo sản phẩm', 1, FALSE, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000074', '019f8000-0000-7000-8000-000000000041', 'Gửi thông cáo báo chí đến các cơ quan truyền thông', 2, FALSE, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000075', '019f8000-0000-7000-8000-000000000041', 'Chuẩn bị tài liệu bán hàng (Sales Kit) cho bộ phận Kinh doanh', 3, FALSE, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000076', '019f8000-0000-7000-8000-000000000045', 'Phân loại danh sách 50 khách hàng doanh nghiệp VIP', 1, FALSE, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000077', '019f8000-0000-7000-8000-000000000045', 'Gọi điện thăm hỏi và gửi phiếu khảo sát mức độ hài lòng', 2, FALSE, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000078', '019f8000-0000-7000-8000-000000000045', 'Tổng hợp báo cáo chỉ số CSAT và đề xuất cải tiến dịch vụ', 3, FALSE, NULL, NULL)
ON CONFLICT ("ChecklistItemId") DO UPDATE SET "Title" = EXCLUDED."Title";

-- 10. Transition Tasks to ASSIGNED
UPDATE "Task" SET "Status" = 'ASSIGNED' WHERE "TaskId" IN (
    '019f8000-0000-7000-8000-000000000040',
    '019f8000-0000-7000-8000-000000000041',
    '019f8000-0000-7000-8000-000000000042',
    '019f8000-0000-7000-8000-000000000043',
    '019f8000-0000-7000-8000-000000000044',
    '019f8000-0000-7000-8000-000000000045'
);

-- 11. Task Assignments
INSERT INTO "TaskAssignment" ("TaskAssignmentId", "TaskId", "DepartmentId", "UserId", "AssignedBy", "AssignedAt", "AcceptedAt", "RejectedAt", "RejectionReason", "EndedAt")
VALUES
    ('019f8000-0000-7000-8000-000000000060', '019f8000-0000-7000-8000-000000000040', '019f8000-0000-7000-8000-000000000003', '019f8000-0000-7000-8000-000000000012', '019f8000-0000-7000-8000-000000000010', NOW() - INTERVAL '2 days', NULL, NULL, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000061', '019f8000-0000-7000-8000-000000000041', '019f8000-0000-7000-8000-000000000006', '019f8000-0000-7000-8000-000000000016', '019f8000-0000-7000-8000-000000000010', NOW() - INTERVAL '1 day', NULL, NULL, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000062', '019f8000-0000-7000-8000-000000000042', '019f8000-0000-7000-8000-000000000005', '019f8000-0000-7000-8000-000000000015', '019f8000-0000-7000-8000-000000000013', NOW() - INTERVAL '3 days', NULL, NULL, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000063', '019f8000-0000-7000-8000-000000000043', '019f8000-0000-7000-8000-000000000007', '019f8000-0000-7000-8000-000000000019', '019f8000-0000-7000-8000-000000000010', NOW() - INTERVAL '2 days', NULL, NULL, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000064', '019f8000-0000-7000-8000-000000000044', '019f8000-0000-7000-8000-000000000008', '019f8000-0000-7000-8000-00000000001b', '019f8000-0000-7000-8000-000000000010', NOW() - INTERVAL '4 days', NULL, NULL, NULL, NULL),
    ('019f8000-0000-7000-8000-000000000065', '019f8000-0000-7000-8000-000000000045', '019f8000-0000-7000-8000-000000000009', '019f8000-0000-7000-8000-00000000001d', '019f8000-0000-7000-8000-000000000013', NOW() - INTERVAL '7 days', NULL, NULL, NULL, NULL)
ON CONFLICT ("TaskAssignmentId") DO NOTHING;

-- 12. Accept assignments for tasks that are in progress or finished
UPDATE "TaskAssignment" SET "AcceptedAt" = NOW() - INTERVAL '2 days' WHERE "TaskAssignmentId" = '019f8000-0000-7000-8000-000000000060';
UPDATE "TaskAssignment" SET "AcceptedAt" = NOW() - INTERVAL '3 days' WHERE "TaskAssignmentId" = '019f8000-0000-7000-8000-000000000062';
UPDATE "TaskAssignment" SET "AcceptedAt" = NOW() - INTERVAL '4 days' WHERE "TaskAssignmentId" = '019f8000-0000-7000-8000-000000000064';
UPDATE "TaskAssignment" SET "AcceptedAt" = NOW() - INTERVAL '7 days' WHERE "TaskAssignmentId" = '019f8000-0000-7000-8000-000000000065';

-- 13. Update Task statuses
UPDATE "Task" SET "Status" = 'IN_PROGRESS' WHERE "TaskId" IN (
    '019f8000-0000-7000-8000-000000000040',
    '019f8000-0000-7000-8000-000000000042',
    '019f8000-0000-7000-8000-000000000044'
);

UPDATE "Task" SET "Status" = 'CONFIRMED', "CompletedAt" = NOW() - INTERVAL '1 day' WHERE "TaskId" = '019f8000-0000-7000-8000-000000000045';

-- 14. Mark completed checklist items
UPDATE "TaskChecklistItem"
SET "IsCompleted" = TRUE, "CompletedBy" = '019f8000-0000-7000-8000-000000000012', "CompletedAt" = NOW() - INTERVAL '1 day'
WHERE "ChecklistItemId" = '019f8000-0000-7000-8000-000000000070';

UPDATE "TaskChecklistItem"
SET "IsCompleted" = TRUE, "CompletedBy" = '019f8000-0000-7000-8000-000000000011', "CompletedAt" = NOW() - INTERVAL '12 hours'
WHERE "ChecklistItemId" = '019f8000-0000-7000-8000-000000000071';

UPDATE "TaskChecklistItem"
SET "IsCompleted" = TRUE, "CompletedBy" = '019f8000-0000-7000-8000-00000000001d', "CompletedAt" = NOW() - INTERVAL '2 days'
WHERE "ChecklistItemId" = '019f8000-0000-7000-8000-000000000076';

UPDATE "TaskChecklistItem"
SET "IsCompleted" = TRUE, "CompletedBy" = '019f8000-0000-7000-8000-00000000001d', "CompletedAt" = NOW() - INTERVAL '1 day'
WHERE "ChecklistItemId" = '019f8000-0000-7000-8000-000000000077';

UPDATE "TaskChecklistItem"
SET "IsCompleted" = TRUE, "CompletedBy" = '019f8000-0000-7000-8000-00000000001d', "CompletedAt" = NOW() - INTERVAL '1 day'
WHERE "ChecklistItemId" = '019f8000-0000-7000-8000-000000000078';

-- 15. Corporate AI Vector Knowledge Base (AIVectorMemory)
INSERT INTO "AIVectorMemory" (
    "MemoryId", "TenantId", "MemoryScope", "Title", "Content", "MetadataJson", "EmbeddingJson", "CreatedAt", "UpdatedAt"
)
VALUES
    (
        '019f8000-0000-7000-8000-000000000080',
        '019f8000-0000-7000-8000-000000000002',
        'POLICY',
        'Chính sách tạm ứng, thanh toán và quyết toán công tác phí',
        'Quy định tạm ứng và quyết toán công tác phí tại BizFlow Corporation: 1. Nhân viên được tạm ứng tối đa 80% chi phí dự kiến trước ngày khởi hành ít nhất 3 ngày làm việc. 2. Định mức lưu trú khách sạn: Cán bộ quản lý tối đa 1.500.000 VNĐ/đêm, nhân viên tối đa 1.000.000 VNĐ/đêm tại Hà Nội và TP.HCM; các tỉnh thành khác tối đa 800.000 VNĐ/đêm. 3. Vé máy bay: Mặc định hạng Phổ thông (Economy), ưu tiên đặt trước ít nhất 7 ngày. 4. Thời hạn hoàn ứng: Nộp đầy đủ hóa đơn GTGT hợp lệ và bảng kê chi phí trong vòng 7 ngày làm việc kể từ ngày kết thúc công tác về Phòng Tài chính - Kế toán.',
        '{{"category": "FINANCE", "department": "FIN", "version": "2026.1"}}'::jsonb,
        '{emb1}'::jsonb,
        NOW() - INTERVAL '10 days', NOW()
    ),
    (
        '019f8000-0000-7000-8000-000000000081',
        '019f8000-0000-7000-8000-000000000002',
        'POLICY',
        'Quy định làm việc từ xa và văn hóa công sở BizFlow',
        'Quy định làm việc từ xa (Remote Work) tại BizFlow Corporation: 1. Nhân sự đã qua thử việc được đăng ký tối đa 2 ngày làm việc từ xa mỗi tuần với sự phê duyệt của Quản lý phòng ban. 2. Khung giờ làm việc cốt lõi bắt buộc phải trực tuyến trên hệ thống và kênh chat nội bộ: Sáng 08:30 - 12:00, Chiều 13:30 - 17:30. 3. Tuân thủ bảo mật: Không sử dụng mạng Wi-Fi công cộng không có bảo mật để truy cập cơ sở dữ liệu nội bộ. Phải sử dụng VPN công ty khi cần truy cập hạ tầng backend.',
        '{{"category": "HR", "department": "HR", "version": "2026.2"}}'::jsonb,
        '{emb2}'::jsonb,
        NOW() - INTERVAL '10 days', NOW()
    ),
    (
        '019f8000-0000-7000-8000-000000000082',
        '019f8000-0000-7000-8000-000000000002',
        'SLA',
        'Quy trình tiếp nhận và xử lý yêu cầu CNTT (IT Service Desk SLA)',
        'Cam kết chất lượng dịch vụ CNTT (IT Service Level Agreement - SLA): 1. Sự cố mức P1 (Hệ thống ngừng hoạt động, ảnh hưởng toàn bộ doanh nghiệp): Thời gian phản hồi dưới 15 phút, thời gian khắc phục dưới 2 giờ. 2. Sự cố mức P2 (Suy giảm tính năng quan trọng của phòng ban): Phản hồi dưới 30 phút, giải quyết dưới 4 giờ. 3. Yêu cầu cấp phát phần cứng, tài khoản mới (P3): Xử lý trong vòng 24 giờ làm việc. Tất cả yêu cầu cần được tạo trên hệ thống BizFlow Service Desk.',
        '{{"category": "IT", "department": "TECH", "version": "2026.1"}}'::jsonb,
        '{emb3}'::jsonb,
        NOW() - INTERVAL '10 days', NOW()
    ),
    (
        '019f8000-0000-7000-8000-000000000083',
        '019f8000-0000-7000-8000-000000000002',
        'SECURITY',
        'Chính sách an toàn thông tin và bảo mật tài khoản người dùng',
        'Chính sách an toàn thông tin BizFlow: 1. Mật khẩu tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt. Phải thay đổi mật khẩu định kỳ tối thiểu 90 ngày một lần. 2. Tuyệt đối không chia sẻ tài khoản, mã xác thực 2FA hoặc thông tin đăng nhập cho người khác. 3. Khóa màn hình máy tính khi rời khỏi bàn làm việc (Windows + L). 4. Nghiêm cấm lưu trữ dữ liệu khách hàng hoặc tài liệu mật trên thiết bị cá nhân chưa được phòng CNTT mã hóa.',
        '{{"category": "SECURITY", "department": "TECH", "version": "2026.3"}}'::jsonb,
        '{emb4}'::jsonb,
        NOW() - INTERVAL '10 days', NOW()
    ),
    (
        '019f8000-0000-7000-8000-000000000084',
        '019f8000-0000-7000-8000-000000000002',
        'PROCEDURE',
        'Quy trình Onboarding và cấp phát tài sản cho nhân sự mới',
        'Quy trình tiếp nhận và đào tạo nhân viên mới (Onboarding Flow): 1. Trước ngày gia nhập 3 ngày: Phòng Nhân sự gửi yêu cầu cấp phát laptop, tạo email doanh nghiệp và thẻ ra vào cho Phòng CNTT. 2. Ngày đầu tiên (Day 1): Gặp gỡ người hướng dẫn (Buddy), tham quan văn phòng, ký kết hợp đồng thử việc và nhận bàn giao tài sản. 3. Tuần đầu tiên: Hoàn thành khóa học trực tuyến về Quy chế văn hóa doanh nghiệp và Hướng dẫn sử dụng nền tảng BizFlow. 4. Sau 60 ngày: Phòng Nhân sự tổ chức đánh giá kết quả thử việc cùng Trưởng phòng ban.',
        '{{"category": "HR", "department": "HR", "version": "2026.1"}}'::jsonb,
        '{emb5}'::jsonb,
        NOW() - INTERVAL '10 days', NOW()
    )
ON CONFLICT ("MemoryId") DO UPDATE SET "Title" = EXCLUDED."Title", "Content" = EXCLUDED."Content", "EmbeddingJson" = EXCLUDED."EmbeddingJson";
"""

out_path = os.path.join("deploy", "seed_enterprise_simulation.sql")
with open(out_path, "w", encoding="utf-8") as f:
    f.write(sql)

print(f"Generated {out_path} with {len(sql)} characters in UTF-8.")

