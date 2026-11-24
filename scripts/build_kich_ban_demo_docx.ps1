<#
    build_kich_ban_demo_docx.ps1
    Chuyen docs/BizFlow_Kich_Ban_Demo.md -> docs/BizFlow_Kich_Ban_Demo.doc (+ .docx)

    Khong can Python / pandoc / LibreOffice. Tu sinh WordprocessingML roi dong goi
    thanh .docx; neu may co Microsoft Word thi dung Word de cap nhat muc luc va
    xuat them ban .doc (Word 97-2003).

    Cach chay:
        powershell -ExecutionPolicy Bypass -File scripts\build_kich_ban_demo_docx.ps1

    Tai lieu sinh ra gom: trang bia, muc luc tu dong, header + so trang,
    moi "BUOC" bat dau trang moi, bang co vien va dong tieu de lap lai.

    Markdown ho tro: # ## ### heading, doan van, bang |...|, bullet "- ",
    checkbox "- [ ]", blockquote "> ", duong ke "---", inline **in dam**,
    *in nghieng*, `ma lenh`. Phan noi dung truoc duong ke "---" dau tien
    duoc dung lam TRANG BIA.
#>

[CmdletBinding()]
param(
    [string]$InputPath  = (Join-Path $PSScriptRoot '..\docs\BizFlow_Kich_Ban_Demo.md'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\docs\BizFlow_Kich_Ban_Demo.docx'),
    [string]$DocPath    = (Join-Path $PSScriptRoot '..\docs\BizFlow_Kich_Ban_Demo.doc'),
    [switch]$SkipWord
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$InputPath  = [System.IO.Path]::GetFullPath($InputPath)
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$DocPath    = [System.IO.Path]::GetFullPath($DocPath)

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Khong tim thay file nguon: $InputPath"
}

# Font va co chu chung cua tai lieu (half-points: 24 = 12pt)
$FontBody   = 'Times New Roman'
$FontMono   = 'Consolas'
$SizeBody   = 24
$SizeTable  = 20
$SizeTitle  = 56
$SizeH1     = 32
$SizeH2     = 28
$SizeH3     = 26

$HeaderText = 'Kich ban demo & script thuyet trinh — BizFlow'

# ---------------------------------------------------------------- helpers ----

function Convert-XmlText {
    param([string]$Text)
    if ($null -eq $Text) { return '' }
    $Text.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

# Chuyen 1 doan text co **dam**, *nghieng*, `code` thanh cac <w:r>
function Convert-InlineRuns {
    param(
        [string]$Text,
        [switch]$Bold,
        [switch]$Italic,
        [int]$Size = $SizeBody,
        [string]$Color = '1A1A1A',
        [string]$Font = $FontBody
    )

    $baseRpr = "<w:rFonts w:ascii=""$Font"" w:hAnsi=""$Font"" w:cs=""$Font""/><w:sz w:val=""$Size""/><w:szCs w:val=""$Size""/>"

    if ([string]::IsNullOrEmpty($Text)) {
        return "<w:r><w:rPr>$baseRpr</w:rPr><w:t xml:space=""preserve""></w:t></w:r>"
    }

    $sb = New-Object System.Text.StringBuilder
    $pattern = '(\*\*.+?\*\*|`[^`]+`|\*[^*\s][^*]*?\*)'
    $tokens = [regex]::Split($Text, $pattern) | Where-Object { $_ -ne '' }

    foreach ($tok in $tokens) {
        $isBold   = [bool]$Bold
        $isItalic = [bool]$Italic
        $isCode   = $false
        $value    = $tok

        if ($tok.Length -ge 4 -and $tok.StartsWith('**') -and $tok.EndsWith('**')) {
            $isBold = $true
            $value  = $tok.Substring(2, $tok.Length - 4)
        }
        elseif ($tok.Length -ge 2 -and $tok.StartsWith('`') -and $tok.EndsWith('`')) {
            $isCode = $true
            $value  = $tok.Substring(1, $tok.Length - 2)
        }
        elseif ($tok.Length -ge 2 -and $tok.StartsWith('*') -and $tok.EndsWith('*')) {
            $isItalic = $true
            $value    = $tok.Substring(1, $tok.Length - 2)
        }

        $runFont = if ($isCode) { $FontMono } else { $Font }
        $runSize = if ($isCode) { [Math]::Max($Size - 3, 16) } else { $Size }

        $rPr = New-Object System.Text.StringBuilder
        [void]$rPr.Append('<w:rPr>')
        [void]$rPr.Append("<w:rFonts w:ascii=""$runFont"" w:hAnsi=""$runFont"" w:cs=""$runFont""/>")
        if ($isBold)   { [void]$rPr.Append('<w:b/><w:bCs/>') }
        if ($isItalic) { [void]$rPr.Append('<w:i/><w:iCs/>') }
        [void]$rPr.Append("<w:color w:val=""$Color""/>")
        [void]$rPr.Append("<w:sz w:val=""$runSize""/><w:szCs w:val=""$runSize""/>")
        if ($isCode) { [void]$rPr.Append('<w:shd w:val="clear" w:color="auto" w:fill="F1F2F4"/>') }
        [void]$rPr.Append('</w:rPr>')

        [void]$sb.Append('<w:r>')
        [void]$sb.Append($rPr.ToString())
        [void]$sb.Append('<w:t xml:space="preserve">')
        [void]$sb.Append((Convert-XmlText $value))
        [void]$sb.Append('</w:t></w:r>')
    }

    $sb.ToString()
}

function New-Paragraph {
    param(
        [string]$Text,
        [string]$StyleId,
        [int]$Size = $SizeBody,
        [switch]$Bold,
        [switch]$Italic,
        [int]$SpaceBefore = 0,
        [int]$SpaceAfter = 120,
        [int]$Line = 300,
        [int]$Indent = 0,
        [string]$Color = '1A1A1A',
        [switch]$PageBreakBefore,
        [switch]$Shaded,
        [switch]$LeftBar,
        [switch]$BottomRule,
        [switch]$KeepNext,
        [string]$Align
    )

    # Thu tu cac phan tu trong <w:pPr> phai dung theo schema OOXML, neu khong
    # Word se bao "unreadable content":
    # pStyle > keepNext > pageBreakBefore > pBdr > shd > spacing > ind > jc
    $pPr = New-Object System.Text.StringBuilder
    [void]$pPr.Append('<w:pPr>')
    if ($StyleId) { [void]$pPr.Append("<w:pStyle w:val=""$StyleId""/>") }
    if ($KeepNext) { [void]$pPr.Append('<w:keepNext/>') }
    if ($PageBreakBefore) { [void]$pPr.Append('<w:pageBreakBefore/>') }

    $borders = New-Object System.Text.StringBuilder
    if ($BottomRule) { [void]$borders.Append('<w:bottom w:val="single" w:sz="8" w:space="6" w:color="0B2545"/>') }
    if ($LeftBar)    { [void]$borders.Append('<w:left w:val="single" w:sz="18" w:space="8" w:color="7A8699"/>') }
    if ($borders.Length -gt 0) {
        # trong <w:pBdr>: top > left > bottom > right
        $ordered = ''
        if ($LeftBar)    { $ordered += '<w:left w:val="single" w:sz="18" w:space="8" w:color="7A8699"/>' }
        if ($BottomRule) { $ordered += '<w:bottom w:val="single" w:sz="8" w:space="6" w:color="0B2545"/>' }
        [void]$pPr.Append('<w:pBdr>' + $ordered + '</w:pBdr>')
    }

    if ($Shaded) { [void]$pPr.Append('<w:shd w:val="clear" w:color="auto" w:fill="F4F6F8"/>') }
    [void]$pPr.Append("<w:spacing w:before=""$SpaceBefore"" w:after=""$SpaceAfter"" w:line=""$Line"" w:lineRule=""auto""/>")
    if ($Indent -gt 0) { [void]$pPr.Append("<w:ind w:left=""$Indent""/>") }
    if ($Align) { [void]$pPr.Append("<w:jc w:val=""$Align""/>") }
    [void]$pPr.Append('</w:pPr>')

    $runArgs = @{ Text = $Text; Size = $Size; Color = $Color }
    if ($Bold)   { $runArgs['Bold'] = $true }
    if ($Italic) { $runArgs['Italic'] = $true }
    $runs = Convert-InlineRuns @runArgs

    "<w:p>$($pPr.ToString())$runs</w:p>"
}

function New-EmptyParagraph {
    param([int]$Size = 16)
    "<w:p><w:pPr><w:spacing w:before=""0"" w:after=""0"" w:line=""240"" w:lineRule=""auto""/><w:rPr><w:sz w:val=""$Size""/></w:rPr></w:pPr></w:p>"
}

function Split-TableRow {
    param([string]$Line)
    $trimmed = $Line.Trim()
    if ($trimmed.StartsWith('|')) { $trimmed = $trimmed.Substring(1) }
    if ($trimmed.EndsWith('|'))   { $trimmed = $trimmed.Substring(0, $trimmed.Length - 1) }
    [regex]::Split($trimmed, '(?<!\\)\|') | ForEach-Object { $_.Trim().Replace('\|', '|') }
}

function Test-TableSeparator {
    param([string]$Line)
    $t = $Line.Trim()
    if (-not $t.StartsWith('|')) { return $false }
    return ($t -match '^\|[\s\-:|]+\|$')
}

function New-Table {
    param(
        [System.Collections.ArrayList]$Rows,
        [switch]$Centered
    )

    if ($Rows.Count -eq 0) { return '' }

    $colCount = 0
    foreach ($r in $Rows) { if ($r.Count -gt $colCount) { $colCount = $r.Count } }
    if ($colCount -eq 0) { return '' }

    # Chieu rong vung in: A4 doc 11906 twips - le 2 x 850 = 10206
    $totalWidth = 10206
    if ($Centered) { $totalWidth = 7600 }

    # Can do rong cot theo do dai noi dung thuc te
    $weights = New-Object 'double[]' $colCount
    for ($c = 0; $c -lt $colCount; $c++) {
        $max = 1.0
        $sum = 0.0
        $n   = 0
        foreach ($r in $Rows) {
            $cell = if ($c -lt $r.Count) { [string]$r[$c] } else { '' }
            $len  = [double]$cell.Length
            if ($len -gt $max) { $max = $len }
            $sum += $len
            $n++
        }
        $avg = if ($n -gt 0) { $sum / $n } else { 1.0 }
        $weights[$c] = [Math]::Sqrt(($avg * 2.0) + $max) + 2.0
    }

    $weightSum = 0.0
    foreach ($w in $weights) { $weightSum += $w }

    $minWidth = [int][Math]::Max(620, [Math]::Floor($totalWidth / ($colCount * 2.6)))
    $widths = New-Object 'int[]' $colCount
    $assigned = 0
    for ($c = 0; $c -lt $colCount; $c++) {
        $w = [int][Math]::Floor($totalWidth * ($weights[$c] / $weightSum))
        if ($w -lt $minWidth) { $w = $minWidth }
        $widths[$c] = $w
        $assigned += $w
    }
    if ($assigned -ne $totalWidth) {
        $scale = [double]$totalWidth / [double]$assigned
        $running = 0
        for ($c = 0; $c -lt $colCount; $c++) {
            if ($c -eq $colCount - 1) {
                $widths[$c] = $totalWidth - $running
            } else {
                $widths[$c] = [int][Math]::Floor($widths[$c] * $scale)
                $running += $widths[$c]
            }
        }
    }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('<w:tbl><w:tblPr><w:tblStyle w:val="BizFlowTable"/>')
    [void]$sb.Append("<w:tblW w:w=""$totalWidth"" w:type=""dxa""/>")
    if ($Centered) { [void]$sb.Append('<w:jc w:val="center"/>') }
    [void]$sb.Append('<w:tblBorders>')
    [void]$sb.Append('<w:top w:val="single" w:sz="8" w:space="0" w:color="8C96A3"/>')
    [void]$sb.Append('<w:left w:val="single" w:sz="8" w:space="0" w:color="8C96A3"/>')
    [void]$sb.Append('<w:bottom w:val="single" w:sz="8" w:space="0" w:color="8C96A3"/>')
    [void]$sb.Append('<w:right w:val="single" w:sz="8" w:space="0" w:color="8C96A3"/>')
    [void]$sb.Append('<w:insideH w:val="single" w:sz="4" w:space="0" w:color="C4CAD3"/>')
    [void]$sb.Append('<w:insideV w:val="single" w:sz="4" w:space="0" w:color="C4CAD3"/>')
    [void]$sb.Append('</w:tblBorders>')
    # thu tu trong <w:tblPr>: tblStyle > tblW > jc > tblBorders > tblLayout > tblCellMar
    [void]$sb.Append('<w:tblLayout w:type="fixed"/>')
    [void]$sb.Append('<w:tblCellMar><w:top w:w="72" w:type="dxa"/><w:left w:w="108" w:type="dxa"/><w:bottom w:w="72" w:type="dxa"/><w:right w:w="108" w:type="dxa"/></w:tblCellMar>')
    [void]$sb.Append('</w:tblPr><w:tblGrid>')
    for ($c = 0; $c -lt $colCount; $c++) {
        [void]$sb.Append("<w:gridCol w:w=""$($widths[$c])""/>")
    }
    [void]$sb.Append('</w:tblGrid>')

    # Bang 2 cot kieu "nhan | gia tri" (dong dau trong) thi khong co dong tieu de
    $headerIsLabels = $false
    if ($Rows.Count -gt 0) {
        $firstRowEmpty = $true
        foreach ($cell in $Rows[0]) { if (-not [string]::IsNullOrWhiteSpace([string]$cell)) { $firstRowEmpty = $false } }
        $headerIsLabels = $firstRowEmpty
    }

    for ($i = 0; $i -lt $Rows.Count; $i++) {
        $row = $Rows[$i]
        $isHeader = ($i -eq 0 -and -not $headerIsLabels)

        # thu tu trong <w:trPr>: cantSplit > trHeight > tblHeader
        [void]$sb.Append('<w:tr><w:trPr>')
        [void]$sb.Append('<w:cantSplit/>')
        if ($isHeader) { [void]$sb.Append('<w:tblHeader/>') }
        [void]$sb.Append('</w:trPr>')

        for ($c = 0; $c -lt $colCount; $c++) {
            $cellText = if ($c -lt $row.Count) { [string]$row[$c] } else { '' }
            $cellText = $cellText -replace '<br\s*/?>', ' '

            [void]$sb.Append('<w:tc><w:tcPr>')
            [void]$sb.Append("<w:tcW w:w=""$($widths[$c])"" w:type=""dxa""/>")
            if ($isHeader) {
                [void]$sb.Append('<w:shd w:val="clear" w:color="auto" w:fill="DCE3EC"/>')
            } elseif ($i % 2 -eq 0) {
                [void]$sb.Append('<w:shd w:val="clear" w:color="auto" w:fill="F7F9FB"/>')
            }
            [void]$sb.Append('<w:vAlign w:val="center"/>')
            [void]$sb.Append('</w:tcPr>')

            $pPr = '<w:pPr><w:spacing w:before="20" w:after="20" w:line="252" w:lineRule="auto"/></w:pPr>'
            $runs = if ($isHeader) {
                Convert-InlineRuns -Text $cellText -Bold -Size $SizeTable -Color '0B2545'
            } else {
                Convert-InlineRuns -Text $cellText -Size $SizeTable
            }
            [void]$sb.Append("<w:p>$pPr$runs</w:p>")
            [void]$sb.Append('</w:tc>')
        }
        [void]$sb.Append('</w:tr>')
    }

    [void]$sb.Append('</w:tbl>')
    [void]$sb.Append('<w:p><w:pPr><w:spacing w:before="0" w:after="80" w:line="140" w:lineRule="auto"/></w:pPr></w:p>')
    $sb.ToString()
}

# ------------------------------------------------------------- doc dau vao ----

$lines = [System.IO.File]::ReadAllLines($InputPath, [System.Text.Encoding]::UTF8)

# Tim duong ke "---" dau tien: moc ket thuc trang bia
$coverEnd = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i].Trim() -match '^(-{3,}|\*{3,}|_{3,})$') { $coverEnd = $i; break }
}
if ($coverEnd -lt 0) { $coverEnd = 0 }

