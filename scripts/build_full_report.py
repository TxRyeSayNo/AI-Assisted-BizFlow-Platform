import os
import sys

# We will generate the entire scripts/generate_report.py cleanly
with open("scripts/generate_report.py", "w", encoding="utf-8") as f:
    f.write('''# Clean generator for BizFlow Final Release Document v1.0.0
import os
import sys
import zipfile
import struct
import shutil
import xml.etree.ElementTree as ET

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS_DIR = os.path.join(BASE_DIR, 'docs')
SCREENSHOTS_DIR = os.path.join(DOCS_DIR, 'screenshots')
BASE_DOCX = os.path.join(DOCS_DIR, 'BizFlow_RDS_v1.1 (1).docx')
OUT_DOCX = os.path.join(DOCS_DIR, 'BizFlow_Final_Release_Document.docx')
OUT_MD = os.path.join(DOCS_DIR, 'BizFlow_Final_Release_Document.md')

def get_image_size(path):
    with open(path, 'rb') as f:
        data = f.read(30)
        w, h = struct.unpack('>LL', data[16:24])
        return w, h

def escape_xml(s):
    if not s:
        return ""
    return (s.replace("&", "&amp;")
             .replace("<", "&lt;")
             .replace(">", "&gt;")
             .replace('"', "&quot;")
             .replace("'", "&apos;"))

SCREENSHOTS_MAP = {
    "login": "01-login.png",
    "login_filled": "02-login-filled.png",
    "reg_company": "03-register-company.png",
    "reg_filled": "04-register-filled.png",
    "forgot_pwd": "05-forgot-password.png",
    "reset_pwd": "06-reset-password.png",
    "access_denied": "07-access-denied.png",
    "ws_superadmin": "08-workspace-superadmin.png",
    "plat_companies": "09-platform-companies.png",
    "plat_audit": "10-platform-audit.png",
    "ws_admin": "11-workspace-admin.png",
    "settings": "12-settings.png",
    "departments": "13-departments.png",
    "users": "14-users.png",
    "roles": "15-roles.png",
    "services": "16-services.png",
    "workflows": "17-workflows.png",
    "workflow_designer": "18-workflow-designer.png",
    "sla_profiles": "19-sla-profiles.png",
    "sla_versions": "20-sla-versions.png",
    "audit_tenant": "21-audit-tenant.png",
    "ws_manager": "22-workspace-manager.png",
    "tasks": "23-tasks.png",
    "task_create": "24-task-create.png",
    "ai_decomp": "25-ai-decomposition.png",
    "approvals": "26-approvals.png",
    "requests": "27-requests.png",
    "task_detail": "28-task-detail.png",
    "request_detail": "29-request-detail.png",
    "reports": "30-reports-admin.png",
    "sla_monitor": "31-sla-monitor.png",
    "record_search": "32-record-search.png",
    "notifications": "33-notifications.png",
    "ws_employee": "34-workspace-employee.png",
    "request_create": "35-request-create.png",
    "tasks_scoped": "36-tasks-scoped.png"
}

def p(text="", style=None, align=None, bold=False, italic=False, size=24, color=None, space_before=0, space_after=120, line_spacing=276):
    p_pr = f'<w:pPr>'
    if style:
        p_pr += f'<w:pStyle w:val="{style}"/>'
    if align:
        p_pr += f'<w:jc w:val="{align}"/>'
    p_pr += f'<w:spacing w:before="{space_before}" w:after="{space_after}" w:line="{line_spacing}" w:lineRule="auto"/>'
    p_pr += f'</w:pPr>'
    
    if not text:
        return f'<w:p>{p_pr}</w:p>'
        
    r_pr = f'<w:rPr>'
    r_pr += f'<w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>'
    if bold:
        r_pr += f'<w:b/><w:bCs/>'
    if italic:
        r_pr += f'<w:i/><w:iCs/>'
    if size:
        r_pr += f'<w:sz w:val="{size}"/><w:szCs w:val="{size}"/>'
    if color:
        r_pr += f'<w:color w:val="{color}"/>'
    r_pr += f'</w:rPr>'
    
    return f'<w:p>{p_pr}<w:r>{r_pr}<w:t xml:space="preserve">{escape_xml(text)}</w:t></w:r></w:p>'

def p_runs(runs, align=None, space_before=0, space_after=120, bullet=False):
    p_pr = f'<w:pPr>'
    if align:
        p_pr += f'<w:jc w:val="{align}"/>'
    if bullet:
        p_pr += f'<w:pStyle w:val="ListParagraph"/><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr>'
    p_pr += f'<w:spacing w:before="{space_before}" w:after="{space_after}" w:line="276" w:lineRule="auto"/></w:pPr>'
    
    r_xml = ""
    for text, bold, italic, size, color, code in runs:
        r_pr = f'<w:rPr>'
        font_name = "Consolas" if code else "Calibri"
        r_pr += f'<w:rFonts w:ascii="{font_name}" w:hAnsi="{font_name}" w:cs="{font_name}"/>'
        if bold:
            r_pr += f'<w:b/><w:bCs/>'
        if italic:
            r_pr += f'<w:i/><w:iCs/>'
        if size:
            r_pr += f'<w:sz w:val="{size}"/><w:szCs w:val="{size}"/>'
        if color:
            r_pr += f'<w:color w:val="{color}"/>'
        if code:
            r_pr += f'<w:shd w:val="clear" w:color="auto" w:fill="F3F4F6"/>'
        r_pr += f'</w:rPr>'
        r_xml += f'<w:r>{r_pr}<w:t xml:space="preserve">{escape_xml(text)}</w:t></w:r>'
        
    return f'<w:p>{p_pr}{r_xml}</w:p>'

def toc_line(title, page_num, level=1):
    indent = 0 if level == 1 else (360 if level == 2 else 720)
    bold = (level == 1)
    sz = 22 if level > 1 else 23
    color = "1F4E79" if level == 1 else ("333333" if level == 2 else "555555")
    
    p_pr = f'<w:pPr>'
    if indent > 0:
        p_pr += f'<w:ind w:left="{indent}"/>'
    p_pr += f'<w:tabs><w:tab w:val="right" w:leader="dot" w:pos="9360"/></w:tabs>'
    p_pr += f'<w:spacing w:before="30" w:after="30" w:line="240" w:lineRule="auto"/></w:pPr>'
    
    r_pr = f'<w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/>'
    if bold: r_pr += '<w:b/>'
    r_pr += f'<w:sz w:val="{sz}"/><w:color w:val="{color}"/></w:rPr>'
    
    return f'<w:p>{p_pr}<w:r>{r_pr}<w:t>{escape_xml(title)}</w:t></w:r><w:r>{r_pr}<w:tab/><w:t>{page_num}</w:t></w:r></w:p>'

def page_break():
    return '<w:p><w:r><w:br w:type="page"/></w:r></w:p>'

def heading1(text):
    return p(text, style="Heading1", align="left", bold=True, size=32, color="C00000", space_before=360, space_after=180)

def heading2(text):
    return p(text, style="Heading2", align="left", bold=True, size=26, color="1F4E79", space_before=240, space_after=120)

def heading3(text):
    return p(text, style="Heading3", align="left", bold=True, size=22, color="262626", space_before=180, space_after=80)

def code_block(code_text):
    lines = code_text.strip().split('\\n')
    out = []
    for line in lines:
        p_pr = '<w:pPr><w:shd w:val="clear" w:color="auto" w:fill="F8FAFC"/><w:spacing w:before="20" w:after="20" w:line="220" w:lineRule="auto"/><w:ind w:left="360" w:right="360"/></w:pPr>'
        r_pr = '<w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="19"/><w:color w:val="0F172A"/></w:rPr>'
        out.append(f'<w:p>{p_pr}<w:r>{r_pr}<w:t xml:space="preserve">{escape_xml(line)}</w:t></w:r></w:p>')
    return "".join(out)

def warning_callout(text):
    p_pr = '<w:pPr><w:shd w:val="clear" w:color="auto" w:fill="FEF2F2"/><w:spacing w:before="120" w:after="120" w:line="240" w:lineRule="auto"/><w:ind w:left="360" w:right="360"/></w:pPr>'
    r_pr_b = '<w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:b/><w:sz w:val="21"/><w:color w:val="991B1B"/></w:rPr>'
    r_pr_t = '<w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="21"/><w:color w:val="7F1D1D"/></w:rPr>'
    return f'<w:p>{p_pr}<w:r>{r_pr_b}<w:t xml:space="preserve">[CẢNH BÁO AN NINH / MÔI TRƯỜNG MẪU] </w:t></w:r><w:r>{r_pr_t}<w:t xml:space="preserve">{escape_xml(text)}</w:t></w:r></w:p>'

def make_table(headers, rows, widths=None):
    total_w = sum(widths) if widths else 9360
    tbl_pr = f\'\'\'<w:tblPr>
        <w:tblW w:w="{total_w}" w:type="dxa"/>
        <w:tblBorders>
            <w:top w:val="single" w:sz="6" w:space="0" w:color="BFBFBF"/>
            <w:left w:val="none"/>
            <w:bottom w:val="single" w:sz="6" w:space="0" w:color="BFBFBF"/>
            <w:right w:val="none"/>
            <w:insideH w:val="single" w:sz="4" w:space="0" w:color="E5E7EB"/>
            <w:insideV w:val="none"/>
        </w:tblBorders>
        <w:tblCellMar>
            <w:top w:w="120" w:type="dxa"/>
            <w:left w:w="160" w:type="dxa"/>
            <w:bottom w:w="120" w:type="dxa"/>
            <w:right w:w="160" w:type="dxa"/>
        </w:tblCellMar>
    </w:tblPr>\'\'\'
    
    xml_rows = []
    # Header row
    hdr_cells = []
    for i, h in enumerate(headers):
        w = widths[i] if widths else int(total_w / len(headers))
        c_pr = f\'<w:tcPr><w:tcW w:w="{w}" w:type="dxa"/><w:shd w:val="clear" w:color="auto" w:fill="F3F4F6"/></w:tcPr>\'
        p_elem = f\'<w:p><w:pPr><w:spacing w:before="60" w:after="60"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:b/><w:sz w:val="21"/><w:color w:val="1F2937"/></w:rPr><w:t>{escape_xml(h)}</w:t></w:r></w:p>\'
        hdr_cells.append(f\'<w:tc>{c_pr}{p_elem}</w:tc>\')
    xml_rows.append(f\'<w:tr><w:trPr><w:tblHeader/><w:cantSplit/></w:trPr>{"".join(hdr_cells)}</w:tr>\')
    
    # Data rows
    for row in rows:
        data_cells = []
        for i, val in enumerate(row):
            w = widths[i] if widths else int(total_w / len(headers))
            c_pr = f\'<w:tcPr><w:tcW w:w="{w}" w:type="dxa"/></w:tcPr>\'
            cell_p = []
            lines = str(val).split('\\n')
            for line in lines:
                cell_p.append(f\'<w:p><w:pPr><w:spacing w:before="40" w:after="40" w:line="240" w:lineRule="auto"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="20"/><w:color w:val="374151"/></w:rPr><w:t xml:space="preserve">{escape_xml(line)}</w:t></w:r></w:p>\')
            data_cells.append(f\'<w:tc>{c_pr}{"".join(cell_p)}</w:tc>\')
        xml_rows.append(f\'<w:tr><w:trPr><w:cantSplit/></w:trPr>{"".join(data_cells)}</w:tr>\')
        
    return f\'<w:tbl>{tbl_pr}{"".join(xml_rows)}</w:tbl>\'

def embed_image(img_key, caption="", img_rids=None):
    filename = SCREENSHOTS_MAP.get(img_key)
    if not filename:
        return ""
    filepath = os.path.join(SCREENSHOTS_DIR, filename)
    if not os.path.exists(filepath):
        return p(f"[Image placeholder: {filename}]", italic=True, color="888888")
        
    w_px, h_px = get_image_size(filepath)
    max_cx = 5486400
    aspect = h_px / w_px
    cx = max_cx
    cy = int(cx * aspect)
    if cy > 4500000:
        cy = 4500000
        cx = int(cy / aspect)
        
    rid = img_rids.get(img_key, "rId8") if img_rids else "rId8"
    
    drawing_xml = f\'\'\'<w:p>
        <w:pPr><w:spacing w:before="120" w:after="60"/><w:jc w:val="center"/></w:pPr>
        <w:r>
            <w:drawing>
                <wp:inline distT="0" distB="0" distL="0" distR="0">
                    <wp:extent cx="{cx}" cy="{cy}"/>
                    <wp:effectExtent l="0" t="0" r="0" b="0"/>
                    <wp:docPr id="{hash(img_key) % 100000}" name="{escape_xml(filename)}"/>
                    <wp:cNvGraphicFramePr>
                        <a:graphicFrameLocks xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" noChangeAspect="1"/>
                    </wp:cNvGraphicFramePr>
                    <a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                        <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
                            <pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
                                <pic:nvPicPr>
                                    <pic:cNvPr id="0" name="{escape_xml(filename)}"/>
                                    <pic:cNvPicPr><a:picLocks noChangeAspect="1" noChangeArrowheads="1"/></pic:cNvPicPr>
                                </pic:nvPicPr>
                                <pic:blipFill>
                                    <a:blip r:embed="{rid}"/>
                                    <a:stretch><a:fillRect/></a:stretch>
                                </pic:blipFill>
                                <pic:spPr bwMode="auto">
                                    <a:xfrm><a:off x="0" y="0"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm>
                                    <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                                    <a:noFill/>
                                </pic:spPr>
                            </pic:pic>
                        </a:graphicData>
                    </a:graphic>
                </wp:inline>
            </w:drawing>
        </w:r>
    </w:p>\'\'\'
    
    caption_xml = ""
    if caption:
        caption_xml = p(caption, align="center", italic=True, size=19, color="555555", space_before=40, space_after=180)
        
    return drawing_xml + caption_xml
''')

print("generate_report.py header written.")
