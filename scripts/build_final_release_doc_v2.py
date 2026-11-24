# Complete generator for BizFlow_Final_Release_Document v1.0.0
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
    lines = code_text.strip().split('\n')
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
    # Header row
    hdr_cells = []
    for i, h in enumerate(headers):
        w = widths[i] if widths else int(total_w / len(headers))
        c_pr = f'<w:tcPr><w:tcW w:w="{w}" w:type="dxa"/><w:shd w:val="clear" w:color="auto" w:fill="F3F4F6"/></w:tcPr>'
        p_elem = f'<w:p><w:pPr><w:spacing w:before="60" w:after="60"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:b/><w:sz w:val="21"/><w:color w:val="1F2937"/></w:rPr><w:t>{escape_xml(h)}</w:t></w:r></w:p>'
        hdr_cells.append(f'<w:tc>{c_pr}{p_elem}</w:tc>')
    xml_rows.append(f'<w:tr><w:trPr><w:tblHeader/><w:cantSplit/></w:trPr>{"".join(hdr_cells)}</w:tr>')
    
    # Data rows
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
        return p(f"[Image placeholder: {filename}]", italic=True, color="888888")
        
    w_px, h_px = get_image_size(filepath)
    max_cx = 5486400
    aspect = h_px / w_px
    cx = max_cx
    cy = int(cx * aspect)
    if cy > 4500000:
        cy = 4500000
        cx = int(cy / aspect)
        
    rid = img_rids.get(img_key, "rId8")
    
    drawing_xml = f'''<w:p>
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
    </w:p>'''
    
    caption_xml = ""
    if caption:
        caption_xml = p(caption, align="center", italic=True, size=19, color="555555", space_before=40, space_after=180)
        
    return drawing_xml + caption_xml

