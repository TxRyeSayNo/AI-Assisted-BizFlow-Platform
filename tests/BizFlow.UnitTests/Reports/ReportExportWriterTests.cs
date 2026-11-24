using BizFlow.Application.Common;
using BizFlow.Application.Reports;
using BizFlow.Infrastructure.Reports;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace BizFlow.UnitTests.Reports;

public sealed class ReportExportWriterTests
{
    private static readonly ReportDocument Sample = new(
        "Báo cáo toàn công ty",
        "Kỳ báo cáo: toàn bộ thời gian",
        [
            new("Tổng quan", ["Chỉ số", "Giá trị"],
            [
                ["Nhiệm vụ đã tạo", "12"],
                ["Tỷ lệ hoàn thành (%)", "83.33"],
                // A value a spreadsheet would otherwise read as a formula, and one needing quotes.
                ["=SUM(A1:A2)", "Phòng \"Kỹ thuật\", tầng 3"]
            ]),
            new("Trống", ["Phòng ban", "Nhiệm vụ"], [])
        ]);

    private static readonly ReportExportWriter Writer = new();

    [Theory]
    [InlineData(null, ReportExportFormat.Xlsx)]
    [InlineData("", ReportExportFormat.Xlsx)]
    [InlineData("xlsx", ReportExportFormat.Xlsx)]
    [InlineData(" CSV ", ReportExportFormat.Csv)]
    [InlineData("Pdf", ReportExportFormat.Pdf)]
    public void A_requested_format_is_parsed_case_and_space_insensitively(string? value, ReportExportFormat expected) =>
        Assert.Equal(expected, ReportExportService.ParseFormat(value));

    [Theory]
    [InlineData("doc")]
    [InlineData("html")]
    [InlineData("xls")]
    public void An_unsupported_format_is_refused_before_any_report_is_read(string value)
    {
        var fault = Assert.Throws<ApplicationFault>(() => ReportExportService.ParseFormat(value));
        Assert.Equal(FaultKind.Validation, fault.Kind);
        Assert.Equal("REPORT.FORMAT", fault.Code);
    }

    [Fact]
    public void Csv_carries_a_bom_quotes_what_needs_it_and_neutralises_formulas()
    {
        var file = Writer.Write(Sample, ReportExportFormat.Csv);

        Assert.EndsWith(".csv", file.FileName);
        Assert.Contains("charset=utf-8", file.ContentType);
        Assert.Equal(Encoding.UTF8.GetPreamble(), file.Content.Take(3).ToArray());

        var text = Encoding.UTF8.GetString(file.Content).TrimStart('﻿');
        Assert.StartsWith("Báo cáo toàn công ty", text);
        Assert.Contains("Chỉ số,Giá trị", text);
        Assert.Contains("Nhiệm vụ đã tạo,12", text);
        // A leading "=" is prefixed so no spreadsheet evaluates it.
        Assert.Contains("'=SUM(A1:A2)", text);
        // Commas and quotes inside a value are escaped the CSV way.
        Assert.Contains("\"Phòng \"\"Kỹ thuật\"\", tầng 3\"", text);
    }

    [Fact]
    public void Xlsx_is_a_valid_package_with_numbers_kept_numeric_and_text_kept_unicode()
    {
        var file = Writer.Write(Sample, ReportExportFormat.Xlsx);

        Assert.EndsWith(".xlsx", file.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);

        using var archive = new ZipArchive(new MemoryStream(file.Content), ZipArchiveMode.Read);
        foreach (var required in new[]
        {
            "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml"
        })
            Assert.NotNull(archive.GetEntry(required));

        using var sheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var sheet = XDocument.Load(sheetStream);
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var cells = sheet.Descendants(ns + "c").ToList();

        // 12 and 83.33 are stored as numbers so the spreadsheet can total them.
        Assert.Contains(cells, c => c.Attribute("t") is null && c.Element(ns + "v")?.Value == "12");
        Assert.Contains(cells, c => c.Attribute("t") is null && c.Element(ns + "v")?.Value == "83.33");
        // Vietnamese text survives as an inline string.
        Assert.Contains(cells, c => c.Attribute("t")?.Value == "inlineStr" &&
            c.Descendants(ns + "t").Any(t => t.Value == "Nhiệm vụ đã tạo"));
        Assert.Contains(cells, c => c.Descendants(ns + "t").Any(t => t.Value == "Phòng \"Kỹ thuật\", tầng 3"));

        // Cell references are A1-style and start at the first row.
        Assert.Equal("A1", cells[0].Attribute("r")!.Value);
        Assert.Contains(cells, c => c.Attribute("r")!.Value == "B5");
    }

    [Fact]
    public void Pdf_is_produced_as_a_real_document()
    {
        var file = Writer.Write(Sample, ReportExportFormat.Pdf);

        Assert.EndsWith(".pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
        // A one-page table of this size is comfortably over a kilobyte once the font is embedded.
        Assert.True(file.Content.Length > 1024, $"PDF was only {file.Content.Length} bytes.");
    }

    [Fact]
    public void A_report_with_no_rows_still_exports_cleanly_in_every_format()
    {
        var empty = new ReportDocument("Rỗng", "Không có kỳ", [new("Mục", ["Cột"], [])]);
        foreach (var format in Enum.GetValues<ReportExportFormat>())
        {
            var file = Writer.Write(empty, format);
            Assert.NotEmpty(file.Content);
            Assert.False(string.IsNullOrWhiteSpace(file.FileName));
        }
    }

    [Fact]
    public void The_file_name_is_a_safe_ascii_slug_derived_from_the_title()
    {
        var file = Writer.Write(Sample, ReportExportFormat.Csv);
        // Diacritics and punctuation never reach a Content-Disposition header.
        Assert.Matches(@"^[a-z0-9-]+-\d{8}-\d{4}\.csv$", file.FileName);
    }
}