$docTitle    = 'BizFlow - Kich ban demo'
$docSubtitle = ''

# ------------------------------------------------------------- TRANG BIA -----

$cover = New-Object System.Text.StringBuilder
$coverRows = New-Object System.Collections.ArrayList
$coverTableXml = ''
$coverNotes = New-Object System.Collections.ArrayList

for ($i = 0; $i -lt $coverEnd; $i++) {
    $trim = $lines[$i].Trim()
    if ($trim -eq '') { continue }

    if ($trim.StartsWith('|')) {
        if (Test-TableSeparator $trim) { continue }
        [void]$coverRows.Add(@(Split-TableRow $trim))
        continue
    }
    if ($trim -match '^#\s+(.*)$')   { $docTitle = $Matches[1].Trim(); continue }
    if ($trim -match '^##\s+(.*)$')  { $docSubtitle = $Matches[1].Trim(); continue }
    [void]$coverNotes.Add($trim)
}
if ($coverRows.Count -gt 0) { $coverTableXml = New-Table -Rows $coverRows -Centered }

[void]$cover.Append((New-EmptyParagraph -Size 40))
[void]$cover.Append((New-Paragraph -Text 'TÀI LIỆU TRÌNH BÀY DỰ ÁN' -Size 24 -Bold -Align 'center' -SpaceAfter 320 -Color '5A6676'))
[void]$cover.Append((New-Paragraph -Text $docTitle -Size $SizeTitle -Bold -Align 'center' -SpaceAfter 160 -Line 276 -Color '0B2545'))
if ($docSubtitle) {
    [void]$cover.Append((New-Paragraph -Text $docSubtitle -Size 26 -Align 'center' -SpaceAfter 200 -Color '3C4858' -BottomRule))
}
[void]$cover.Append((New-EmptyParagraph -Size 36))
[void]$cover.Append($coverTableXml)
[void]$cover.Append((New-EmptyParagraph -Size 24))
foreach ($note in $coverNotes) {
    [void]$cover.Append((New-Paragraph -Text $note -Size 22 -SpaceAfter 140 -Line 288 -Align 'both' -Shaded -LeftBar -Indent 160 -Color '33373D'))
}