def build_docx():
    print("Reading base docx...")
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
        else:
            print(f"Warning: image {src_path} not found!")

    body_parts = []

    # ================= PAGE 1: COVER PAGE =================
    body_parts.append(p("", space_before=720))
    body_parts.append(f'''<w:p>
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
    body_parts.append(toc_line("0.5 Deployment & Rollback Strategy", "7", level=2))
    body_parts.append(toc_line("0.6 Post-Release Monitoring & Operational Metrics", "8", level=2))
    body_parts.append(toc_line("I. Deliverable Package", "9", level=1))
    body_parts.append(toc_line("II. Installation Guides", "11", level=1))
    body_parts.append(toc_line("1. Environment Prerequisites & Hardware Specifications", "11", level=2))
    body_parts.append(toc_line("2. Configuration & Environment Variables Reference", "12", level=2))
    body_parts.append(toc_line("3. Infrastructure & Backing Services Deployment", "14", level=2))
    body_parts.append(toc_line("4. Backend Service Setup & Execution (.NET 10 Web API)", "15", level=2))
    body_parts.append(toc_line("5. Frontend Application Setup & Execution (Angular 22 SPA)", "17", level=2))
    body_parts.append(toc_line("6. Automated Test Suite Execution & Quality Metrics", "18", level=2))
    body_parts.append(toc_line("7. Baseline Test Accounts & Access Credentials Matrix", "19", level=2))
    body_parts.append(toc_line("III. User Manual", "21", level=1))
    body_parts.append(toc_line("1. Overview", "21", level=2))
    body_parts.append(toc_line("1.1 Dual-Plane Architectural Governance", "21", level=3))
    body_parts.append(toc_line("1.2 Four Primary User Roles & Responsibility Matrix", "22", level=3))
    body_parts.append(toc_line("1.3 Core State Machine Lifecycles", "23", level=3))
    body_parts.append(toc_line("2. Workflow 1: Đăng ký & Cấp phát Doanh nghiệp (Company Registration)", "26", level=2))
    body_parts.append(toc_line("3. Workflow 2: Xác thực & Quản trị Không gian làm việc (Authentication)", "30", level=2))
    body_parts.append(toc_line("4. Workflow 3: Cấu hình Doanh nghiệp & Danh mục Dịch vụ (Tenant Setup)", "34", level=2))
    body_parts.append(toc_line("5. Workflow 4: Vòng đời Nhiệm vụ Đầu - Cuối (Task Lifecycle Management)", "41", level=2))
    body_parts.append(toc_line("6. Workflow 5: Vòng đời Yêu cầu Dịch vụ Đầu - Cuối (Service Request)", "48", level=2))
    body_parts.append(toc_line("7. Workflow 6: Trợ lý AI & Bóc tách Công việc Tự động (AI Decomposition)", "55", level=2))
    body_parts.append(toc_line("8. Workflow 7: Giám sát Cam kết SLA & Báo cáo Vận hành (SLA & Reports)", "59", level=2))
    body_parts.append(toc_line("9. Workflow 8: Khôi phục Mật khẩu & Thu hồi Phiên (Password Recovery)", "65", level=2))
    body_parts.append(page_break())

    # ================= SECTION 0: RELEASE NOTES v1.0.0 =================
    body_parts.append(heading1("0. Release Notes v1.0.0"))
    
    body_parts.append(heading2("0.1 Executive Summary"))
    body_parts.append(p("Bản phát hành v1.0.0 (ngày 15/11/2026) đóng gói toàn bộ năng lực cốt lõi của nền tảng AI-Assisted BizFlow Platform trên hạ tầng .NET 10 và Angular 22. Hệ thống vận hành theo mô hình đa thuê bao (multi-tenant) với 2 mặt phẳng quản trị tách biệt (Platform Plane và Tenant Plane), bao gồm 8 luồng nghiệp vụ hoàn chỉnh, máy trạng thái 10 bước cho Nhiệm vụ (Task) và 12 bước cho Yêu cầu dịch vụ (Request), cùng trợ lý AI bóc tách công việc có sự xác nhận bắt buộc của con người (Human-in-the-Loop). Kết quả kiểm toán đối soát kỹ thuật xác thực 108/132 yêu cầu (81.8% VERIFIED cốt lõi); 36 màn hình và chức năng UI đáp ứng điều kiện vận hành MVP. Dự án hiện có 8 hạn chế kỹ thuật đã nhận diện (Known Issues) được kiểm soát với giải pháp thay thế (workaround) cụ thể, không còn lỗi chặn nghiêm trọng (Blocker). Đánh giá tổng thể chất lượng bàn giao đạt trạng thái: GO (Đủ điều kiện nghiệm thu đồ án)."))

    body_parts.append(heading2("0.2 Release Scope"))
    body_parts.append(p("Bảng phạm vi tính năng bàn giao thực tế trong bản phát hành v1.0.0:"))
    scope_headers = ["Tính năng", "Mã FR", "Screen ID", "Route thật", "Trạng thái"]
    scope_rows = [
        ["Đăng ký doanh nghiệp công khai", "FR-TEN-001, FR-TEN-005", "S-04", "/register-company", "Done"],
        ["Phê duyệt cấp phát doanh nghiệp", "FR-TEN-002", "S-08", "/platform/companies", "Done"],
        ["Nhật ký kiểm toán toàn nền tảng", "FR-AUD-001", "S-09", "/platform/audit", "Done"],
        ["Đăng nhập xác thực hợp nhất", "FR-AUTH-001, FR-AUTH-005", "S-01, S-02", "/login", "Done"],
        ["Khôi phục mật khẩu qua email", "FR-AUTH-003", "S-05, S-06", "/forgot-password, /reset-password", "Partial"],
        ["Bảng điều khiển không gian làm việc", "FR-WS-001", "S-07", "/workspace", "Done"],
        ["Quản lý danh bạ người dùng", "FR-ORG-001", "S-14", "/settings/organization/users", "Done"],
        ["Quản lý cơ cấu phòng ban", "FR-ORG-002", "S-13", "/settings/organization/departments", "Done"],
        ["Quản lý vai trò & quyền hạn RBAC", "FR-SEC-001", "S-15", "/settings/organization/roles", "Done"],
        ["Danh mục dịch vụ nội bộ", "FR-SVC-001", "S-16", "/settings/services", "Done"],
        ["Quản lý quy trình & Thiết kế trực quan", "FR-WF-001", "S-17, S-18", "/settings/workflows, /settings/workflows/:id/versions/:v", "Done"],
        ["Quản lý hồ sơ & phiên bản cam kết SLA", "FR-SLA-001, FR-SLA-002", "S-19, S-20", "/settings/sla-profiles, /settings/sla-profiles/:id/versions", "Done"],
        ["Danh sách & Khởi tạo nhiệm vụ", "FR-TASK-001, FR-TASK-002", "S-10, S-11", "/tasks, /tasks/new", "Done"],
        ["Chi tiết nhiệm vụ & Vòng đời 10 bước", "FR-TASK-003", "S-13", "/tasks/:id", "Done"],
        ["Trợ lý AI bóc tách công việc thông minh", "FR-AI-013", "S-12", "/tasks/ai-decomposition", "Done"],
        ["Danh sách & Khởi tạo yêu cầu dịch vụ", "FR-REQ-001, FR-REQ-002", "S-14, S-15", "/requests, /requests/new", "Done"],
        ["Chi tiết yêu cầu dịch vụ & Vòng đời 12 bước", "FR-REQ-003", "S-16", "/requests/:id", "Done"],
        ["Hàng đợi phê duyệt yêu cầu đa cấp", "FR-REQ-004", "S-26", "/approvals", "Partial"],
        ["Giám sát vi phạm SLA thời gian thực", "FR-SLA-003", "S-31", "/sla-monitor", "Done"],
        ["Báo cáo quản trị hiệu suất", "FR-REP-001", "S-30", "/reports", "Done"],
        ["Tra cứu lưu trữ hồ sơ tập trung", "FR-ARC-001", "S-32", "/records/search", "Done"],
        ["Trung tâm thông báo sự kiện", "FR-NOTIF-001", "S-33", "/notifications", "Done"],
        ["Nhật ký kiểm toán doanh nghiệp", "FR-AUD-002", "S-21", "/audit", "Done"]
    ]
    body_parts.append(make_table(scope_headers, scope_rows, widths=[2400, 1500, 900, 3360, 1200]))

    body_parts.append(heading2("0.3 Bug Fixes & Resolved Issues"))
    body_parts.append(p("Danh mục các khiếm khuyết kỹ thuật và lỗi kiến trúc đã được xử lý triệt để trước khi phát hành v1.0.0:"))
    bug_headers = ["Mã Defect", "Mô tả lỗi", "Mức độ", "Module", "Phương án sửa đổi", "Người verify"]
    bug_rows = [
        ["BKL-SEC-01", "Đăng nhập ID cho phép bỏ trống WorkspaceKey và tự động đoán tenant nếu chỉ có 1 ứng viên.", "Critical", "Authentication", "Sửa LoginInput.TenantKey thành bắt buộc [Required], cưỡng chế kiểm tra đồng thời cả 3 trường và trả 401 AUTH.INVALID_CREDENTIALS nếu sai.", "Truyền / Lead QA"],
        ["BKL-DB-01", "Bảng Company thiếu các trường định danh bắt buộc v1.2 (TaxId, ContactPhone, ContactPersonName); thiếu bảng lưu tài liệu pháp lý.", "High", "Tenancy", "Tạo migration 20261021100000_CompanyRegistration6FieldsAndEvidence.cs bổ sung 3 cột và tạo bảng CompanyRegistrationDocuments.", "Tường / DB Lead"],
        ["BKL-FNC-01", "Form đăng ký công khai thiếu kiểm tra trùng mã workspace real-time và tải tệp MinIO.", "High", "Onboarding", "Bổ sung endpoint check-code debounce 400ms và tích hợp upload Presigned URL MinIO an toàn.", "Tường / Dev"],
        ["BKL-FNC-03", "Platform Admin không mở và xem được tài liệu giấy phép kinh doanh khi phê duyệt.", "High", "Platform", "Bổ sung endpoint sinh Presigned URL 15 phút an toàn cho Platform Admin tải xem tài liệu thẩm định.", "Nguyên / Sec Lead"],
        ["BKL-FNC-04", "Phân rã công việc bằng AI tự động lưu ngầm dữ liệu khi preview, vi phạm nguyên tắc con người kiểm soát.", "Medium", "AI Assistant", "Tách 2 endpoint riêng biệt: preview thuần túy trả DTO tính toán không ghi DB; chỉ ghi DB khi người dùng bấm xác nhận (confirm).", "Hà / AI Lead"],
        ["BKL-DB-02", "Thiếu ràng buộc chống trùng lặp bản ghi lịch nhắc hẹn deadline đa kênh.", "Medium", "Notifications", "Tạo migration bổ sung bảng với ràng buộc Unique kết hợp (TenantId, TaskId, UserId, TriggerType, ScheduledAt).", "Dũng / Backend"],
        ["BKL-FNC-05", "Widget Chatbot AI chưa lưu trạng thái đóng/mở theo tab làm việc.", "Low", "Frontend", "Quản lý state widget qua sessionStorage để không tự bung lại khi người dùng đã chủ động tắt.", "Truyền / Frontend"]
    ]
    body_parts.append(make_table(bug_headers, bug_rows, widths=[1200, 2600, 1000, 1300, 2260, 1000]))
    body_parts.append(p("Thống kê: Tổng cộng 7/7 lỗi và khiếm khuyết kỹ thuật trọng yếu đã được khắc phục hoàn toàn trên nhánh develop (commit 150eabb).", bold=True, size=21, color="1F4E79", space_before=60, space_after=120))

    body_parts.append(heading2("0.4 Known Issues & Limitations"))
    body_parts.append(p("Bảng danh mục các hạn chế và vấn đề kỹ thuật còn tồn tại, giải pháp thay thế và kế hoạch khắc phục:"))
    known_headers = ["Mã Issue", "Mô tả vấn đề", "Mức độ", "Lý do kỹ thuật", "Giải pháp thay thế (Workaround)", "Kế hoạch"]
    known_rows = [
        ["KISS-01", "Đăng nhập bằng Magic Link chưa gửi email thực tế.", "Medium", "Chưa cấu hình máy chủ SMTP production; nút magic link chưa hiển thị trên UI chính.", "Đăng nhập bằng Mã không gian + Tên đăng nhập + Mật khẩu hoặc Google Sign-in.", "Bổ sung cấu hình SMTP trong Sprint sau."],
        ["KISS-02", "Tính năng Quên mật khẩu bị tắt mặc định.", "Medium", "Biến cấu hình ResetEmail__Enabled mặc định false để tránh lỗi gửi mail khi chưa có SMTP; API trả AUTH.RESET_UNAVAILABLE.", "Quản trị viên cấu hình máy chủ SMTP TLS và bật ResetEmail__Enabled=true.", "Cung cấp giao diện cấu hình SMTP cho Admin."],
        ["KISS-03", "Kịch bản seed chưa tạo tài khoản Super Admin.", "High", "Tệp seed_enterprise_simulation.sql đặt IsPlatformAdministrator = FALSE cho toàn bộ tài khoản.", "Chèn thủ công bản ghi Super Admin hoặc bổ sung kịch bản seed.", "[CẦN BỔ SUNG SEED SUPER ADMIN]."],
        ["KISS-04", "Nginx Reverse Proxy giới hạn tải file 1MB.", "Medium", "Tệp deploy/nginx.conf chưa cấu hình chỉ thị client_max_body_size (mặc định 1MB), trong khi Backend cho phép tới 500MB.", "Thêm dòng client_max_body_size 500M; vào khối server trong nginx.conf.", "Cập nhật tệp cấu hình container app."],
        ["KISS-05", "Quy trình phê duyệt đa cấp động mới hỗ trợ một phần.", "Medium", "Kiến trúc lưu trữ JSON graph đã sẵn sàng nhưng chưa mở rộng toàn bộ trigger đa cấp trên giao diện.", "Sử dụng phê duyệt cấp phòng ban và phê duyệt đơn cấp tại /approvals (lọc trạng thái SUBMITTED).", "Mở rộng giao diện Designer."],
        ["KISS-06", "Trợ lý AI phụ thuộc vào OpenAI API ngoài.", "Low", "Khi môi trường không có internet hoặc thiếu OpenAI__ApiKey, hệ thống chạy ở chế độ dự phòng Heuristic.", "Hệ thống tự động fallback sang luật heuristic dựa trên danh mục dịch vụ.", "Tích hợp mô hình AI cục bộ."],
        ["KISS-07", "Một số nhãn giao diện và thông báo lỗi hiển thị tiếng Anh.", "Low", "Kế thừa từ token assets và mã lỗi tiêu chuẩn của hệ thống backend.", "Tra cứu mã lỗi theo tài liệu kỹ thuật.", "Song ngữ hóa toàn bộ i18n."],
        ["KISS-08", "Chuỗi bí mật mẫu lưu trong appsettings.Development.json.", "Low", "Cấu hình mặc định phục vụ chạy thử nghiệm cục bộ tiện lợi.", "Chỉ dùng cho môi trường dev/chấm bài; môi trường production nạp biến qua Secret Manager.", "Xóa bỏ tệp development trong image prod."]
    ]
    body_parts.append(make_table(known_headers, known_rows, widths=[1000, 1800, 900, 1900, 2400, 1360]))

    body_parts.append(heading2("0.5 Deployment & Rollback Strategy"))
    body_parts.append(p("Quy trình triển khai bản phát hành và các bước phục hồi thảm họa chi tiết:"))
    body_parts.append(p_runs([
        ("•  Bước 1: Sao lưu toàn vẹn dữ liệu: ", True, False, 22, "1F4E79", False),
        ("Thực thi sao lưu cơ sở dữ liệu trước khi nâng cấp:", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block("docker exec -t bizflow-local-postgres-1 pg_dump -U bizflow -d bizflow > backup_pre_release.sql"))
    
    body_parts.append(p_runs([
        ("•  Bước 2: Triển khai phát hành: ", True, False, 22, "1F4E79", False),
        ("Khởi chạy các container ứng dụng đóng gói thông qua overlay app:", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block("docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d --build"))
    body_parts.append(p("Thời gian gián đoạn dự kiến (Downtime): Dưới 3 phút."))

    body_parts.append(p_runs([
        ("•  Bước 3: Kịch bản Smoke Test kiểm tra sức khỏe phát hành: ", True, False, 22, "1F4E79", False),
        ("Kiểm tra tính sẵn sàng của hệ thống:", False, False, 22, "374151", False)
    ]))
    body_parts.append(code_block(
        "curl -f http://localhost:5154/health/live
"
        "curl -f http://localhost:5154/health/ready
"
        "curl -f http://localhost:4200"
    ))

    body_parts.append(p("Chiến lược Rollback khi xảy ra sự cố nghiêm trọng:", bold=True, size=22, color="1F4E79", space_before=120, space_after=60))
    rb_headers = ["Kịch bản sự cố", "Hành động Rollback", "Công cụ / Lệnh thực thi", "Thời gian phục hồi"]
    rb_rows = [
        ["Sự cố mã nguồn App / UI", "Quay về Docker image phiên bản trước", "docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d", "< 2 phút"],
        ["Sự cố Migration DB hỏng", "Hạ cấp schema về migration trước", "dotnet ef database update <PreviousMigrationName> --project backend/BizFlow.Infrastructure", "< 3 phút"],
        ["Sự cố hỏng cấu trúc dữ liệu", "Khôi phục dữ liệu từ tệp pg_dump", "docker exec -i bizflow-local-postgres-1 psql -U bizflow -d bizflow < backup_pre_release.sql", "< 5 phút"]
    ]
    body_parts.append(make_table(rb_headers, rb_rows, widths=[2000, 2600, 3360, 1400]))

    body_parts.append(heading2("0.6 Post-Release Monitoring & Operational Metrics"))
    body_parts.append(p("Các chỉ số vận hành trọng yếu cần theo dõi liên tục sau khi đưa bản phát hành vào hoạt động:"))
    mon_headers = ["Chỉ số giám sát", "Ngưỡng tiêu chuẩn", "Nguồn theo dõi", "Hành động ứng phó"]
    mon_rows = [
        ["Endpoint sẵn sàng (/health/ready)", "HTTP 200 OK (< 200ms)", "/health/ready probe", "Nếu trả 503: kiểm tra kết nối DB và migration history drift."],
        ["Tỷ lệ lỗi máy chủ (5xx Error Rate)", "< 0.5% tổng request", "Serilog / Docker API logs", "Kiểm tra log lỗi unhandled exception trong container API."],
        ["Hangfire Failed Jobs", "0 jobs lỗi liên tiếp", "Hangfire / DB Job table", "Kiểm tra kết nối Redis và thông tin cấu hình máy chủ SMTP."],
        ["Tỷ lệ lỗi Trợ lý AI (OpenAI API)", "< 2% tổng prompt", "OpenAiTaskAssistance log", "Hệ thống tự động fallback sang Heuristic; kiểm tra quota/key OpenAI."],
        ["Tải tài nguyên container", "CPU < 80%, RAM < 85%", "docker stats", "Khởi động lại container hoặc nâng ngưỡng cấp phát bộ nhớ Docker."]
    ]
    body_parts.append(make_table(mon_headers, mon_rows, widths=[2200, 1800, 2200, 3160]))
    body_parts.append(page_break())

    # ================= SECTION I. DELIVERABLE PACKAGE =================
    body_parts.append(heading1("I. Deliverable Package"))
    
    headers_pkg = ["No.", "File", "Version", "Ngày", "Notes"]
    rows_pkg = [
        [
            "1",
            "G1-BizFlow_DB_final.sql [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.0.0",
            "15/11/2026",
            "Kịch bản PostgreSQL 18 chứa toàn bộ cấu trúc 39 bản EF Core Migration, các bảng phân vùng, khóa ngoại, chỉ mục và dữ liệu mô phỏng doanh nghiệp (Enterprise Simulation Seed)."
        ],
        [
            "2",
            "G1-BizFlow_SRS_final.docx [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.1",
            "15/11/2026",
            "Tài liệu đặc tả yêu cầu phần mềm cuối cùng bao gồm 16 Use Cases (UC-001 đến UC-016), các module yêu cầu chức năng (FR-AUTH, FR-TEN, FR-TASK, FR-REQ, FR-SLA, FR-AI, FR-AUD) và phi chức năng."
        ],
        [
            "3",
            "G1-BizFlow_SDS_final.docx [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.1",
            "15/11/2026",
            "Tài liệu thiết kế hệ thống phần mềm chi tiết về Clean Architecture, lược đồ quan hệ thực thể ERD, sơ đồ tuần tự, đặc tả máy trạng thái Task/Request và danh mục API endpoint."
        ],
        [
            "4",
            "G1-BizFlow_Final_Product_Backlog.xlsx [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.0.0",
            "15/11/2026",
            "Trạng thái hoàn thành 36 chức năng hệ thống qua 3 lần lặp (Iter1, Iter2, Iter3). Tỷ lệ kiểm toán đạt 108/132 yêu cầu nghiệp vụ (81.8% VERIFIED cốt lõi); 36 màn hình UI đáp ứng điều kiện vận hành MVP."
        ],
        [
            "5",
            "G1-BizFlow_Issues_Report.xlsx [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.0.0",
            "15/11/2026",
            "Báo cáo theo dõi danh sách toàn bộ sự cố, lỗi kiểm thử (Defects) trong suốt vòng đời dự án, phân loại độ nghiêm trọng, nguyên nhân gốc rễ, phương án khắc phục và trạng thái nghiệm thu."
        ],
        [
            "6",
            "G1-BizFlow_Screen_Flow_Guide.docx [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.0.0",
            "15/11/2026",
            "Tài liệu hướng dẫn luồng màn hình và tiến trình nghiệp vụ chi tiết 33 màn hình, 4 vai trò người dùng và 8 luồng vận hành chính."
        ],
        [
            "7",
            "G1-BizFlow_Backend_Source_Code.zip [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.0.0",
            "15/11/2026",
            "Mã nguồn giải pháp Backend hoàn chỉnh xây dựng trên .NET 10, C# 13, Entity Framework Core 10, ASP.NET Core Web API, Hangfire Worker và SignalR Hub."
        ],
        [
            "8",
            "G1-BizFlow_Frontend_Source_Code.zip [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.0.0",
            "15/11/2026",
            "Mã nguồn ứng dụng Frontend hiện đại xây dựng trên Angular 22, TypeScript, Tailwind CSS, Standalone Components, Angular Signals và bộ kiểm thử Playwright."
        ],
        [
            "9",
            "G1-BizFlow_Deployment_Package.zip [CẦN XÁC NHẬN MÃ NHÓM]",
            "v1.0.0",
            "15/11/2026",
            "Gói triển khai đóng gói container hóa gồm docker-compose.yml, Dockerfiles, cấu hình Nginx Reverse Proxy, tệp biến môi trường mẫu (.env.example) và kịch bản kiểm tra sẵn sàng."
        ]
    ]
    body_parts.append(make_table(headers_pkg, rows_pkg, widths=[600, 2800, 900, 1100, 3960]))
    
    body_parts.append(p("G1-BizFlow là mã nhóm sinh viên và mã dự án (ví dụ: G1-BizFlow đại diện cho nhóm G1 phát triển nền tảng AI-Assisted BizFlow Platform). [CẦN XÁC NHẬN MÃ NHÓM: G1-BizFlow]", italic=True, size=21, color="555555", space_before=160, space_after=180))
    
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
    body_parts.append(p("Trước khi tiến hành cài đặt, máy chủ hoặc máy phát triển cần đáp ứng các yêu cầu phần cứng và phần mềm sau:"))
    
    prereq_headers = ["Thành phần", "Cấu hình tối thiểu", "Cấu hình khuyến nghị"]
    prereq_rows = [
        ["Hệ điều hành", "Windows 10/11 x64, macOS 13+, hoặc Ubuntu 22.04 LTS", "Windows 11 Pro 64-bit với WSL2 kích hoạt"],
        ["Phần cứng", "CPU 4 nhân @ 2.0 GHz, 8 GB RAM, 10 GB ổ SSD trống", "CPU 8 nhân @ 3.2 GHz, 16 GB+ RAM, NVMe SSD"],
        [".NET SDK", ".NET SDK 10.0.100+", ".NET SDK 10.0.401 (khai báo trong global.json)"],
        ["Node.js & npm", "Node.js v22.17.0 LTS / npm 10.8+", "Node.js v22.22.3 LTS & npm 11.6.2 (khai báo trong frontend/.nvmrc)"],
        ["Container Virtualization", "Docker Desktop 4.25+ (Linux containers mode)", "Docker Desktop 4.34+ (Linux engine, cấp 4GB+ RAM)"],
        ["Backing Services", "PostgreSQL 17.x, Redis 7.x, S3-Compatible Storage", "PostgreSQL 18.3-alpine, Redis 7-alpine, MinIO S3"]
    ]
    body_parts.append(make_table(prereq_headers, prereq_rows, widths=[2200, 3500, 3660]))

    body_parts.append(heading2("2. Configuration & Environment Variables Reference"))
    body_parts.append(p("Bảng tổng hợp các biến môi trường mà mã nguồn Backend thực sự đọc (khai báo tại deploy/.env.example, Program.cs, PasswordResetRegistration.cs):"))
    
    env_headers = ["Tên biến môi trường", "Bắt buộc", "Mục đích sử dụng & Giá trị mặc định", "Cơ chế dự phòng (Fallback)"]
    env_rows = [
        ["ConnectionStrings__BizFlow", "Có", "Chuỗi kết nối PostgreSQL (mặc định cổng 5433 hoặc 5432, DB: bizflow, User: bizflow).", "Không có. Khởi động dừng nếu thiếu."],
        ["Jwt__Issuer", "Có", "Tên đơn vị phát hành JWT token (mặc định: BizFlow).", "Không có. Khởi động dừng nếu thiếu."],
        ["Jwt__Audience", "Có", "Đơn vị tiêu thụ JWT token (mặc định: BizFlow.Web).", "Không có. Khởi động dừng nếu thiếu."],
        ["Jwt__SigningKeyBase64", "Có", "Khóa ký HMAC-SHA256 mã hóa Base64 (độ dài tối thiểu 32 byte).", "Không có. Khởi động dừng nếu thiếu."],
        ["Redis__ConnectionString", "Không", "Chuỗi kết nối Redis cache (ví dụ: localhost:6379).", "Tự động chuyển sang IMemoryCache trong bộ nhớ RAM."],
        ["Storage__Provider / Endpoint / PublicEndpoint / Bucket / AccessKey / SecretKey", "Không", "Cấu hình lưu trữ tệp đính kèm và tài liệu MinIO/S3.", "Tự động lưu trữ tệp cục bộ trên ổ cứng máy chủ (Local File Storage)."],
        ["OpenAI__ApiKey / Model / BaseUrl / TimeoutSeconds", "Không", "Khóa API và mô hình OpenAI (mặc định: gpt-4o-mini).", "Tự động kích hoạt cơ chế dự phòng Heuristic dựa trên danh mục dịch vụ."],
        ["ResetEmail__Enabled / Host / Port / Username / Password / FromAddress / FrontendOrigin", "Không", "Cấu hình gửi email khôi phục mật khẩu qua máy chủ SMTP có TLS.", "Mặc định Enabled=false, API trả AUTH.RESET_UNAVAILABLE nếu chưa bật."],
        ["PasswordReset__TokenLifetimeMinutes", "Không", "Thời gian hiệu lực của mã token đặt lại mật khẩu (mặc định: 15 phút).", "Mặc định sử dụng 15 phút nếu không cấu hình."]
    ]
    body_parts.append(make_table(env_headers, env_rows, widths=[2800, 900, 3660, 2000]))

    body_parts.append(warning_callout("Mọi chuỗi bí mật, mật khẩu, và khóa ký JWT trong tài liệu này là GIÁ TRỊ MẪU DÀNH CHO MÔI TRƯỜNG PHÁT TRIỂN / CHẤM BÀI (DEV/EVALUATION). TUYỆT ĐỐI KHÔNG sử dụng lại các giá trị này trên môi trường Production!"))

    body_parts.append(heading2("3. Infrastructure & Backing Services Deployment"))
    body_parts.append(p("Nền tảng sử dụng Docker Compose để khởi chạy PostgreSQL 18.3-alpine, Redis 7-alpine và MinIO (image pgsty/silo)."))
    
    body_parts.append(p("Bước 3.1: Sao chép tệp cấu hình môi trường:"))
    body_parts.append(p_runs([("PowerShell 7:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block("Copy-Item deploy\.env.example deploy\.env"))
    body_parts.append(p_runs([("Bash:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block("cp deploy/.env.example deploy/.env"))
    
    body_parts.append(p("Bước 3.2: Khởi chạy các container dịch vụ ở chế độ chạy nền:"))
    body_parts.append(p_runs([("PowerShell 7 & Bash:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block("docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d"))
    body_parts.append(p("Kiểm tra trạng thái container (tên container mặc định là bizflow-local-postgres-1, bizflow-local-redis-1, bizflow-local-minio-1):"))
    body_parts.append(code_block("docker compose ps"))

    body_parts.append(heading2("4. Backend Service Setup & Execution (.NET 10 Web API)"))
    body_parts.append(p("Bước 4.1: Khôi phục công cụ và gói thư viện .NET:"))
    body_parts.append(code_block("dotnet tool restore
dotnet restore BizFlow.slnx"))
    
    body_parts.append(p("Bước 4.2: Thiết lập biến môi trường kết nối cơ sở dữ liệu và bảo mật:"))
    body_parts.append(p_runs([("PowerShell 7:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block(
        "$env:ConnectionStrings__BizFlow = 'Host=127.0.0.1;Port=5433;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret'
"
        "$env:Jwt__Issuer = 'BizFlow'
"
        "$env:Jwt__Audience = 'BizFlow.Web'
"
        "$env:Jwt__SigningKeyBase64 = 'Loizdg9h99sl7lZJSV1LJwNCNTIdi7PjAtR3yib+brk='"
    ))
    body_parts.append(p_runs([("Bash:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block(
        "export ConnectionStrings__BizFlow='Host=127.0.0.1;Port=5433;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret'
"
        "export Jwt__Issuer='BizFlow'
"
        "export Jwt__Audience='BizFlow.Web'
"
        "export Jwt__SigningKeyBase64='Loizdg9h99sl7lZJSV1LJwNCNTIdi7PjAtR3yib+brk='"
    ))
    
    body_parts.append(p("Bước 4.3: Áp dụng các bản di chuyển cơ sở dữ liệu (39 migrations):"))
    body_parts.append(code_block("dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure"))
    
    body_parts.append(p("Bước 4.4 (BẮT BUỘC VỚI NGƯỜI CHẤM BÀI): Nạp dữ liệu mô phỏng doanh nghiệp:"))
    body_parts.append(p_runs([("PowerShell 7 & Bash:", True, False, 20, "1F4E79", False)]))
    body_parts.append(code_block("docker compose -f deploy/docker-compose.yml exec -T postgres psql -U bizflow -d bizflow < deploy/seed_enterprise_simulation.sql"))
    
    body_parts.append(p("Bước 4.5: Khởi chạy dịch vụ Backend Web API:"))
    body_parts.append(code_block("dotnet run --project backend/BizFlow.Api"))
    body_parts.append(p("Dịch vụ API lắng nghe tại http://localhost:5154. Kiểm tra probe sẵn sàng tại http://localhost:5154/health/ready và tài liệu OpenAPI tại http://localhost:5154/openapi/v1.json (chỉ kích hoạt ở môi trường Development)."))

    body_parts.append(heading2("5. Frontend Application Setup & Execution (Angular 22 SPA)"))
    body_parts.append(p("Bước 5.1: Mở cửa sổ dòng lệnh thứ hai, vào thư mục frontend và cài đặt dependencies theo tệp package-lock.json:"))
    body_parts.append(code_block("cd frontend
npm ci"))
    
    body_parts.append(p("Bước 5.2: Khởi động máy chủ phát triển Angular:"))
    body_parts.append(code_block("npm start"))
    body_parts.append(p("Ứng dụng web sẵn sàng tại http://localhost:4200. Máy chủ Angular tự động chuyển tiếp (proxy) các request bắt đầu bằng /api sang Backend API tại cổng 5154."))

    body_parts.append(heading2("6. Automated Test Suite Execution & Quality Metrics"))
    body_parts.append(p("Kết quả kiểm tra tự động hóa thực tế trên mã nguồn:"))
    test_met_headers = ["Bộ kiểm thử", "Lệnh thực thi", "Kết quả thực tế", "Ghi chú kỹ thuật"]
    test_met_rows = [
        ["Backend Unit & Integration Tests", "dotnet test BizFlow.slnx", "495/495 tests PASS (theo tài liệu 01/11/2026)", "[CẦN CHẠY LẠI: dotnet test BizFlow.slnx để cập nhật số liệu mới nhất]"],
        ["Frontend Unit Tests (Jasmine/Karma)", "cd frontend && npm test -- --watch=false", "268/270 tests PASS (99.2%)", "2 tests lỗi: users.spec.ts (cancels pending retrieval when destroyed) và requests.spec.ts khi chạy chung suite."],
        ["End-to-End Mock Tests (Playwright)", "cd tests/BizFlow.E2ETests && npm test", "2/6 tests PASS (33.3%)", "4 tests mock cũ không khớp giao diện tiếng Việt mới."],
        ["Source Code Prettier Check", "cd frontend && npm run format:check", "89 files chưa chuẩn format", "Không ảnh hưởng tới runtime build; các token asset được giữ nguyên bản."]
    ]
    body_parts.append(make_table(test_met_headers, test_met_rows, widths=[2300, 2600, 2200, 2260]))

    body_parts.append(heading2("7. Baseline Test Accounts & Access Credentials Matrix"))
    body_parts.append(p("Bảng tài khoản thử nghiệm thực tế được tạo từ tệp deploy/seed_enterprise_simulation.sql:"))
    body_parts.append(p("Mã không gian làm việc (Workspace Code): bizflow-corp | Mật khẩu chung: Password123!", bold=True, size=22, color="1F4E79", space_before=40, space_after=100))
    
    acc_headers = ["Vai trò", "Email / Tài khoản", "Mã nhân viên", "Họ tên & Chức danh", "Phòng ban"]
    acc_rows = [
        ["Company Admin", "admin@bizflow.local", "ADMIN001", "Phạm Minh Đức (CEO)", "Ban Giám đốc & Điều hành (EXEC)"],
        ["Ban Giám đốc", "huong.nguyen@bizflow.local", "EXEC001", "Nguyễn Thị Mai Hương (COO)", "Ban Giám đốc & Điều hành (EXEC)"],
        ["Department Manager", "manager@bizflow.local", "MGR001", "Lê Hoàng Nam (CTO)", "Phòng Công nghệ & Chuyển đổi số (TECH)"],
        ["DevOps Engineer", "huy.do@bizflow.local", "TECH002", "Đỗ Quang Huy (DevOps)", "Phòng Công nghệ & Chuyển đổi số (TECH)"],
        ["Employee (Dev)", "employee@bizflow.local", "EMP001", "Nguyễn Văn Nhân Viên (Dev)", "Phòng Công nghệ & Chuyển đổi số (TECH)"],
        ["Department Manager", "dat.nguyen@bizflow.local", "SALES_MGR", "Nguyễn Tiến Đạt (Trưởng phòng Sales)", "Phòng Kinh doanh B2B & Đối tác (SALES)"],
        ["Employee (Sales)", "kiet.hoang@bizflow.local", "SALES001", "Hoàng Tuấn Kiệt (Chuyên viên B2B)", "Phòng Kinh doanh B2B & Đối tác (SALES)"],
        ["Department Manager", "thao.vu@bizflow.local", "MKT_MGR", "Vũ Phương Thảo (Trưởng phòng Marketing)", "Phòng Tiếp thị & Truyền thông (MKT)"],
        ["Employee (Content)", "linh.le@bizflow.local", "MKT001", "Lê Mỹ Linh (Chuyên viên Content)", "Phòng Tiếp thị & Truyền thông (MKT)"]
    ]
    body_parts.append(make_table(acc_headers, acc_rows, widths=[1800, 2400, 1300, 2260, 1600]))
    body_parts.append(warning_callout("Kịch bản seed_enterprise_simulation.sql hiện chưa tạo tài khoản Super Admin (toàn bộ người dùng có IsPlatformAdministrator = FALSE). Để thử nghiệm mặt phẳng /platform, người chấm bài cần chèn thêm bản ghi Super Admin hoặc bổ sung vào seed [CẦN BỔ SUNG SEED SUPER ADMIN]."))
    
    body_parts.append(page_break())

    # ================= SECTION III. USER MANUAL =================
    body_parts.append(heading1("III. User Manual"))
    
    body_parts.append(heading2("1. Overview"))
    body_parts.append(p("AI-Assisted BizFlow Platform là nền tảng điều phối công việc và yêu cầu dịch vụ nội bộ đa doanh nghiệp, hỗ trợ tự động hóa luồng chuyển trạng thái, giám sát cam kết thời hạn (SLA) và ứng dụng Trí tuệ Nhân tạo hỗ trợ bóc tách nhiệm vụ với sự phê chuẩn bắt buộc của con người."))

    body_parts.append(heading3("1.1 Dual-Plane Architectural Governance"))
    body_parts.append(p("Hệ thống phân tách quyền hạn thành hai mặt phẳng độc lập:"))
    body_parts.append(p_runs([
        ("•  Platform Plane (Quản trị Nền tảng): ", True, False, 22, "1F4E79", False),
        ("Dành riêng cho Platform Administrator tại ", False, False, 22, "374151", False),
        ("/platform/companies", False, False, 20, "111827", True),
        (" và ", False, False, 22, "374151", False),
        ("/platform/audit", False, False, 20, "111827", True),
        (". Chịu trách nhiệm thẩm định giấy phép đăng ký kinh doanh của doanh nghiệp mới, cấp phát dữ liệu thuê bao (Tenant Provisioning) và theo dõi nhật ký kiểm toán bất biến toàn cầu.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_superadmin", "Hình 1: Không gian Quản trị Toàn nền tảng (Platform Plane Workspace)", img_rids))
    
    body_parts.append(p_runs([
        ("•  Tenant Plane (Vận hành Doanh nghiệp): ", True, False, 22, "1F4E79", False),
        ("Dành cho các thành viên doanh nghiệp tại ", False, False, 22, "374151", False),
        ("/workspace", False, False, 20, "111827", True),
        (". Dữ liệu từng doanh nghiệp hoàn toàn cô lập theo mã TenantId, quản lý sơ đồ tổ chức phòng ban, phân quyền vai trò (RBAC), danh mục dịch vụ, quy trình nghiệp vụ và hồ sơ SLA.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_admin", "Hình 2: Không gian Quản trị Doanh nghiệp (Tenant Plane Workspace)", img_rids))

    body_parts.append(heading3("1.2 Four Primary User Roles & Responsibility Matrix"))
    roles_headers = ["Vai trò (Role)", "Phạm vi quản trị", "Trách nhiệm chính", "Các route truy cập chính"]
    roles_rows = [
        ["Super Admin", "Platform Plane", "Thẩm tra đăng ký doanh nghiệp, duyệt cấp phát tenant, giám sát nhật ký kiểm toán nền tảng.", "/platform, /platform/companies, /platform/audit"],
        ["Company Admin", "Tenant Plane", "Cấu hình tham số doanh nghiệp, phòng ban, tài khoản người dùng, vai trò, dịch vụ, quy trình và hồ sơ SLA.", "/workspace, /settings, /settings/organization/*, /settings/services, /settings/workflows, /settings/sla-profiles, /audit"],
        ["Department Manager", "Department Scope", "Khởi tạo nhiệm vụ, phân rã công việc bằng AI, giao việc nhân viên, duyệt yêu cầu dịch vụ, nghiệm thu kết quả, giám sát vi phạm SLA.", "/workspace, /tasks, /tasks/new, /tasks/ai-decomposition, /requests, /approvals, /reports, /sla-monitor"],
        ["Employee", "Assigned / Own Scope", "Gửi yêu cầu dịch vụ, nhận việc được phân công, thực thi và nộp kết quả kèm tệp bằng chứng, tra cứu hồ sơ và nhận thông báo.", "/workspace, /tasks, /requests, /requests/new, /notifications"]
    ]
    body_parts.append(make_table(roles_headers, roles_rows, widths=[1800, 1500, 3660, 2400]))

    body_parts.append(heading3("1.3 Core State Machine Lifecycles"))
    body_parts.append(p("Hệ thống quản lý vòng đời chặt chẽ qua máy trạng thái tầng Domain. Mức độ ưu tiên áp dụng cho cả Task và Request gồm 4 cấp độ: Low, Medium, High, Critical."))

    body_parts.append(p("Bảng 1: Ma trận chuyển trạng thái Nhiệm vụ (TaskLifecyclePolicy - 10 trạng thái):", bold=True, size=21, color="1F4E79", space_before=80, space_after=40))
    task_sm_headers = ["Trạng thái hiện tại", "Trạng thái tiếp theo", "Điều kiện & Bằng chứng (Evidence)", "Quyền hạn yêu cầu (Permission)"]
    task_sm_rows = [
        ["Draft hoặc Rejected", "Assigned", "Bắt buộc có phòng ban hoặc cá nhân nhận việc hợp lệ", "tasks.assign"],
        ["Assigned", "Accepted", "Người nhận việc xác nhận đồng ý tiếp nhận", "tasks.accept"],
        ["Assigned", "Rejected", "Người nhận việc từ chối, bắt buộc nhập lý do từ chối", "tasks.accept"],
        ["Accepted hoặc Overdue", "InProgress", "Người nhận việc bắt đầu bấm thực thi công việc", "tasks.execute"],
        ["InProgress", "Submitted", "Bắt buộc có tệp minh chứng kết quả (Submitted Result)", "tasks.submit"],
        ["Submitted", "Confirmed", "Người nghiệm thu phê duyệt kết quả đạt yêu cầu", "tasks.confirm"],
        ["Submitted", "InProgress", "Người nghiệm thu yêu cầu làm lại (Rework), chuyển về thực thi", "tasks.confirm"],
        ["Confirmed", "Completed", "Chuyển trạng thái hoàn thành tự động bởi hệ thống", "System Only"],
        ["InProgress hoặc Submitted", "Overdue", "Quá hạn deadline hoặc vi phạm SLA (kích hoạt bởi hệ thống)", "System Only"],
        ["Mọi trạng thái (trừ Completed, Cancelled)", "Cancelled", "Hủy bỏ nhiệm vụ, bắt buộc nhập lý do hủy", "tasks.cancel"]
    ]
    body_parts.append(make_table(task_sm_headers, task_sm_rows, widths=[2200, 1800, 3560, 1800]))

    body_parts.append(p("Bảng 2: Ma trận chuyển trạng thái Yêu cầu Dịch vụ (RequestLifecyclePolicy - 12 trạng thái):", bold=True, size=21, color="1F4E79", space_before=120, space_after=40))
    req_sm_headers = ["Trạng thái hiện tại", "Trạng thái tiếp theo", "Điều kiện & Bằng chứng (Evidence)", "Quyền hạn yêu cầu (Permission)"]
    req_sm_rows = [
        ["Draft", "Submitted", "Người gửi nộp yêu cầu dịch vụ", "requests.submit"],
        ["Submitted", "Routed", "Điều phối yêu cầu về phòng ban xử lý chuyên môn", "requests.route"],
        ["Routed", "Received", "Phòng ban tiếp nhận yêu cầu vào hàng đợi xử lý", "requests.receive"],
        ["Received", "InProgress", "Bắt đầu xử lý yêu cầu dịch vụ", "requests.process"],
        ["InProgress", "WaitingForInformation", "Tạm dừng để yêu cầu người gửi bổ sung thông tin", "requests.process"],
        ["WaitingForInformation", "InProgress", "Tiếp tục xử lý sau khi đã nhận đủ thông tin", "requests.process"],
        ["InProgress", "Resolved", "Chuyên viên nhập giải pháp xử lý và hoàn tất (Resolution)", "requests.resolve"],
        ["Resolved", "Confirmed", "Người gửi xác nhận hài lòng với kết quả xử lý", "requests.confirm"],
        ["Resolved", "InProgress", "Người gửi phản hồi kết quả chưa đạt, yêu cầu xử lý lại", "requests.confirm"],
        ["Confirmed", "Closed", "Hệ thống tự động đóng yêu cầu vĩnh viễn", "System Only"],
        ["Submitted hoặc Routed", "Rejected", "Từ chối yêu cầu, bắt buộc nhập lý do từ chối", "requests.reject"],
        ["InProgress, Received...", "Overdue", "Quá thời hạn cam kết SLA xử lý (kích hoạt bởi hệ thống)", "System Only"],
        ["Mọi trạng thái (trừ Closed, Cancelled, Rejected)", "Cancelled", "Hủy bỏ yêu cầu, bắt buộc nhập lý do hủy", "requests.cancel"]
    ]
    body_parts.append(make_table(req_sm_headers, req_sm_rows, widths=[2200, 1800, 3560, 1800]))
    body_parts.append(page_break())

    # ================= WORKFLOWS 1 - 8 =================
    # --- Workflow 1 ---
    body_parts.append(heading2("2. Workflow 1: Đăng ký & Cấp phát Doanh nghiệp (Company Registration & Tenant Provisioning)"))
    body_parts.append(p("Mục đích: Tiếp nhận hồ sơ đăng ký của doanh nghiệp mới, xác thực thông tin pháp lý và cấp phát tự động không gian làm việc độc lập."))
    body_parts.append(p("Tác nhân: Đại diện Doanh nghiệp (Guest), Super Admin (Platform Owner)."))
    body_parts.append(p("Trình tự thao tác chi tiết:"))
    
    body_parts.append(p("Bước 1: Đại diện doanh nghiệp truy cập cổng đăng ký công khai tại route /register-company. Biểu mẫu yêu cầu cung cấp đúng 6 trường pháp lý bắt buộc: Tên công ty (Company Name), Mã số thuế (Tax Code), Mã không gian làm việc mong muốn (Workspace Code), Họ tên người đại diện pháp luật, Số điện thoại liên hệ, và Email nhận thông báo."))
    body_parts.append(embed_image("reg_company", "Hình 3: Giao diện Biểu mẫu Đăng ký Doanh nghiệp mới (S-04)", img_rids))

    body_parts.append(p("Bước 2: Đại diện doanh nghiệp tải lên tệp Giấy phép Đăng ký Kinh doanh (.pdf hoặc .png, dung lượng tối đa 10MB). Hệ thống gọi API kiểm tra trùng mã workspace real-time với debounce 400ms và tải tài liệu trực tiếp lên bucket MinIO qua Presigned URL an toàn. Nhấn 'Gửi đăng ký' [Trạng thái: Khởi tạo → PendingReview]."))
    body_parts.append(embed_image("reg_filled", "Hình 4: Nhập đầy đủ thông tin pháp lý và đính kèm giấy phép ĐKKD (S-04)", img_rids))

    body_parts.append(p("Bước 3: Super Admin đăng nhập vào mặt phẳng quản trị toàn nền tảng, điều hướng đến /platform/companies để xem danh sách hàng đợi đăng ký. Super Admin mở chi tiết hồ sơ doanh nghiệp và nhấp vào tệp giấy phép kinh doanh để xem tài liệu qua URL tạm thời 15 phút."))
    body_parts.append(embed_image("plat_companies", "Hình 5: Hàng đợi Duyệt Đăng ký Doanh nghiệp của Super Admin (S-08)", img_rids))

    body_parts.append(p("Bước 4: Super Admin thực hiện hành động phê duyệt: mở hộp thoại duyệt, nhập bắt buộc các trường: Tenant Key (chuỗi slug chữ thường, ví dụ 'acme-corp'), Mã nhân viên Admin, Email Admin, Họ tên Admin, Mật khẩu Admin ban đầu (tối thiểu 8 ký tự), và Múi giờ IANA (ví dụ 'Asia/Ho_Chi_Minh'). Hệ thống thực thi giao dịch nguyên tử: tạo Company, Tenant, tài khoản Admin ban đầu, lịch làm việc và ghi nhận Audit log. (Lưu ý: Không có email tự động kích hoạt hay mật khẩu ngẫu nhiên được gửi đi; Admin sử dụng thông tin do Super Admin thiết lập để đăng nhập). Nếu từ chối, Super Admin bắt buộc nhập lý do từ chối từ 5 đến 1000 ký tự [Trạng thái: PendingReview → Approved / Rejected]."))
    body_parts.append(embed_image("plat_audit", "Hình 6: Nhật ký Kiểm toán Toàn nền tảng xác nhận cấp phát Tenant thành công (S-09)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 2 ---
    body_parts.append(heading2("3. Workflow 2: Xác thực & Quản trị Không gian làm việc (Authentication & Workspace Navigation)"))
    body_parts.append(p("Mục đích: Xác thực người dùng an toàn vào hệ thống, giải quyết trường hợp một tài khoản tham gia nhiều doanh nghiệp và điều hướng về bảng điều khiển phù hợp vai trò."))
    body_parts.append(p("Tác nhân: Toàn bộ người dùng hệ thống."))
    body_parts.append(p("Trình tự thao tác chi tiết:"))

    body_parts.append(p("Bước 1: Người dùng truy cập trang đăng nhập tại route /login. Hệ thống cung cấp 2 phương thức xác thực trên giao diện: (1) Đăng nhập bằng Mã không gian làm việc (Workspace Code) + Tên đăng nhập/Email + Mật khẩu; (2) Đăng nhập bằng tài khoản Google thông qua Firebase Popup (yêu cầu cấu hình Firebase Project)."))
    body_parts.append(embed_image("login", "Hình 7: Giao diện Đăng nhập Hợp nhất BizFlow (S-01)", img_rids))

    body_parts.append(p("Bước 2: Trong trường hợp người dùng đăng nhập bằng Google hoặc Email và địa chỉ email này thuộc về hai hay nhiều doanh nghiệp khác nhau, hộp thoại Workspace Selector (S-03) sẽ tự động hiển thị để người dùng chọn không gian làm việc muốn làm việc. Phiên đăng nhập (JWT token) chỉ được cấp phát sau khi người dùng đã chỉ định chính xác TenantId."))
    body_parts.append(p("[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh hộp thoại S-03 Workspace Selector khi tài khoản thuộc nhiều doanh nghiệp] [CẦN CHỤP LẠI]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Bước 3: Hệ thống điều hướng người dùng về màn hình Không gian làm việc (/workspace) được cá nhân hóa theo vai trò:"))
    body_parts.append(p_runs([
        ("•  Trưởng phòng (Department Manager): ", True, False, 22, "1F4E79", False),
        ("Bảng điều khiển hiển thị hiệu suất phòng ban, nhiệm vụ cần phân công, cảnh báo vi phạm SLA và hàng đợi phê duyệt kết quả.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_manager", "Hình 8: Không gian làm việc Trưởng phòng (Manager Workspace - S-22)", img_rids))

    body_parts.append(p_runs([
        ("•  Nhân viên tác nghiệp (Employee): ", True, False, 22, "1F4E79", False),
        ("Bảng điều khiển tập trung vào danh sách nhiệm vụ được giao riêng (My Assigned Tasks) và danh sách yêu cầu dịch vụ do mình gửi.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_employee", "Hình 9: Không gian làm việc Nhân viên tác nghiệp (Employee Workspace - S-34)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 3 ---
    body_parts.append(heading2("4. Workflow 3: Cấu hình Doanh nghiệp & Danh mục Dịch vụ (Initial Tenant & Service Configuration)"))
    body_parts.append(p("Mục đích: Thiết lập nền tảng tổ chức ban đầu cho doanh nghiệp gồm tham số chung, phòng ban, người dùng, phân quyền vai trò, dịch vụ nội bộ, quy trình trực quan và cam kết chất lượng SLA."))
    body_parts.append(p("Tác nhân: Company Admin (Tenant Administrator)."))
    body_parts.append(p("Trình tự thao tác chi tiết:"))

    body_parts.append(p("Bước 1: Thiết lập tham số doanh nghiệp tại route /settings để cập nhật logo, múi giờ vận hành IANA và chính sách mật khẩu nội bộ."))
    body_parts.append(embed_image("settings", "Hình 10: Thiết lập Tham số Doanh nghiệp (Tenant Settings - S-12)", img_rids))

    body_parts.append(p("Bước 2: Quản lý cơ cấu phòng ban tại route /settings/organization/departments để khởi tạo sơ đồ cây phòng ban, chỉ định Trưởng phòng phụ trách và email nhận thông báo chung."))
    body_parts.append(embed_image("departments", "Hình 11: Quản lý Cơ cấu Phòng ban Doanh nghiệp (S-13) [Route thật: /settings/organization/departments]", img_rids))

    body_parts.append(p("Bước 3: Quản lý tài khoản thành viên tại route /settings/organization/users để thêm nhân viên mới, gán vào phòng ban tương ứng và cấp phát vai trò ban đầu."))
    body_parts.append(embed_image("users", "Hình 12: Danh bạ & Quản lý Tài khoản Nhân viên (S-14) [Route thật: /settings/organization/users]", img_rids))

    body_parts.append(p("Bước 4: Cấu hình vai trò và quyền hạn tinh chỉnh tại route /settings/organization/roles để thiết lập ma trận quyền hạn granular (tasks.create, tasks.assign, requests.route, sla.read...)."))
    body_parts.append(embed_image("roles", "Hình 13: Cấu hình Ma trận Quyền hạn Vai trò (S-15) [Route thật: /settings/organization/roles]", img_rids))

    body_parts.append(p("Bước 5: Xây dựng danh mục dịch vụ nội bộ tại route /settings/services để tạo các loại yêu cầu phục vụ (cấp thiết bị, nghỉ phép, hỗ trợ phần mềm...), gán phòng ban xử lý và thời hạn cam kết SLA mặc định."))
    body_parts.append(embed_image("services", "Hình 14: Danh mục Dịch vụ Nội bộ Doanh nghiệp (S-16) [Route thật: /settings/services]", img_rids))

    body_parts.append(p("Bước 6: Quản lý quy trình và thiết kế trực quan: Truy cập /settings/workflows để xem danh mục quy trình đã ban hành, và /settings/workflows/:id/versions/:v để xem/chỉnh sửa phiên bản quy trình trên canvas đồ họa. Quy trình được xuất bản sẽ áp dụng vào điều kiện kiểm soát WorkflowAllowsTransition."))
    body_parts.append(embed_image("workflows", "Hình 15: Thư viện Quy trình Doanh nghiệp (S-17) [Route thật: /settings/workflows]", img_rids))
    body_parts.append(embed_image("workflow_designer", "Hình 16: Trình Thiết kế Quy trình Trực quan (S-18) [Route thật: /settings/workflows/:id/versions/:v]", img_rids))

    body_parts.append(p("Bước 7: Thiết lập hồ sơ và phiên bản cam kết SLA: Truy cập /settings/sla-profiles để cấu hình thời gian phản hồi (Response Target), thời gian xử lý (Resolution Target), lịch làm việc (loại trừ cuối tuần, ngày lễ), và xem lịch sử phiên bản SLA tại /settings/sla-profiles/:id/versions."))
    body_parts.append(embed_image("sla_profiles", "Hình 17: Quản lý Hồ sơ Cam kết Thời gian SLA (S-19) [Route thật: /settings/sla-profiles]", img_rids))
    body_parts.append(embed_image("sla_versions", "Hình 18: Lịch sử Phiên bản Cam kết SLA (S-20) [Route thật: /settings/sla-profiles/:id/versions]", img_rids))
    body_parts.append(page_break())

    # --- Workflow 4 ---
    body_parts.append(heading2("5. Workflow 4: Vòng đời Nhiệm vụ Đầu - Cuối (End-to-End Task Lifecycle Management)"))
    body_parts.append(p("Mục đích: Điều phối toàn bộ quá trình giao việc, phân công, tiếp nhận, thực thi, nộp minh chứng kết quả và nghiệm thu theo máy trạng thái 10 bước."))
    body_parts.append(p("Tác nhân: Department Manager, Assignee (Employee), Quality Reviewer."))
    body_parts.append(p("Trình tự thao tác chi tiết:"))

    body_parts.append(p("Bước 1: Khởi tạo Nhiệm vụ: Trưởng phòng truy cập route /tasks/new (hoặc /tasks/create) để tạo nhiệm vụ mới. Nhập tiêu đề, mô tả, độ ưu tiên (Low, Medium, High, Critical), hạn chót deadline, danh sách đầu việc kiểm tra (Checklist), và chọn Hồ sơ SLA áp dụng [Trạng thái: Khởi tạo → Draft]."))
    body_parts.append(embed_image("task_create", "Hình 19: Biểu mẫu Khởi tạo Nhiệm vụ & Thiết lập Checklist (S-24)", img_rids))

    body_parts.append(p("Bước 2: Phân công Nhiệm vụ: Người quản lý phân công nhiệm vụ cho cá nhân cụ thể hoặc đưa về Hàng đợi chưa nhận của phòng ban tại route /tasks [Trạng thái: Draft → Assigned]."))
    body_parts.append(embed_image("tasks", "Hình 20: Danh sách Nhiệm vụ Phòng ban với Bộ lọc Phân quyền (S-23)", img_rids))

    body_parts.append(p("Bước 3: Tiếp nhận Nhiệm vụ: Nhân viên kiểm tra danh sách nhiệm vụ được giao riêng tại /tasks. Nhân viên mở chi tiết nhiệm vụ /tasks/:id và nhấp nút 'Tiếp nhận Nhiệm vụ' [Trạng thái: Assigned → Accepted]. Nếu không thể nhận việc, nhân viên nhấp 'Từ chối' và bắt buộc nhập lý do [Trạng thái: Assigned → Rejected]."))
    body_parts.append(embed_image("tasks_scoped", "Hình 21: Danh sách Nhiệm vụ Được giao Riêng cho Nhân viên (S-36)", img_rids))

    body_parts.append(p("Bước 4: Bắt đầu Thực thi & Ghi nhận Tiến độ: Nhân viên nhấp nút 'Bắt đầu Thực thi' [Trạng thái: Accepted → InProgress]. Trong quá trình thực hiện, nhân viên tích chọn các đầu việc hoàn thành trong Checklist, nhập ghi chú trao đổi và đính kèm tài liệu sơ bộ."))
    body_parts.append(p("[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thao tác tích checklist và cập nhật tiến độ trong quá trình InProgress] [CẦN CHỤP LẠI]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Bước 5: Nộp Kết quả Bàn giao & Minh chứng: Khi công việc hoàn tất, nhân viên nhấp nút 'Nộp Kết quả' (Submit Result). Hộp thoại yêu cầu nhập nội dung tóm tắt kết quả bàn giao và bắt buộc tải lên tệp minh chứng (.pdf, .zip dung lượng cho phép tới 500MB theo cấu hình tenant) [Trạng thái: InProgress → Submitted]."))
    body_parts.append(p("[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh hộp thoại S-13d Submit Result Dialog tải tệp minh chứng kết quả] [CẦN CHỤP LẠI]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Bước 6: Nghiệm thu & Đóng Nhiệm vụ: Người quản lý hoặc Người nghiệm thu được chỉ định mở màn hình /tasks/:id để thẩm định kết quả và kiểm tra tệp minh chứng: Nếu đạt yêu cầu, nhấp 'Nghiệm thu Đạt' [Trạng thái: Submitted → Confirmed], sau đó hệ thống tự động hoàn tất [Trạng thái: Confirmed → Completed]. Nếu chưa đạt, nhấp 'Yêu cầu Làm lại' (Rework) kèm chỉ dẫn chỉnh sửa bắt buộc; nhiệm vụ chuyển ngược lại [Trạng thái: Submitted → InProgress] cho nhân viên xử lý tiếp."))
    body_parts.append(embed_image("task_detail", "Hình 22: Không gian Thực thi Nhiệm vụ với 10 Trạng thái, Thảo luận & Minh chứng (S-28)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 5 ---
    body_parts.append(heading2("6. Workflow 5: Vòng đời Yêu cầu Dịch vụ Đầu - Cuối (End-to-End Service Request Management)"))
    body_parts.append(p("Mục đích: Tiếp nhận các yêu cầu dịch vụ nội bộ từ nhân viên, hỗ trợ phê duyệt đa cấp, điều phối về phòng ban chuyên môn, giải quyết và cho phép người gửi xác nhận kết quả."))
    body_parts.append(p("Tác nhân: Requester (Nhân viên gửi), Approver (Cấp phê duyệt), Dispatcher/Specialist (Phòng ban thụ lý)."))
    body_parts.append(p("Trình tự thao tác chi tiết:"))

    body_parts.append(p("Bước 1: Khởi tạo Yêu cầu Dịch vụ: Nhân viên truy cập route /requests/new, chọn dịch vụ trong Danh mục Dịch vụ Nội bộ, mô tả nhu cầu chi tiết, chọn độ ưu tiên (Low, Medium, High, Critical) và đính kèm tài liệu liên quan [Trạng thái: Khởi tạo → Submitted]."))
    body_parts.append(embed_image("request_create", "Hình 23: Biểu mẫu Gửi Yêu cầu Dịch vụ Nội bộ (S-35)", img_rids))

    body_parts.append(p("Bước 2: Phê duyệt Yêu cầu: Yêu cầu ở trạng thái Submitted sẽ xuất hiện trong Hàng đợi Phê duyệt tại route /approvals. Cấp quản lý kiểm tra nội dung và thực hiện phê duyệt hoặc từ chối kèm lý do bắt buộc [Trạng thái: Submitted → Rejected nếu từ chối]."))
    body_parts.append(embed_image("approvals", "Hình 24: Hàng đợi Phê duyệt Yêu cầu Đa cấp (S-26) [Hiển thị Task & Request ở trạng thái SUBMITTED]", img_rids))

    body_parts.append(p("Bước 3: Điều phối & Tiếp nhận Phòng ban: Yêu cầu được điều phối về Hàng đợi của Phòng ban xử lý tại route /requests [Trạng thái: Submitted → Routed]. Điều phối viên phòng ban tiếp nhận yêu cầu [Trạng thái: Routed → Received] và phân công chuyên viên xử lý [Trạng thái: Received → InProgress]."))
    body_parts.append(embed_image("requests", "Hình 25: Danh sách Quản lý Yêu cầu Dịch vụ Phòng ban (S-27)", img_rids))

    body_parts.append(p("Bước 4: Phân nhánh Nhiệm vụ Liên kết (Linked Tasks): Đối với các yêu cầu kỹ thuật phức tạp, chuyên viên có thể tạo các Nhiệm vụ con liên kết (Linked Tasks) trực tiếp từ màn hình chi tiết yêu cầu để theo dõi tiến độ cụ thể."))
    body_parts.append(p("[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thao tác tạo Nhiệm vụ con liên kết (Linked Task) từ màn hình chi tiết Yêu cầu] [CẦN CHỤP LẠI]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Bước 5: Giải quyết & Xác nhận Hoàn tất: Sau khi hoàn thành xử lý, chuyên viên nhập nội dung giải pháp (Resolution) và nhấp 'Giải quyết Yêu cầu' [Trạng thái: InProgress → Resolved]. Người gửi ban đầu nhận thông báo kiểm tra kết quả tại route /requests/:id: Nếu hài lòng, người gửi nhấp 'Xác nhận Đóng' [Trạng thái: Resolved → Confirmed], hệ thống tự động hoàn tất [Trạng thái: Confirmed → Closed]. Nếu chưa đạt, người gửi nhấp 'Phản hồi Chưa đạt' kèm lý do; yêu cầu chuyển ngược lại [Trạng thái: Resolved → InProgress] để xử lý bổ sung."))
    body_parts.append(embed_image("request_detail", "Hình 26: Chi tiết Xử lý Yêu cầu Dịch vụ & Liên kết Nhiệm vụ (S-29)", img_rids))
    body_parts.append(page_break())

    # --- Workflow 6 ---
    body_parts.append(heading2("7. Workflow 6: Trợ lý AI & Bóc tách Công việc Tự động (AI Assistance & Task Decomposition)"))
    body_parts.append(p("Mục đích: Tăng tốc độ lập kế hoạch dự án bằng AI thông qua cơ chế kiểm soát con người (Human-in-the-Loop). Mô hình AI chỉ đưa ra đề xuất bản nháp, con người có toàn quyền xem xét và xác nhận trước khi lưu vào cơ sở dữ liệu."))
    body_parts.append(p("Tác nhân: Department Manager, Company Admin."))
    body_parts.append(p("Trình tự thao tác chi tiết:"))

    body_parts.append(p("Bước 1: Từ màn hình Khởi tạo Nhiệm vụ (/tasks/new), người quản lý nhập mục tiêu lớn của dự án và nhấp nút 'Bóc tách bằng AI' (Decompose with AI)."))
    body_parts.append(p("[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thao tác nhập mục tiêu công việc và bấm nút Decompose with AI tại /tasks/new] [CẦN CHỤP LẠI]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Bước 2: Hệ thống chuyển hướng sang màn hình Trợ lý AI Bóc tách Công việc tại route /tasks/ai-decomposition. Mô hình AI phân tích mục tiêu và trả về danh sách các nhiệm vụ con có cấu trúc: Tiêu đề nhiệm vụ, mô tả chi tiết, thời lượng dự kiến, độ ưu tiên và cây phụ thuộc tuần tự (Directed Acyclic Graph - DAG). Thuật toán kiểm tra chu trình bảo đảm cây phụ thuộc không có vòng lặp kín."))
    body_parts.append(embed_image("ai_decomp", "Hình 27: Giao diện Trợ lý AI Đề xuất Bóc tách Công việc & Cây Phụ thuộc DAG (S-25)", img_rids))

    body_parts.append(p("Bước 3 (Cơ chế Human-in-the-Loop): Endpoint preview thuần túy tính toán và trả về DTO xem trước, tuyệt đối không ghi bản ghi nào vào DB. Người quản lý có toàn quyền chỉnh sửa trực tiếp nội dung từng đề xuất, thêm mới, xóa bớt hoặc bỏ chọn các nhiệm vụ không cần thiết."))
    body_parts.append(p("[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thao tác người quản lý chỉnh sửa inline trực tiếp trên danh sách đề xuất của AI] [CẦN CHỤP LẠI]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Bước 4: Nhấp nút 'Xác nhận & Tạo Hàng loạt Nhiệm vụ'. Hệ thống kiểm tra quyền hạn và thực thi lệnh khởi tạo đồng loạt các nhiệm vụ hợp lệ vào cơ sở dữ liệu, tự động thiết lập liên kết cha - con và ràng buộc phụ thuộc thực thi."))
    body_parts.append(p("[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thông báo xác nhận tạo thành công hàng loạt nhiệm vụ sau khi bấm confirm] [CẦN CHỤP LẠI]", italic=True, size=20, color="888888", align="center"))

    body_parts.append(p("Bước 5 (Trợ lý AI Chat Sidebar): Trong màn hình Chi tiết Nhiệm vụ và Chi tiết Yêu cầu, người dùng có thể mở widget BizFlow AI Agent để tra cứu nhanh rủi ro hạn chót, yêu cầu AI tóm tắt nội dung trao đổi hoặc gợi ý phân công công việc tối ưu."))
    body_parts.append(p("[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh widget BizFlow AI Agent góc dưới màn hình khi đang tương tác trả lời câu hỏi] [CẦN CHỤP LẠI]", italic=True, size=20, color="888888", align="center"))
    body_parts.append(page_break())

    # --- Workflow 7 ---
    body_parts.append(heading2("8. Workflow 7: Giám sát Cam kết SLA & Báo cáo Vận hành (SLA Monitoring & Operational Reports)"))
    body_parts.append(p("Mục đích: Cung cấp công cụ giám sát cam kết dịch vụ theo thời gian thực, phát hiện điểm nghẽn vận hành, phân tích hiệu suất và truy vết kiểm toán bất biến."))
    body_parts.append(p("Tác nhân: Company Admin, Department Manager, Ban Giám đốc."))
    body_parts.append(p("Trình tự thao tác chi tiết:"))

    body_parts.append(p("Bước 1: Giám sát SLA Thời gian thực: Truy cập route /sla-monitor để theo dõi toàn bộ các nhiệm vụ và yêu cầu đang hoạt động. Trực quan hóa 3 mức cảnh báo: Màu xanh (An toàn, < 75% thời hạn), Màu vàng (Cảnh báo sớm, ≥ 75% thời hạn), và Màu đỏ (Vi phạm SLA, > 100% thời hạn). Quản lý có thể can thiệp kích hoạt leo thang xử lý khẩn cấp."))
    body_parts.append(embed_image("sla_monitor", "Hình 28: Bảng Giám sát Vi phạm SLA Thời gian thực (S-31) [Route thật: /sla-monitor]", img_rids))

    body_parts.append(p("Bước 2: Báo cáo Quản trị Hiệu suất: Truy cập route /reports để theo dõi biểu đồ phân tích năng suất phòng ban, tỷ lệ hoàn thành đúng hạn (On-time Delivery Rate), thời gian xử lý trung bình và phân bổ tải công việc."))
    body_parts.append(embed_image("reports", "Hình 29: Báo cáo Quản trị Hiệu suất & Chỉ số Vận hành Doanh nghiệp (S-30) [Route thật: /reports]", img_rids))

    body_parts.append(p("Bước 3: Tra cứu Lịch sử & Lưu trữ Hồ sơ Tập trung: Truy cập route /records/search để tra cứu nhanh toàn bộ hồ sơ nghiệp vụ, tài liệu đính kèm và kết quả thực thi cũ thông qua bộ lọc đa tiêu chí."))
    body_parts.append(embed_image("record_search", "Hình 30: Tra cứu Lịch sử & Lưu trữ Hồ sơ Tập trung (S-32) [Route thật: /records/search]", img_rids))

    body_parts.append(p("Bước 4: Nhật ký Kiểm toán Doanh nghiệp: Truy cập route /audit để xem sổ lưu vết bất biến toàn bộ các thao tác nghiệp vụ trong doanh nghiệp (thời điểm, người thao tác, hành động, giá trị cũ, giá trị mới)."))
    body_parts.append(embed_image("audit_tenant", "Hình 31: Nhật ký Kiểm toán Doanh nghiệp Bất biến (Tenant Audit Trail - S-21) [Route thật: /audit]", img_rids))

    body_parts.append(p("Bước 5: Trung tâm Thông báo Sự kiện: Truy cập route /notifications để theo dõi các thông báo nhắc hạn deadline, cập nhật trạng thái nhiệm vụ và yêu cầu mới nhất."))
    body_parts.append(embed_image("notifications", "Hình 32: Trung tâm Thông báo Sự kiện Hàng ngày (S-33) [Route thật: /notifications]", img_rids))
    body_parts.append(page_break())

    # --- Workflow 8 ---
    body_parts.append(heading2("9. Workflow 8: Khôi phục Mật khẩu & Thu hồi Phiên (Password Recovery & Session Invalidation)"))
    body_parts.append(p("Mục đích: Khôi phục quyền truy cập tài khoản thông qua tiến trình gửi email ngầm bất đồng bộ (Hangfire Worker) và tự động thu hồi toàn bộ phiên làm việc cũ khi đổi mật khẩu để bảo đảm an ninh."))
    body_parts.append(p("Tác nhân: Toàn bộ người dùng đã đăng ký email trong hệ thống."))
    body_parts.append(p("Lưu ý: Tính năng này bị TẮT mặc định trong cấu hình (ResetEmail__Enabled=false, API trả AUTH.RESET_UNAVAILABLE) cho đến khi người vận hành cấu hình máy chủ SMTP hỗ trợ TLS."))
    body_parts.append(p("Trình tự thao tác chi tiết:"))

    body_parts.append(p("Bước 1: Yêu cầu Khôi phục Mật khẩu: Tại màn hình đăng nhập, người dùng nhấp liên kết 'Quên mật khẩu?' để chuyển đến route /forgot-password. Người dùng nhập địa chỉ email đã đăng ký và nhấn 'Gửi liên kết khôi phục'."))
    body_parts.append(embed_image("forgot_pwd", "Hình 33: Màn hình Yêu cầu Khôi phục Mật khẩu (S-05) [Route thật: /forgot-password]", img_rids))

    body_parts.append(p("Bước 2: Xử lý Bất đồng bộ qua Hangfire Worker: Hệ thống sinh mã Token đặt lại mật khẩu có chữ ký bảo mật và giới hạn thời gian hiệu lực (15 phút). Tiến trình Hangfire Worker chạy nền kết nối máy chủ SMTP và gửi thư chứa liên kết khôi phục duy nhất tới hòm thư người dùng."))

    body_parts.append(p("Bước 3: Thiết lập Mật khẩu Mới: Người dùng nhấp vào liên kết trong email để mở màn hình /reset-password?token=.... Nhập mật khẩu mới thỏa mãn chính sách an toàn (tối thiểu 12 ký tự, chứa ít nhất 4 ký tự khác nhau) và xác nhận lại mật khẩu."))
    body_parts.append(embed_image("reset_pwd", "Hình 34: Thiết lập Mật khẩu Mới từ Mã Token An toàn (S-06) [Route thật: /reset-password]", img_rids))

    body_parts.append(p("Bước 4: Thu hồi Phiên làm việc Cũ & Đăng nhập Lại: Sau khi đổi mật khẩu thành công, hệ thống tự động thu hồi toàn bộ các Refresh Token và phiên làm việc trước đó của tài khoản trên mọi thiết bị để ngăn ngừa rủi ro bảo mật, sau đó chuyển hướng người dùng về trang /login để đăng nhập bằng mật khẩu mới."))

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

    print(f"Writing output docx to {OUT_DOCX}...")
    with zipfile.ZipFile(OUT_DOCX, 'w', compression=zipfile.ZIP_DEFLATED) as zout:
        for fname, data in files.items():
            zout.writestr(fname, data)

    print(f"Docx generated successfully! Size: {os.path.getsize(OUT_DOCX):,} bytes")

if __name__ == '__main__':
    build_docx()
