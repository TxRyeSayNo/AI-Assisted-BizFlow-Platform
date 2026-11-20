using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BizFlow.Application.Organization;

public static class EmployeeFileProcessing
{
    private static readonly XNamespace SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static IReadOnlyList<BatchImportUserRow> ParseFile(Stream stream, string fileNameOrContentType)
    {
        var isXlsx = fileNameOrContentType.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                     || fileNameOrContentType.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase);

        if (isXlsx)
        {
            return ParseXlsx(stream);
        }

        return ParseCsv(stream);
    }

    public static IReadOnlyList<BatchImportUserRow> ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<BatchImportUserRow>();
        string? headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine)) return rows;

        var separator = headerLine.Contains(';') ? ';' : ',';
        var headers = ParseCsvLine(headerLine, separator).Select(h => h.Trim().ToLowerInvariant()).ToList();

        int codeIdx = headers.FindIndex(h => h.Contains("mã") || h.Contains("code"));
        int nameIdx = headers.FindIndex(h => h.Contains("tên") || h.Contains("name"));
        int emailIdx = headers.FindIndex(h => h.Contains("email") || h.Contains("thư"));
        int deptIdx = headers.FindIndex(h => h.Contains("phòng") || h.Contains("dept") || h.Contains("department"));
        int roleIdx = headers.FindIndex(h => h.Contains("vai") || h.Contains("role") || h.Contains("chức"));

        if (codeIdx < 0) codeIdx = 0;
        if (nameIdx < 0) nameIdx = 1;
        if (emailIdx < 0) emailIdx = 2;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = ParseCsvLine(line, separator);
            if (parts.Count <= Math.Max(codeIdx, Math.Max(nameIdx, emailIdx))) continue;

            var code = parts.ElementAtOrDefault(codeIdx)?.Trim() ?? "";
            var name = parts.ElementAtOrDefault(nameIdx)?.Trim() ?? "";
            var email = parts.ElementAtOrDefault(emailIdx)?.Trim() ?? "";
            var dept = deptIdx >= 0 ? parts.ElementAtOrDefault(deptIdx)?.Trim() : null;
            var role = roleIdx >= 0 ? parts.ElementAtOrDefault(roleIdx)?.Trim() : null;

            if (!string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(email))
            {
                rows.Add(new BatchImportUserRow(code, name, email, string.IsNullOrEmpty(dept) ? null : dept, string.IsNullOrEmpty(role) ? null : role));
            }
        }

        return rows;
    }

    public static IReadOnlyList<BatchImportUserRow> ParseXlsx(Stream stream)
    {
        var rows = new List<BatchImportUserRow>();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        // 1. Read shared strings if present
        var sharedStrings = new List<string>();
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry != null)
        {
            using var entryStream = sharedStringsEntry.Open();
            var doc = XDocument.Load(entryStream);
            sharedStrings = doc.Descendants(SheetNs + "si")
                .Select(si => string.Concat(si.Descendants(SheetNs + "t").Select(t => t.Value)))
                .ToList();
        }

        // 2. Read first worksheet
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml") ?? archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase));
        if (sheetEntry == null) return rows;

        using var sheetStream = sheetEntry.Open();
        var sheetDoc = XDocument.Load(sheetStream);
        var sheetRows = sheetDoc.Descendants(SheetNs + "row").ToList();
        if (sheetRows.Count == 0) return rows;

        // Header row
        var headerRow = sheetRows[0];
        var headerCells = headerRow.Elements(SheetNs + "c").ToList();
        var headers = new List<string>();
        foreach (var c in headerCells)
        {
            headers.Add(GetCellValue(c, sharedStrings).Trim().ToLowerInvariant());
        }

        int codeIdx = headers.FindIndex(h => h.Contains("mã") || h.Contains("code"));
        int nameIdx = headers.FindIndex(h => h.Contains("tên") || h.Contains("name"));
        int emailIdx = headers.FindIndex(h => h.Contains("email") || h.Contains("thư"));
        int deptIdx = headers.FindIndex(h => h.Contains("phòng") || h.Contains("dept") || h.Contains("department"));
        int roleIdx = headers.FindIndex(h => h.Contains("vai") || h.Contains("role") || h.Contains("chức"));

        if (codeIdx < 0) codeIdx = 0;
        if (nameIdx < 0) nameIdx = 1;
        if (emailIdx < 0) emailIdx = 2;

        for (int r = 1; r < sheetRows.Count; r++)
        {
            var row = sheetRows[r];
            var cellsByCol = new Dictionary<int, string>();

            foreach (var c in row.Elements(SheetNs + "c"))
            {
                var cellRef = (string?)c.Attribute("r") ?? "";
                var colIndex = ColumnNameToIndex(Regex.Match(cellRef, @"^[A-Z]+").Value);
                cellsByCol[colIndex] = GetCellValue(c, sharedStrings);
            }

            var code = cellsByCol.GetValueOrDefault(codeIdx, "").Trim();
            var name = cellsByCol.GetValueOrDefault(nameIdx, "").Trim();
            var email = cellsByCol.GetValueOrDefault(emailIdx, "").Trim();
            var dept = deptIdx >= 0 ? cellsByCol.GetValueOrDefault(deptIdx, "").Trim() : null;
            var role = roleIdx >= 0 ? cellsByCol.GetValueOrDefault(roleIdx, "").Trim() : null;

            if (!string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(email))
            {
                rows.Add(new BatchImportUserRow(code, name, email, string.IsNullOrEmpty(dept) ? null : dept, string.IsNullOrEmpty(role) ? null : role));
            }
        }

        return rows;
    }

    public static byte[] GenerateCsvTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Mã nhân viên,Họ và tên,Email,Mã phòng ban,Vai trò");
        sb.AppendLine("EMP001,Nguyễn Văn An,an.nguyen@company.com,IT,EMPLOYEE");
        sb.AppendLine("EMP002,Trần Thị Bình,binh.tran@company.com,HR,MANAGER");
        sb.AppendLine("EMP003,Lê Hoàng Cường,cuong.le@company.com,MKT,EMPLOYEE");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public static byte[] GenerateXlsxTemplate()
    {
        // Minimal standard OpenXML spreadsheet container with sample rows
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // [Content_Types].xml
            var ct = zip.CreateEntry("[Content_Types].xml");
            using (var s = ct.Open())
            using (var w = new StreamWriter(s, Encoding.UTF8))
            {
                w.Write("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            }

            // _rels/.rels
            var rels = zip.CreateEntry("_rels/.rels");
            using (var s = rels.Open())
            using (var w = new StreamWriter(s, Encoding.UTF8))
            {
                w.Write("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            }

            // xl/workbook.xml
            var wb = zip.CreateEntry("xl/workbook.xml");
            using (var s = wb.Open())
            using (var w = new StreamWriter(s, Encoding.UTF8))
            {
                w.Write("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Users" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            }

            // xl/_rels/workbook.xml.rels
            var wbRels = zip.CreateEntry("xl/_rels/workbook.xml.rels");
            using (var s = wbRels.Open())
            using (var w = new StreamWriter(s, Encoding.UTF8))
            {
                w.Write("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            }

            // xl/worksheets/sheet1.xml with inline strings
            var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using (var s = sheet.Open())
            using (var w = new StreamWriter(s, Encoding.UTF8))
            {
                w.Write("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>Mã nhân viên</t></is></c><c r="B1" t="inlineStr"><is><t>Họ và tên</t></is></c><c r="C1" t="inlineStr"><is><t>Email</t></is></c><c r="D1" t="inlineStr"><is><t>Mã phòng ban</t></is></c><c r="E1" t="inlineStr"><is><t>Vai trò</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>EMP001</t></is></c><c r="B2" t="inlineStr"><is><t>Nguyễn Văn An</t></is></c><c r="C2" t="inlineStr"><is><t>an.nguyen@company.com</t></is></c><c r="D2" t="inlineStr"><is><t>IT</t></is></c><c r="E2" t="inlineStr"><is><t>EMPLOYEE</t></is></c></row><row r="3"><c r="A3" t="inlineStr"><is><t>EMP002</t></is></c><c r="B3" t="inlineStr"><is><t>Trần Thị Bình</t></is></c><c r="C3" t="inlineStr"><is><t>binh.tran@company.com</t></is></c><c r="D3" t="inlineStr"><is><t>HR</t></is></c><c r="E3" t="inlineStr"><is><t>MANAGER</t></is></c></row><row r="4"><c r="A4" t="inlineStr"><is><t>EMP003</t></is></c><c r="B4" t="inlineStr"><is><t>Lê Hoàng Cường</t></is></c><c r="C4" t="inlineStr"><is><t>cuong.le@company.com</t></is></c><c r="D4" t="inlineStr"><is><t>MKT</t></is></c><c r="E4" t="inlineStr"><is><t>EMPLOYEE</t></is></c></row></sheetData></worksheet>""");
            }
        }
        return ms.ToArray();
    }

    public static byte[] GenerateErrorReportCsv(IReadOnlyList<BatchImportValidationItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Dòng,Mã nhân viên,Họ và tên,Email,Mã phòng ban,Vai trò,Trạng thái,Nguyên nhân lỗi");
        foreach (var item in items)
        {
            var reasons = string.Join(" | ", item.Errors);
            sb.AppendLine($"{item.RowIndex},\"{EscapeCsv(item.EmployeeCode)}\",\"{EscapeCsv(item.FullName)}\",\"{EscapeCsv(item.Email)}\",\"{EscapeCsv(item.DepartmentCode ?? "")}\",\"{EscapeCsv(item.Role ?? "")}\",{(item.IsValid ? "Hợp lệ" : "Lỗi")},\"{EscapeCsv(reasons)}\"");
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string EscapeCsv(string val) => val.Replace("\"", "\"\"");

    private static string GetCellValue(XElement cell, List<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "s")
        {
            var v = cell.Element(SheetNs + "v")?.Value;
            if (int.TryParse(v, out var idx) && idx >= 0 && idx < sharedStrings.Count)
            {
                return sharedStrings[idx];
            }
            return "";
        }

        if (type == "inlineStr")
        {
            return cell.Element(SheetNs + "is")?.Element(SheetNs + "t")?.Value ?? "";
        }

        return cell.Element(SheetNs + "v")?.Value ?? "";
    }

    private static int ColumnNameToIndex(string col)
    {
        int index = 0;
        foreach (char c in col)
        {
            if (c >= 'A' && c <= 'Z')
            {
                index = index * 26 + (c - 'A' + 1);
            }
        }
        return Math.Max(0, index - 1);
    }

    private static List<string> ParseCsvLine(string line, char separator)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    current.Append('\"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == separator && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}

