# Complete English generator for BizFlow Final Release Document v1.0.0
# Strict adherence to source code at commit 150eabb
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
OUT_DOCX_EN = os.path.join(DOCS_DIR, 'BizFlow_Final_Release_Document_EN.docx')
OUT_MD_EN = os.path.join(DOCS_DIR, 'BizFlow_Final_Release_Document_EN.md')

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
    lines = code_text.strip().split('\n')
    out = []
    for line in lines:
        p_pr = '<w:pPr><w:shd w:val="clear" w:color="auto" w:fill="F8FAFC"/><w:spacing w:before="20" w:after="20" w:line="220" w:lineRule="auto"/><w:ind w:left="360" w:right="360"/></w:pPr>'
        r_pr = '<w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="19"/><w:color w:val="0F172A"/></w:rPr>'
        out.append(f'<w:p>{p_pr}<w:r>{r_pr}<w:t xml:space="preserve">{escape_xml(line)}</w:t></w:r></w:p>')
    return "".join(out)

def warning_callout(text, title="[SECURITY WARNING / EVALUATION ENVIRONMENT] "):
    p_pr = '<w:pPr><w:shd w:val="clear" w:color="auto" w:fill="FEF2F2"/><w:spacing w:before="120" w:after="120" w:line="240" w:lineRule="auto"/><w:ind w:left="360" w:right="360"/></w:pPr>'
    r_pr_b = '<w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:b/><w:sz w:val="21"/><w:color w:val="991B1B"/></w:rPr>'
    r_pr_t = '<w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="21"/><w:color w:val="7F1D1D"/></w:rPr>'
    return f'<w:p>{p_pr}<w:r>{r_pr_b}<w:t xml:space="preserve">{escape_xml(title)}</w:t></w:r><w:r>{r_pr_t}<w:t xml:space="preserve">{escape_xml(text)}</w:t></w:r></w:p>'

def make_table(headers, rows, widths=None):
    total_w = sum(widths) if widths else 9360
    tbl_pr = f'''<w:tblPr>
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
    </w:tblPr>'''
    
    xml_rows = []
    hdr_cells = []
    for i, h in enumerate(headers):
        w = widths[i] if widths else int(total_w / len(headers))
        c_pr = f'<w:tcPr><w:tcW w:w="{w}" w:type="dxa"/><w:shd w:val="clear" w:color="auto" w:fill="F3F4F6"/></w:tcPr>'
        p_elem = f'<w:p><w:pPr><w:spacing w:before="60" w:after="60"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:b/><w:sz w:val="21"/><w:color w:val="1F2937"/></w:rPr><w:t>{escape_xml(h)}</w:t></w:r></w:p>'
        hdr_cells.append(f'<w:tc>{c_pr}{p_elem}</w:tc>')
    xml_rows.append(f'<w:tr><w:trPr><w:tblHeader/><w:cantSplit/></w:trPr>{"".join(hdr_cells)}</w:tr>')
    
    for row in rows:
        data_cells = []
        for i, val in enumerate(row):
            w = widths[i] if widths else int(total_w / len(headers))
            c_pr = f'<w:tcPr><w:tcW w:w="{w}" w:type="dxa"/></w:tcPr>'
            cell_p = []
            lines = str(val).split('\n')
            for line in lines:
                cell_p.append(f'<w:p><w:pPr><w:spacing w:before="40" w:after="40" w:line="240" w:lineRule="auto"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="20"/><w:color w:val="374151"/></w:rPr><w:t xml:space="preserve">{escape_xml(line)}</w:t></w:r></w:p>')
            data_cells.append(f'<w:tc>{c_pr}{"".join(cell_p)}</w:tc>')
        xml_rows.append(f'<w:tr><w:trPr><w:cantSplit/></w:trPr>{"".join(data_cells)}</w:tr>')
        
    return f'<w:tbl>{tbl_pr}{"".join(xml_rows)}</w:tbl>'

def embed_image(img_key, caption="", img_rids=None):
    filename = SCREENSHOTS_MAP.get(img_key)
    if not filename:
        return ""
    filepath = os.path.join(SCREENSHOTS_DIR, filename)
    if not os.path.exists(filepath):
        return p(f"[IMAGE PLACEHOLDER: {caption}] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center")
        
    w_px, h_px = get_image_size(filepath)
    max_cx = 5486400
    aspect = h_px / w_px
    cx = max_cx
    cy = int(cx * aspect)
    if cy > 4500000:
        cy = 4500000
        cx = int(cy / aspect)
        
    rid = img_rids.get(img_key, "rId8") if img_rids else "rId8"
    
    drawing_xml = f'''<w:p>
        <w:pPr><w:spacing w:before="120" w:after="60"/><w:jc w:val="center"/></w:pPr>
        <w:r>
            <w:drawing>
                <wp:inline distT="0" distB="0" distL="0" distR="0">
                    <wp:extent cx="{cx}" cy="{cy}"/>
                    <wp:effectExtent l="0" t="0" r="0" b="0"/>
                    <wp:docPr id="{abs(hash(img_key)) % 100000}" name="{escape_xml(filename)}"/>
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
    </w:p>'''
    
    caption_xml = ""
    if caption:
        caption_xml = p(caption, align="center", italic=True, size=19, color="555555", space_before=40, space_after=180)
        
    return drawing_xml + caption_xml