# --------------------------------------------------------- NOI DUNG CHINH ----

$body = New-Object System.Text.StringBuilder
$tableRows = New-Object System.Collections.ArrayList
$headings = New-Object System.Collections.ArrayList
$inTable = $false
$firstH1Seen = $false

function Close-Table {
    if ($script:inTable) {
        [void]$script:body.Append((New-Table -Rows $script:tableRows))
        $script:tableRows = New-Object System.Collections.ArrayList
        $script:inTable = $false
    }
}

for ($idx = $coverEnd + 1; $idx -lt $lines.Count; $idx++) {
    $line = $lines[$idx].TrimEnd()
    $trim = $line.Trim()

    # --- bang ---
    if ($trim.StartsWith('|')) {
        if (Test-TableSeparator $trim) { continue }
        [void]$tableRows.Add(@(Split-TableRow $trim))
        $inTable = $true
        continue
    }
    Close-Table

    if ($trim -eq '') { continue }

    # --- duong ke ngang ---
    if ($trim -match '^(-{3,}|\*{3,}|_{3,})$') { continue }

    # --- heading ---
    if ($trim -match '^#{1,6}\s+(.*)$') {
        $level = ($trim -replace '^(#{1,6}).*$', '$1').Length
        $text  = $Matches[1].Trim()

        if ($level -le 3) {
            [void]$headings.Add([pscustomobject]@{ Level = $level; Text = $text })
        }

        # Heading dau tien cua phan noi dung sang trang moi, de khong dinh vao
        # trang muc luc. Moi "BUOC" (heading cap 1) cung luon sang trang moi.
        $hArgs = @{ Text = $text; Bold = $true; KeepNext = $true }
        switch ($level) {
            1 { $hArgs += @{ StyleId = 'Heading1'; Size = $SizeH1; SpaceAfter = 220; Color = '0B2545'; BottomRule = $true; PageBreakBefore = $true } }
            2 { $hArgs += @{ StyleId = 'Heading2'; Size = $SizeH2; SpaceBefore = 300; SpaceAfter = 150; Color = '14365C' } }
            3 { $hArgs += @{ StyleId = 'Heading3'; Size = $SizeH3; SpaceBefore = 240; SpaceAfter = 120; Color = '1F4E79' } }
            default { $hArgs += @{ Size = $SizeBody; SpaceBefore = 200; SpaceAfter = 100; Color = '1F4E79' } }
        }
        if (-not $firstH1Seen) {
            $firstH1Seen = $true
            $hArgs['PageBreakBefore'] = $true
            $hArgs['SpaceBefore'] = 0
        }
        [void]$body.Append((New-Paragraph @hArgs))
        continue
    }

    # --- blockquote ---
    if ($trim.StartsWith('>')) {
        $inner = $trim.Substring(1).TrimStart()
        if ($inner -match '^#{1,6}\s+(.*)$') {
            $inner = $Matches[1].Trim()
            [void]$body.Append((New-Paragraph -Text $inner -Size 26 -Bold -Indent 240 -SpaceBefore 180 -SpaceAfter 90 -Color '8A3A00' -Shaded -LeftBar -KeepNext))
        } elseif ($inner -eq '') {
            [void]$body.Append((New-Paragraph -Text '' -Size 14 -Indent 240 -SpaceBefore 0 -SpaceAfter 0 -Shaded -LeftBar))
        } else {
            [void]$body.Append((New-Paragraph -Text $inner -Size 22 -Indent 240 -SpaceBefore 0 -SpaceAfter 90 -Align 'both' -Color '33373D' -Shaded -LeftBar))
        }
        continue
    }

    # --- checkbox ---
    if ($trim -match '^[-*+]\s+\[([ xX])\]\s*(.*)$') {
        $mark = if ($Matches[1] -match '[xX]') { [char]0x2611 } else { [char]0x2610 }
        [void]$body.Append((New-Paragraph -Text ("$mark  " + $Matches[2]) -Size 22 -Indent 300 -SpaceBefore 0 -SpaceAfter 80))
        continue
    }

    # --- bullet ---
    if ($trim -match '^[-*+]\s+(.*)$') {
        $bullet = [char]0x2022
        [void]$body.Append((New-Paragraph -Text ("$bullet  " + $Matches[1]) -Size 22 -Indent 300 -SpaceBefore 0 -SpaceAfter 80))
        continue
    }

    # --- danh sach so ---
    if ($trim -match '^(\d+)[.)]\s+(.*)$') {
        [void]$body.Append((New-Paragraph -Text ($Matches[1] + '.  ' + $Matches[2]) -Size 22 -Indent 300 -SpaceBefore 0 -SpaceAfter 80 -Align 'both'))
        continue
    }

    # --- doan van thuong ---
    [void]$body.Append((New-Paragraph -Text $trim -Size $SizeBody -SpaceBefore 0 -SpaceAfter 130 -Align 'both'))
}
Close-Table

