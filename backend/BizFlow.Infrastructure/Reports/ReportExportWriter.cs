using BizFlow.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace BizFlow.Infrastructure.Reports;

/// <summary>
/// Turns a <see cref="ReportDocument"/> into a downloadable file (F5).
///
/// CSV and XLSX are written by hand: an .xlsx is a zip of a handful of OOXML parts, so a
/// spreadsheet needs no extra dependency. PDF goes through QuestPDF, because a PDF carrying
/// Vietnamese has to embed a Unicode font and that is not something to hand-roll.
///
/// Everything is written with invariant, Unicode-safe encoding: UTF-8 with a BOM for CSV so Excel
/// opens Vietnamese correctly, and inline strings in XLSX so there is no shared-string table to
/// keep consistent.
/// </summary>
public sealed class ReportExportWriter : IReportExportWriter
{
    static ReportExportWriter()
    {
        // QuestPDF refuses to render until a licence is declared. Community is the free tier; set
        // it here rather than in startup so a test that uses the writer directly behaves the same.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportExportFile Write(ReportDocument document, ReportExportFormat format) => format switch
    {
        ReportExportFormat.Csv => new(FileName(document, "csv"), "text/csv; charset=utf-8", Csv(document)),
        ReportExportFormat.Xlsx => new(FileName(document, "xlsx"),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Xlsx(document)),
        _ => new(FileName(document, "pdf"), "application/pdf", Pdf(document))
    };

    private static string FileName(ReportDocument document, string extension)
    {
        var slug = new string([.. document.Title.Normalize(NormalizationForm.FormD)
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-')])
            .Trim().Replace(' ', '-').ToLowerInvariant();
        if (slug.Length == 0) slug = "report";
        return $"{slug}-{DateTime.UtcNow:yyyyMMdd-HHmm}.{extension}";
    }

    // ---------------------------------------------------------------------------------------------
    // CSV
    // ---------------------------------------------------------------------------------------------

    private static byte[] Csv(ReportDocument document)
    {
        var text = new StringBuilder();
        text.Append(Field(document.Title)).Append('\n');
        text.Append(Field(document.Subtitle)).Append('\n');
        foreach (var section in document.Sections)
        {
            text.Append('\n').Append(Field(section.Heading)).Append('\n');
            text.Append(string.Join(',', section.Columns.Select(Field))).Append('\n');
            foreach (var row in section.Rows)
                text.Append(string.Join(',', row.Select(Field))).Append('\n');
        }
        // Excel needs the BOM to read UTF-8 Vietnamese from a .csv without mangling it.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Field(string? value)
    {
        var text = value ?? "";
        // Guard against a leading character a spreadsheet would read as a formula.
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@') text = "'" + text;
        return text.Any(c => c is ',' or '"' or '\n' or '\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    // ---------------------------------------------------------------------------------------------
    // XLSX, written as the minimal set of OOXML parts
    // ---------------------------------------------------------------------------------------------

    private static byte[] Xlsx(ReportDocument document)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            Add(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Add(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Report" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Add(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            Add(archive, "xl/worksheets/sheet1.xml", Sheet(document));
        }
        return buffer.ToArray();
    }

    private static string Sheet(ReportDocument document)
    {
        var rows = new List<IReadOnlyList<string>>();
        rows.Add([document.Title]);
        rows.Add([document.Subtitle]);
        foreach (var section in document.Sections)
        {
            rows.Add([]);
            rows.Add([section.Heading]);
            rows.Add(section.Columns);
            foreach (var row in section.Rows) rows.Add(row);
        }

        var xml = new StringBuilder();
        xml.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        xml.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        for (var r = 0; r < rows.Count; r++)
        {
            xml.Append("<row r=\"").Append(r + 1).Append("\">");
            for (var c = 0; c < rows[r].Count; c++)
            {
                var value = rows[r][c] ?? "";
                if (value.Length == 0) continue;
                xml.Append("<c r=\"").Append(Reference(c, r + 1)).Append('"');
                // A plain number stays numeric so the spreadsheet can total it; anything else is
                // an inline string, which keeps Vietnamese text intact without a shared table.
                if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var number))
                    xml.Append("><v>").Append(number.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("</v></c>");
                else
                    xml.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                       .Append(Escape(value)).Append("</t></is></c>");
            }
            xml.Append("</row>");
        }
        xml.Append("</sheetData></worksheet>");
        return xml.ToString();
    }

    /// <summary>A1-style cell reference for a zero-based column and a one-based row.</summary>
    private static string Reference(int columnIndex, int rowNumber)
    {
        var name = new StringBuilder();
        for (var remaining = columnIndex; ; remaining = remaining / 26 - 1)
        {
            name.Insert(0, (char)('A' + remaining % 26));
            if (remaining < 26) break;
        }
        return name.Append(rowNumber).ToString();
    }

    private static string Escape(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            // Control characters are not legal in XML content; drop them rather than corrupt the part.
            if (character is not ('\t' or '\n' or '\r') && char.IsControl(character)) continue;
            text.Append(character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                _ => character.ToString()
            });
        }
        return text.ToString();
    }

    private static void Add(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    // ---------------------------------------------------------------------------------------------
    // PDF
    // ---------------------------------------------------------------------------------------------

    private static byte[] Pdf(ReportDocument document) => QuestPDF.Fluent.Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.2f, Unit.Centimetre);
            // Lato ships with QuestPDF and covers Vietnamese diacritics.
            page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(9));

            page.Header().Column(header =>
            {
                header.Item().Text(document.Title).FontSize(15).SemiBold();
                header.Item().Text(document.Subtitle).FontSize(8).FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingVertical(8).Column(body =>
            {
                body.Spacing(12);
                foreach (var section in document.Sections)
                {
                    body.Item().Column(block =>
                    {
                        block.Item().PaddingBottom(3).Text(section.Heading).FontSize(11).SemiBold();
                        if (section.Columns.Count == 0) return;
                        block.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                for (var i = 0; i < section.Columns.Count; i++) columns.RelativeColumn();
                            });
                            table.Header(head =>
                            {
                                foreach (var column in section.Columns)
                                    head.Cell().Element(HeaderCell).Text(column).SemiBold();
                            });
                            foreach (var row in section.Rows)
                            {
                                for (var i = 0; i < section.Columns.Count; i++)
                                    table.Cell().Element(BodyCell).Text(i < row.Count ? row[i] ?? "" : "");
                            }
                        });
                        if (section.Rows.Count == 0)
                            block.Item().PaddingTop(3).Text("Không có dữ liệu.")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                }
            });

            page.Footer().AlignRight().Text(text =>
            {
                text.Span("Trang ").FontSize(8);
                text.CurrentPageNumber().FontSize(8);
                text.Span(" / ").FontSize(8);
                text.TotalPages().FontSize(8);
            });
        });
    }).GeneratePdf();

    private static IContainer HeaderCell(IContainer container) => container
        .Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Medium)
        .PaddingVertical(3).PaddingHorizontal(4);

    private static IContainer BodyCell(IContainer container) => container
        .BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);
}