print("English Helper definitions complete.")
def generate_docx_en():
    print("Reading base docx for English version...")
    with zipfile.ZipFile(BASE_DOCX, 'r') as zin:
        files = {name: zin.read(name) for name in zin.namelist()}

    # Update document.xml.rels
    rels_xml = files['word/_rels/document.xml.rels'].decode('utf-8')
    root_rels = ET.fromstring(rels_xml)
    ns = "{http://schemas.openxmlformats.org/package/2006/relationships}"

    img_rids = {}
    for key, filename in SCREENSHOTS_MAP.items():
        rid = f"rIdImg_{key}"
        img_rids[key] = rid
        rel_elem = ET.Element(f"{ns}Relationship", {
            "Id": rid,
            "Type": "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image",
            "Target": f"media/{filename}"
        })
        root_rels.append(rel_elem)

    files['word/_rels/document.xml.rels'] = ET.tostring(root_rels, encoding='utf-8', xml_declaration=True)

    # Add images into media/
    for key, filename in SCREENSHOTS_MAP.items():
        src_path = os.path.join(SCREENSHOTS_DIR, filename)
        if os.path.exists(src_path):
            with open(src_path, 'rb') as f:
                files[f"word/media/{filename}"] = f.read()

    body_parts = []

    # ================= PAGE 1: COVER PAGE =================
    body_parts.append(p("", space_before=720))
    body_parts.append('''<w:p>
        <w:pPr><w:spacing w:line="360" w:lineRule="auto"/><w:jc w:val="center"/></w:pPr>
        <w:r>
            <w:drawing>
                <wp:inline distT="0" distB="0" distL="0" distR="0">
                    <wp:extent cx="2702618" cy="830580"/>
                    <wp:docPr id="1" name="FPT University Logo"/>
                    <wp:cNvGraphicFramePr>
                        <a:graphicFrameLocks xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" noChangeAspect="1"/>
                    </wp:cNvGraphicFramePr>
                    <a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                        <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
                            <pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
                                <pic:nvPicPr>
                                    <pic:cNvPr id="0" name="Logo"/>
                                    <pic:cNvPicPr><a:picLocks noChangeAspect="1" noChangeArrowheads="1"/></pic:cNvPicPr>
                                </pic:nvPicPr>
                                <pic:blipFill>
                                    <a:blip r:embed="rId8"/>
                                    <a:stretch><a:fillRect/></a:stretch>
                                </pic:blipFill>
                                <pic:spPr bwMode="auto">
                                    <a:xfrm><a:off x="0" y="0"/><a:ext cx="2702618" cy="830580"/></a:xfrm>
                                    <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                                    <a:noFill/>
                                </pic:spPr>
                            </pic:pic>
                        </a:graphicData>
                    </a:graphic>
                </wp:inline>
            </w:drawing>
        </w:r>
    </w:p>''')
    body_parts.append(p("", space_before=2800))
    body_parts.append(p("AI-Assisted BizFlow Platform", align="center", bold=True, size=52, color="C00000", space_after=200))
    body_parts.append(p("Final Release Document", align="center", bold=True, size=36, color="C00000", space_after=3600))
    body_parts.append(p("– Hanoi, November 2026 –", align="center", size=24, color="333333", space_before=1200))
    body_parts.append(page_break())

    # ================= PAGE 2: TABLE OF CONTENTS =================
    body_parts.append(p("Table of Contents", align="left", bold=True, size=36, color="2E75B6", space_before=120, space_after=300))
    body_parts.append(toc_line("0. Release Notes v1.0.0", "3", level=1))
    body_parts.append(toc_line("0.1 Executive Summary", "3", level=2))
    body_parts.append(toc_line("0.2 Release Scope", "3", level=2))
    body_parts.append(toc_line("0.3 Bug Fixes & Resolved Issues", "5", level=2))
    body_parts.append(toc_line("0.4 Known Issues & Limitations", "6", level=2))
    body_parts.append(toc_line("0.5 Deployment & Rollback Strategy", "8", level=2))
    body_parts.append(toc_line("0.6 Post-Release Monitoring & Operational Metrics", "9", level=2))
    body_parts.append(toc_line("0.7 Sign-off & Release Approval", "10", level=2))
    body_parts.append(toc_line("0.8 Compatibility & Breaking Changes Notes", "10", level=2))
    body_parts.append(toc_line("I. Deliverable Package", "11", level=1))
    body_parts.append(toc_line("II. Installation Guides", "13", level=1))
    body_parts.append(toc_line("1. Environment Prerequisites & Hardware Specifications", "13", level=2))
    body_parts.append(toc_line("2. Configuration & Environment Variables Reference", "14", level=2))
    body_parts.append(toc_line("3. Infrastructure & Backing Services Deployment", "16", level=2))
    body_parts.append(toc_line("4. Backend Service Setup & Execution (.NET 10 Web API)", "17", level=2))
    body_parts.append(toc_line("5. Frontend Application Setup & Execution (Angular 22 SPA)", "19", level=2))
    body_parts.append(toc_line("6. Automated Test Suite Execution & Quality Metrics", "20", level=2))
    body_parts.append(toc_line("7. Baseline Test Accounts & Access Credentials Matrix", "21", level=2))
    body_parts.append(toc_line("III. User Manual", "23", level=1))
    body_parts.append(toc_line("1. Overview", "23", level=2))
    body_parts.append(toc_line("1.1 Dual-Plane Architectural Governance", "23", level=3))
    body_parts.append(toc_line("1.2 Four Primary User Roles & Responsibility Matrix", "24", level=3))
    body_parts.append(toc_line("1.3 Core State Machine Lifecycles", "25", level=3))
    body_parts.append(toc_line("2. Workflow 1: Company Registration & Tenant Provisioning", "28", level=2))
    body_parts.append(toc_line("3. Workflow 2: Authentication & Workspace Navigation", "32", level=2))
    body_parts.append(toc_line("4. Workflow 3: Initial Tenant & Service Configuration", "36", level=2))
    body_parts.append(toc_line("5. Workflow 4: End-to-End Task Lifecycle Management", "43", level=2))
    body_parts.append(toc_line("6. Workflow 5: End-to-End Service Request Management", "50", level=2))
    body_parts.append(toc_line("7. Workflow 6: AI Assistance & Task Decomposition", "57", level=2))
    body_parts.append(toc_line("8. Workflow 7: SLA Monitoring & Operational Reports", "61", level=2))
    body_parts.append(toc_line("9. Workflow 8: Password Recovery & Session Invalidation", "67", level=2))
    body_parts.append(page_break())

    # ================= SECTION 0: RELEASE NOTES v1.0.0 =================
    body_parts.append(heading1("0. Release Notes v1.0.0"))
    
    body_parts.append(heading2("0.1 Executive Summary"))
    body_parts.append(p("Release v1.0.0 (November 15, 2026) encapsulates all core functional and non-functional capabilities of the AI-Assisted BizFlow Platform on modern .NET 10 and Angular 22 architecture. The platform operates on a robust multi-tenant model with two strictly segregated governance planes (Platform Plane and Tenant Plane), encompassing 8 business workflows; Workflow 8 (password recovery) and multi-channel deadline notification dispatch carry identified operational limitations (see Section 0.4). The domain layer enforces strict state machine lifecycles: a 10-state machine for Tasks and a 12-state machine for Service Requests, integrated with an AI-powered task decomposition engine operating under mandatory human-in-the-loop oversight."))
    body_parts.append(p("A rigorous technical audit verified 108 out of 132 requirements (81.8% VERIFIED core, 16 PARTIAL, 6 NOT_IMPL, and 2 OUT_OF_SCOPE per docs/requirements/srs/REQUIREMENTS_TRACEABILITY.md). The user interface comprises exactly 33 functional screens (S-01 through S-33) strictly aligned with docs/BizFlow_Luong_Man_Hinh.docx, meeting MVP operational readiness."))
    body_parts.append(p("The project maintains an active registry of 13 known technical issues (KISS-01 through KISS-13); notably, one Critical vulnerability remains in backend authentication (KISS-09 / BKL-SEC-01), with client-side UI enforcement acting as an interim mitigation."))
    body_parts.append(p("Overall release readiness evaluation: CONDITIONAL GO – qualified for academic capstone defense; resolution of KISS-09 is mandatory prior to live production staging.", bold=True, size=22, color="1F4E79"))

    body_parts.append(heading2("0.2 Release Scope"))
    body_parts.append(p("Delivered functional scope matrix for Release v1.0.0:"))
    scope_headers = ["Feature / Capability", "Requirement ID", "Screen ID", "Actual Route", "Status"]
    scope_rows = [
        ["Public Enterprise Registration", "FR-TEN-001, FR-TEN-005", "S-04", "/register-company", "Done"],
        ["Tenant Provisioning & Approval Queue", "FR-TEN-002", "S-08", "/platform/companies", "Done"],
        ["Global Platform Immutable Audit Trail", "FR-AUD-001", "S-09", "/platform/audit", "Done"],
        ["Unified Multi-Tenant Authentication", "FR-AUTH-001, FR-AUTH-005", "S-01, S-02", "/login", "Done"],
        ["Self-Service Password Recovery", "FR-AUTH-003", "S-05, S-06", "/forgot-password, /reset-password", "Partial"],
        ["Role-Tailored Operational Workspace", "FR-WS-001", "S-07", "/dashboard", "Done"],
        ["Task Registry & Creation Flow", "FR-TASK-001, FR-TASK-002", "S-10, S-11", "/tasks, /tasks/new", "Done"],
        ["AI-Assisted Task Decomposition", "FR-AI-013", "S-12", "/tasks/ai-decomposition", "Done"],
        ["Task Detail & 10-State Lifecycle", "FR-TASK-003", "S-13", "/tasks/:id", "Done"],
        ["Service Request Registry & Creation", "FR-REQ-001, FR-REQ-002", "S-14, S-15", "/requests, /requests/new", "Done"],
        ["Service Request Detail & 12-State Lifecycle", "FR-REQ-003", "S-16", "/requests/:id", "Done"],
        ["Multi-Level Request Approval Queue", "FR-REQ-004", "S-17", "/approvals", "Partial"],
        ["Real-Time SLA Violation Monitoring", "FR-SLA-003", "S-18", "/sla-monitor", "Done"],
        ["Executive Operational Analytics", "FR-REP-001", "S-19", "/reports", "Done"],
        ["Centralized Archival & Document Retrieval", "FR-ARC-001", "S-20", "/records/search", "Done"],
        ["Event Notification Center", "FR-NOTIF-001", "S-21", "/notifications", "Partial"],
        ["Tenant-Level Immutable Audit Trail", "FR-AUD-002", "S-22", "/audit", "Done"],
        ["Tenant General Parameters & Branding", "FR-SET-001", "S-23", "/settings", "Done"],
        ["Enterprise Internal Service Catalog", "FR-SVC-001", "S-24", "/settings/services", "Done"],
        ["Organizational Departments & Hierarchy", "FR-ORG-002", "S-25", "/settings/organization/departments", "Done"],
        ["User Directory & Account Governance", "FR-ORG-001", "S-26", "/settings/organization/users", "Done"],
        ["Granular RBAC Roles & Permissions", "FR-SEC-001", "S-27", "/settings/organization/roles", "Done"],
        ["Visual Workflow Designer & Library", "FR-WF-001", "S-28, S-29", "/settings/workflows, /settings/workflows/:id/versions/:v", "Done"],
        ["SLA Profiles & Version Management", "FR-SLA-001, FR-SLA-002", "S-30, S-31", "/settings/sla-profiles, /settings/sla-profiles/:id/versions", "Done"],
        ["RBAC Access Denied 403 Barrier", "FR-SEC-002", "S-32", "/access-denied", "Done"],
        ["Intelligent AI Assistant Robot Widget", "FR-AI-014", "S-33", "Shared RobotWidget", "Done"]
    ]
    body_parts.append(make_table(scope_headers, scope_rows, widths=[2500, 1500, 900, 3260, 1200]))

    body_parts.append(heading2("0.3 Bug Fixes & Resolved Issues"))
    body_parts.append(p("Technical defects and architectural remediations resolved prior to Release v1.0.0:"))
    bug_headers = ["Defect ID", "Defect Description", "Severity", "Module", "Resolution Strategy", "Commit / PR", "Verified By"]
    bug_rows = [
        ["BKL-DB-01", "Company table lacked mandatory v1.2 legal identity fields (TaxId, ContactPhone, ContactPersonName) and legal document store.", "High", "Tenancy", "Implemented migration 20261021100000_CompanyRegistration6FieldsAndEvidence.cs adding 3 columns and CompanyRegistrationDocuments table.", "6c8066f", "[CẦN XÁC NHẬN NGƯỜI VERIFY]"],
        ["BKL-FNC-01", "Public registration form lacked real-time workspace code collision checks and secure MinIO upload handling.", "High", "Onboarding", "Added real-time workspace code check endpoint with 400ms debounce and presigned MinIO upload workflow.", "6c8066f", "[CẦN XÁC NHẬN NGƯỜI VERIFY]"],
        ["BKL-FNC-03", "Platform Admin could not inspect uploaded business license documents during registration review.", "High", "Platform", "Implemented secure 15-minute time-bound presigned URL retrieval endpoint for Platform Admin verification.", "6c8066f", "[CẦN XÁC NHẬN NGƯỜI VERIFY]"],
        ["BKL-FNC-04", "AI task decomposition automatically persisted records on preview, violating human control principles.", "Medium", "AI Assistant", "Segregated into preview-only (transient DTO computation) and confirm (explicit user confirmation persistence) endpoints.", "c033ca2", "[CẦN XÁC NHẬN NGƯỜI VERIFY]"],
        ["BKL-DB-02", "Lacked unique constraints to prevent duplicate deadline notification dispatch schedules across channels.", "Medium", "Notifications", "Created migration 20261102143000_DeadlineNotificationDispatchRecords.cs adding unique constraint on (TenantId, TaskId, UserId, TriggerType, ScheduledAt).", "6b3663b", "[CẦN XÁC NHẬN NGƯỜI VERIFY]"],
        ["BKL-FNC-05", "Chatbot widget open/close toggle state did not persist across navigation tabs.", "Low", "Frontend", "Stored widget visibility state in sessionStorage to prevent unwanted expansion upon navigation.", "4508805", "[CẦN XÁC NHẬN NGƯỜI VERIFY]"]
    ]
    body_parts.append(make_table(bug_headers, bug_rows, widths=[1100, 2400, 800, 1100, 2060, 900, 1000]))
    body_parts.append(p("Summary: 6 out of 7 major defects resolved; 1 defect (BKL-SEC-01) remains open under KISS-09.", bold=True, size=21, color="1F4E79", space_before=60, space_after=120))

    body_parts.append(heading2("0.4 Known Issues & Limitations"))
    body_parts.append(p("Inventory of known issues, technical root causes, mitigations, and scheduled remediation roadmap:"))
    known_headers = ["Issue ID", "Description", "Severity", "Technical Root Cause", "Workaround / Mitigation", "Roadmap"]
    known_rows = [
        ["KISS-01", "Magic Link authentication does not dispatch physical emails.", "Medium", "Production SMTP service not configured; magic link button hidden on primary login view.", "Authenticate via Workspace Code + Username + Password or Google Sign-In.", "Targeted for Sprint v1.0.1."],
        ["KISS-02", "Self-service password recovery is disabled by default.", "Medium", "ResetEmail__Enabled defaults to false to prevent runtime mailer exceptions; API returns AUTH.RESET_UNAVAILABLE.", "System Administrator configures TLS-enabled SMTP host and enables ResetEmail__Enabled=true.", "Admin SMTP configuration UI."],
        ["KISS-03", "Baseline database seed does not provision a Super Admin account.", "Medium", "deploy/seed_enterprise_simulation.sql sets IsPlatformAdministrator = FALSE for all users.", "Execute Step 4.4b: run manual psql script to insert Super Admin and PLATFORM_ADMIN role.", "Official seed script inclusion."],
        ["KISS-04", "Nginx Reverse Proxy enforces default 1MB upload limit.", "Medium", "deploy/nginx.conf lacks explicit client_max_body_size directive (defaults to 1MB) while Backend allows up to 500MB.", "Inject client_max_body_size 500M; into server block, or utilize local development mode.", "App container image update."],
        ["KISS-05", "Dynamic multi-tier approval routing is partially supported.", "Medium", "JSON graph schema supports complex conditions but frontend visual triggers are limited.", "Utilize department-level approvals and single-tier review in /approvals (SUBMITTED filter).", "Workflow Designer expansion."],
        ["KISS-06", "AI Assistant services rely on external OpenAI API connectivity.", "Low", "When internet connectivity is absent or OpenAI__ApiKey is missing, falls back to heuristic engine.", "System gracefully executes heuristic categorization based on internal service catalog.", "Local LLM integration."],
        ["KISS-07", "Certain UI labels and API error messages display in English.", "Low", "Inherited from domain token assets and standard error codes.", "Consult technical glossary and API specification mapping.", "Full i18n localization."],
        ["KISS-08", "Development credentials stored in appsettings.Development.json.", "Low", "Local development template configuration for evaluation convenience.", "Permissible solely in dev/evaluation environments; production utilizes external Secret Manager.", "Clean production container image."],
        ["KISS-09", "Omitting Workspace Code permits tenant login when identifier belongs to a single tenant (BKL-SEC-01 open).", "Critical", "AuthenticationStore.FindCandidatesAsync does not enforce account plane segregation.", "Frontend web UI strictly requires Workspace Code for standard users; risk confined to direct API calls.", "Patch in v1.0.1."],
        ["KISS-10", "Multi-channel deadline escalation (SignalR -> Web Push -> Email) incomplete (BKL-FNC-06).", "Medium", "Background scheduling worker for multi-tier channel dispatch is not yet finalized.", "Monitor approaching deadlines via /sla-monitor and /notifications.", "Patch in v1.0.1."],
        ["KISS-11", "Automated test suite contains known failures: 2/270 unit tests, 4/6 E2E tests, 89 Prettier formatting errors.", "Low", "Outdated mock test locators and cancelled request handler in users.spec.ts.", "Does not impact runtime execution; application builds successfully.", "Patch in v1.0.1."],
        ["KISS-12", "Docker deployment: local file uploads stored on /tmp (tmpfs) and lost upon container restart.", "Medium", "deploy/docker-compose.app.yml does not mount persistent MinIO storage for API container.", "Utilize local development mode (Sections II.4-II.5) during capstone evaluation.", "Patch in v1.0.1."],
        ["KISS-13", "Docker container hardcodes ResetEmail__Enabled=\"false\".", "Low", "Designed as safe default in containerized demo overlay.", "Utilize development mode and configure external SMTP parameters.", "—"]
    ]
    body_parts.append(make_table(known_headers, known_rows, widths=[900, 1800, 800, 1800, 2760, 1300]))

    body_parts.append(heading2("0.5 Deployment & Rollback Strategy"))
    body_parts.append(p("Standard containerized release deployment and disaster recovery procedure based on deploy/docker-compose.app.yml:"))
    
    body_parts.append(p_runs([
        ("•  Step 1: Environment Variables Preparation: ", True, False, 22, "1F4E79", False),
        ("Generate a cryptographically strong 32-byte JWT Signing Key using ", False, False, 22, "374151", False),
        ("openssl rand -base64 32", False, False, 20, "111827", True),
        (" and configure JWT_SIGNING_KEY_BASE64 in deploy/.env (the placeholder in .env.example is not valid Base64; the API will fail to start if omitted). Ensure BIZFLOW_DATABASE_CONNECTION password matches POSTGRES_PASSWORD.", False, False, 22, "374151", False)
    ]))
    
    body_parts.append(p_runs([
        ("•  Step 2: Pre-Release Database Backup: ", True, False, 22, "1F4E79", False),
        ("Execute full schema and data dump in custom binary format (-Fc):", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block("docker compose -f deploy/docker-compose.yml exec -T postgres pg_dump -U bizflow -d bizflow -Fc > backup_pre_v1.0.0.dump"))

    body_parts.append(p_runs([
        ("•  Step 3: Preserve Active Images for Instant Rollback: ", True, False, 22, "1F4E79", False),
        ("Tag active images with the fallback tag :prev:", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block("docker tag bizflow/api:local bizflow/api:prev\ndocker tag bizflow/web:local bizflow/web:prev"))

    body_parts.append(p_runs([
        ("•  Step 4: Build Application Container Images: ", True, False, 22, "1F4E79", False),
        ("Compile and assemble packaged application artifacts:", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block("docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml build"))

    body_parts.append(p_runs([
        ("•  Step 5: Apply Database Schema Migrations: ", True, False, 22, "1F4E79", False),
        ("Execute schema updates using the standalone migrate service (profile tools):", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block("docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml --profile tools run --rm migrate"))

    body_parts.append(p_runs([
        ("•  Step 6: Launch Application Services: ", True, False, 22, "1F4E79", False),
        ("Start application daemon containers in detached mode:", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block("docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d api web"))

    body_parts.append(p_runs([
        ("•  Step 7: Smoke Test Execution & Health Verification: ", True, False, 22, "1F4E79", False),
        ("Validate service health endpoints exposed via Web port 8080:", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block("curl -f http://localhost:8080/health/live\ncurl -f http://localhost:8080/health/ready\ncurl -f http://localhost:8080/login"))
    body_parts.append(p("Authenticate via browser using seeded credentials: admin@bizflow.local (Workspace Code: bizflow-corp | Password: Password123!)."))

    body_parts.append(p_runs([
        ("•  Step 8: Expected Service Downtime: ", True, False, 22, "1F4E79", False),
        ("[CẦN ĐO THỰC TẾ]", False, True, 22, "C00000", False)
    ]))

    body_parts.append(p("Rollback Strategy for Critical Failure Scenarios:", bold=True, size=22, color="1F4E79", space_before=120, space_after=60))
    rb_headers = ["Failure Scenario", "Rollback Procedure", "Execution Command", "Recovery Objective (RTO)"]
    rb_rows = [
        ["Application / UI Fault", "Revert to previous Docker image (:prev)", "docker tag bizflow/api:prev bizflow/api:local && docker tag bizflow/web:prev bizflow/web:local && docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d --no-build api web", "[CẦN ĐO THỰC TẾ]"],
        ["Database Migration Failure", "Rollback schema to preceding stable migration (TaskParentTaskAndAiLearnedChoice)", "dotnet ef database update 20261111090436_TaskParentTaskAndAiLearnedChoice --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure", "[CẦN ĐO THỰC TẾ]"],
        ["Data Corruption Catastrophe", "Restore clean state from pre-release custom binary backup (-Fc)", "docker compose -f deploy/docker-compose.yml exec -T postgres pg_restore -U bizflow -d bizflow --clean --if-exists < backup_pre_v1.0.0.dump", "[CẦN ĐO THỰC TẾ]"]
    ]
    body_parts.append(make_table(rb_headers, rb_rows, widths=[1800, 2400, 3760, 1400]))
    body_parts.append(p("Note: Nginx Reverse Proxy in deploy/nginx.conf enforces a 1MB upload ceiling (KISS-04). For capstone evaluation requiring large attachments, utilize local development mode.", italic=True, size=20, color="555555", space_before=60, space_after=120))

    body_parts.append(heading2("0.6 Post-Release Monitoring & Operational Metrics"))
    body_parts.append(p("Key operational metrics and health thresholds for continuous post-release monitoring:"))
    mon_headers = ["Monitoring Target", "Standard Threshold", "Observation Source", "Incident Response Action", "Owner", "Interval"]
    mon_rows = [
        ["Readiness Probe (/health/ready)", "HTTP 200 OK (< 200ms)", "Docker Probe: http://localhost:8080/health/ready", "If 503 returned: inspect PostgreSQL connection pool and migration state.", "[CẦN PHÂN CÔNG]", "Every 15 min for 24h"],
        ["5xx Server Error Rate", "< 0.5% of total requests", "Serilog / Docker API logs", "Examine unhandled exceptions in backend container logs.", "[CẦN PHÂN CÔNG]", "Every 15 min for 24h"],
        ["Hangfire Failed Jobs", "0 consecutive failures", "PostgreSQL tables hangfire.job / hangfire.state", "Check ResetEmail__* SMTP host parameters and DB pool.", "[CẦN PHÂN CÔNG]", "Every 15 min for 24h"],
        ["AI Assistant Error Rate", "< 2% of total prompts", "OpenAiModelClient logs", "Engine falls back to heuristic rule-base; inspect OpenAI quota.", "[CẦN PHÂN CÔNG]", "Every 15 min for 24h"],
        ["Container Resource Utilization", "CPU < 80%, RAM < 85%", "docker stats", "Restart daemon or increase memory allocation ceiling.", "[CẦN PHÂN CÔNG]", "Every 15 min for 24h"]
    ]
    body_parts.append(make_table(mon_headers, mon_rows, widths=[1800, 1500, 2100, 2360, 800, 800]))

    body_parts.append(heading2("0.7 Sign-off & Release Approval"))
    body_parts.append(p("Formal sign-off matrix for Release v1.0.0 authorization:"))
    sign_headers = ["Project Role", "Full Name", "Approval Date", "Signature / Status"]
    sign_rows = [
        ["Product Owner", "[CẦN BỔ SUNG]", "15/11/2026", "[CẦN KÝ]"],
        ["Technical Lead", "[CẦN BỔ SUNG]", "15/11/2026", "[CẦN KÝ]"],
        ["QA / Test Lead", "[CẦN BỔ SUNG]", "15/11/2026", "[CẦN KÝ]"],
        ["Release Manager", "[CẦN BỔ SUNG]", "15/11/2026", "[CẦN KÝ]"],
        ["Academic Advisor (GVHD)", "[CẦN BỔ SUNG]", "15/11/2026", "[CẦN KÝ]"]
    ]
    body_parts.append(make_table(sign_headers, sign_rows, widths=[2500, 2500, 2000, 2360]))

    body_parts.append(heading2("0.8 Compatibility & Breaking Changes Notes"))
    body_parts.append(p("Critical compatibility guidelines and breaking behavioral changes in Release v1.0.0:"))
    body_parts.append(p("1. Database Schema Lineage: The system incorporates exactly 38 Entity Framework Core migrations. Schema updates must be executed sequentially from initial creation up to 20261111100000_GranularUserManagementPermissions. Non-linear or skipped migration rollback is strictly prohibited."))
    body_parts.append(p("2. Divergence Between Docker and Development Environments:"))
    body_parts.append(p("   • Network Endpoints: Docker overlay serves traffic through unified Web port 8080 (http://localhost:8080); Development environment operates segregated ports for Frontend 4200 (http://localhost:4200) and Backend API 5154 (http://localhost:5154)."))
    body_parts.append(p("   • File Attachment Storage: Docker setup utilizes /tmp (tmpfs) temporary storage, meaning local uploads are wiped upon container destruction (KISS-12); Development setup connects to MinIO S3 bucket or host persistent volume."))
    body_parts.append(p("   • Password Recovery: Docker deployment hardcodes ResetEmail__Enabled=\"false\" (KISS-13); Development mode enables mail dispatch when configured with valid SMTP parameters."))
    body_parts.append(p("3. Authentication Breaking Change: Standard enterprise users must explicitly supply their WorkspaceKey during authentication to prevent tenant ambiguity (KISS-09)."))
    body_parts.append(page_break())

    # ================= SECTION I. DELIVERABLE PACKAGE =================
    body_parts.append(heading1("I. Deliverable Package"))
    
    headers_pkg = ["No.", "File Name", "Version", "Date", "Description & Technical Notes"]
    rows_pkg = [
        [
            "1",
            "Fall26_SE20B02_G3_DB_final.sql [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.0.0",
            "15/11/2026",
            "Complete PostgreSQL 18 database script incorporating all 38 EF Core migrations, table partitions, foreign keys, indexes, and seeded enterprise simulation data."
        ],
        [
            "2",
            "Fall26_SE20B02_G3_SRS_final.docx [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.1",
            "15/11/2026",
            "Software Requirements Specification document detailing 16 core Use Cases (UC-001 through UC-016), functional requirement modules (FR-AUTH, FR-TEN, FR-TASK, FR-REQ, FR-SLA, FR-AI, FR-AUD), and non-functional specifications."
        ],
        [
            "3",
            "Fall26_SE20B02_G3_SDS_final.docx [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.1",
            "15/11/2026",
            "Software Design Specification document detailing Clean Architecture layers, ERD schemas, sequence diagrams, Task/Request state machines, and RESTful API catalogs."
        ],
        [
            "4",
            "Fall26_SE20B02_G3_Final_Product_Backlog.xlsx [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.0.0",
            "15/11/2026",
            "Final product backlog reflecting completion tracking across 3 iterations. Audited verification confirms 108/132 requirements verified (81.8%) and 33 screens operational for MVP."
        ],
        [
            "5",
            "Fall26_SE20B02_G3_Issues_Report.xlsx [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.0.0",
            "15/11/2026",
            "Comprehensive issue and defect registry tracking all lifecycle defects, root causes, severity ratings, resolution strategies, and verification statuses."
        ],
        [
            "6",
            "Fall26_SE20B02_G3_Screen_Flow_Guide.docx [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.0.0",
            "15/11/2026",
            "Screen flow guide detailing navigation transitions across all 33 user interface screens (S-01 to S-33), 4 user roles, and 8 business workflows."
        ],
        [
            "7",
            "Fall26_SE20B02_G3_Backend_Source_Code.zip [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.0.0",
            "15/11/2026",
            "Complete backend solution source code developed on .NET 10, C# 13, Entity Framework Core 10, ASP.NET Core Web API, Hangfire Background Worker, and SignalR Hub."
        ],
        [
            "8",
            "Fall26_SE20B02_G3_Frontend_Source_Code.zip [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.0.0",
            "15/11/2026",
            "Modern frontend single-page application source code developed on Angular 22, TypeScript, Tailwind CSS, Standalone Components, Angular Signals, and Vitest / Playwright."
        ],
        [
            "9",
            "Fall26_SE20B02_G3_Deployment_Package.zip [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]",
            "v1.0.0",
            "15/11/2026",
            "Containerized deployment package containing docker-compose.yml, docker-compose.app.yml, Dockerfiles, Nginx Reverse Proxy configs, .env.example, and verification health check probes."
        ]
    ]
    body_parts.append(make_table(headers_pkg, rows_pkg, widths=[600, 2800, 900, 1100, 3960]))
    
    body_parts.append(p("Note: Fall26_SE20B02_G3 represents the designated student capstone project group identifier. [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]", italic=True, size=21, color="555555", space_before=160, space_after=180))
    
    body_parts.append(p("Other related deliverables:", bold=True, size=23, color="1F2937", space_before=120, space_after=80))
    body_parts.append(p_runs([
        ("•  Tagged source codes: ", True, False, 22, "1F2937", False),
        ("https://github.com/TxRyeSayNo/AI-Assisted-BizFlow-Platform/releases/tag/v1.0.0 [CẦN TẠO TAG: git tag -a v1.0.0 -m 'Release v1.0.0']", False, False, 22, "005BAA", True)
    ], space_before=40, space_after=60))
    body_parts.append(p_runs([
        ("•  Demonstration video: ", True, False, 22, "1F2937", False),
        ("[CẦN DÁN LINK THẬT: Video demo YouTube 11 ký tự, ví dụ: https://youtu.be/XXXXXXXXXXX]", False, False, 22, "005BAA", True)
    ], space_before=40, space_after=240))
    
    body_parts.append(page_break())

    # ================= SECTION II. INSTALLATION GUIDES =================
    body_parts.append(heading1("II. Installation Guides"))
    
    body_parts.append(heading2("1. Environment Prerequisites & Hardware Specifications"))
    body_parts.append(p("Prior to installation, the target host or developer workstation must satisfy the following hardware and software prerequisites:"))
    
    prereq_headers = ["Component", "Minimum Requirement", "Recommended Specification"]
    prereq_rows = [
        ["Operating System", "Windows 10/11 x64, macOS 13+, or Ubuntu 22.04 LTS", "Windows 11 Pro 64-bit with WSL2 enabled"],
        ["Hardware Capacity", "4-core CPU @ 2.0 GHz, 8 GB RAM, 10 GB available SSD", "8-core CPU @ 3.2 GHz, 16 GB+ RAM, NVMe SSD storage"],
        [".NET SDK", ".NET SDK 10.0.100+", ".NET SDK 10.0.401 (pinned in global.json)"],
        ["Node.js & npm", "Node.js v22.17.0 LTS / npm 10.8+", "Node.js v22.22.3 LTS & npm 11.6.2 (pinned in frontend/.nvmrc)"],
        ["Container Engine", "Docker Desktop 4.25+ (Linux containers mode)", "Docker Desktop 4.34+ (Linux engine, 4GB+ allocated RAM)"],
        ["Backing Services", "PostgreSQL 17.x, Redis 7.x, S3-Compatible Storage", "PostgreSQL 18.3-alpine, Redis 7-alpine, MinIO S3"]
    ]
    body_parts.append(make_table(prereq_headers, prereq_rows, widths=[2200, 3500, 3660]))

    body_parts.append(heading2("2. Configuration & Environment Variables Reference"))
    body_parts.append(p("Consolidated specification of environment variables consumed by the backend application (defined in deploy/.env.example, Program.cs, PasswordResetRegistration.cs):"))
    
    env_headers = ["Environment Variable", "Required", "Purpose & Default Value", "Fallback Mechanism"]
    env_rows = [
        ["ConnectionStrings__BizFlow", "Yes", "PostgreSQL connection string (default: Port 5432, DB: bizflow, User: bizflow).", "None. Startup halts immediately if missing."],
        ["Jwt__Issuer", "Yes", "JWT token issuing authority name (default: BizFlow).", "None. Startup halts immediately if missing."],
        ["Jwt__Audience", "Yes", "JWT token audience identifier (default: BizFlow.Web).", "None. Startup halts immediately if missing."],
        ["Jwt__SigningKeyBase64", "Yes", "HMAC-SHA256 Base64-encoded secret signing key (minimum 32 bytes).", "None. Startup halts immediately if missing."],
        ["Redis__ConnectionString", "No", "Redis cache endpoint connection string (e.g. localhost:6379).", "Automatically falls back to in-memory IMemoryCache."],
        ["Storage__Provider / Endpoint / PublicEndpoint / Bucket / AccessKey / SecretKey", "No", "MinIO/S3 object storage configuration for file attachments.", "Falls back to local file storage only when Provider is not MinIO/S3. In Development, appsettings.Development.json enables MinIO; credentials must match deploy/.env."],
        ["OpenAI__ApiKey / Model / BaseUrl / TimeoutSeconds", "No", "OpenAI API authentication key and model name (default: gpt-4o-mini).", "Automatically activates heuristic rule-based categorization."],
        ["ResetEmail__Enabled / Host / Port / Username / Password / FromAddress / FrontendOrigin", "No", "SMTP configuration for self-service password recovery emails.", "Defaults to Enabled=false; API returns AUTH.RESET_UNAVAILABLE if disabled."],
        ["PasswordReset__TokenLifetimeMinutes", "No", "Validity lifespan of password recovery tokens (default: 15 minutes).", "Defaults to 15 minutes if unconfigured."]
    ]
    body_parts.append(make_table(env_headers, env_rows, widths=[2600, 900, 3100, 2760]))

    body_parts.append(warning_callout("All passwords, database credentials, and cryptographic signing keys in this document are DEMO / EVALUATION SAMPLES. UNDER NO CIRCUMSTANCES should these values be reused in Production environments!"))

    body_parts.append(heading2("3. Infrastructure & Backing Services Deployment"))
    body_parts.append(p("The platform utilizes Docker Compose to provision PostgreSQL 18.3-alpine, Redis 7-alpine, and MinIO S3-compatible storage (image pgsty/silo)."))
    
    body_parts.append(p("Step 3.1: Copy environment template configuration:"))
    body_parts.append(p_runs([("PowerShell 7:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block("Copy-Item deploy\\.env.example deploy\\.env"))
    body_parts.append(p_runs([("Bash:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block("cp deploy/.env.example deploy/.env"))
    
    body_parts.append(p("Step 3.2: Spin up infrastructure containers in detached mode:"))
    body_parts.append(p_runs([("PowerShell 7 & Bash:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block("docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d"))
    body_parts.append(p("Verify container health (default container names: bizflow-local-postgres-1, bizflow-local-redis-1, bizflow-local-minio-1):"))
    body_parts.append(code_block("docker compose ps"))

    body_parts.append(heading2("4. Backend Service Setup & Execution (.NET 10 Web API)"))
    body_parts.append(p("Step 4.1: Restore .NET local tools and NuGet packages:"))
    body_parts.append(code_block("dotnet tool restore\ndotnet restore BizFlow.slnx"))
    
    body_parts.append(p("Step 4.2: Export runtime database connection and security parameters:"))
    body_parts.append(p("Note: If POSTGRES_PORT is customized in deploy/.env, adjust the Port parameter accordingly.", italic=True, size=21, color="1F4E79"))
    body_parts.append(p_runs([("PowerShell 7:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block(
        "$env:ConnectionStrings__BizFlow = 'Host=127.0.0.1;Port=5432;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret'\n"
        "$env:Jwt__Issuer = 'BizFlow'\n"
        "$env:Jwt__Audience = 'BizFlow.Web'\n"
        "$env:Jwt__SigningKeyBase64 = 'Loizdg9h99sl7lZJSV1LJwNCNTIdi7PjAtR3yib+brk='\n"
        "$env:Storage__AccessKey = 'bizflow'\n"
        "$env:Storage__SecretKey = 'replace-with-a-unique-local-secret'"
    ))
    body_parts.append(p_runs([("Bash:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block(
        "export ConnectionStrings__BizFlow='Host=127.0.0.1;Port=5432;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret'\n"
        "export Jwt__Issuer='BizFlow'\n"
        "export Jwt__Audience='BizFlow.Web'\n"
        "export Jwt__SigningKeyBase64='Loizdg9h99sl7lZJSV1LJwNCNTIdi7PjAtR3yib+brk='\n"
        "export Storage__AccessKey='bizflow'\n"
        "export Storage__SecretKey='replace-with-a-unique-local-secret'"
    ))
    
    body_parts.append(p("Step 4.3: Apply EF Core database schema migrations (38 migrations):"))
    body_parts.append(code_block("dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure"))
    
    body_parts.append(p("Step 4.4 (MANDATORY FOR EVALUATORS): Seed enterprise simulation dataset:"))
    body_parts.append(p_runs([("PowerShell 7 & Bash:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block("docker compose -f deploy/docker-compose.yml exec -T postgres psql -U bizflow -d bizflow < deploy/seed_enterprise_simulation.sql"))

    body_parts.append(p("Step 4.4b: Provision Super Admin Account (Required for Workflow 1 evaluation):", bold=True, size=22, color="C00000", space_before=100, space_after=40))
    body_parts.append(p("Because the baseline SQL seed does not provision a platform superadmin (KISS-03), evaluators must open a terminal session:"))
    body_parts.append(code_block("docker compose -f deploy/docker-compose.yml exec -T postgres psql -U bizflow -d bizflow"))
    body_parts.append(p("and paste the following SQL block to create the Super Admin user and assign the PLATFORM_ADMIN system role:"))
    body_parts.append(code_block(
        'INSERT INTO "User" ("UserId","TenantId","IsPlatformAdministrator","EmployeeCode","NormalizedEmployeeCode","Email","NormalizedEmail","PasswordHash","SecurityStamp","FullName","DepartmentId","Status","AccessFailedCount","MustChangePassword","CreatedAt","UpdatedAt")\n'
        "VALUES ('019f8000-0000-7000-8000-0000000000ff', NULL, TRUE, 'SUPERADMIN', 'SUPERADMIN', 'platform@bizflow.local', 'PLATFORM@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_SUPERADMIN', 'Platform Administrator', NULL, 'ACTIVE', 0, FALSE, NOW(), NOW())\n"
        'ON CONFLICT ("UserId") DO NOTHING;\n'
        'INSERT INTO "UserRole" ("UserId","RoleId") SELECT \'019f8000-0000-7000-8000-0000000000ff\', "RoleId" FROM "Role" WHERE "Name"=\'PLATFORM_ADMIN\' AND "IsSystem" AND "TenantId" IS NULL ON CONFLICT DO NOTHING;'
    ))
    body_parts.append(p("Note: If accessing /platform/audit results in a 403 Forbidden, verify that the platform.audit.read permission is mapped to the PLATFORM_ADMIN role [CẦN XÁC NHẬN].", italic=True, size=20, color="555555"))
    
    body_parts.append(p("Step 4.5: Launch Backend Web API service:"))
    body_parts.append(code_block("dotnet run --project backend/BizFlow.Api"))
    body_parts.append(p("The Web API listens at http://localhost:5154. Validate probe readiness at http://localhost:5154/health/ready and inspect OpenAPI Swagger specifications at http://localhost:5154/openapi/v1.json (Development mode only)."))

    body_parts.append(heading2("5. Frontend Application Setup & Execution (Angular 22 SPA)"))
    body_parts.append(p("Step 5.1: Open a secondary terminal, navigate to the frontend directory, and install dependencies from package-lock.json:"))
    body_parts.append(code_block("cd frontend\nnpm ci"))
    
    body_parts.append(p("Step 5.2: Launch Angular development server:"))
    body_parts.append(code_block("npm start"))
    body_parts.append(p("The single-page application is available at http://localhost:4200. The dev server automatically proxies /api requests to the Backend API on port 5154."))

    body_parts.append(heading2("6. Automated Test Suite Execution & Quality Metrics"))
    body_parts.append(p("Recorded automated testing metrics across the codebase (recorded November 15, 2026):"))
    test_met_headers = ["Test Suite", "Execution Command", "Recorded Outcome", "Technical Notes"]
    test_met_rows = [
        ["Backend Unit Tests", 'dotnet test BizFlow.slnx --filter "Category=Unit"', "495/495 (as of 01/11/2026 report, pending re-run) [CẦN CHẠY LẠI]", "Requires comprehensive test re-execution"],
        ["Backend Integration Tests", 'dotnet test BizFlow.slnx --filter "Category=Integration"', "[CHƯA CHẠY]", "Requires active Testcontainers Docker runtime"],
        ["Frontend Unit Tests (Vitest)", "cd frontend && npm test -- --watch=false", "268/270 tests PASS (99.2%)", "2 known failures: users.spec.ts (pending retrieval cancellation) and requests.spec.ts in concurrent suite"],
        ["End-to-End Mock Tests (Playwright)", "cd tests/BizFlow.E2ETests && npm test", "2/6 tests PASS (33.3%)", "4 outdated mock tests failing against localized Vietnamese UI"],
        ["Source Code Prettier Check", "cd frontend && npm run format:check", "89 unformatted files detected", "Will fail CI pipeline format:check gate"]
    ]
    body_parts.append(make_table(test_met_headers, test_met_rows, widths=[2200, 2600, 2300, 2260]))

    body_parts.append(heading2("7. Baseline Test Accounts & Access Credentials Matrix"))
    body_parts.append(p("Baseline demonstration accounts provisioned from deploy/seed_enterprise_simulation.sql and Step 4.4b:"))
    body_parts.append(p("Workspace Code: bizflow-corp | Shared Password: Password123!", bold=True, size=22, color="1F4E79", space_before=40, space_after=100))
    
    acc_headers = ["Role", "Email / Account", "Employee Code", "Full Name & Title", "Department", "Workspace", "Password"]
    acc_rows = [
        ["Super Admin", "platform@bizflow.local", "SUPERADMIN", "Platform Administrator", "NULL (Platform Plane)", "Workspace: leave blank", "Password123!"],
        ["Company Admin", "admin@bizflow.local", "ADMIN001", "Phạm Minh Đức (CEO)", "Executive Leadership (EXEC)", "bizflow-corp", "Password123!"],
        ["Executive Board", "huong.nguyen@bizflow.local", "EXEC001", "Nguyễn Thị Mai Hương (COO)", "Executive Leadership (EXEC)", "bizflow-corp", "Password123!"],
        ["Department Manager", "manager@bizflow.local", "MGR001", "Lê Hoàng Nam (CTO)", "Technology & Digital Transformation (TECH)", "bizflow-corp", "Password123!"],
        ["DevOps Engineer", "huy.do@bizflow.local", "TECH002", "Đỗ Quang Huy (DevOps)", "Technology & Digital Transformation (TECH)", "bizflow-corp", "Password123!"],
        ["Employee (Dev)", "employee@bizflow.local", "EMP001", "Nguyễn Văn Nhân Viên (Dev)", "Technology & Digital Transformation (TECH)", "bizflow-corp", "Password123!"],
        ["Department Manager", "dat.nguyen@bizflow.local", "SALES_MGR", "Nguyễn Tiến Đạt (Sales Lead)", "B2B Sales & Partnerships (SALES)", "bizflow-corp", "Password123!"],
        ["Employee (Sales)", "kiet.hoang@bizflow.local", "SALES001", "Hoàng Tuấn Kiệt (B2B Specialist)", "B2B Sales & Partnerships (SALES)", "bizflow-corp", "Password123!"],
        ["Department Manager", "thao.vu@bizflow.local", "MKT_MGR", "Vũ Phương Thảo (Marketing Lead)", "Marketing & Communications (MKT)", "bizflow-corp", "Password123!"],
        ["Employee (Content)", "linh.le@bizflow.local", "MKT001", "Lê Mỹ Linh (Content Creator)", "Marketing & Communications (MKT)", "bizflow-corp", "Password123!"]
    ]
    body_parts.append(make_table(acc_headers, acc_rows, widths=[1300, 1900, 1100, 1800, 1600, 1160, 500]))
    
    body_parts.append(page_break())

    # ================= SECTION III. USER MANUAL =================
    body_parts.append(heading1("III. User Manual"))
    
    body_parts.append(heading2("1. Overview"))
    body_parts.append(p("The AI-Assisted BizFlow Platform is a comprehensive multi-enterprise task and service orchestration system supporting automated state machine transitions, strict SLA deadline tracking, and human-in-the-loop AI-assisted task decomposition."))

    body_parts.append(heading3("1.1 Dual-Plane Architectural Governance"))
    body_parts.append(p("The platform enforces strict administrative boundary segregation between two operational planes:"))
    body_parts.append(p_runs([
        ("•  Platform Plane (Global Governance): ", True, False, 22, "1F4E79", False),
        ("Reserved exclusively for Platform Administrators via ", False, False, 22, "374151", False),
        ("/platform/companies", False, False, 20, "111827", True),
        (" and ", False, False, 22, "374151", False),
        ("/platform/audit", False, False, 20, "111827", True),
        (". Responsible for evaluating new enterprise registration dossiers, atomic tenant database provisioning, and inspecting immutable platform-wide audit trails.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_superadmin", "Figure 1: Platform Management Plane Workspace Overview", img_rids))
    
    body_parts.append(p_runs([
        ("•  Tenant Plane (Enterprise Operations): ", True, False, 22, "1F4E79", False),
        ("Dedicated to tenant members via ", False, False, 22, "374151", False),
        ("/dashboard", False, False, 20, "111827", True),
        (" (or ", False, False, 22, "374151", False),
        ("/workspace", False, False, 20, "111827", True),
        ("). Data across enterprises is strictly isolated by TenantId, governing departmental structures, RBAC permissions, service catalogs, workflows, and SLA profiles.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_admin", "Figure 2: Tenant Operations Plane Workspace Overview", img_rids))

    body_parts.append(heading3("1.2 Four Primary User Roles & Responsibility Matrix"))
    roles_headers = ["User Role", "Governance Scope", "Primary Responsibilities", "Primary Accessible Routes"]
    roles_rows = [
        ["Super Admin", "Platform Plane", "Audit registration dossiers, approve tenant provisioning, monitor platform audit trail.", "/platform/companies, /platform/audit"],
        ["Company Admin", "Tenant Plane", "Configure tenant parameters, departments, user directory, RBAC roles, services, workflows, and SLAs.", "/dashboard, /settings, /settings/organization/*, /settings/services, /settings/workflows, /settings/sla-profiles, /audit"],
        ["Department Manager", "Department Scope", "Create tasks, AI decomposition, assign work, review service requests, confirm task completion, track SLAs.", "/dashboard, /tasks, /tasks/new, /tasks/ai-decomposition, /requests, /approvals, /reports, /sla-monitor"],
        ["Employee", "Assigned / Own Scope", "Submit service requests, accept assigned tasks, execute work, upload evidence files, search records, receive alerts.", "/dashboard, /tasks, /requests, /requests/new, /notifications"]
    ]
    body_parts.append(make_table(roles_headers, roles_rows, widths=[1800, 1500, 3660, 2400]))

    body_parts.append(heading3("1.3 Core State Machine Lifecycles"))
    body_parts.append(p("The domain layer enforces strict state machine lifecycle management. Priority levels across Tasks and Requests comprise 4 standardized tiers: Low, Medium, High, Critical."))

    body_parts.append(p("Table 1: Task State Machine Transition Matrix (TaskLifecyclePolicy - 10 States):", bold=True, size=21, color="1F4E79", space_before=80, space_after=40))
    task_sm_headers = ["Current State", "Next State", "Condition & Required Evidence", "Required Permission"]
    task_sm_rows = [
        ["Draft or Rejected", "Assigned", "Requires valid assigned department or individual user", "tasks.assign"],
        ["Assigned", "Accepted", "Assignee formally confirms task acceptance", "tasks.accept"],
        ["Assigned", "Rejected", "Assignee rejects task, mandatory rejection justification", "tasks.accept"],
        ["Accepted or Overdue", "InProgress", "Assignee initiates execution", "tasks.execute"],
        ["InProgress", "Submitted", "Mandatory submission of delivery evidence files", "tasks.submit"],
        ["Submitted", "Confirmed", "Designated reviewer verifies and approves outcome", "tasks.confirm"],
        ["Submitted", "InProgress", "Reviewer requests rework, returning task to active execution", "tasks.confirm"],
        ["Confirmed", "Completed", "System automatically concludes task lifecycle", "System Only"],
        ["InProgress or Submitted", "Overdue", "Deadline breached or SLA violation detected", "System Only"],
        ["Any (except Completed, Cancelled)", "Cancelled", "Task cancelled with mandatory audit justification", "tasks.cancel"]
    ]
    body_parts.append(make_table(task_sm_headers, task_sm_rows, widths=[2200, 1800, 3560, 1800]))

    body_parts.append(p("Table 2: Service Request Transition Matrix (RequestLifecyclePolicy - 12 States):", bold=True, size=21, color="1F4E79", space_before=120, space_after=40))
    req_sm_headers = ["Current State", "Next State", "Condition & Required Evidence", "Required Permission"]
    req_sm_rows = [
        ["Draft", "Submitted", "Requester submits formal service request", "requests.submit"],
        ["Submitted", "Routed", "Request routed to responsible functional department", "requests.route"],
        ["Routed", "Received", "Department acknowledges and queues request", "requests.receive"],
        ["Received", "InProgress", "Specialist commences request fulfillment", "requests.process"],
        ["InProgress", "WaitingForInformation", "Suspended awaiting additional information from requester", "requests.process"],
        ["WaitingForInformation", "InProgress", "Fulfillment resumes upon receipt of required information", "requests.process"],
        ["InProgress", "Resolved", "Specialist inputs resolution summary and marks resolved", "requests.resolve"],
        ["Resolved", "Confirmed", "Requester confirms satisfaction with resolution", "requests.confirm"],
        ["Resolved", "InProgress", "Requester rejects resolution, returning to in-progress", "requests.confirm"],
        ["Confirmed", "Closed", "System permanently closes fulfilled request", "System Only"],
        ["Submitted or Routed", "Rejected", "Request rejected with mandatory justification", "requests.reject"],
        ["InProgress, Received...", "Overdue", "SLA commitment target elapsed", "System Only"],
        ["Any (except Closed, Cancelled, Rejected)", "Cancelled", "Request cancelled with mandatory justification", "requests.cancel"]
    ]
    body_parts.append(make_table(req_sm_headers, req_sm_rows, widths=[2200, 1800, 3560, 1800]))
    body_parts.append(page_break())

    # ================= WORKFLOWS 1 - 8 =================
    # --- Workflow 1 ---
    body_parts.append(heading2("2. Workflow 1: Company Registration & Tenant Provisioning"))
    body_parts.append(p("Objective: Receive new enterprise registration applications, verify legal credentials, and execute atomic tenant workspace provisioning."))
    body_parts.append(p("Actors: Enterprise Representative (Guest), Super Admin (Platform Owner)."))
    body_parts.append(p("Step-by-Step Procedure:"))
    
    body_parts.append(p("Step 1: Enterprise representative visits public registration portal at /register-company. Form requires 6 mandatory legal fields: Company Name, Tax Identification Code, Desired Workspace Code, Legal Representative Name, Contact Phone, and Official Notification Email."))
    body_parts.append(embed_image("reg_company", "Figure 3: Public Enterprise Registration Form (S-04)", img_rids))

    body_parts.append(p("Step 2: Representative uploads Business Registration License (.pdf or .png, max 10MB). System invokes real-time workspace code collision check with 400ms debounce and uploads document directly to MinIO bucket via presigned URL. Clicks 'Submit Registration' [Status: Initial -> PendingReview]."))
    body_parts.append(embed_image("reg_filled", "Figure 4: Registration Form Filled with Attached Business License (S-04)", img_rids))

    body_parts.append(p("Step 3: Super Admin signs in to Platform Plane and navigates to /platform/companies to view pending registrations. (Note: Provision Super Admin via Step 4.4b prior to performing Step 3). Super Admin inspects dossier details and clicks license file to view document via 15-minute secure presigned URL."))
    body_parts.append(embed_image("plat_companies", "Figure 5: Platform Admin Company Approval Queue (S-08)", img_rids))

    body_parts.append(p("Step 4: Super Admin executes approval action: opens approval dialog and supplies mandatory fields: Tenant Key (lowercase slug, e.g. 'acme-corp'), Initial Admin Employee Code, Admin Email, Admin Full Name, Initial Admin Password (min 8 characters), and IANA Timezone (e.g. 'Asia/Ho_Chi_Minh'). System executes atomic transaction creating Company, Tenant, initial Admin account, calendar, and audit record. (Note: No automatic activation emails are sent; Admin uses credentials established by Super Admin). If rejecting, Super Admin provides justification (5-1000 characters) [Status: PendingReview -> Approved / Rejected]."))
    body_parts.append(embed_image("plat_audit", "Figure 6: Global Platform Audit Trail Confirming Tenant Provisioning (S-09)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 2 ---
    body_parts.append(heading2("3. Workflow 2: Authentication & Workspace Navigation"))
    body_parts.append(p("Objective: Securely authenticate platform users, resolve multi-tenant account associations, and route to role-tailored workspaces."))
    body_parts.append(p("Actors: All system users."))
    body_parts.append(p("Step-by-Step Procedure:"))

    body_parts.append(p("Step 1: User navigates to unified login screen at /login. UI offers two authentication mechanisms: (1) Workspace Code + Username/EmployeeCode/Email + Password; (2) Google Sign-In via Firebase Popup (requires Firebase configuration)."))
    body_parts.append(embed_image("login", "Figure 7: Unified Multi-Tenant Login Interface (S-01)", img_rids))

    body_parts.append(p("Step 2: If a user logs in via Google and the email address belongs to multiple tenant enterprises, the Workspace Selector dialog (S-03) is displayed for tenant selection. JWT session token is issued strictly after TenantId resolution."))
    body_parts.append(p("[IMAGE PLACEHOLDER: S-03 Workspace Selector Dialog for multi-tenant accounts] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Step 3: System redirects user to tailored Workspace (/dashboard or /workspace) based on assigned role:"))
    body_parts.append(p_runs([
        ("•  Department Manager: ", True, False, 22, "1F4E79", False),
        ("Dashboard surfaces departmental performance metrics, pending task assignments, SLA violation alerts, and approval queues.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_manager", "Figure 8: Department Manager Workspace Dashboard (S-07 / Manager Scope)", img_rids))

    body_parts.append(p_runs([
        ("•  Employee: ", True, False, 22, "1F4E79", False),
        ("Dashboard focuses on individual task queue (My Assigned Tasks) and personal service requests.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_employee", "Figure 9: Employee Workspace Dashboard (S-07 / Employee Scope)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 3 ---
    body_parts.append(heading2("4. Workflow 3: Initial Tenant & Service Configuration"))
    body_parts.append(p("Objective: Establish organizational foundation including tenant parameters, departments, user accounts, RBAC matrix, service catalog, visual workflows, and SLA profiles."))
    body_parts.append(p("Actors: Company Admin (Tenant Administrator)."))
    body_parts.append(p("Step-by-Step Procedure:"))

    body_parts.append(p("Step 1: Configure general tenant parameters at /settings (S-23) to update branding logo, operational IANA timezone, and password policies."))
    body_parts.append(embed_image("settings", "Figure 10: Tenant General Configuration Parameters (S-23)", img_rids))

    body_parts.append(p("Step 2: Manage departmental organizational structure at /settings/organization/departments (S-25) to build department tree, designate Department Leads, and set group notification emails."))
    body_parts.append(embed_image("departments", "Figure 11: Department Hierarchy & Organization Governance (S-25)", img_rids))

    body_parts.append(p("Step 3: Manage staff accounts at /settings/organization/users (S-26) to invite new employees, assign department affiliations, and grant initial roles."))
    body_parts.append(embed_image("users", "Figure 12: Employee Directory & Account Governance (S-26)", img_rids))

    body_parts.append(p("Step 4: Configure granular RBAC permissions at /settings/organization/roles (S-27) to customize permission matrices (tasks.create, tasks.assign, requests.route, sla.read...)."))
    body_parts.append(embed_image("roles", "Figure 13: RBAC Granular Role & Permission Matrix (S-27)", img_rids))

    body_parts.append(p("Step 5: Define internal service catalog at /settings/services (S-24) to create request types (equipment provisioning, leave requests, IT support), map fulfillment departments, and associate SLA profiles."))
    body_parts.append(embed_image("services", "Figure 14: Internal Enterprise Service Catalog Management (S-24)", img_rids))

    body_parts.append(p("Step 6: Workflow management and visual design: Access /settings/workflows (S-28) to inspect active workflows, and /settings/workflows/:id/versions/:v (S-29) to design state transitions on the visual canvas. Published workflows enforce WorkflowAllowsTransition validation rules."))
    body_parts.append(embed_image("workflows", "Figure 15: Enterprise Business Workflow Library (S-28)", img_rids))
    body_parts.append(embed_image("workflow_designer", "Figure 16: Visual Business Workflow Designer Canvas (S-29)", img_rids))

    body_parts.append(p("Step 7: Establish SLA commitment profiles: Navigate to /settings/sla-profiles (S-30) to configure Response and Resolution targets, operating calendars (excluding weekends/holidays), and audit version history at /settings/sla-profiles/:id/versions (S-31)."))
    body_parts.append(embed_image("sla_profiles", "Figure 17: SLA Commitment Profiles Governance (S-30)", img_rids))
    body_parts.append(embed_image("sla_versions", "Figure 18: SLA Profile Version History & Lineage (S-31)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 4 ---
    body_parts.append(heading2("5. Workflow 4: End-to-End Task Lifecycle Management"))
    body_parts.append(p("Objective: Coordinate entire task lifecycle including creation, assignment, acceptance, execution, evidence submission, and formal verification across the 10-state lifecycle."))
    body_parts.append(p("Actors: Department Manager, Assignee (Employee), Quality Reviewer."))
    body_parts.append(p("Step-by-Step Procedure:"))

    body_parts.append(p("Step 1: Task Creation: Department Manager navigates to /tasks/new (S-11) to define task title, description, priority (Low, Medium, High, Critical), completion deadline, checklist items, and applicable SLA profile [Status: Initial -> Draft]."))
    body_parts.append(embed_image("task_create", "Figure 19: Task Creation Form & Checklist Builder (S-11)", img_rids))

    body_parts.append(p("Step 2: Task Assignment: Manager delegates task to specific team member or routes to unassigned department queue at /tasks (S-10) [Status: Draft -> Assigned]."))
    body_parts.append(embed_image("tasks", "Figure 20: Department Task Registry with Filtering (S-10)", img_rids))

    body_parts.append(p("Step 3: Task Acceptance: Employee inspects personal queue at /tasks (S-10). Opens task detail view at /tasks/:id (S-13) and clicks 'Accept Task' (S-13b) [Status: Assigned -> Accepted]. If unable to execute, clicks 'Reject' with mandatory justification [Status: Assigned -> Rejected]."))
    body_parts.append(embed_image("tasks_scoped", "Figure 21: Assigned Individual Task Queue View (S-10)", img_rids))

    body_parts.append(p("Step 4: Execution & Progress Tracking: Assignee clicks 'Start Execution' (S-13c) [Status: Accepted -> InProgress]. During execution, assignee checks off completed checklist items, posts discussion notes, and attaches preliminary files."))
    body_parts.append(p("[IMAGE PLACEHOLDER: Checklist interaction and progress update during InProgress state in S-13] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Step 5: Submission & Evidence Delivery: Upon completion, assignee clicks 'Submit Result' (S-13d). Modal dialog requires summary of deliverables and mandatory evidence attachment (.pdf, .zip up to 500MB per backend limit) [Status: InProgress -> Submitted]."))
    body_parts.append(p("[IMAGE PLACEHOLDER: S-13d Submit Result Dialog with evidence attachment upload] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Step 6: Review & Final Acceptance: Designated reviewer opens /tasks/:id (S-13) to inspect deliverables and evidence (S-13e): If acceptable, clicks 'Confirm & Accept' [Status: Submitted -> Confirmed], triggering automatic conclusion [Status: Confirmed -> Completed]. If deficient, clicks 'Request Rework' with mandatory instructions; task reverts to InProgress."))
    body_parts.append(embed_image("task_detail", "Figure 22: Task Execution Detail with 10 States, Discussions & Evidence (S-13)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 5 ---
    body_parts.append(heading2("6. Workflow 5: End-to-End Service Request Management"))
    body_parts.append(p("Objective: Receive internal service requests from employees, facilitate multi-tier approvals, route to specialized fulfillment departments, and verify resolution."))
    body_parts.append(p("Actors: Requester (Employee), Approver (Manager), Dispatcher / Specialist."))
    body_parts.append(p("Step-by-Step Procedure:"))

    body_parts.append(p("Step 1: Service Request Submission: Employee navigates to /requests/new (S-15), selects catalog service item, details requirements, specifies priority (Low, Medium, High, Critical), and attaches relevant files [Status: Initial -> Submitted]."))
    body_parts.append(embed_image("request_create", "Figure 23: Internal Service Request Submission Form (S-15)", img_rids))

    body_parts.append(p("Step 2: Request Review & Approval: Submitted request surfaces in Manager Approval Queue at /approvals (S-17). Management evaluates request and approves or rejects with mandatory justification [Status: Submitted -> Rejected if denied]."))
    body_parts.append(embed_image("approvals", "Figure 24: Management Approval Queue (S-17) [Displays Submitted Tasks & Requests]", img_rids))

    body_parts.append(p("Step 3: Departmental Routing & Acknowledgment: Approved request routes to fulfillment department at /requests (S-14) [Status: Submitted -> Routed]. Dispatcher acknowledges receipt [Status: Routed -> Received] and assigns specialist [Status: Received -> InProgress]."))
    body_parts.append(embed_image("requests", "Figure 25: Department Service Request Fulfillment Queue (S-14)", img_rids))

    body_parts.append(p("Step 4: Linked Task Sub-Division: For complex fulfillment operations, specialist may instantiate Linked Sub-Tasks directly from the request detail view to track specific execution phases."))
    body_parts.append(p("[IMAGE PLACEHOLDER: Creating Linked Sub-Tasks from S-16 Service Request Detail] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Step 5: Resolution & Requester Confirmation: Specialist inputs resolution summary and clicks 'Resolve Request' [Status: InProgress -> Resolved]. Requester receives notification to verify outcome at /requests/:id (S-16): If satisfied, clicks 'Confirm Resolution' [Status: Resolved -> Confirmed], triggering permanent closure [Status: Confirmed -> Closed]. If unsatisfied, clicks 'Feedback' with justification, returning request to InProgress."))
    body_parts.append(embed_image("request_detail", "Figure 26: Service Request Fulfillment Detail & Linked Tasks (S-16)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 6 ---
    body_parts.append(heading2("7. Workflow 6: AI Assistance & Task Decomposition"))
    body_parts.append(p("Objective: Accelerate project task planning via AI under strict Human-in-the-Loop oversight. AI suggestions remain transient drafts until explicitly confirmed and persisted by authorized managers."))
    body_parts.append(p("Actors: Department Manager, Company Admin."))
    body_parts.append(p("Step-by-Step Procedure:"))

    body_parts.append(p("Step 1: On the /tasks page (S-10), user clicks the 'AI Decompose' button to open /tasks/ai-decomposition (S-12), and inputs overarching project objectives."))
    body_parts.append(p("[IMAGE PLACEHOLDER: Clicking 'AI Decompose' button on S-10 /tasks page] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Step 2: System redirects to AI Task Decomposition view at /tasks/ai-decomposition (S-12). AI parses goal and returns structured sub-tasks: task titles, descriptions, estimated duration, priority, and Directed Acyclic Graph (DAG) dependencies."))
    body_parts.append(embed_image("ai_decomp", "Figure 27: AI Task Decomposition Interface & Dependency DAG (S-12)", img_rids))

    body_parts.append(p("Step 3 (Human-in-the-Loop Validation): Preview endpoint performs transient computation and emits read-only DTOs without writing to the database. Manager directly edits task details inline, removes redundant sub-tasks, or adds manual items."))
    body_parts.append(p("[IMAGE PLACEHOLDER: Manager performing inline edits on AI proposal DAG list in S-12] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Step 4: Manager clicks 'Confirm & Batch Create Tasks'. System validates permissions and transactionally creates validated tasks in database, wiring parent-child hierarchy and execution dependencies."))
    body_parts.append(p("[IMAGE PLACEHOLDER: Success confirmation toast after batch task generation confirmation] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Step 5 (BizFlow Robot Chat Widget S-33): Within Task and Request detail screens, users open the BizFlow AI Agent drawer (S-33) to query deadline risks, summarize discussion history, or seek assignment recommendations."))
    body_parts.append(p("[IMAGE PLACEHOLDER: S-33 Intelligent AI Assistant Robot Chat Widget active in conversation] [SCREENSHOT REQUIRED]", italic=True, size=20, color="888888", align="center"))
    body_parts.append(page_break())

    # --- Workflow 7 ---
    body_parts.append(heading2("8. Workflow 7: SLA Monitoring & Operational Reports"))
    body_parts.append(p("Objective: Provide real-time SLA commitment tracking, operational bottleneck detection, executive productivity analytics, and immutable audit trailing."))
    body_parts.append(p("Actors: Company Admin, Department Manager, Executive Leadership."))
    body_parts.append(p("Step-by-Step Procedure:"))

    body_parts.append(p("Step 1: Real-Time SLA Monitoring: Navigate to /sla-monitor (S-18) to track active tasks and service requests across 3 visual tiers: Green (Safe, < 75% elapsed), Yellow (Approaching Breach, >= 75% elapsed), and Red (Breached, > 100% elapsed). Managers may initiate emergency escalation interventions."))
    body_parts.append(embed_image("sla_monitor", "Figure 28: Real-Time SLA Commitment & Breach Monitoring (S-18)", img_rids))

    body_parts.append(p("Step 2: Executive Performance Analytics: Access /reports (S-19) to review departmental throughput, on-time delivery rates, mean resolution times, and workload distribution."))
    body_parts.append(embed_image("reports", "Figure 29: Executive Operational Performance & Analytics Dashboard (S-19)", img_rids))

    body_parts.append(p("Step 3: Centralized Archival & Records Retrieval: Navigate to /records/search (S-20) to filter and retrieve historical task records, evidence attachments, and resolution artifacts."))
    body_parts.append(embed_image("record_search", "Figure 30: Centralized Document & Records Search (S-20)", img_rids))

    body_parts.append(p("Step 4: Immutable Enterprise Audit Trail: Access /audit (S-22) to review tamper-resistant transaction logs recording all business mutations (timestamp, actor, action, previous state, new state)."))
    body_parts.append(embed_image("audit_tenant", "Figure 31: Immutable Enterprise Audit Trail (S-22)", img_rids))

    body_parts.append(p("Step 5: Event Notification Center: Visit /notifications (S-21) to review deadline alerts, assignment updates, and status change notices."))
    body_parts.append(embed_image("notifications", "Figure 32: Enterprise Event Notification Center (S-21)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 8 ---
    body_parts.append(heading2("9. Workflow 8: Password Recovery & Session Invalidation"))
    body_parts.append(p("Objective: Restore account access via asynchronous background mailer (Hangfire Worker) and automatically invalidate active sessions upon password reset to safeguard account security."))
    body_parts.append(p("Actors: All registered enterprise users."))
    body_parts.append(p("Note: This feature is disabled by default in baseline configuration (ResetEmail__Enabled=false, API returns AUTH.RESET_UNAVAILABLE) until operators configure a TLS-capable SMTP server."))
    body_parts.append(p("Step-by-Step Procedure:"))

    body_parts.append(p("Step 1: Password Recovery Request: On login screen, user clicks 'Forgot password?' navigating to /forgot-password (S-05). Enters registered corporate email and clicks 'Send Recovery Link'."))
    body_parts.append(embed_image("forgot_pwd", "Figure 33: Self-Service Password Recovery Request (S-05)", img_rids))

    body_parts.append(p("Step 2: Asynchronous Hangfire Worker Processing: System generates a cryptographically signed recovery token with 15-minute expiration. Background worker dispatches email containing secure single-use recovery link."))

    body_parts.append(p("Step 3: New Password Creation: User opens link navigating to /reset-password?token=... (S-06). Enters new password complying with security policy (min 12 characters, >= 4 unique characters) and confirms password."))
    body_parts.append(embed_image("reset_pwd", "Figure 34: Secure Password Reset Screen (S-06)", img_rids))

    body_parts.append(p("Step 4: Session Invalidation & Re-Authentication: Upon successful reset, backend invalidates all existing Refresh Tokens and sessions across devices. User is redirected to /login to authenticate with new credentials."))

    body_parts.append(embed_image("access_denied", "Figure 35: Access Denied 403 Forbidden Screen for Unauthorized RBAC Operations (S-32)", img_rids))

    # Assemble Document XML
    doc_xml_content = f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:wpc="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"
    xmlns:cx="http://schemas.microsoft.com/office/drawing/2014/chartex"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:o="urn:schemas-microsoft-com:office:office"
    xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
    xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math"
    xmlns:v="urn:schemas-microsoft-com:vml"
    xmlns:wp14="http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing"
    xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
    xmlns:w10="urn:schemas-microsoft-com:office:word"
    xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
    xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml"
    xmlns:w15="http://schemas.microsoft.com/office/word/2012/wordml"
    mc:Ignorable="w14 w15 wp14">
  <w:body>
    {"".join(body_parts)}
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>
      <w:cols w:space="720"/>
      <w:docGrid w:linePitch="360"/>
    </w:sectPr>
  </w:body>
</w:document>'''

    files['word/document.xml'] = doc_xml_content.encode('utf-8')

    print(f"Writing English output docx to {OUT_DOCX_EN}...")
    with zipfile.ZipFile(OUT_DOCX_EN, 'w', compression=zipfile.ZIP_DEFLATED) as zout:
        for fname, data in files.items():
            zout.writestr(fname, data)

    print(f"English Docx generated successfully! Size: {os.path.getsize(OUT_DOCX_EN):,} bytes")

if __name__ == '__main__':
    generate_docx_en()
