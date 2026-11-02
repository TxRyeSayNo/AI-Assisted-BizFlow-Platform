using System.Text.Json;
using System.Text.RegularExpressions;
using BizFlow.Application.Common;
using BizFlow.Application.Security;

namespace BizFlow.Application.AI;

public sealed class AiTaskDecompositionService(
    ITenantContext tenantContext,
    IResourceAuthorizer authorizer,
    IAiContextReader contextReader,
    ITaskDecompositionStore store,
    TimeProvider clock,
    IAiModelClient? modelClient = null) : IAiTaskDecompositionService
{
    private Guid GetRequiredTenantId() =>
        tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "TENANT.REQUIRED", "An active tenant context is required.");

    private Guid GetRequiredUserId() =>
        tenantContext.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "USER.REQUIRED", "An authenticated user is required.");

    public async Task<TaskDecompositionPreviewResult> PreviewAsync(
        TaskDecompositionPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        _ = GetRequiredUserId();

        if (string.IsNullOrWhiteSpace(command.Prompt))
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Task description cannot be empty.");
        }

        var prompt = command.Prompt.Trim();
        if (prompt.Length > 4000)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Task prompt cannot exceed 4000 characters.");
        }

        var context = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);
        var proposals = new List<TaskDecompositionProposalDto>();

        if (modelClient?.IsConfigured == true)
        {
            try
            {
                var deptSummary = string.Join(", ", context.Departments.Select(d => $"{d.DepartmentId}:{d.Name}"));
                var sysInstructions = $"""
                    You are BizFlow AI Project Manager. Decompose the user's work instruction into structured task items.
                    Each item must be either:
                    - "SupportRequest": cross-department assistance, service request, IT help, equipment, or approvals.
                    - "Assignment": direct execution work, drafting, internal tasks, deliverables, or reviews.

                    Available departments in tenant: [{deptSummary}].
                    Match each task to the most appropriate departmentId and departmentName.

                    Return ONLY a JSON array of objects with:
                    - "title": concise task title (string, max 100 chars)
                    - "description": clear instructions and deliverables (string)
                    - "type": "SupportRequest" or "Assignment"
                    - "departmentId": string UUID from available departments or null
                    - "departmentName": string department name or null
                    - "priority": "LOW", "MEDIUM", or "HIGH"
                    - "checklistItems": array of short step-by-step strings
                    - "reasoning": brief explanation of why this task is structured this way
                    """;

                var aiResp = await modelClient.CompleteAsync(prompt, sysInstructions, cancellationToken);
                using var doc = JsonDocument.Parse(aiResp.Content);
                var rootArray = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement
                    : (doc.RootElement.TryGetProperty("proposals", out var pArray) && pArray.ValueKind == JsonValueKind.Array
                        ? pArray
                        : (doc.RootElement.TryGetProperty("tasks", out var tArray) && tArray.ValueKind == JsonValueKind.Array
                            ? tArray
                            : (doc.RootElement.TryGetProperty("items", out var iArray) && iArray.ValueKind == JsonValueKind.Array
                                ? iArray
                                : default)));

                if (rootArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in rootArray.EnumerateArray())
                    {
                        var title = elem.TryGetProperty("title", out var tp) ? tp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        var desc = elem.TryGetProperty("description", out var dp) ? dp.GetString() : "";
                        var type = elem.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "Assignment";
                        if (type != "SupportRequest" && type != "Assignment") type = "Assignment";

                        var priority = elem.TryGetProperty("priority", out var priProp) ? priProp.GetString()?.ToUpperInvariant() : "MEDIUM";
                        if (priority != "LOW" && priority != "HIGH") priority = "MEDIUM";

                        Guid? deptId = null;
                        string? deptName = null;
                        if (elem.TryGetProperty("departmentId", out var didProp) && Guid.TryParse(didProp.GetString(), out var parsedDid))
                        {
                            var matched = context.Departments.FirstOrDefault(d => d.DepartmentId == parsedDid);
                            if (matched != null)
                            {
                                deptId = matched.DepartmentId;
                                deptName = matched.Name;
                            }
                        }

                        if (deptId == null && elem.TryGetProperty("departmentName", out var dnameProp))
                        {
                            var nameStr = dnameProp.GetString();
                            var matched = context.Departments.FirstOrDefault(d => d.Name.Contains(nameStr ?? "", StringComparison.OrdinalIgnoreCase));
                            if (matched != null)
                            {
                                deptId = matched.DepartmentId;
                                deptName = matched.Name;
                            }
                        }

                        var checklist = new List<string>();
                        if (elem.TryGetProperty("checklistItems", out var clProp) && clProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var clItem in clProp.EnumerateArray())
                            {
                                var s = clItem.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) checklist.Add(s.Trim());
                            }
                        }

                        var reasoning = elem.TryGetProperty("reasoning", out var rp) ? rp.GetString() : null;

                        proposals.Add(new TaskDecompositionProposalDto(
                            Title: title.Trim(),
                            Description: desc ?? "",
                            Type: type,
                            DepartmentId: deptId,
                            DepartmentName: deptName ?? (context.Departments.FirstOrDefault()?.Name ?? "General"),
                            Priority: priority ?? "MEDIUM",
                            ChecklistItems: checklist,
                            Reasoning: reasoning));
                    }
                }
            }
            catch
            {
                // Fall back to deterministic rule-based decomposition
            }
        }

        if (proposals.Count == 0)
        {
            proposals.AddRange(DecomposeDeterministically(prompt, context));
        }

        // BR-028 Invariant: Pure preview, absolutely NO database records created or modified.
        return new TaskDecompositionPreviewResult(prompt, proposals);
    }

    public async Task<ConfirmTaskDecompositionResponse> ConfirmAsync(
        ConfirmTaskDecompositionCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        if (command.Items == null || command.Items.Count == 0)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "At least one task item must be selected for confirmation.");
        }

        if (command.Items.Count > 50)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Cannot confirm more than 50 items in a single batch.");
        }

        // Validate items and verify required permissions
        var hasAssignment = command.Items.Any(i => string.Equals(i.Type, "Assignment", StringComparison.OrdinalIgnoreCase));
        var hasSupportRequest = command.Items.Any(i => string.Equals(i.Type, "SupportRequest", StringComparison.OrdinalIgnoreCase));

        if (hasAssignment)
        {
            try
            {
                await authorizer.AuthorizeAsync("tasks.create", new(tenantId), cancellationToken: cancellationToken);
            }
            catch (ApplicationFault)
            {
                // Fallback: If caller only has requests.create, convert assignment items to SupportRequest
                try
                {
                    await authorizer.AuthorizeAsync("requests.create", new(tenantId), cancellationToken: cancellationToken);
                    command = command with
                    {
                        Items = command.Items.Select(i => string.Equals(i.Type, "Assignment", StringComparison.OrdinalIgnoreCase)
                            ? i with { Type = "SupportRequest" }
                            : i).ToList()
                    };
                    hasAssignment = false;
                    hasSupportRequest = true;
                }
                catch
                {
                    throw;
                }
            }
        }

        if (hasSupportRequest)
        {
            try
            {
                await authorizer.AuthorizeAsync("requests.create", new(tenantId), cancellationToken: cancellationToken);
            }
            catch (ApplicationFault)
            {
                // Fallback: convert any SupportRequest items to Assignment and authorize tasks.create
                command = command with
                {
                    Items = command.Items.Select(i => string.Equals(i.Type, "SupportRequest", StringComparison.OrdinalIgnoreCase)
                        ? i with { Type = "Assignment" }
                        : i).ToList()
                };
                if (!hasAssignment)
                {
                    await authorizer.AuthorizeAsync("tasks.create", new(tenantId), cancellationToken: cancellationToken);
                }
            }
        }

        foreach (var item in command.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 300)
            {
                throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Task title must be between 1 and 300 characters.");
            }

            if (!string.Equals(item.Type, "Assignment", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Type, "SupportRequest", StringComparison.OrdinalIgnoreCase))
            {
                throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Task type must be either 'Assignment' or 'SupportRequest'.");
            }
        }

        var now = clock.GetUtcNow();
        var created = await store.PersistDecompositionBatchAsync(tenantId, userId, command.Items, now, cancellationToken);

        return new ConfirmTaskDecompositionResponse(created.Count, created);
    }

    private static IReadOnlyList<TaskDecompositionProposalDto> DecomposeDeterministically(
        string prompt,
        AiTenantContextData context)
    {
        var proposals = new List<TaskDecompositionProposalDto>();
        var lines = prompt.Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 3)
            .ToList();

        if (lines.Count <= 1)
        {
            var pLower = prompt.ToLowerInvariant();
            var adminDept = context.Departments.FirstOrDefault(d => d.Name.Contains("Hành chính", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Nhân sự", StringComparison.OrdinalIgnoreCase)) ?? context.Departments.FirstOrDefault();
            var itDept = context.Departments.FirstOrDefault(d => d.Name.Contains("Thông tin", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("IT", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Kỹ thuật", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Công nghệ", StringComparison.OrdinalIgnoreCase)) ?? context.Departments.FirstOrDefault();
            var execDept = context.Departments.FirstOrDefault(d => d.Name.Contains("Điều hành", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Kế hoạch", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Tổng hợp", StringComparison.OrdinalIgnoreCase)) ?? context.Departments.FirstOrDefault();
            var mktDept = context.Departments.FirstOrDefault(d => d.Name.Contains("Marketing", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Truyền thông", StringComparison.OrdinalIgnoreCase)) ?? execDept;
            var salesDept = context.Departments.FirstOrDefault(d => d.Name.Contains("Kinh doanh", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Sales", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Bán hàng", StringComparison.OrdinalIgnoreCase)) ?? execDept;

            // 1. Meeting Preparation
            if (Regex.IsMatch(prompt, @"\b(?:họp|meeting)\b", RegexOptions.IgnoreCase))
            {
                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Chuẩn bị phòng họp, nước uống và in ấn tài liệu",
                    Description: "Vệ sinh, sắp xếp bàn ghế phòng họp, chuẩn bị nước uống và in ấn các tài liệu báo cáo phục vụ cuộc họp.",
                    Type: "Assignment",
                    DepartmentId: adminDept?.DepartmentId,
                    DepartmentName: adminDept?.Name ?? "Phòng Hành chính - Nhân sự",
                    Priority: "HIGH",
                    ChecklistItems: ["Kiểm tra vệ sinh và sắp xếp bàn ghế phòng họp", "Chuẩn bị nước uống và tài liệu cuộc họp", "Bàn giao phòng họp trước giờ bắt đầu 15 phút"],
                    Reasoning: "Công tác hậu cần cơ bản cho cuộc họp do bộ phận Hành chính phụ trách."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Kiểm tra máy chiếu, thiết bị âm thanh và kết nối mạng",
                    Description: "Khởi động máy chiếu, kiểm tra micro, loa và đường truyền internet/họp trực tuyến để đảm bảo cuộc họp diễn ra thông suốt.",
                    Type: "SupportRequest",
                    DepartmentId: itDept?.DepartmentId,
                    DepartmentName: itDept?.Name ?? "Phòng Công nghệ Thông tin",
                    Priority: "HIGH",
                    ChecklistItems: ["Bật máy chiếu và kết nối dây cáp HDMI/trình chiếu", "Kiểm tra micro, loa âm thanh", "Kiểm tra ổn định mạng internet và link họp trực tuyến"],
                    Reasoning: "Đảm bảo hạ tầng kỹ thuật và thiết bị vận hành ổn định không gây gián đoạn."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Gửi thông báo lịch họp và tổng hợp danh sách đại biểu tham dự",
                    Description: "Gửi thư mời/lịch họp cho các phòng ban, tổng hợp danh sách đại biểu xác nhận tham gia và gửi tài liệu trước cuộc họp.",
                    Type: "Assignment",
                    DepartmentId: execDept?.DepartmentId,
                    DepartmentName: execDept?.Name ?? "Ban Thư ký / Điều hành",
                    Priority: "MEDIUM",
                    ChecklistItems: ["Gửi email/thông báo lịch họp tới các đại biểu", "Đính kèm tài liệu chương trình nghị sự", "Điểm danh và lập biên bản cuộc họp"],
                    Reasoning: "Công tác điều phối lịch trình và nội dung cuộc họp do Thư ký/Điều hành đảm nhiệm."));

                return proposals;
            }

            // 2. Marketing Campaign / Product Launch
            if (Regex.IsMatch(prompt, @"\b(?:marketing|chiến dịch|campaign|quảng cáo|ra mắt|launch|bán hàng|sales)\b", RegexOptions.IgnoreCase))
            {
                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Xây dựng thông điệp truyền thông, nội dung và chạy quảng cáo chiến dịch",
                    Description: "Thiết kế kế hoạch content, thông điệp truyền thông đa kênh và thiết lập chiến dịch quảng cáo tiếp cận khách hàng mục tiêu.",
                    Type: "Assignment",
                    DepartmentId: mktDept?.DepartmentId,
                    DepartmentName: mktDept?.Name ?? "Phòng Marketing",
                    Priority: "HIGH",
                    ChecklistItems: ["Soạn thảo thông điệp truyền thông và nội dung bài viết", "Thiết lập ngân sách và chiến dịch quảng cáo đa kênh", "Theo dõi chỉ số chuyển đổi và tối ưu chi phí quảng cáo hàng ngày"],
                    Reasoning: "Nhiệm vụ cốt lõi của chiến dịch do phòng Marketing chủ trì."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Thiết kế bộ ấn phẩm hình ảnh, banner và landing page chiến dịch",
                    Description: "Thiết kế key visual, banner quảng cáo các kích thước và phối hợp dựng giao diện landing page chuyển đổi.",
                    Type: "SupportRequest",
                    DepartmentId: itDept?.DepartmentId,
                    DepartmentName: itDept?.Name ?? "Phòng Kỹ thuật & Thiết kế",
                    Priority: "HIGH",
                    ChecklistItems: ["Duyệt phong cách Key Visual chủ đạo", "Thiết kế trọn bộ banner quảng cáo các tỉ lệ", "Hỗ trợ tích hợp biểu mẫu đăng ký trên landing page"],
                    Reasoning: "Hỗ trợ visual và công nghệ cho chiến dịch truyền thông."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Chuẩn bị kịch bản bán hàng, phân bổ danh sách khách hàng tiềm năng",
                    Description: "Huấn luyện đội ngũ tư vấn viên về sản phẩm mới, xây dựng kịch bản chăm sóc khách hàng và đón nhận đơn hàng.",
                    Type: "Assignment",
                    DepartmentId: salesDept?.DepartmentId,
                    DepartmentName: salesDept?.Name ?? "Phòng Kinh doanh",
                    Priority: "MEDIUM",
                    ChecklistItems: ["Cập nhật tài liệu sản phẩm và chính sách giá", "Tổ chức đào tạo kịch bản tư vấn chốt sale", "Phân bổ danh sách khách hàng và báo cáo doanh số định kỳ"],
                    Reasoning: "Khai thác tối đa kết quả từ chiến dịch để tạo doanh số."));

                return proposals;
            }

            // 3. Recruitment / HR / Training
            if (Regex.IsMatch(prompt, @"\b(?:tuyển|tuyển dụng|hiring|onboarding|đào tạo|training|nhân sự)\b", RegexOptions.IgnoreCase))
            {
                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Đăng tin tuyển dụng, tiếp nhận CV và sàng lọc ứng viên tiềm năng",
                    Description: "Xây dựng bản mô tả công việc (JD), đăng tin tuyển dụng trên các kênh chính thức và lọc danh sách ứng viên phù hợp.",
                    Type: "Assignment",
                    DepartmentId: adminDept?.DepartmentId,
                    DepartmentName: adminDept?.Name ?? "Phòng Hành chính - Nhân sự",
                    Priority: "HIGH",
                    ChecklistItems: ["Hoàn thiện JD và mức ngân sách lương", "Đăng tuyển trên các kênh việc làm và mạng xã hội", "Sàng lọc hồ sơ và gửi thư mời phỏng vấn"],
                    Reasoning: "Khâu tiếp cận và thu hút nhân tài ban đầu do Nhân sự chủ trì."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Tổ chức phỏng vấn chuyên môn và đánh giá năng lực ứng viên",
                    Description: "Phối hợp với Trưởng bộ phận chuyên môn phỏng vấn đánh giá kỹ năng thực tế và thẩm định độ phù hợp văn hóa.",
                    Type: "Assignment",
                    DepartmentId: execDept?.DepartmentId,
                    DepartmentName: execDept?.Name ?? "Bộ phận Chuyên môn",
                    Priority: "HIGH",
                    ChecklistItems: ["Thiết kế bộ câu hỏi tình huống chuyên môn", "Tham gia phỏng vấn vòng 2", "Ghi nhận phiếu đánh giá và đưa ra quyết định tuyển dụng"],
                    Reasoning: "Đảm bảo ứng viên đáp ứng đúng yêu cầu công việc thực tế."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Cấp phát tài khoản email, trang thiết bị máy tính và chuẩn bị onboarding",
                    Description: "Tạo tài khoản hệ thống BizFlow, cấp email doanh nghiệp và cài đặt trang thiết bị làm việc sẵn sàng cho ngày đầu tiên.",
                    Type: "SupportRequest",
                    DepartmentId: itDept?.DepartmentId,
                    DepartmentName: itDept?.Name ?? "Phòng Công nghệ Thông tin",
                    Priority: "MEDIUM",
                    ChecklistItems: ["Cấp phát máy tính và phụ kiện văn phòng", "Tạo tài khoản email và cấp quyền truy cập hệ thống", "Hướng dẫn sử dụng công cụ nội bộ trong buổi onboarding đầu tiên"],
                    Reasoning: "Đảm bảo nhân sự mới có đầy đủ công cụ làm việc ngay từ ngày đầu."));

                return proposals;
            }

            // 4. Technical / IT / Security / Upgrade
            if (Regex.IsMatch(prompt, @"\b(?:máy chủ|server|bảo mật|nâng cấp|hạ tầng|hệ thống|kỹ thuật|devops|crm|erp)\b", RegexOptions.IgnoreCase))
            {
                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Lập phương án kỹ thuật, sao lưu cơ sở dữ liệu và kịch bản Rollback",
                    Description: "Kiểm tra hiện trạng hệ thống, tiến hành sao lưu toàn diện dữ liệu và chuẩn bị sẵn phương án phục hồi khi có sự cố.",
                    Type: "Assignment",
                    DepartmentId: itDept?.DepartmentId,
                    DepartmentName: itDept?.Name ?? "Phòng Kỹ thuật & Hệ thống",
                    Priority: "HIGH",
                    ChecklistItems: ["Tạo snapshot sao lưu toàn bộ dữ liệu máy chủ", "Viết kịch bản chi tiết từng bước nâng cấp", "Xây dựng phương án phục hồi khẩn cấp Rollback"],
                    Reasoning: "Đảm bảo tính toàn vẹn dữ liệu và an toàn hệ thống trước khi thay đổi."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Thực hiện nâng cấp cấu hình hệ thống và tối ưu hiệu năng",
                    Description: "Triển khai cập nhật phần mềm/máy chủ trong khung giờ bảo trì, giám sát tài nguyên và kiểm tra logs.",
                    Type: "Assignment",
                    DepartmentId: itDept?.DepartmentId,
                    DepartmentName: itDept?.Name ?? "Phòng Công nghệ Thông tin",
                    Priority: "HIGH",
                    ChecklistItems: ["Thực hiện nâng cấp phiên bản/phần cứng theo kịch bản", "Kiểm tra log lỗi và kết nối cơ sở dữ liệu", "Đo lường độ trễ và tài nguyên CPU/RAM"],
                    Reasoning: "Trọng tâm kỹ thuật thực thi việc nâng cấp."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Kiểm thử kiểm tra toàn bộ luồng chức năng và nghiệm thu vận hành",
                    Description: "Kiểm thử các tính năng trọng yếu của người dùng, xác nhận hệ thống vận hành ổn định và phát đi thông báo hoàn tất.",
                    Type: "SupportRequest",
                    DepartmentId: execDept?.DepartmentId,
                    DepartmentName: execDept?.Name ?? "Ban Quản lý & Vận hành",
                    Priority: "MEDIUM",
                    ChecklistItems: ["Kiểm thử chức năng đăng nhập và tác vụ chính", "Xác nhận không phát sinh lỗi dữ liệu", "Gửi thông báo hoàn tất bảo trì tới toàn thể công ty"],
                    Reasoning: "Nghiệm thu độc lập trước khi mở lại cho người dùng sử dụng."));

                return proposals;
            }

            // 5. General Business Goal / Project / Optimization
            if (Regex.IsMatch(prompt, @"\b(?:kế hoạch|chiến lược|tối ưu|chi phí|dự án|quy trình|sự cố|khiếu nại)\b", RegexOptions.IgnoreCase))
            {
                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Xây dựng đề án chi tiết, phân bổ KPI và thiết lập các mốc tiến độ",
                    Description: "Khảo sát hiện trạng, đặt ra chỉ số mục tiêu đo lường được và phân bổ trách nhiệm cụ thể cho các phòng ban.",
                    Type: "Assignment",
                    DepartmentId: execDept?.DepartmentId,
                    DepartmentName: execDept?.Name ?? "Ban Điều hành & Kế hoạch",
                    Priority: "HIGH",
                    ChecklistItems: ["Đánh giá hiện trạng và số liệu lịch sử", "Xác định mục tiêu KPI cần đạt được", "Ban hành kế hoạch phân công cho các bộ phận"],
                    Reasoning: "Định hướng chiến lược và phân bổ mục tiêu rõ ràng."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Triển khai các biện pháp chuyên môn và giải pháp tối ưu vận hành",
                    Description: "Các bộ phận chức năng áp dụng các cải tiến quy trình, cắt giảm lãng phí và tăng tốc độ xử lý công việc.",
                    Type: "Assignment",
                    DepartmentId: adminDept?.DepartmentId,
                    DepartmentName: adminDept?.Name ?? "Phòng Hành chính - Vận hành",
                    Priority: "HIGH",
                    ChecklistItems: ["Rà soát quy trình làm việc hiện tại", "Cắt giảm các bước dư thừa và thủ tục giấy tờ", "Báo cáo kết quả cải tiến tuần đầu tiên"],
                    Reasoning: "Thực thi giải pháp thực tế tại cơ sở."));

                proposals.Add(new TaskDecompositionProposalDto(
                    Title: "Theo dõi chỉ số định kỳ, đối soát ngân sách và đánh giá hiệu quả",
                    Description: "Tổng hợp dữ liệu đo lường, rà soát chi phí tiết kiệm được và đề xuất khen thưởng hoặc điều chỉnh kịp thời.",
                    Type: "SupportRequest",
                    DepartmentId: execDept?.DepartmentId,
                    DepartmentName: execDept?.Name ?? "Bộ phận Giám sát & Tài chính",
                    Priority: "MEDIUM",
                    ChecklistItems: ["Cập nhật bảng theo dõi tiến độ hàng tuần", "Đối chiếu chi phí thực tế so với ngân sách mục tiêu", "Lập báo cáo tổng kết nghiệm thu dự án"],
                    Reasoning: "Kiểm soát rủi ro và đo lường chính xác hiệu quả đầu tư."));

                return proposals;
            }

            // Split by bullet markers or numbering
            lines = Regex.Split(prompt, @"(?:\d+\.|\-|\*|\•)\s+")
                .Select(l => l.Trim())
                .Where(l => l.Length > 3)
                .ToList();
        }

        if (lines.Count == 0) lines.Add(prompt);

        foreach (var line in lines)
        {
            var isSupport = Regex.IsMatch(line, @"\b(hỗ trợ|request|yêu cầu|nhờ|cấp|sửa|it|hr)\b", RegexOptions.IgnoreCase);
            var type = isSupport ? "SupportRequest" : "Assignment";

            // Find matching department
            var matchedDept = context.Departments.FirstOrDefault(d =>
                line.Contains(d.Name, StringComparison.OrdinalIgnoreCase) ||
                (d.Code != null && line.Contains(d.Code, StringComparison.OrdinalIgnoreCase)));

            var priority = Regex.IsMatch(line, @"\b(khẩn|gấp|urgent|asap|ngay)\b", RegexOptions.IgnoreCase) ? "HIGH" : "MEDIUM";
            var title = line.Length > 80 ? line[..80].Trim() + "…" : line;

            proposals.Add(new TaskDecompositionProposalDto(
                Title: title,
                Description: line,
                Type: type,
                DepartmentId: matchedDept?.DepartmentId ?? context.Departments.FirstOrDefault()?.DepartmentId,
                DepartmentName: matchedDept?.Name ?? (context.Departments.FirstOrDefault()?.Name ?? "General"),
                Priority: priority,
                ChecklistItems: [$"Bước 1: Tiếp nhận và làm rõ {title}", $"Bước 2: Triển khai nội dung {title}", "Bước 3: Bàn giao và nghiệm thu"],
                Reasoning: $"Phân loại dựa trên mục tiêu: {(isSupport ? "Hỗ trợ dịch vụ" : "Giao việc thực hiện")}"));
        }

        return proposals;
    }
}