# -------------------------------------------------------------- MUC LUC ------
# Muc luc TINH (khong dung field TOC): luon dung, khong phu thuoc Word.
# Word van nhan dien cac heading (co outlineLvl) nen Navigation Pane va
# chuc nang References > Table of Contents vẫn dùng được nếu cần số trang.

$toc = New-Object System.Text.StringBuilder
[void]$toc.Append((New-Paragraph -Text 'NỘI DUNG TÀI LIỆU' -Size $SizeH1 -Bold -Align 'center' -PageBreakBefore -SpaceAfter 260 -Color '0B2545'))
foreach ($h in $headings) {
    switch ($h.Level) {
        1 { [void]$toc.Append((New-Paragraph -Text $h.Text -StyleId 'TOC1' -Size 23 -Bold -SpaceBefore 180 -SpaceAfter 60 -Line 264 -Color '0B2545')) }
        2 { [void]$toc.Append((New-Paragraph -Text $h.Text -StyleId 'TOC2' -Size 22 -Indent 320 -SpaceBefore 0 -SpaceAfter 40 -Line 264 -Color '26303C')) }
    }
}

# ------------------------------------------------------------ cac part XML ----

$sectPr = @"
<w:sectPr>
  <w:headerReference w:type="default" r:id="rId2"/>
  <w:footerReference w:type="default" r:id="rId3"/>
  <w:headerReference w:type="first" r:id="rId4"/>
  <w:footerReference w:type="first" r:id="rId5"/>
  <w:pgSz w:w="11906" w:h="16838"/>
  <w:pgMar w:top="1000" w:right="850" w:bottom="1000" w:left="850" w:header="520" w:footer="520" w:gutter="0"/>
  <w:titlePg/>
  <w:docGrid w:linePitch="360"/>
