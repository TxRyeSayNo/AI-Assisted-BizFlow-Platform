using BizFlow.Application.Common;
using System.Globalization;

namespace BizFlow.Application.Reports;

public enum ReportExportFormat { Csv, Xlsx, Pdf }

/// <summary>One table inside an exported report.</summary>
public sealed record ReportSection(string Heading, IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// A report flattened to headings and rows. Keeping the export format-agnostic means CSV, XLSX and
/// PDF all describe the very same numbers, and a new format needs no change here.
/// </summary>
public sealed record ReportDocument(string Title, string Subtitle, IReadOnlyList<ReportSection> Sections);

public sealed record ReportExportFile(string FileName, string ContentType, byte[] Content);

public interface IReportExportWriter
{
    ReportExportFile Write(ReportDocument document, ReportExportFormat format);
}

/// <summary>
/// Exports the existing reports (F5). It never queries storage itself: it calls the very same
/// <see cref="ReportingService"/> methods the view endpoints call, so an export can only ever
/// contain what the caller is already allowed to see, and the numbers cannot drift from the screen.
/// </summary>
public sealed class ReportExportService(ReportingService reports, IReportExportWriter writer)
{
    public static ReportExportFormat ParseFormat(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "xlsx" => ReportExportFormat.Xlsx,
        "csv" => ReportExportFormat.Csv,
        "pdf" => ReportExportFormat.Pdf,
        _ => throw new ApplicationFault(FaultKind.Validation, "REPORT.FORMAT",
            "Use xlsx, csv or pdf as the export format.")
    };

    public async Task<ReportExportFile> ExportCompanyAsync(DateTimeOffset? from, DateTimeOffset? to,
        ReportExportFormat format, CancellationToken cancellationToken)
    {
        var report = await reports.GetCompanyReportAsync(from, to, cancellationToken);
        var document = new ReportDocument("Báo cáo toàn công ty", Period(from, to, report.GeneratedAt),
        [
            new("Tổng quan",
                ["Chỉ số", "Giá trị"],
                [
                    ["Nhiệm vụ đã tạo", Int(report.TotalTasksCreated)],
                    ["Nhiệm vụ hoàn thành", Int(report.TotalTasksCompleted)],
                    ["Tỷ lệ hoàn thành nhiệm vụ (%)", Num(report.TaskCompletionRatePercent)],
                    ["Thời gian hoàn thành trung bình (giờ)", Num(report.AverageTaskCompletionTimeHours)],
                    ["Yêu cầu đã gửi", Int(report.TotalRequestsSubmitted)],
                    ["Yêu cầu đã đóng", Int(report.TotalRequestsClosed)],
                    ["Tỷ lệ giải quyết yêu cầu (%)", Num(report.RequestResolutionRatePercent)],
                    ["Thời gian giải quyết trung bình (giờ)", Num(report.AverageRequestResolutionTimeHours)],
                    ["Hoạt động audit gần đây", Int(report.RecentAuditActivityCount)]
                ]),
            new("Theo phòng ban",
                ["Phòng ban", "Nhiệm vụ", "Hoàn thành", "Yêu cầu", "Đã đóng"],
                [.. report.DepartmentMetrics.Select(d => (IReadOnlyList<string>)
                    [d.DepartmentName, Int(d.TotalTasks), Int(d.CompletedTasks), Int(d.TotalRequests), Int(d.ClosedRequests)])])
        ]);
        return writer.Write(document, format);
    }

    public async Task<ReportExportFile> ExportWorkloadAsync(Guid? departmentId, DateTimeOffset? from,
        DateTimeOffset? to, ReportExportFormat format, CancellationToken cancellationToken)
    {
        var report = await reports.GetWorkloadReportAsync(departmentId, from, to, cancellationToken);
        var document = new ReportDocument("Báo cáo khối lượng công việc", Period(from, to, report.GeneratedAt),
        [
            new("Theo nhân sự",
                ["Mã nhân sự", "Họ tên", "Phòng ban", "Đang làm", "Hoàn thành", "Quá hạn", "Tổng được giao"],
                [.. report.Items.Select(i => (IReadOnlyList<string>)
                    [i.EmployeeCode, i.FullName, i.DepartmentName ?? "", Int(i.ActiveTasks), Int(i.CompletedTasks),
                     Int(i.OverdueTasks), Int(i.TotalAssigned)])]),
            new("Theo phòng ban",
                ["Phòng ban", "Đang làm", "Số thành viên", "Trung bình/người"],
                [.. report.DepartmentBreakdown.Select(d => (IReadOnlyList<string>)
                    [d.DepartmentName, Int(d.ActiveTasks), Int(d.TotalMembers), Num(d.AvgTasksPerMember)])])
        ]);
        return writer.Write(document, format);
    }

    public async Task<ReportExportFile> ExportSlaAsync(Guid? serviceId, DateTimeOffset? from, DateTimeOffset? to,
        ReportExportFormat format, CancellationToken cancellationToken)
    {
        var report = await reports.GetSlaPerformanceReportAsync(serviceId, from, to, cancellationToken);
        var document = new ReportDocument("Báo cáo tuân thủ SLA", Period(from, to, report.GeneratedAt),
        [
            new("Tổng quan",
                ["Chỉ số", "Giá trị"],
                [
                    ["Số yêu cầu theo dõi", Int(report.TotalTracked)],
                    ["Đúng hạn", Int(report.CompliantCount)],
                    ["Vi phạm", Int(report.BreachedCount)],
                    ["Tỷ lệ tuân thủ (%)", Num(report.ComplianceRatePercent)],
                    ["Thời gian xử lý trung bình (giờ)", Num(report.AvgElapsedHours)]
                ]),
            new("Theo dịch vụ",
                ["Dịch vụ", "Số yêu cầu", "Đúng hạn", "Vi phạm", "Tỷ lệ tuân thủ (%)"],
                [.. report.ServiceBreakdown.Select(s => (IReadOnlyList<string>)
                    [s.ServiceName, Int(s.TotalRequests), Int(s.CompliantRequests), Int(s.BreachedRequests),
                     Num(s.ComplianceRatePercent)])])
        ]);
        return writer.Write(document, format);
    }

    private static string Period(DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset generatedAt)
    {
        var range = (from, to) switch
        {
            ({ } f, { } t) => $"{f:dd/MM/yyyy} – {t:dd/MM/yyyy}",
            ({ } f, null) => $"từ {f:dd/MM/yyyy}",
            (null, { } t) => $"đến {t:dd/MM/yyyy}",
            _ => "toàn bộ thời gian"
        };
        return $"Kỳ báo cáo: {range} · Kết xuất lúc {generatedAt:dd/MM/yyyy HH:mm} UTC";
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Num(double? value) =>
        value is null ? "" : Math.Round(value.Value, 2).ToString(CultureInfo.InvariantCulture);
}