</w:sectPr>
"@

$documentXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document
    xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <w:body>
$($cover.ToString())
$($toc.ToString())
$($body.ToString())
$sectPr
  </w:body>
</w:document>
"@

$headerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p>
    <w:pPr>
      <w:pBdr><w:bottom w:val="single" w:sz="4" w:space="4" w:color="B6BCC6"/></w:pBdr>
      <w:spacing w:before="0" w:after="60" w:line="240" w:lineRule="auto"/>
      <w:jc w:val="right"/>
    </w:pPr>
    $(Convert-InlineRuns -Text $HeaderText -Size 18 -Color '5A6676')
  </w:p>
</w:hdr>
"@

$footerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p>
    <w:pPr>
      <w:spacing w:before="60" w:after="0" w:line="240" w:lineRule="auto"/>
      <w:jc w:val="center"/>
    </w:pPr>
    $(Convert-InlineRuns -Text 'Trang ' -Size 18 -Color '5A6676')
    <w:fldSimple w:instr=" PAGE ">$(Convert-InlineRuns -Text '1' -Size 18 -Color '5A6676')</w:fldSimple>
    $(Convert-InlineRuns -Text ' / ' -Size 18 -Color '5A6676')
    <w:fldSimple w:instr=" NUMPAGES ">$(Convert-InlineRuns -Text '1' -Size 18 -Color '5A6676')</w:fldSimple>
  </w:p>
</w:ftr>
"@

# Trang bia: khong header, khong so trang
$headerFirstXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p><w:pPr><w:spacing w:before="0" w:after="0" w:line="240" w:lineRule="auto"/></w:pPr></w:p>
</w:hdr>
'@

$footerFirstXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p><w:pPr><w:spacing w:before="0" w:after="0" w:line="240" w:lineRule="auto"/></w:pPr></w:p>
</w:ftr>
'@

$settingsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:zoom w:percent="100"/>
  <w:defaultTabStop w:val="720"/>
  <w:compat/>
</w:settings>
'@

$stylesXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:docDefaults>
    <w:rPrDefault>
      <w:rPr>
        <w:rFonts w:ascii="$FontBody" w:hAnsi="$FontBody" w:eastAsia="$FontBody" w:cs="$FontBody"/>
        <w:sz w:val="$SizeBody"/>
        <w:szCs w:val="$SizeBody"/>
        <w:lang w:val="vi-VN"/>
      </w:rPr>
    </w:rPrDefault>
    <w:pPrDefault>
      <w:pPr>
        <w:spacing w:after="130" w:line="300" w:lineRule="auto"/>
      </w:pPr>
    </w:pPrDefault>
  </w:docDefaults>
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
    <w:name w:val="Normal"/>
    <w:qFormat/>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading1">
    <w:name w:val="heading 1"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr><w:keepNext/><w:outlineLvl w:val="0"/></w:pPr>
    <w:rPr><w:b/><w:sz w:val="$SizeH1"/><w:szCs w:val="$SizeH1"/><w:color w:val="0B2545"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading2">
    <w:name w:val="heading 2"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr><w:keepNext/><w:outlineLvl w:val="1"/></w:pPr>
    <w:rPr><w:b/><w:sz w:val="$SizeH2"/><w:szCs w:val="$SizeH2"/><w:color w:val="14365C"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading3">
    <w:name w:val="heading 3"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr><w:keepNext/><w:outlineLvl w:val="2"/></w:pPr>
    <w:rPr><w:b/><w:sz w:val="$SizeH3"/><w:szCs w:val="$SizeH3"/><w:color w:val="1F4E79"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="TOC1">
    <w:name w:val="toc 1"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:tabs><w:tab w:val="right" w:leader="dot" w:pos="10206"/></w:tabs><w:spacing w:before="140" w:after="60"/></w:pPr>
    <w:rPr><w:b/><w:color w:val="0B2545"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="TOC2">
    <w:name w:val="toc 2"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:tabs><w:tab w:val="right" w:leader="dot" w:pos="10206"/></w:tabs><w:spacing w:before="20" w:after="20"/><w:ind w:left="300"/></w:pPr>
    <w:rPr><w:sz w:val="22"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="TOC3">
    <w:name w:val="toc 3"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:tabs><w:tab w:val="right" w:leader="dot" w:pos="10206"/></w:tabs><w:spacing w:before="20" w:after="20"/><w:ind w:left="620"/></w:pPr>
    <w:rPr><w:sz w:val="21"/><w:color w:val="33373D"/></w:rPr>
  </w:style>
  <w:style w:type="character" w:styleId="Hyperlink">
    <w:name w:val="Hyperlink"/>
    <w:rPr><w:color w:val="1F4E79"/></w:rPr>
  </w:style>
  <w:style w:type="table" w:default="1" w:styleId="TableNormal">
    <w:name w:val="Normal Table"/>
    <w:uiPriority w:val="99"/>
    <w:semiHidden/>
    <w:unhideWhenUsed/>
  </w:style>
  <w:style w:type="table" w:styleId="BizFlowTable">
    <w:name w:val="BizFlow Table"/>
    <w:basedOn w:val="TableNormal"/>
    <w:qFormat/>
    <w:tblPr>
      <w:tblCellMar>
        <w:top w:w="72" w:type="dxa"/><w:left w:w="108" w:type="dxa"/>
        <w:bottom w:w="72" w:type="dxa"/><w:right w:w="108" w:type="dxa"/>
      </w:tblCellMar>
    </w:tblPr>
  </w:style>
</w:styles>
"@

$contentTypesXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
  <Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
  <Override PartName="/word/header1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml"/>
  <Override PartName="/word/header2.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml"/>
  <Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
  <Override PartName="/word/footer2.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>
'@

$rootRelsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
'@

$docRelsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
  <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header2.xml"/>
  <Relationship Id="rId5" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer2.xml"/>
  <Relationship Id="rId6" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/>
</Relationships>
'@

$nowIso = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$corePropsXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties
    xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
    xmlns:dc="http://purl.org/dc/elements/1.1/"
    xmlns:dcterms="http://purl.org/dc/terms/"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dcterms:created xsi:type="dcterms:W3CDTF">$nowIso</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">$nowIso</dcterms:modified>
  <cp:revision>1</cp:revision>
  <dc:subject>$(Convert-XmlText $docSubtitle)</dc:subject>
  <dc:title>$(Convert-XmlText $docTitle)</dc:title>
</cp:coreProperties>
"@

$appPropsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties
    xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"
    xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>BizFlow docs builder</Application>
</Properties>
'@

# ----------------------------------------------------------------- dong goi ---

# Ten entry BAT BUOC dung dau '/'. ZipFile::CreateFromDirectory tren
# .NET Framework sinh ten entry mang dau '\' -> Word bao file loi.
$parts = [ordered]@{
    '[Content_Types].xml'          = $contentTypesXml
    '_rels/.rels'                  = $rootRelsXml
    'word/document.xml'            = $documentXml
    'word/styles.xml'              = $stylesXml
    'word/settings.xml'            = $settingsXml
    'word/header1.xml'             = $headerXml
    'word/header2.xml'             = $headerFirstXml
    'word/footer1.xml'             = $footerXml
    'word/footer2.xml'             = $footerFirstXml
    'word/_rels/document.xml.rels' = $docRelsXml
    'docProps/core.xml'            = $corePropsXml
    'docProps/app.xml'             = $appPropsXml
}

foreach ($name in $parts.Keys) {
    $xml = New-Object System.Xml.XmlDocument
    $xml.LoadXml($parts[$name])
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
if (Test-Path -LiteralPath $OutputPath) { Remove-Item -LiteralPath $OutputPath -Force }

$zipStream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::CreateNew)
try {
    $zip = New-Object System.IO.Compression.ZipArchive(
        $zipStream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($name in $parts.Keys) {
            $entry  = $zip.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
            $es     = $entry.Open()
            $writer = New-Object System.IO.StreamWriter($es, $utf8NoBom)
            try   { $writer.Write($parts[$name]) }
            finally { $writer.Dispose(); $es.Dispose() }
        }
    }
    finally { $zip.Dispose() }
}
finally { $zipStream.Dispose() }

$info = Get-Item -LiteralPath $OutputPath
Write-Output ("[docx] " + $info.FullName + "  (" + [Math]::Round($info.Length / 1KB, 1) + " KB)")

# ------------------------------- dung Word de cap nhat muc luc + xuat .doc ----

if ($SkipWord) {
    Write-Output "[doc ] Bo qua buoc Word (-SkipWord)."
    return
}

# Word hay tra ve RPC_E_CALL_REJECTED (hoac tra null) khi dang ban xu ly.
# Moi lenh goi Word deu phai di qua ham nay de tu thu lai.
function Invoke-WordOp {
    param(
        [scriptblock]$Block,
        [int]$Retries = 80,
        [switch]$Require      # bat buoc co ket qua khac null, neu null thi thu lai
    )
    for ($i = 0; $i -lt $Retries; $i++) {
        try {
            $result = & $Block
            if ($Require -and $null -eq $result) { throw 'Word chua san sang (ket qua null).' }
            return $result
        }
        catch {
            if ($i -eq $Retries - 1) { throw }
            Start-Sleep -Milliseconds 400
        }
    }
}

$word = $null
$wdoc = $null
try {
    $word = New-Object -ComObject Word.Application
}
catch {
    Write-Warning "Khong tim thay Microsoft Word tren may nay. Chi tao duoc .docx."
    Write-Output  "        Mo file .docx bang Word roi chon Save As -> Word 97-2003 (*.doc) de co ban .doc."
    return
}

try {
    Invoke-WordOp { $word.Visible = $false }
    Invoke-WordOp { $word.DisplayAlerts = 0 }

    $wdoc = Invoke-WordOp -Require { $word.Documents.Open($OutputPath, $false, $false) }

    # Sau khi Open, Word con dan trang mat vai giay va tu choi moi lenh goi vao
    # (RPC_E_CALL_REJECTED). PowerShell bien loi do thanh null khi doc property,
    # nen o day chi dung METHOD va cho den khi Word tra loi.
    Start-Sleep -Seconds 3
    $pages = Invoke-WordOp -Require -Retries 150 { $wdoc.ComputeStatistics(2) }  # wdStatisticPages
    $words = Invoke-WordOp -Require -Retries 150 { $wdoc.ComputeStatistics(0) }  # wdStatisticWords

    # xuat ban .doc (Word 97-2003 = wdFormatDocument97 = 0)
    if (Test-Path -LiteralPath $DocPath) { Remove-Item -LiteralPath $DocPath -Force }
    try {
        Invoke-WordOp { $wdoc.SaveAs2($DocPath, 0) } | Out-Null
    }
    catch {
        Invoke-WordOp { $wdoc.SaveAs($DocPath, 0) } | Out-Null
    }

    Invoke-WordOp { $wdoc.Close(0) } | Out-Null
    $wdoc = $null
    Invoke-WordOp { $word.Quit() } | Out-Null
    $word = $null

    $docInfo = Get-Item -LiteralPath $DocPath
    Write-Output ("[doc ] " + $docInfo.FullName + "  (" + [Math]::Round($docInfo.Length / 1KB, 1) + " KB)")
    Write-Output ("[info] $pages trang, $words tu.")
}
finally {
    if ($wdoc) { try { Invoke-WordOp -Retries 10 { $wdoc.Close(0) } } catch {} }
    if ($word) { try { Invoke-WordOp -Retries 10 { $word.Quit() } } catch {} }
    Get-Process WINWORD -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -eq 0 } |
        Stop-Process -Force -ErrorAction SilentlyContinue
}
