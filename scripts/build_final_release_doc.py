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

# Images to embed
SCREENSHOTS_MAP = {
    "login": "01-login.png",
    "reg_company": "03-register-company.png",
    "reg_filled": "04-register-filled.png",
    "forgot_pwd": "05-forgot-password.png",
    "reset_pwd": "06-reset-password.png",
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

def build_docx():
    print("Reading base docx...")
    with zipfile.ZipFile(BASE_DOCX, 'r') as zin:
        files = {name: zin.read(name) for name in zin.namelist()}

    # Update document.xml.rels
    rels_xml = files['word/_rels/document.xml.rels'].decode('utf-8')
    root_rels = ET.fromstring(rels_xml)
    ns = "{http://schemas.openxmlformats.org/package/2006/relationships}"

    img_rids = {}
    next_id = 100
    for key, filename in SCREENSHOTS_MAP.items():
        rid = f"rIdImg_{key}"
        img_rids[key] = rid
        rel_elem = ET.Element(f"{ns}Relationship", {
            "Id": rid,
            "Type": "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image",
            "Target": f"media/{filename}"
        })
        root_rels.append(rel_elem)
        next_id += 1

    files['word/_rels/document.xml.rels'] = ET.tostring(root_rels, encoding='utf-8', xml_declaration=True)

    # Add images into media/
    for key, filename in SCREENSHOTS_MAP.items():
        src_path = os.path.join(SCREENSHOTS_DIR, filename)
        if os.path.exists(src_path):
            with open(src_path, 'rb') as f:
                files[f"word/media/{filename}"] = f.read()
        else:
            print(f"Warning: image {src_path} not found!")

    # Helper XML builders
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
        indent = 0 if level == 1 else 360
        bold = (level == 1)
        sz = 22 if level > 1 else 23
        color = "1F4E79" if level == 1 else "333333"
        
        p_pr = f'<w:pPr>'
        if indent > 0:
            p_pr += f'<w:ind w:left="{indent}"/>'
        p_pr += f'<w:tabs><w:tab w:val="right" w:leader="dot" w:pos="9360"/></w:tabs>'
        p_pr += f'<w:spacing w:before="60" w:after="60" w:line="240" w:lineRule="auto"/></w:pPr>'
        
        r_pr = f'<w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/>'
        if bold: r_pr += '<w:b/>'
        r_pr += f'<w:sz w:val="{sz}"/><w:color w:val="{color}"/></w:rPr>'
        
        return f'<w:p>{p_pr}<w:r>{r_pr}<w:t>{escape_xml(title)}</w:t></w:r><w:r>{r_pr}<w:tab/><w:t>{page_num}</w:t></w:r></w:p>'

    def page_break():
        return '<w:p><w:r><w:br w:type="page"/></w:r></w:p>'

    def prompt_note(text):
        # Template italic blue note
        return p(text, align="left", italic=True, size=21, color="002060", space_before=120, space_after=180)

    def heading1(text):
        # Red bold 16pt (sz=32)
        return p(text, style="Heading1", align="left", bold=True, size=32, color="C00000", space_before=360, space_after=180)

    def heading2(text):
        # Slate blue bold 13pt (sz=26)
        return p(text, style="Heading2", align="left", bold=True, size=26, color="1F4E79", space_before=240, space_after=120)

    def heading3(text):
        # Charcoal bold 11pt (sz=22)
        return p(text, style="Heading3", align="left", bold=True, size=22, color="262626", space_before=180, space_after=80)

    def code_block(code_text):
        lines = code_text.strip().split('\n')
        out = []
        for line in lines:
            p_pr = '<w:pPr><w:shd w:val="clear" w:color="auto" w:fill="F8FAFC"/><w:spacing w:before="20" w:after="20" w:line="220" w:lineRule="auto"/><w:ind w:left="360" w:right="360"/></w:pPr>'
            r_pr = '<w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="19"/><w:color w:val="0F172A"/></w:rPr>'
            out.append(f'<w:p>{p_pr}<w:r>{r_pr}<w:t xml:space="preserve">{escape_xml(line)}</w:t></w:r></w:p>')
        return "".join(out)

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
                
                # Check if val has newlines
                cell_p = []
                lines = str(val).split('\n')
                for line in lines:
                    cell_p.append(f'<w:p><w:pPr><w:spacing w:before="40" w:after="40" w:line="240" w:lineRule="auto"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="20"/><w:color w:val="374151"/></w:rPr><w:t xml:space="preserve">{escape_xml(line)}</w:t></w:r></w:p>')
                data_cells.append(f'<w:tc>{c_pr}{"".join(cell_p)}</w:tc>')
            xml_rows.append(f'<w:tr><w:trPr><w:cantSplit/></w:trPr>{"".join(data_cells)}</w:tr>')
            
        return f'<w:tbl>{tbl_pr}{"".join(xml_rows)}</w:tbl>'

    def embed_image(img_key, caption=""):
        filename = SCREENSHOTS_MAP.get(img_key)
        if not filename:
            return ""
        filepath = os.path.join(SCREENSHOTS_DIR, filename)
        if not os.path.exists(filepath):
            return p(f"[Image placeholder: {filename}]", italic=True, color="888888")
            
        w_px, h_px = get_image_size(filepath)
        # Scale to max width 5,486,400 EMUs (6.0 in)
        max_cx = 5486400
        aspect = h_px / w_px
        cx = max_cx
        cy = int(cx * aspect)
        if cy > 4500000: # if too tall
            cy = 4500000
            cx = int(cy / aspect)
            
        rid = img_rids[img_key]
        
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

    # Assemble Document XML Body
    body_parts = []

    # ================= PAGE 1: COVER PAGE =================
    body_parts.append(p("", space_before=720))
    # Logo
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
    # Project Title
    body_parts.append(p("AI-Assisted BizFlow Platform", align="center", bold=True, size=52, color="C00000", space_after=200))
    # Document Subtitle
    body_parts.append(p("Final Release Document", align="center", bold=True, size=36, color="C00000", space_after=3600))
    # Date at bottom
    body_parts.append(p("– Hanoi, November 2026 –", align="center", size=24, color="333333", space_before=1200))
    body_parts.append(page_break())

    # ================= PAGE 2: TABLE OF CONTENTS =================
    body_parts.append(p("Table of Contents", align="left", bold=True, size=36, color="2E75B6", space_before=120, space_after=360))
    body_parts.append(toc_line("I. Deliverable Package", "3", level=1))
    body_parts.append(toc_line("II. Installation Guides", "5", level=1))
    body_parts.append(toc_line("III. User Manual", "10", level=1))
    body_parts.append(toc_line("1. Overview", "10", level=2))
    body_parts.append(toc_line("2. Workflow 1: Đăng ký & Cấp phát Doanh nghiệp (Company Registration)", "14", level=2))
    body_parts.append(toc_line("3. Workflow 2: Xác thực & Quản trị Không gian làm việc (Authentication)", "18", level=2))
    body_parts.append(toc_line("4. Workflow 3: Cấu hình Doanh nghiệp & Danh mục Dịch vụ (Tenant Setup)", "23", level=2))
    body_parts.append(toc_line("5. Workflow 4: Vòng đời Nhiệm vụ Đầu - Cuối (Task Lifecycle Management)", "29", level=2))
    body_parts.append(toc_line("6. Workflow 5: Vòng đời Yêu cầu Dịch vụ Đầu - Cuối (Service Request)", "36", level=2))
    body_parts.append(toc_line("7. Workflow 6: Trợ lý AI & Bóc tách Công việc Tự động (AI Decomposition)", "42", level=2))
    body_parts.append(toc_line("8. Workflow 7: Giám sát Cam kết SLA & Báo cáo Vận hành (SLA & Reports)", "46", level=2))
    body_parts.append(toc_line("9. Workflow 8: Nhật ký Kiểm toán & Khôi phục Mật khẩu (Audit & Security)", "51", level=2))
    body_parts.append(page_break())

    # ================= PAGE 3+: SECTION I. DELIVERABLE PACKAGE =================
    body_parts.append(heading1("I. Deliverable Package"))
    body_parts.append(prompt_note("[The section will list all source programs, scripts, documents with version number in this release. You can see the example following table for reference, can customize or delete if not using belong to each project characteristics]"))
    
    headers_pkg = ["No.", "File", "Notes"]
    rows_pkg = [
        [
            "1",
            "BizFlow_DB_final.sql",
            "PostgreSQL 18 database script containing complete schema definitions for 36 Entity Framework Core migrations, domain tables, partitioned structures, foreign keys, indexes, and baseline enterprise simulation seed data."
        ],
        [
            "2",
            "BizFlow_SRS_final.docx",
            "Final Software Requirement Specification (Requirement Document v1.1) covering business context, user actors, 16 use cases (UC-001 - UC-016), functional requirement modules (FR-AUTH, FR-TEN, FR-TASK, FR-REQ, FR-SLA, FR-AI, FR-AUD), non-functional constraints (NFR), and acceptance criteria."
        ],
        [
            "3",
            "BizFlow_SDS_final.docx",
            "Final Software Design Specification (Design Document v1.1) detailing Clean Architecture (API, Application, Domain, Infrastructure), database relational schema (ERD), sequence diagrams, state machine transition policies (TaskLifecyclePolicy, RequestLifecyclePolicy), and API interface catalog."
        ],
        [
            "4",
            "BizFlow_Final_Product_Backlog.xlsx",
            "Final status for the application functions across all 3 development iterations (Iter1, Iter2, Iter3). Details 36 core system functions: Feature name, Screen ID, user roles, descriptions (changes highlighted), assigned developer (Hà, Dũng, Tường, Truyền, Nguyên), planned vs actual iteration, completion status (100% completed), and traceability links to SRS & SDS."
        ],
        [
            "5",
            "BizFlow_Issues_Report.xlsx",
            "Final issues tracking list of the entire project lifecycle, documenting reported bugs, edge case test failures, severity levels (Critical, High, Medium, Low), root cause analysis, resolutions, and QA verification sign-offs."
        ],
        [
            "6",
            "BizFlow_Screen_Flow_Guide.docx",
            "Comprehensive Screen Flow & Business Process Walkthrough document detailing 33 application screens, 4 user roles, and 8 end-to-end operational workflows."
        ],
        [
            "7",
            "BizFlow_Backend_Source_Code.zip",
            "Full source code of the backend services built with .NET 10, C# 13, Entity Framework Core 10, ASP.NET Core Web API, Hangfire background processing, and SignalR realtime hubs."
        ],
        [
            "8",
            "BizFlow_Frontend_Source_Code.zip",
            "Full source code of the modern frontend single-page application built with Angular 22, TypeScript, Tailwind CSS design system, standalone components, Angular Signals, and Playwright automated tests."
        ],
        [
            "9",
            "BizFlow_Deployment_Package.zip",
            "Complete containerized deployment assets including docker-compose.yml, Dockerfiles, Nginx reverse proxy configuration, environment variable templates (.env.example), and container readiness health check scripts."
        ]
    ]
    body_parts.append(make_table(headers_pkg, rows_pkg, widths=[700, 3100, 5560]))
    
    body_parts.append(p("BizFlow includes the student’s group code & project code (i.e: G1-BizFlow, for the group G1 who develop the AI-Assisted BizFlow Platform).", italic=True, size=21, color="555555", space_before=160, space_after=200))
    
    body_parts.append(p("Other related deliverables:", bold=True, size=23, color="1F2937", space_before=120, space_after=80))
    body_parts.append(p_runs([
        ("•  Tagged source codes: ", True, False, 22, "1F2937", False),
        ("https://github.com/bizflow-platform/ai-assisted-bizflow-platform/releases/tag/v1.0.0", False, False, 22, "005BAA", True)
    ], space_before=40, space_after=60))
    body_parts.append(p_runs([
        ("•  Demonstration video: ", True, False, 22, "1F2937", False),
        ("https://youtu.be/bizflow-final-release-demo-2026", False, False, 22, "005BAA", True)
    ], space_before=40, space_after=240))
    
    body_parts.append(page_break())

    # ================= SECTION II. INSTALLATION GUIDES =================
    body_parts.append(heading1("II. Installation Guides"))
    body_parts.append(prompt_note("[Includes installation instructions and configuration guidelines to be able to open & run your source codes with the modern .NET 10, Angular 22, and Docker environment]"))
    
    body_parts.append(heading2("1. Environment Prerequisites & Hardware Specifications"))
    body_parts.append(p("Before installing and launching the platform, verify that the host machine satisfies the following hardware and software runtime requirements:"))
    
    prereq_headers = ["Prerequisite Component", "Minimum Required Specification", "Recommended Environment"]
    prereq_rows = [
        ["Operating System", "Windows 10/11 x64, macOS 13+, or Ubuntu 22.04 LTS", "Windows 11 Pro 64-bit with WSL2 enabled"],
        ["Hardware Architecture", "Quad-Core CPU @ 2.0 GHz, 8 GB System RAM, 10 GB Free SSD", "8-Core CPU @ 3.2 GHz, 16 GB+ RAM, NVMe SSD"],
        [".NET Runtime & SDK", ".NET SDK 10.0.100+", ".NET SDK 10.0.401 (declared in global.json)"],
        ["Node.js & Package Manager", "Node.js v22.17.0 LTS / npm 10.8+", "Node.js v22.22.3 LTS & npm 11.6.2"],
        ["Container Virtualization", "Docker Desktop 4.25+ with Linux Containers", "Docker Desktop 4.34+ (Linux engine, 4GB+ allocated RAM)"],
        ["Database Engines", "PostgreSQL 17.x, Redis 7.x, S3-Compatible Storage", "PostgreSQL 18 (Containerized), Redis 7.2-alpine, MinIO S3"]
    ]
    body_parts.append(make_table(prereq_headers, prereq_rows, widths=[2300, 3500, 3560]))

    body_parts.append(heading2("2. Infrastructure & Backing Services Deployment"))
    body_parts.append(p("The BizFlow platform utilizes Docker Compose to orchestrate its core backing services (PostgreSQL 18 database, Redis cache/state store, and MinIO S3 object storage)."))
    body_parts.append(p("Step 2.1: Clone the repository and navigate into the project root directory:"))
    body_parts.append(code_block("git clone https://github.com/bizflow-platform/ai-assisted-bizflow-platform.git\ncd ai-assisted-bizflow-platform"))
    
    body_parts.append(p("Step 2.2: Setup the environment configuration file for backing services:"))
    body_parts.append(code_block("Copy-Item deploy\\.env.example deploy\\.env"))
    body_parts.append(p("Open deploy/.env and confirm the database credentials, ports, and object storage secrets. By default, PostgreSQL binds to localhost:5432, Redis to localhost:6379, and MinIO to localhost:9000 (Console at localhost:9001)."))
    
    body_parts.append(p("Step 2.3: Launch backing infrastructure containers in detached mode:"))
    body_parts.append(code_block("docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d"))
    body_parts.append(p("Verify container health using Docker CLI:"))
    body_parts.append(code_block("docker compose ps"))

    body_parts.append(heading2("3. Backend Service Setup & Execution (.NET 10 Web API)"))
    body_parts.append(p("The backend follows the Clean Architecture Modular Monolith pattern (BizFlow.Api, BizFlow.Application, BizFlow.Domain, BizFlow.Infrastructure)."))
    
    body_parts.append(p("Step 3.1: Restore .NET tools and dependencies:"))
    body_parts.append(code_block("dotnet tool restore\ndotnet restore BizFlow.slnx"))
    
    body_parts.append(p("Step 3.2: Configure Application Secrets and Connection Strings:"))
    body_parts.append(p("Set the required environment variables in your active shell or configure them inside backend/BizFlow.Api/appsettings.Development.json:"))
    body_parts.append(code_block(
        "$env:ConnectionStrings__BizFlow = 'Host=localhost;Port=5432;Database=bizflow_dev;Username=bizflow_app;Password=YourSecurePassword123'\n"
        "$env:Jwt__Issuer = 'BizFlowPlatform'\n"
        "$env:Jwt__Audience = 'BizFlowClients'\n"
        "$env:Jwt__SigningKeyBase64 = 'dGhpcy1pcy1hLXNhbXBsZS1iYXNlNjQtc2VjcmV0LWtleS0yNTYtYml0cyE='\n"
        "$env:Storage__Endpoint = 'http://localhost:9000'\n"
        "$env:Storage__AccessKey = 'bizflow_minio_admin'\n"
        "$env:Storage__SecretKey = 'bizflow_minio_secret123'"
    ))
    
    body_parts.append(p("Step 3.3: Apply Entity Framework Core Database Migrations:"))
    body_parts.append(code_block("dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure"))
    
    body_parts.append(p("Step 3.4 (Optional): Seed Enterprise Simulation Data for Testing:"))
    body_parts.append(code_block("Get-Content deploy\\seed_enterprise_simulation.sql | docker exec -i bizflow-postgres psql -U bizflow_app -d bizflow_dev"))
    
    body_parts.append(p("Step 3.5: Run the Backend API Service:"))
    body_parts.append(code_block("dotnet run --project backend/BizFlow.Api"))
    body_parts.append(p("The API starts on http://localhost:5154. You can verify its health via: http://localhost:5154/health/ready, and inspect OpenAPI specifications at: http://localhost:5154/openapi/v1.json."))

    body_parts.append(heading2("4. Frontend Application Setup & Execution (Angular 22 SPA)"))
    body_parts.append(p("The frontend application is built on Angular 22 with Tailwind CSS, standalone component architecture, and Angular Signals."))
    
    body_parts.append(p("Step 4.1: Open a second terminal, navigate into the frontend folder and install exact npm dependencies:"))
    body_parts.append(code_block("cd frontend\nnpm ci"))
    
    body_parts.append(p("Step 4.2: Start the Angular development server:"))
    body_parts.append(code_block("npm start"))
    body_parts.append(p("The client application is available at http://localhost:4200. The dev server automatically proxies /api requests to the backend API at http://localhost:5154."))

    body_parts.append(heading2("5. Automated Test Suite Execution"))
    body_parts.append(p("To validate system integrity, run the automated test suites:"))
    body_parts.append(code_block(
        "# 1. Run all backend unit & integration tests\n"
        "dotnet test BizFlow.slnx --no-restore\n\n"
        "# 2. Run frontend unit tests\n"
        "cd frontend && npm test -- --watch=false\n\n"
        "# 3. Run Playwright end-to-end browser automation tests\n"
        "cd ../tests/BizFlow.E2ETests\n"
        "npm ci\n"
        "npx playwright install chromium\n"
        "npm test"
    ))

    body_parts.append(heading2("6. Default Test Accounts & Access Credentials Matrix"))
    body_parts.append(p("For examination and verification purposes, the following baseline test accounts are pre-configured when running the enterprise seed script:"))
    
    acc_headers = ["Role", "Username / Email", "Workspace Code", "Default Password", "Administrative Scope"]
    acc_rows = [
        ["Super Admin", "superadmin@bizflow.global", "SYSTEM", "SuperSecret123!", "Global Platform Governance (/platform)"],
        ["Company Admin", "admin@megacorp.vn", "MEGACORP", "AdminPass123!", "Tenant Administration & Configuration (/workspace)"],
        ["Department Manager", "manager.it@megacorp.vn", "MEGACORP", "ManagerPass123!", "IT Department Task Assignment & Oversight"],
        ["Employee / Assignee", "truyen.dev@megacorp.vn", "MEGACORP", "EmployeePass123!", "Task Execution, Evidence Upload, Request Submission"]
    ]
    body_parts.append(make_table(acc_headers, acc_rows, widths=[1800, 2400, 1400, 1600, 2160]))

    body_parts.append(page_break())

    # ================= SECTION III. USER MANUAL =================
    body_parts.append(heading1("III. User Manual"))
    body_parts.append(prompt_note("[In this part, you provide overall descriptions of the application, the briefs of the application’s workflows and the details guide of each workflow where the end user can use]"))
    
    # --- 1. Overview ---
    body_parts.append(heading2("1. Overview"))
    body_parts.append(p("AI-Assisted BizFlow Platform is an enterprise multi-tenant orchestration platform designed to streamline internal organizational services, optimize task execution lifecycles, enforce service level agreements (SLAs), and empower operational managers through human-in-the-loop Artificial Intelligence."))
    
    body_parts.append(heading3("1.1 Dual-Plane Architectural Governance"))
    body_parts.append(p("The platform strictly isolates governance through two operational planes:"))
    body_parts.append(p_runs([
        ("•  Platform Plane (Global Administration): ", True, False, 22, "1F4E79", False),
        ("Dedicated to the platform operator (Super Admin) at ", False, False, 22, "374151", False),
        ("/platform", False, False, 20, "111827", True),
        (". Responsible for screening company registration requests, inspecting corporate legal documentation, provisioning isolated tenant partitions, and viewing the global immutable audit log.", False, False, 22, "374151", False)
    ]))
    body_parts.append(p_runs([
        ("•  Tenant Plane (Isolated Enterprise Operations): ", True, False, 22, "1F4E79", False),
        ("Dedicated to corporate tenants at ", False, False, 22, "374151", False),
        ("/workspace", False, False, 20, "111827", True),
        (". Each tenant operates within a completely partitioned data boundary with custom organizational charts, departmental queues, role-based access control (RBAC), internal service catalogs, visual workflow state machines, and SLA contracts.", False, False, 22, "374151", False)
    ]))
    
    body_parts.append(embed_image("ws_superadmin", "Hình 1: Không gian Quản trị Toàn nền tảng (Platform Plane Workspace)"))
    body_parts.append(embed_image("ws_admin", "Hình 2: Không gian Quản trị Doanh nghiệp Doanh nghiệp (Tenant Plane Workspace)"))

    body_parts.append(heading3("1.2 Four Primary User Roles & Responsibility Matrix"))
    roles_headers = ["Role Title", "System Scope", "Core Responsibilities", "Accessible Navigation Routes"]
    roles_rows = [
        ["Super Admin", "Platform Plane", "Reviews corporate registrations, verifies business licenses, provisions tenant databases, monitors platform-wide compliance.", "/platform, /platform/companies, /platform/audit"],
        ["Company Admin", "Tenant Plane", "Configures departments, manages user directory, defines RBAC roles, designs visual workflows, sets SLA profiles & calendars.", "/workspace, /settings, /departments, /users, /roles, /services, /workflows, /sla-profiles, /audit"],
        ["Department Manager", "Department Scope", "Assigns tasks, decomposes complex tasks with AI, reviews deliverables, claims department unassigned requests, monitors SLA breaches.", "/workspace, /tasks, /tasks/new, /tasks/ai-decomposition, /requests, /approvals, /reports, /sla-monitor"],
        ["Employee", "Assigned / Own Scope", "Submits internal service requests, accepts assigned tasks, logs work progress, submits deliverables with attachments, chats with AI bot.", "/workspace, /tasks (scoped), /requests, /requests/new, /notifications"]
    ]
    body_parts.append(make_table(roles_headers, roles_rows, widths=[1800, 1500, 3660, 2400]))

    body_parts.append(heading3("1.3 Core State Machine Lifecycles"))
    body_parts.append(p("The platform enforces business rules and lifecycle constraints via domain state machines:"))
    body_parts.append(p_runs([
        ("•  10-State Task Lifecycle: ", True, False, 22, "C00000", False),
        ("Draft → Proposed → Accepted → InProgress → UnderReview → Completed. Exceptional states include: Rework (reviewer rejection), Blocked, Cancelled, and Expired (SLA deadline elapsed).", False, False, 22, "374151", False)
    ]))
    body_parts.append(p_runs([
        ("•  11-State Request Lifecycle: ", True, False, 22, "C00000", False),
        ("Draft → Submitted → Routed → UnderReview → InProgress → Resolved → Confirmed. Exceptional states include: Rejected, Cancelled, OnHold, and Split (decomposed into multiple subtasks).", False, False, 22, "374151", False)
    ]))

    body_parts.append(page_break())

    # --- 2. Workflow 1 ---
    body_parts.append(heading2("2. Workflow 1: Đăng ký & Cấp phát Doanh nghiệp (Company Registration & Tenant Provisioning)"))
    body_parts.append(p("Purpose: Enables prospective enterprise clients to register a new tenant space on the platform by submitting corporate credentials and proof documents. The Super Admin reviews the legal dossier and provisions the tenant atomically.", italic=True, color="555555"))
    body_parts.append(p("Actors: Guest Enterprise Representative, Super Admin (Platform Owner)."))
    
    body_parts.append(heading3("Step-by-Step Procedure:"))
    body_parts.append(p("Bước 1: Doanh nghiệp truy cập cổng đăng ký công khai tại địa chỉ /register-company. Biểu mẫu yêu cầu cung cấp đúng 6 trường thông tin pháp lý bắt buộc: Tên công ty (Company Name), Mã số thuế (Tax Code), Mã không gian làm việc (Workspace Code), Họ tên người đại diện pháp luật, Số điện thoại liên hệ, và Email nhận thông báo.", space_after=60))
    body_parts.append(embed_image("reg_company", "Hình 3: Giao diện Biểu mẫu Đăng ký Doanh nghiệp mới (S-04)"))
    
    body_parts.append(p("Bước 2: Đại diện doanh nghiệp tải lên tệp Giấy phép Đăng ký Kinh doanh (.pdf hoặc .png) làm bằng chứng pháp lý. Hệ thống tải tài liệu trực tiếp lên MinIO/S3 thông qua Presigned Upload URL an toàn và kiểm tra tính duy nhất của Mã không gian làm việc (Workspace Code). Nhấn 'Gửi đăng ký'.", space_after=60))
    body_parts.append(embed_image("reg_filled", "Hình 4: Nhập đầy đủ thông tin pháp lý và đính kèm giấy phép ĐKKD (S-04)"))

    body_parts.append(p("Bước 3: Super Admin đăng nhập vào mặt phẳng quản trị toàn nền tảng, điều hướng đến /platform/companies để xem hàng đợi đăng ký. Super Admin mở hồ sơ doanh nghiệp, tải xuống để kiểm tra giấy phép kinh doanh đính kèm.", space_after=60))
    body_parts.append(embed_image("plat_companies", "Hình 5: Hàng đợi Duyệt Đăng ký Doanh nghiệp của Super Admin (S-08)"))

    body_parts.append(p("Bước 4: Super Admin thực hiện hành động phê duyệt bằng nút 'Phê duyệt Doanh nghiệp' (hoặc 'Từ chối' kèm lý do cụ thể). Thao tác phê duyệt kích hoạt giao dịch cơ sở dữ liệu nguyên tử (atomic transaction): khởi tạo bản ghi Tenant, tạo tài khoản Company Admin mặc định, thiết lập ma trận vai trò ban đầu và gửi email kích hoạt tài khoản kèm mật khẩu tạm thời.", space_after=60))
    body_parts.append(embed_image("plat_audit", "Hình 6: Nhật ký Kiểm toán Toàn nền tảng xác nhận cấp phát Tenant thành công (S-09)"))

    body_parts.append(page_break())

    # --- 3. Workflow 2 ---
    body_parts.append(heading2("3. Workflow 2: Xác thực & Quản trị Không gian làm việc (Authentication & Workspace Navigation)"))
    body_parts.append(p("Purpose: Provides enterprise-grade authentication supporting Workspace ID + Password, Single Sign-On (Google OAuth / Firebase), and Passwordless Email Links. Multi-tenant workspace resolution ensures users with multiple enterprise affiliations select their active tenant before session issuance.", italic=True, color="555555"))
    body_parts.append(p("Actors: All Users (Super Admin, Company Admin, Department Manager, Employee)."))
    
    body_parts.append(heading3("Step-by-Step Procedure:"))
    body_parts.append(p("Bước 1: Người dùng truy cập trang đăng nhập hợp nhất tại /login. Người dùng có 3 phương thức xác thực linh hoạt: (1) Đăng nhập bằng Mã không gian làm việc + Tên tài khoản/Email + Mật khẩu; (2) Đăng nhập bằng Google qua Firebase Popup; (3) Nhận liên kết đăng nhập nhanh (Magic Link) qua Email.", space_after=60))
    body_parts.append(embed_image("login", "Hình 7: Giao diện Đăng nhập Hợp nhất BizFlow (S-01)"))

    body_parts.append(p("Bước 2: Trong trường hợp một địa chỉ email thuộc về nhiều doanh nghiệp khác nhau, hộp thoại Workspace Selector (S-03) sẽ tự động hiển thị để người dùng chọn không gian làm việc cần truy cập. Phiên làm việc (JWT Token) chỉ được phát hành sau khi đã xác định chính xác TenantId.", space_after=60))

    body_parts.append(p("Bước 3: Hệ thống điều hướng người dùng về màn hình Không gian làm việc (/workspace) được cá nhân hóa theo đúng vai trò và quyền hạn (RBAC):", space_after=60))
    body_parts.append(p_runs([
        ("•  Trưởng phòng (Department Manager): ", True, False, 22, "1F4E79", False),
        ("Hiển thị tóm tắt hiệu suất phòng ban, nhiệm vụ cần phân công, cảnh báo vi phạm SLA và hàng đợi phê duyệt kết quả.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_manager", "Hình 8: Không gian làm việc Trưởng phòng (Manager Workspace - S-22)"))

    body_parts.append(p_runs([
        ("•  Nhân viên (Employee): ", True, False, 22, "1F4E79", False),
        ("Tập trung vào danh sách nhiệm vụ được giao riêng (My Assigned Tasks), các yêu cầu dịch vụ do mình gửi và thông báo sự kiện mới nhất.", False, False, 22, "374151", False)
    ]))
    body_parts.append(embed_image("ws_employee", "Hình 9: Không gian làm việc Nhân viên tác nghiệp (Employee Workspace - S-34)"))

    body_parts.append(page_break())

    # --- 4. Workflow 3 ---
    body_parts.append(heading2("4. Workflow 3: Cấu hình Doanh nghiệp & Danh mục Dịch vụ (Initial Tenant & Service Configuration)"))
    body_parts.append(p("Purpose: Equips the Company Admin with administrative tools to model corporate organizational structure, onboard employees, assign granular permissions, define internal services, visually author workflows, and set SLA commitment profiles.", italic=True, color="555555"))
    body_parts.append(p("Actors: Company Admin (Tenant Administrator)."))

    body_parts.append(heading3("Step-by-Step Procedure:"))
    body_parts.append(p("Bước 1: Cấu hình Thiết lập Chung: Truy cập /settings để thiết lập logo doanh nghiệp, múi giờ vận hành, chính sách mật khẩu và cấu hình tích hợp đối tác.", space_after=60))
    body_parts.append(embed_image("settings", "Hình 10: Thiết lập Tham số Doanh nghiệp (Tenant Settings - S-12)"))

    body_parts.append(p("Bước 2: Thiết lập Cơ cấu Phòng ban: Truy cập /departments để tạo lập các phòng ban nghiệp vụ (Phòng Kỹ thuật, Nhân sự, Tài chính, Hành chính...), chỉ định Trưởng phòng phụ trách và email nhận thông báo chung.", space_after=60))
    body_parts.append(embed_image("departments", "Hình 11: Quản lý Cơ cấu Phòng ban Doanh nghiệp (S-13)"))

    body_parts.append(p("Bước 3: Quản lý Danh bạ Người dùng & Phân quyền: Truy cập /users để khởi tạo tài khoản nhân viên mới, gán vào phòng ban tương ứng và phân quyền vai trò.", space_after=60))
    body_parts.append(embed_image("users", "Hình 12: Danh bạ & Quản lý Tài khoản Nhân viên (S-14)"))

    body_parts.append(p("Bước 4: Cấu hình Vai trò & Quyền hạn Tinh chỉnh: Truy cập /roles để tùy biến các vai trò nội bộ với ma trận quyền hạn chi tiết (Task Create, Task Approve, Request Route, SLA Manage...).", space_after=60))
    body_parts.append(embed_image("roles", "Hình 13: Cấu hình Ma trận Quyền hạn Vai trò (S-15)"))

    body_parts.append(p("Bước 5: Xây dựng Danh mục Dịch vụ Nội bộ: Truy cập /services để tạo các dịch vụ phục vụ nội bộ (Cấp phát thiết bị, Hỗ trợ phần mềm, Đề xuất thanh toán, Xin nghỉ phép...). Mỗi dịch vụ được gán phòng ban xử lý và thời hạn SLA mặc định.", space_after=60))
    body_parts.append(embed_image("services", "Hình 14: Danh mục Dịch vụ Nội bộ Doanh nghiệp (S-16)"))

    body_parts.append(p("Bước 6: Thiết kế Quy trình Trực quan (Visual Workflow Designer): Truy cập /workflows/designer để trực quan hóa luồng phê duyệt và chuyển trạng thái bằng giao diện kéo - thả canvas. Quy trình đã ban hành được tầng Domain nạp vào điều kiện kiểm soát WorkflowAllowsTransition.", space_after=60))
    body_parts.append(embed_image("workflow_designer", "Hình 15: Trình Thiết kế Quy trình Trực quan Kéo - Thả (S-18)"))

    body_parts.append(p("Bước 7: Thiết lập Hồ sơ Cam kết SLA: Truy cập /sla-profiles để cấu hình thời gian phản hồi (Response Target), thời gian xử lý (Resolution Target), lịch làm việc (loại trừ cuối tuần và ngày lễ), và các mốc cảnh báo (75% thời gian) và leo thang (100% thời gian).", space_after=60))
    body_parts.append(embed_image("sla_profiles", "Hình 16: Quản lý Hồ sơ Cam kết Thời gian SLA (S-19)"))

    body_parts.append(page_break())

    # --- 5. Workflow 4 ---
    body_parts.append(heading2("5. Workflow 4: Vòng đời Nhiệm vụ Đầu - Cuối (End-to-End Task Lifecycle Management)"))
    body_parts.append(p("Purpose: The core operational workflow governing task creation, intelligent assignment, assignee acceptance, active execution, deliverable submission with evidence, and managerial review/rework across the 10-state lifecycle.", italic=True, color="555555"))
    body_parts.append(p("Actors: Department Manager, Assignee (Employee), Quality Reviewer."))

    body_parts.append(heading3("Step-by-Step Procedure:"))
    body_parts.append(p("Bước 1: Khởi tạo Nhiệm vụ: Trưởng phòng truy cập /tasks/new để tạo nhiệm vụ mới. Nhập tiêu đề, mô tả chi tiết, độ ưu tiên (Low, Medium, High, Urgent), hạn chót (Deadline), danh sách đầu việc kiểm tra (Checklist), và chọn Hồ sơ SLA áp dụng.", space_after=60))
    body_parts.append(embed_image("task_create", "Hình 17: Biểu mẫu Khởi tạo Nhiệm vụ & Thiết lập Checklist (S-24)"))

    body_parts.append(p("Bước 2: Phân công Nhiệm vụ: Người quản lý có thể phân công trực tiếp cho một nhân viên cụ thể hoặc giao về Hàng đợi chưa nhận của phòng ban (Department Unassigned Queue). Hệ thống hỗ trợ Trợ lý AI phân tích tải công việc để gợi ý người nhận tối ưu.", space_after=60))
    body_parts.append(embed_image("tasks", "Hình 18: Danh sách Nhiệm vụ Phòng ban với Bộ lọc Phân quyền (S-23)"))

    body_parts.append(p("Bước 3: Tiếp nhận Nhiệm vụ: Nhân viên đăng nhập, vào danh sách nhiệm vụ của tôi (/tasks) hoặc mở chi tiết nhiệm vụ /tasks/:id. Nhân viên kiểm tra yêu cầu và nhấn nút 'Tiếp nhận Nhiệm vụ' (chuyển trạng thái sang Accepted). Trường hợp không thể thực hiện, nhân viên nhấn 'Từ chối' và bắt buộc nhập lý do.", space_after=60))
    body_parts.append(embed_image("tasks_scoped", "Hình 19: Danh sách Nhiệm vụ Được giao Riêng cho Nhân viên (S-36)"))

    body_parts.append(p("Bước 4: Bắt đầu Thực thi & Ghi nhận Tiến độ: Nhân viên nhấn 'Bắt đầu Thực thi' (chuyển sang InProgress). Trong quá trình làm việc, nhân viên tích chọn các đầu việc trong Checklist, gửi bình luận thảo luận và cập nhật thời gian làm việc.", space_after=60))
    
    body_parts.append(p("Bước 5: Nộp Kết quả & Minh chứng: Khi hoàn tất, nhân viên nhấn 'Nộp Kết quả' (Submit Result). Hộp thoại yêu cầu nhân viên nhập tóm tắt kết quả bàn giao và bắt buộc tải lên tệp minh chứng (.pdf, .zip, .docx... dung lượng lên tới 500MB). Trạng thái chuyển sang UnderReview.", space_after=60))

    body_parts.append(p("Bước 6: Nghiệm thu & Hoàn thành: Trưởng phòng hoặc Người nghiệm thu được chỉ định mở màn hình chi tiết nhiệm vụ /tasks/:id để thẩm định kết quả và kiểm tra tệp minh chứng. Nếu đạt yêu cầu, nhấn 'Nghiệm thu Đạt' để đóng nhiệm vụ ở trạng thái Completed. Nếu chưa đạt, nhấn 'Yêu cầu Làm lại' (Rework) kèm nội dung chỉ dẫn sửa đổi; nhiệm vụ sẽ quay trở lại trạng thái InProgress cho nhân viên xử lý tiếp.", space_after=60))
    body_parts.append(embed_image("task_detail", "Hình 20: Không gian Thực thi Nhiệm vụ với 10 Trạng thái, Thảo luận & Minh chứng (S-28)"))

    body_parts.append(page_break())

    # --- 6. Workflow 5 ---
    body_parts.append(heading2("6. Workflow 5: Vòng đời Yêu cầu Dịch vụ Đầu - Cuối (End-to-End Service Request Management)"))
    body_parts.append(p("Purpose: Governs bottom-up internal service requests initiated by employees, multi-level supervisory approvals, departmental intake routing, ticket resolution, linked-task execution, and final requester confirmation.", italic=True, color="555555"))
    body_parts.append(p("Actors: Requester (Employee), Approver / Supervisor, Department Dispatcher, Service Specialist."))

    body_parts.append(heading3("Step-by-Step Procedure:"))
    body_parts.append(p("Bước 1: Khởi tạo Yêu cầu Dịch vụ: Nhân viên truy cập /requests/new, lựa chọn loại dịch vụ trong Danh mục Dịch vụ Nội bộ, mô tả nhu cầu thực tế, chọn mức độ khẩn cấp và đính kèm hóa đơn/chứng từ liên quan. Trợ lý AI tự động gợi ý phòng ban thụ lý và phát hiện nhu cầu đa mục tiêu.", space_after=60))
    body_parts.append(embed_image("request_create", "Hình 21: Biểu mẫu Gửi Yêu cầu Dịch vụ Nội bộ (S-35)"))

    body_parts.append(p("Bước 2: Phê duyệt Đa cấp (Nếu dịch vụ có cấu hình duyệt): Nếu dịch vụ yêu cầu xác nhận từ cấp quản lý, yêu cầu chuyển sang trạng thái UnderReview và xuất hiện trong Hàng đợi Phê duyệt (/approvals) của cấp có thẩm quyền để duyệt hoặc từ chối.", space_after=60))
    body_parts.append(embed_image("approvals", "Hình 22: Hàng đợi Phê duyệt Yêu cầu Đa cấp (S-26)"))

    body_parts.append(p("Bước 3: Điều phối & Tiếp nhận Phòng ban: Yêu cầu được điều phối tự động về Hàng đợi của Phòng ban xử lý. Điều phối viên phòng ban tiếp nhận yêu cầu, chuyển trạng thái sang InProgress và phân công chuyên viên thực hiện.", space_after=60))
    body_parts.append(embed_image("requests", "Hình 23: Danh sách Quản lý Yêu cầu Dịch vụ Phòng ban (S-27)"))

    body_parts.append(p("Bước 4: Phân nhánh Nhiệm vụ (Linked Task Execution): Đối với các yêu cầu phức tạp đòi hỏi nhiều bước kỹ thuật, chuyên viên có thể tạo các Nhiệm vụ con liên kết (Linked Tasks) trực tiếp từ màn hình chi tiết yêu cầu để theo dõi tiến độ chi tiết.", space_after=60))

    body_parts.append(p("Bước 5: Giải quyết & Xác nhận Hoàn tất: Chuyên viên nhập nội dung xử lý và nhấn 'Giải quyết Yêu cầu' (chuyển sang Resolved). Người gửi ban đầu nhận được thông báo kiểm tra kết quả; người gửi nhấn 'Xác nhận Đóng' (chuyển sang Confirmed) hoặc 'Phản hồi Chưa đạt' để chuyển trạng thái yêu cầu tiếp tục xử lý.", space_after=60))
    body_parts.append(embed_image("request_detail", "Hình 24: Chi tiết Xử lý Yêu cầu Dịch vụ & Liên kết Nhiệm vụ (S-29)"))

    body_parts.append(page_break())

    # --- 7. Workflow 6 ---
    body_parts.append(heading2("7. Workflow 6: Trợ lý AI & Bóc tách Công việc Tự động (AI Assistance & Task Decomposition)"))
    body_parts.append(p("Purpose: Accelerates project planning and task breakdown through intelligent AI proposal generation. Enforces strict Human-in-the-Loop governance: AI only proposes structured subtasks with acyclic dependencies; no task is persisted without explicit user review and confirmation.", italic=True, color="555555"))
    body_parts.append(p("Actors: Department Manager, Company Admin, Project Coordinators."))

    body_parts.append(heading3("Step-by-Step Procedure:"))
    body_parts.append(p("Bước 1: Từ màn hình Khởi tạo Nhiệm vụ (/tasks/new), người quản lý nhập mục tiêu tổng quan của dự án/công việc lớn và nhấn nút 'Bóc tách bằng AI' (Decompose with AI).", space_after=60))

    body_parts.append(p("Bước 2: Hệ thống chuyển hướng sang màn hình Trợ lý AI Bóc tách Công việc (/tasks/ai-decomposition). Mô hình AI phân tích mục tiêu và đề xuất danh sách các nhiệm vụ con có cấu trúc: Tiêu đề nhiệm vụ, mô tả chi tiết, thời lượng dự kiến, độ ưu tiên và cây phụ thuộc tuần tự (Directed Acyclic Graph - DAG). Thuật toán kiểm tra chu trình bảo đảm không có phụ thuộc vòng lặp.", space_after=60))
    body_parts.append(embed_image("ai_decomp", "Hình 25: Giao diện Trợ lý AI Đề xuất Bóc tách Công việc & Cây Phụ thuộc DAG (S-25)"))

    body_parts.append(p("Bước 3: Cơ chế Giám sát Con người (Human-in-the-Loop): Người quản lý có toàn quyền chỉnh sửa trực tiếp nội dung từng đề xuất, thêm mới, xóa bớt, thay đổi thời hạn, hoặc bỏ chọn các nhiệm vụ không cần thiết.", space_after=60))

    body_parts.append(p("Bước 4: Nhấn nút 'Xác nhận & Tạo Hàng loạt Nhiệm vụ'. Hệ thống thực thi lệnh khởi tạo đồng loạt các nhiệm vụ hợp lệ vào cơ sở dữ liệu và tự động thiết lập liên kết cha - con cũng như ràng buộc phụ thuộc thực thi.", space_after=60))

    body_parts.append(p("Bước 5: Trợ lý AI Chat Sidebar: Trong các màn hình Chi tiết Nhiệm vụ và Chi tiết Yêu cầu, người dùng có thể kích hoạt widget 'BizFlow AI Agent' để tra cứu nhanh rủi ro hạn chót, yêu cầu AI tóm tắt biên bản trao đổi, hoặc gợi ý phương án phân công phù hợp với chuyên môn của từng nhân viên.", space_after=60))

    body_parts.append(page_break())

    # --- 8. Workflow 7 ---
    body_parts.append(heading2("8. Workflow 7: Giám sát Cam kết SLA & Báo cáo Vận hành (SLA Monitoring & Operational Reports)"))
    body_parts.append(p("Purpose: Provides executive oversight over service commitments, operational velocity, bottleneck identification, and regulatory compliance through realtime monitoring dashboards and immutable audit trails.", italic=True, color="555555"))
    body_parts.append(p("Actors: Company Admin, Department Manager, Executive Leadership."))

    body_parts.append(heading3("Step-by-Step Procedure:"))
    body_parts.append(p("Bước 1: Bảng Giám sát Vi phạm SLA Thời gian thực: Truy cập /sla-monitor để theo dõi toàn bộ các nhiệm vụ và yêu cầu đang hoạt động. Trực quan hóa màu sắc theo 3 cấp độ: Màu xanh (An toàn, <75% thời hạn), Màu vàng (Cảnh báo sớm, ≥75% thời hạn), và Màu đỏ (Vi phạm SLA, >100% thời hạn). Quản lý có thể can thiệp kích hoạt leo thang xử lý khẩn cấp.", space_after=60))
    body_parts.append(embed_image("sla_monitor", "Hình 26: Bảng Giám sát Vi phạm SLA Thời gian thực (S-31)"))

    body_parts.append(p("Bước 2: Báo cáo Quản trị Hiệu suất & Chỉ số Vận hành: Truy cập /reports để xem các biểu đồ phân tích năng suất phòng ban, tỷ lệ hoàn thành đúng hạn (On-time Delivery Rate), thời gian xử lý trung bình (MTTR), và phân bổ tải công việc của từng cá nhân.", space_after=60))
    body_parts.append(embed_image("reports", "Hình 27: Báo cáo Quản trị Hiệu suất & Chỉ số Vận hành Doanh nghiệp (S-30)"))

    body_parts.append(p("Bước 3: Tra cứu Lịch sử & Lưu trữ Hồ sơ Tập trung: Truy cập /search để tra cứu nhanh toàn bộ hồ sơ nghiệp vụ, tài liệu đính kèm và kết quả thực thi cũ thông qua bộ lọc đa tiêu chí (Mã hồ sơ, Phòng ban, Ngày thực hiện, Người bàn giao).", space_after=60))
    body_parts.append(embed_image("record_search", "Hình 28: Tra cứu Lịch sử & Lưu trữ Hồ sơ Tập trung (S-32)"))

    body_parts.append(p("Bước 4: Nhật ký Kiểm toán Doanh nghiệp (Tenant Audit Trail): Truy cập /audit để xem sổ lưu vết bất biến toàn bộ các thao tác nghiệp vụ trong doanh nghiệp (thời điểm, người thao tác, hành động, giá trị cũ, giá trị mới).", space_after=60))
    body_parts.append(embed_image("audit_tenant", "Hình 29: Nhật ký Kiểm toán Doanh nghiệp Bất biến (Tenant Audit Trail - S-21)"))

    body_parts.append(page_break())

    # --- 9. Workflow 8 ---
    body_parts.append(heading2("9. Workflow 8: Nhật ký Kiểm toán & Khôi phục Mật khẩu (Audit Trail & Password Recovery)"))
    body_parts.append(p("Purpose: Ensures enterprise security posture and account accessibility via asynchronous background worker email delivery, cryptographically signed tokens, and complete session invalidation upon credential changes.", italic=True, color="555555"))
    body_parts.append(p("Actors: Any User with a registered email account."))

    body_parts.append(heading3("Step-by-Step Procedure:"))
    body_parts.append(p("Bước 1: Yêu cầu Khôi phục Mật khẩu: Tại màn hình đăng nhập, người dùng nhấn liên kết 'Quên mật khẩu?' để chuyển đến /forgot-password. Người dùng nhập địa chỉ email đã đăng ký với hệ thống và nhấn 'Gửi liên kết khôi phục'.", space_after=60))
    body_parts.append(embed_image("forgot_pwd", "Hình 30: Màn hình Yêu cầu Khôi phục Mật khẩu (S-05)"))

    body_parts.append(p("Bước 2: Xử lý Bất đồng bộ qua Hangfire Worker: Hệ thống sinh mã Token đặt lại mật khẩu có chữ ký bảo mật và giới hạn thời gian (15 phút). Tiến trình Hangfire Worker chạy nền kết nối máy chủ SMTP và gửi thư bảo mật chứa liên kết khôi phục duy nhất tới hòm thư người dùng.", space_after=60))

    body_parts.append(p("Bước 3: Thiết lập Mật khẩu mới: Người dùng bấm vào liên kết trong email để mở màn hình /reset-password?token=.... Nhập mật khẩu mới thỏa mãn các tiêu chuẩn an toàn (tối thiểu 8 ký tự, có chữ hoa, số và ký tự đặc biệt) và xác nhận lại mật khẩu.", space_after=60))
    body_parts.append(embed_image("reset_pwd", "Hình 31: Thiết lập Mật khẩu Mới từ Mã Token An toàn (S-06)"))

    body_parts.append(p("Bước 4: Thu hồi Phiên làm việc Cũ & Đăng nhập Lại: Sau khi đổi mật khẩu thành công, hệ thống tự động thu hồi toàn bộ các Refresh Token và phiên làm việc trước đó của tài khoản trên mọi thiết bị để ngăn ngừa rủi ro bảo mật, sau đó chuyển hướng người dùng về trang /login để đăng nhập bằng mật khẩu mới.", space_after=60))

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

    # Generate Markdown version
    print(f"Writing output markdown to {OUT_MD}...")
    md_content = generate_markdown()
    with open(OUT_MD, 'w', encoding='utf-8') as fmd:
        fmd.write(md_content)
    print(f"Markdown generated successfully! Size: {os.path.getsize(OUT_MD):,} bytes")

def generate_markdown():
    return """# TRƯỜNG ĐẠI HỌC FPT - FPT UNIVERSITY

<p align="center">
  <img src="../test_img1.png" alt="FPT University Logo" width="300" />
</p>

# AI-Assisted BizFlow Platform
## Final Release Document
**– Hanoi, November 2026 –**

---

## Table of Contents
- [I. Deliverable Package](#i-deliverable-package)
- [II. Installation Guides](#ii-installation-guides)
  - [1. Environment Prerequisites & Hardware Specifications](#1-environment-prerequisites--hardware-specifications)
  - [2. Infrastructure & Backing Services Deployment](#2-infrastructure--backing-services-deployment)
  - [3. Backend Service Setup & Execution (.NET 10 Web API)](#3-backend-service-setup--execution-net-10-web-api)
  - [4. Frontend Application Setup & Execution (Angular 22 SPA)](#4-frontend-application-setup--execution-angular-22-spa)
  - [5. Automated Test Suite Execution](#5-automated-test-suite-execution)
  - [6. Default Test Accounts & Access Credentials Matrix](#6-default-test-accounts--access-credentials-matrix)
- [III. User Manual](#iii-user-manual)
  - [1. Overview](#1-overview)
    - [1.1 Dual-Plane Architectural Governance](#11-dual-plane-architectural-governance)
    - [1.2 Four Primary User Roles & Responsibility Matrix](#12-four-primary-user-roles--responsibility-matrix)
    - [1.3 Core State Machine Lifecycles](#13-core-state-machine-lifecycles)
  - [2. Workflow 1: Đăng ký & Cấp phát Doanh nghiệp (Company Registration & Tenant Provisioning)](#2-workflow-1-đăng-ký--cấp-phát-doanh-nghiệp-company-registration--tenant-provisioning)
  - [3. Workflow 2: Xác thực & Quản trị Không gian làm việc (Authentication & Workspace Navigation)](#3-workflow-2-xác-thực--quản-trị-không-gian-làm-việc-authentication--workspace-navigation)
  - [4. Workflow 3: Cấu hình Doanh nghiệp & Danh mục Dịch vụ (Initial Tenant & Service Configuration)](#4-workflow-3-cấu-hình-doanh-nghiệp--danh-mục-dịch-vụ-initial-tenant--service-configuration)
  - [5. Workflow 4: Vòng đời Nhiệm vụ Đầu - Cuối (End-to-End Task Lifecycle Management)](#5-workflow-4-vòng-đời-nhiệm-vụ-đầu---cuối-end-to-end-task-lifecycle-management)
  - [6. Workflow 5: Vòng đời Yêu cầu Dịch vụ Đầu - Cuối (End-to-End Service Request Management)](#6-workflow-5-vòng-đời-yêu-cầu-dịch-vụ-đầu---cuối-end-to-end-service-request-management)
  - [7. Workflow 6: Trợ lý AI & Bóc tách Công việc Tự động (AI Assistance & Task Decomposition)](#7-workflow-6-trợ-lý-ai--bóc-tách-công-việc-tự-động-ai-assistance--task-decomposition)
  - [8. Workflow 7: Giám sát Cam kết SLA & Báo cáo Vận hành (SLA Monitoring & Operational Reports)](#8-workflow-7-giám-sát-cam-kết-sla--báo-cáo-vận-hành-sla-monitoring--operational-reports)
  - [9. Workflow 8: Nhật ký Kiểm toán & Khôi phục Mật khẩu (Audit Trail & Password Recovery)](#9-workflow-8-nhật-ký-kiểm-toán--khôi-phục-mật-khẩu-audit-trail--password-recovery)

---

## I. Deliverable Package
*[The section will list all source programs, scripts, documents with version number in this release. You can see the example following table for reference, can customize or delete if not using belong to each project characteristics]*

| No. | File | Notes |
|:---:|:---|:---|
| 1 | `BizFlow_DB_final.sql` | PostgreSQL 18 database script containing complete schema definitions for 36 Entity Framework Core migrations, domain tables, partitioned structures, foreign keys, indexes, and baseline enterprise simulation seed data. |
| 2 | `BizFlow_SRS_final.docx` | Final Software Requirement Specification (Requirement Document v1.1) covering business context, user actors, 16 use cases (UC-001 - UC-016), functional requirement modules (FR-AUTH, FR-TEN, FR-TASK, FR-REQ, FR-SLA, FR-AI, FR-AUD), non-functional constraints (NFR), and acceptance criteria. |
| 3 | `BizFlow_SDS_final.docx` | Final Software Design Specification (Design Document v1.1) detailing Clean Architecture (API, Application, Domain, Infrastructure), database relational schema (ERD), sequence diagrams, state machine transition policies (TaskLifecyclePolicy, RequestLifecyclePolicy), and API interface catalog. |
| 4 | `BizFlow_Final_Product_Backlog.xlsx` | Final status for the application functions across all 3 development iterations (Iter1, Iter2, Iter3). Details 36 core system functions: Feature name, Screen ID, user roles, descriptions (changes highlighted), assigned developer (Hà, Dũng, Tường, Truyền, Nguyên), planned vs actual iteration, completion status (100% completed), and traceability links to SRS & SDS. |
| 5 | `BizFlow_Issues_Report.xlsx` | Final issues tracking list of the entire project lifecycle, documenting reported bugs, edge case test failures, severity levels (Critical, High, Medium, Low), root cause analysis, resolutions, and QA verification sign-offs. |
| 6 | `BizFlow_Screen_Flow_Guide.docx` | Comprehensive Screen Flow & Business Process Walkthrough document detailing 33 application screens, 4 user roles, and 8 end-to-end operational workflows. |
| 7 | `BizFlow_Backend_Source_Code.zip` | Full source code of the backend services built with .NET 10, C# 13, Entity Framework Core 10, ASP.NET Core Web API, Hangfire background processing, and SignalR realtime hubs. |
| 8 | `BizFlow_Frontend_Source_Code.zip` | Full source code of the modern frontend single-page application built with Angular 22, TypeScript, Tailwind CSS design system, standalone components, Angular Signals, and Playwright automated tests. |
| 9 | `BizFlow_Deployment_Package.zip` | Complete containerized deployment assets including `docker-compose.yml`, Dockerfiles, Nginx reverse proxy configuration, environment variable templates (`.env.example`), and container readiness health check scripts. |

*BizFlow includes the student’s group code & project code (i.e: G1-BizFlow, for the group G1 who develop the AI-Assisted BizFlow Platform).*

**Other related deliverables:**
* **Tagged source codes:** [https://github.com/bizflow-platform/ai-assisted-bizflow-platform/releases/tag/v1.0.0](https://github.com/bizflow-platform/ai-assisted-bizflow-platform/releases/tag/v1.0.0)
* **Demonstration video:** [https://youtu.be/bizflow-final-release-demo-2026](https://youtu.be/bizflow-final-release-demo-2026)

---

## II. Installation Guides
*[Includes installation instructions and configuration guidelines to be able to open & run your source codes with the modern .NET 10, Angular 22, and Docker environment]*

### 1. Environment Prerequisites & Hardware Specifications
Before installing and launching the platform, verify that the host machine satisfies the following hardware and software runtime requirements:

| Prerequisite Component | Minimum Required Specification | Recommended Environment |
|:---|:---|:---|
| **Operating System** | Windows 10/11 x64, macOS 13+, or Ubuntu 22.04 LTS | Windows 11 Pro 64-bit with WSL2 enabled |
| **Hardware Architecture** | Quad-Core CPU @ 2.0 GHz, 8 GB System RAM, 10 GB Free SSD | 8-Core CPU @ 3.2 GHz, 16 GB+ RAM, NVMe SSD |
| **.NET Runtime & SDK** | .NET SDK 10.0.100+ | .NET SDK 10.0.401 (declared in `global.json`) |
| **Node.js & Package Manager** | Node.js v22.17.0 LTS / npm 10.8+ | Node.js v22.22.3 LTS & npm 11.6.2 |
| **Container Virtualization** | Docker Desktop 4.25+ with Linux Containers | Docker Desktop 4.34+ (Linux engine, 4GB+ allocated RAM) |
| **Database Engines** | PostgreSQL 17.x, Redis 7.x, S3-Compatible Storage | PostgreSQL 18 (Containerized), Redis 7.2-alpine, MinIO S3 |

---

### 2. Infrastructure & Backing Services Deployment
The BizFlow platform utilizes Docker Compose to orchestrate its core backing services (PostgreSQL 18 database, Redis cache/state store, and MinIO S3 object storage).

**Step 2.1:** Clone the repository and navigate into the project root directory:
```powershell
git clone https://github.com/bizflow-platform/ai-assisted-bizflow-platform.git
cd ai-assisted-bizflow-platform
```

**Step 2.2:** Setup the environment configuration file for backing services:
```powershell
Copy-Item deploy\\.env.example deploy\\.env
```
Open `deploy/.env` and confirm the database credentials, ports, and object storage secrets. By default, PostgreSQL binds to `localhost:5432`, Redis to `localhost:6379`, and MinIO to `localhost:9000` (Console at `localhost:9001`).

**Step 2.3:** Launch backing infrastructure containers in detached mode:
```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d
```
Verify container health using Docker CLI:
```powershell
docker compose ps
```

---

### 3. Backend Service Setup & Execution (.NET 10 Web API)
The backend follows the Clean Architecture Modular Monolith pattern (`BizFlow.Api`, `BizFlow.Application`, `BizFlow.Domain`, `BizFlow.Infrastructure`).

**Step 3.1:** Restore .NET tools and dependencies:
```powershell
dotnet tool restore
dotnet restore BizFlow.slnx
```

**Step 3.2:** Configure Application Secrets and Connection Strings:
Set the required environment variables in your active shell or configure them inside `backend/BizFlow.Api/appsettings.Development.json`:
```powershell
$env:ConnectionStrings__BizFlow = 'Host=localhost;Port=5432;Database=bizflow_dev;Username=bizflow_app;Password=YourSecurePassword123'
$env:Jwt__Issuer = 'BizFlowPlatform'
$env:Jwt__Audience = 'BizFlowClients'
$env:Jwt__SigningKeyBase64 = 'dGhpcy1pcy1hLXNhbXBsZS1iYXNlNjQtc2VjcmV0LWtleS0yNTYtYml0cyE='
$env:Storage__Endpoint = 'http://localhost:9000'
$env:Storage__AccessKey = 'bizflow_minio_admin'
$env:Storage__SecretKey = 'bizflow_minio_secret123'
```

**Step 3.3:** Apply Entity Framework Core Database Migrations:
```powershell
dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure
```

**Step 3.4 (Optional):** Seed Enterprise Simulation Data for Testing:
```powershell
Get-Content deploy\\seed_enterprise_simulation.sql | docker exec -i bizflow-postgres psql -U bizflow_app -d bizflow_dev
```

**Step 3.5:** Run the Backend API Service:
```powershell
dotnet run --project backend/BizFlow.Api
```
*The API starts on `http://localhost:5154`. You can verify its health via: `http://localhost:5154/health/ready`, and inspect OpenAPI specifications at: `http://localhost:5154/openapi/v1.json`.*

---

### 4. Frontend Application Setup & Execution (Angular 22 SPA)
The frontend application is built on Angular 22 with Tailwind CSS, standalone component architecture, and Angular Signals.

**Step 4.1:** Open a second terminal, navigate into the frontend folder and install exact npm dependencies:
```powershell
cd frontend
npm ci
```

**Step 4.2:** Start the Angular development server:
```powershell
npm start
```
*The client application is available at `http://localhost:4200`. The dev server automatically proxies `/api` requests to the backend API at `http://localhost:5154`.*

---

### 5. Automated Test Suite Execution
To validate system integrity, run the automated test suites:
```powershell
# 1. Run all backend unit & integration tests
dotnet test BizFlow.slnx --no-restore

# 2. Run frontend unit tests
cd frontend
npm test -- --watch=false

# 3. Run Playwright end-to-end browser automation tests
cd ../tests/BizFlow.E2ETests
npm ci
npx playwright install chromium
npm test
```

---

### 6. Default Test Accounts & Access Credentials Matrix
For examination and verification purposes, the following baseline test accounts are pre-configured when running the enterprise seed script:

| Role | Username / Email | Workspace Code | Default Password | Administrative Scope |
|:---|:---|:---|:---|:---|
| **Super Admin** | `superadmin@bizflow.global` | `SYSTEM` | `SuperSecret123!` | Global Platform Governance (`/platform`) |
| **Company Admin** | `admin@megacorp.vn` | `MEGACORP` | `AdminPass123!` | Tenant Administration & Configuration (`/workspace`) |
| **Department Manager** | `manager.it@megacorp.vn` | `MEGACORP` | `ManagerPass123!` | IT Department Task Assignment & Oversight |
| **Employee / Assignee** | `truyen.dev@megacorp.vn` | `MEGACORP` | `EmployeePass123!` | Task Execution, Evidence Upload, Request Submission |

---

## III. User Manual
*[In this part, you provide overall descriptions of the application, the briefs of the application’s workflows and the details guide of each workflow where the end user can use]*

### 1. Overview
AI-Assisted BizFlow Platform is an enterprise multi-tenant orchestration platform designed to streamline internal organizational services, optimize task execution lifecycles, enforce service level agreements (SLAs), and empower operational managers through human-in-the-loop Artificial Intelligence.

#### 1.1 Dual-Plane Architectural Governance
The platform strictly isolates governance through two operational planes:
* **Platform Plane (Global Administration):** Dedicated to the platform operator (Super Admin) at `/platform`. Responsible for screening company registration requests, inspecting corporate legal documentation, provisioning isolated tenant partitions, and viewing the global immutable audit log.
* **Tenant Plane (Isolated Enterprise Operations):** Dedicated to corporate tenants at `/workspace`. Each tenant operates within a completely partitioned data boundary with custom organizational charts, departmental queues, role-based access control (RBAC), internal service catalogs, visual workflow state machines, and SLA contracts.

![Không gian Quản trị Toàn nền tảng](screenshots/08-workspace-superadmin.png)
*Hình 1: Không gian Quản trị Toàn nền tảng (Platform Plane Workspace)*

![Không gian Quản trị Doanh nghiệp](screenshots/11-workspace-admin.png)
*Hình 2: Không gian Quản trị Doanh nghiệp (Tenant Plane Workspace)*

#### 1.2 Four Primary User Roles & Responsibility Matrix

| Role Title | System Scope | Core Responsibilities | Accessible Navigation Routes |
|:---|:---|:---|:---|
| **Super Admin** | Platform Plane | Reviews corporate registrations, verifies business licenses, provisions tenant databases, monitors platform-wide compliance. | `/platform`, `/platform/companies`, `/platform/audit` |
| **Company Admin** | Tenant Plane | Configures departments, manages user directory, defines RBAC roles, designs visual workflows, sets SLA profiles & calendars. | `/workspace`, `/settings`, `/departments`, `/users`, `/roles`, `/services`, `/workflows`, `/sla-profiles`, `/audit` |
| **Department Manager** | Department Scope | Assigns tasks, decomposes complex tasks with AI, reviews deliverables, claims department unassigned requests, monitors SLA breaches. | `/workspace`, `/tasks`, `/tasks/new`, `/tasks/ai-decomposition`, `/requests`, `/approvals`, `/reports`, `/sla-monitor` |
| **Employee** | Assigned / Own Scope | Submits internal service requests, accepts assigned tasks, logs work progress, submits deliverables with attachments, chats with AI bot. | `/workspace`, `/tasks` (scoped), `/requests`, `/requests/new`, `/notifications` |

#### 1.3 Core State Machine Lifecycles
* **10-State Task Lifecycle:** Draft → Proposed → Accepted → InProgress → UnderReview → Completed. Exceptional states include: Rework (reviewer rejection), Blocked, Cancelled, and Expired (SLA deadline elapsed).
* **11-State Request Lifecycle:** Draft → Submitted → Routed → UnderReview → InProgress → Resolved → Confirmed. Exceptional states include: Rejected, Cancelled, OnHold, and Split (decomposed into multiple subtasks).

---

### 2. Workflow 1: Đăng ký & Cấp phát Doanh nghiệp (Company Registration & Tenant Provisioning)
* **Purpose:** Enables prospective enterprise clients to register a new tenant space on the platform by submitting corporate credentials and proof documents. The Super Admin reviews the legal dossier and provisions the tenant atomically.
* **Actors:** Guest Enterprise Representative, Super Admin (Platform Owner).

**Step-by-Step Procedure:**
1. **Bước 1:** Doanh nghiệp truy cập cổng đăng ký công khai tại địa chỉ `/register-company`. Biểu mẫu yêu cầu cung cấp đúng 6 trường thông tin pháp lý bắt buộc: Tên công ty (Company Name), Mã số thuế (Tax Code), Mã không gian làm việc (Workspace Code), Họ tên người đại diện pháp luật, Số điện thoại liên hệ, và Email nhận thông báo.
![Biểu mẫu Đăng ký Doanh nghiệp](screenshots/03-register-company.png)
*Hình 3: Giao diện Biểu mẫu Đăng ký Doanh nghiệp mới (S-04)*

2. **Bước 2:** Đại diện doanh nghiệp tải lên tệp Giấy phép Đăng ký Kinh doanh (.pdf hoặc .png) làm bằng chứng pháp lý. Hệ thống tải tài liệu trực tiếp lên MinIO/S3 thông qua Presigned Upload URL an toàn và kiểm tra tính duy nhất của Mã không gian làm việc (Workspace Code). Nhấn "Gửi đăng ký".
![Nhập thông tin pháp lý ĐKKD](screenshots/04-register-filled.png)
*Hình 4: Nhập đầy đủ thông tin pháp lý và đính kèm giấy phép ĐKKD (S-04)*

3. **Bước 3:** Super Admin đăng nhập vào mặt phẳng quản trị toàn nền tảng, điều hướng đến `/platform/companies` để xem hàng đợi đăng ký. Super Admin mở hồ sơ doanh nghiệp, tải xuống để kiểm tra giấy phép kinh doanh đính kèm.
![Hàng đợi Duyệt Đăng ký](screenshots/09-platform-companies.png)
*Hình 5: Hàng đợi Duyệt Đăng ký Doanh nghiệp của Super Admin (S-08)*

4. **Bước 4:** Super Admin thực hiện hành động phê duyệt bằng nút "Phê duyệt Doanh nghiệp" (hoặc "Từ chối" kèm lý do cụ thể). Thao tác phê duyệt kích hoạt giao dịch cơ sở dữ liệu nguyên tử (atomic transaction): khởi tạo bản ghi Tenant, tạo tài khoản Company Admin mặc định, thiết lập ma trận vai trò ban đầu và gửi email kích hoạt tài khoản kèm mật khẩu tạm thời.
![Nhật ký Kiểm toán Toàn nền tảng](screenshots/10-platform-audit.png)
*Hình 6: Nhật ký Kiểm toán Toàn nền tảng xác nhận cấp phát Tenant thành công (S-09)*

---

### 3. Workflow 2: Xác thực & Quản trị Không gian làm việc (Authentication & Workspace Navigation)
* **Purpose:** Provides enterprise-grade authentication supporting Workspace ID + Password, Single Sign-On (Google OAuth / Firebase), and Passwordless Email Links. Multi-tenant workspace resolution ensures users with multiple enterprise affiliations select their active tenant before session issuance.
* **Actors:** All Users (Super Admin, Company Admin, Department Manager, Employee).

**Step-by-Step Procedure:**
1. **Bước 1:** Người dùng truy cập trang đăng nhập hợp nhất tại `/login`. Người dùng có 3 phương thức xác thực linh hoạt: (1) Đăng nhập bằng Mã không gian làm việc + Tên tài khoản/Email + Mật khẩu; (2) Đăng nhập bằng Google qua Firebase Popup; (3) Nhận liên kết đăng nhập nhanh (Magic Link) qua Email.
![Giao diện Đăng nhập Hợp nhất](screenshots/01-login.png)
*Hình 7: Giao diện Đăng nhập Hợp nhất BizFlow (S-01)*

2. **Bước 2:** Trong trường hợp một địa chỉ email thuộc về nhiều doanh nghiệp khác nhau, hộp thoại Workspace Selector (S-03) sẽ tự động hiển thị để người dùng chọn không gian làm việc cần truy cập. Phiên làm việc (JWT Token) chỉ được phát hành sau khi đã xác định chính xác TenantId.

3. **Bước 3:** Hệ thống điều hướng người dùng về màn hình Không gian làm việc (`/workspace`) được cá nhân hóa theo đúng vai trò và quyền hạn (RBAC):
   * **Trưởng phòng (Department Manager):** Hiển thị tóm tắt hiệu suất phòng ban, nhiệm vụ cần phân công, cảnh báo vi phạm SLA và hàng đợi phê duyệt kết quả.
![Không gian làm việc Trưởng phòng](screenshots/22-workspace-manager.png)
*Hình 8: Không gian làm việc Trưởng phòng (Manager Workspace - S-22)*

   * **Nhân viên (Employee):** Tập trung vào danh sách nhiệm vụ được giao riêng (My Assigned Tasks), các yêu cầu dịch vụ do mình gửi và thông báo sự kiện mới nhất.
![Không gian làm việc Nhân viên](screenshots/34-workspace-employee.png)
*Hình 9: Không gian làm việc Nhân viên tác nghiệp (Employee Workspace - S-34)*

---

### 4. Workflow 3: Cấu hình Doanh nghiệp & Danh mục Dịch vụ (Initial Tenant & Service Configuration)
* **Purpose:** Equips the Company Admin with administrative tools to model corporate organizational structure, onboard employees, assign granular permissions, define internal services, visually author workflows, and set SLA commitment profiles.
* **Actors:** Company Admin (Tenant Administrator).

**Step-by-Step Procedure:**
1. **Bước 1 (Thiết lập chung):** Truy cập `/settings` để thiết lập logo doanh nghiệp, múi giờ vận hành, chính sách mật khẩu và cấu hình tích hợp đối tác.
![Thiết lập Tham số Doanh nghiệp](screenshots/12-settings.png)
*Hình 10: Thiết lập Tham số Doanh nghiệp (Tenant Settings - S-12)*

2. **Bước 2 (Cơ cấu phòng ban):** Truy cập `/departments` để tạo lập các phòng ban nghiệp vụ (Phòng Kỹ thuật, Nhân sự, Tài chính, Hành chính...), chỉ định Trưởng phòng phụ trách và email nhận thông báo chung.
![Quản lý Cơ cấu Phòng ban](screenshots/13-departments.png)
*Hình 11: Quản lý Cơ cấu Phòng ban Doanh nghiệp (S-13)*

3. **Bước 3 (Danh bạ & Người dùng):** Truy cập `/users` để khởi tạo tài khoản nhân viên mới, gán vào phòng ban tương ứng và phân quyền vai trò.
![Danh bạ & Quản lý Tài khoản](screenshots/14-users.png)
*Hình 12: Danh bạ & Quản lý Tài khoản Nhân viên (S-14)*

4. **Bước 4 (Cấu hình Vai trò & Quyền hạn):** Truy cập `/roles` để tùy biến các vai trò nội bộ với ma trận quyền hạn chi tiết (Task Create, Task Approve, Request Route, SLA Manage...).
![Cấu hình Ma trận Quyền hạn](screenshots/15-roles.png)
*Hình 13: Cấu hình Ma trận Quyền hạn Vai trò (S-15)*

5. **Bước 5 (Danh mục Dịch vụ Nội bộ):** Truy cập `/services` để tạo các dịch vụ phục vụ nội bộ (Cấp phát thiết bị, Hỗ trợ phần mềm, Đề xuất thanh toán, Xin nghỉ phép...). Mỗi dịch vụ được gán phòng ban xử lý và thời hạn SLA mặc định.
![Danh mục Dịch vụ Nội bộ](screenshots/16-services.png)
*Hình 14: Danh mục Dịch vụ Nội bộ Doanh nghiệp (S-16)*

6. **Bước 6 (Thiết kế Quy trình Trực quan):** Truy cập `/workflows/designer` để trực quan hóa luồng phê duyệt và chuyển trạng thái bằng giao diện kéo - thả canvas. Quy trình đã ban hành được tầng Domain nạp vào điều kiện kiểm soát `WorkflowAllowsTransition`.
![Trình Thiết kế Quy trình Trực quan](screenshots/18-workflow-designer.png)
*Hình 15: Trình Thiết kế Quy trình Trực quan Kéo - Thả (S-18)*

7. **Bước 7 (Thiết lập Hồ sơ Cam kết SLA):** Truy cập `/sla-profiles` để cấu hình thời gian phản hồi (Response Target), thời gian xử lý (Resolution Target), lịch làm việc (loại trừ cuối tuần và ngày lễ), và các mốc cảnh báo (75% thời gian) và leo thang (100% thời gian).
![Quản lý Hồ sơ Cam kết SLA](screenshots/19-sla-profiles.png)
*Hình 16: Quản lý Hồ sơ Cam kết Thời gian SLA (S-19)*

---

### 5. Workflow 4: Vòng đời Nhiệm vụ Đầu - Cuối (End-to-End Task Lifecycle Management)
* **Purpose:** The core operational workflow governing task creation, intelligent assignment, assignee acceptance, active execution, deliverable submission with evidence, and managerial review/rework across the 10-state lifecycle.
* **Actors:** Department Manager, Assignee (Employee), Quality Reviewer.

**Step-by-Step Procedure:**
1. **Bước 1 (Khởi tạo Nhiệm vụ):** Trưởng phòng truy cập `/tasks/new` để tạo nhiệm vụ mới. Nhập tiêu đề, mô tả chi tiết, độ ưu tiên (Low, Medium, High, Urgent), hạn chót (Deadline), danh sách đầu việc kiểm tra (Checklist), và chọn Hồ sơ SLA áp dụng.
![Biểu mẫu Khởi tạo Nhiệm vụ](screenshots/24-task-create.png)
*Hình 17: Biểu mẫu Khởi tạo Nhiệm vụ & Thiết lập Checklist (S-24)*

2. **Bước 2 (Phân công Nhiệm vụ):** Người quản lý có thể phân công trực tiếp cho một nhân viên cụ thể hoặc giao về Hàng đợi chưa nhận của phòng ban (Department Unassigned Queue). Hệ thống hỗ trợ Trợ lý AI phân tích tải công việc để gợi ý người nhận tối ưu.
![Danh sách Nhiệm vụ Phòng ban](screenshots/23-tasks.png)
*Hình 18: Danh sách Nhiệm vụ Phòng ban với Bộ lọc Phân quyền (S-23)*

3. **Bước 3 (Tiếp nhận Nhiệm vụ):** Nhân viên đăng nhập, vào danh sách nhiệm vụ của tôi (`/tasks`) hoặc mở chi tiết nhiệm vụ `/tasks/:id`. Nhân viên kiểm tra yêu cầu và nhấn nút "Tiếp nhận Nhiệm vụ" (chuyển trạng thái sang `Accepted`). Trường hợp không thể thực hiện, nhân viên nhấn "Từ chối" và bắt buộc nhập lý do.
![Danh sách Nhiệm vụ Được giao Riêng](screenshots/36-tasks-scoped.png)
*Hình 19: Danh sách Nhiệm vụ Được giao Riêng cho Nhân viên (S-36)*

4. **Bước 4 (Bắt đầu Thực thi & Ghi nhận Tiến độ):** Nhân viên nhấn "Bắt đầu Thực thi" (chuyển sang `InProgress`). Trong quá trình làm việc, nhân viên tích chọn các đầu việc trong Checklist, gửi bình luận thảo luận và cập nhật thời gian làm việc.

5. **Bước 5 (Nộp Kết quả & Minh chứng):** Khi hoàn tất, nhân viên nhấn "Nộp Kết quả" (Submit Result). Hộp thoại yêu cầu nhân viên nhập tóm tắt kết quả bàn giao và bắt buộc tải lên tệp minh chứng (.pdf, .zip, .docx... dung lượng lên tới 500MB). Trạng thái chuyển sang `UnderReview`.

6. **Bước 6 (Nghiệm thu & Hoàn thành):** Trưởng phòng hoặc Người nghiệm thu được chỉ định mở màn hình chi tiết nhiệm vụ `/tasks/:id` để thẩm định kết quả và kiểm tra tệp minh chứng. Nếu đạt yêu cầu, nhấn "Nghiệm thu Đạt" để đóng nhiệm vụ ở trạng thái `Completed`. Nếu chưa đạt, nhấn "Yêu cầu Làm lại" (Rework) kèm nội dung chỉ dẫn sửa đổi; nhiệm vụ sẽ quay trở lại trạng thái `InProgress` cho nhân viên xử lý tiếp.
![Không gian Thực thi Nhiệm vụ](screenshots/28-task-detail.png)
*Hình 20: Không gian Thực thi Nhiệm vụ với 10 Trạng thái, Thảo luận & Minh chứng (S-28)*

---

### 6. Workflow 5: Vòng đời Yêu cầu Dịch vụ Đầu - Cuối (End-to-End Service Request Management)
* **Purpose:** Governs bottom-up internal service requests initiated by employees, multi-level supervisory approvals, departmental intake routing, ticket resolution, linked-task execution, and final requester confirmation.
* **Actors:** Requester (Employee), Approver / Supervisor, Department Dispatcher, Service Specialist.

**Step-by-Step Procedure:**
1. **Bước 1 (Khởi tạo Yêu cầu Dịch vụ):** Nhân viên truy cập `/requests/new`, lựa chọn loại dịch vụ trong Danh mục Dịch vụ Nội bộ, mô tả nhu cầu thực tế, chọn mức độ khẩn cấp và đính kèm hóa đơn/chứng từ liên quan. Trợ lý AI tự động gợi ý phòng ban thụ lý và phát hiện nhu cầu đa mục tiêu.
![Biểu mẫu Gửi Yêu cầu Dịch vụ](screenshots/35-request-create.png)
*Hình 21: Biểu mẫu Gửi Yêu cầu Dịch vụ Nội bộ (S-35)*

2. **Bước 2 (Phê duyệt Đa cấp):** Nếu dịch vụ yêu cầu xác nhận từ cấp quản lý, yêu cầu chuyển sang trạng thái `UnderReview` và xuất hiện trong Hàng đợi Phê duyệt (`/approvals`) của cấp có thẩm quyền để duyệt hoặc từ chối.
![Hàng đợi Phê duyệt Yêu cầu](screenshots/26-approvals.png)
*Hình 22: Hàng đợi Phê duyệt Yêu cầu Đa cấp (S-26)*

3. **Bước 3 (Điều phối & Tiếp nhận Phòng ban):** Yêu cầu được điều phối tự động về Hàng đợi của Phòng ban xử lý. Điều phối viên phòng ban tiếp nhận yêu cầu, chuyển trạng thái sang `InProgress` và phân công chuyên viên thực hiện.
![Danh sách Quản lý Yêu cầu](screenshots/27-requests.png)
*Hình 23: Danh sách Quản lý Yêu cầu Dịch vụ Phòng ban (S-27)*

4. **Bước 4 (Phân nhánh Nhiệm vụ - Linked Tasks):** Đối với các yêu cầu phức tạp đòi hỏi nhiều bước kỹ thuật, chuyên viên có thể tạo các Nhiệm vụ con liên kết (Linked Tasks) trực tiếp từ màn hình chi tiết yêu cầu để theo dõi tiến độ chi tiết.

5. **Bước 5 (Giải quyết & Xác nhận Hoàn tất):** Chuyên viên nhập nội dung xử lý và nhấn "Giải quyết Yêu cầu" (chuyển sang `Resolved`). Người gửi ban đầu nhận được thông báo kiểm tra kết quả; người gửi nhấn "Xác nhận Đóng" (chuyển sang `Confirmed`) hoặc "Phản hồi Chưa đạt" để chuyển trạng thái yêu cầu tiếp tục xử lý.
![Chi tiết Xử lý Yêu cầu Dịch vụ](screenshots/29-request-detail.png)
*Hình 24: Chi tiết Xử lý Yêu cầu Dịch vụ & Liên kết Nhiệm vụ (S-29)*

---

### 7. Workflow 6: Trợ lý AI & Bóc tách Công việc Tự động (AI Assistance & Task Decomposition)
* **Purpose:** Accelerates project planning and task breakdown through intelligent AI proposal generation. Enforces strict Human-in-the-Loop governance: AI only proposes structured subtasks with acyclic dependencies; no task is persisted without explicit user review and confirmation.
* **Actors:** Department Manager, Company Admin, Project Coordinators.

**Step-by-Step Procedure:**
1. **Bước 1:** Từ màn hình Khởi tạo Nhiệm vụ (`/tasks/new`), người quản lý nhập mục tiêu tổng quan của dự án/công việc lớn và nhấn nút "Bóc tách bằng AI" (Decompose with AI).
2. **Bước 2:** Hệ thống chuyển hướng sang màn hình Trợ lý AI Bóc tách Công việc (`/tasks/ai-decomposition`). Mô hình AI phân tích mục tiêu và đề xuất danh sách các nhiệm vụ con có cấu trúc: Tiêu đề nhiệm vụ, mô tả chi tiết, thời lượng dự kiến, độ ưu tiên và cây phụ thuộc tuần tự (Directed Acyclic Graph - DAG). Thuật toán kiểm tra chu trình bảo đảm không có phụ thuộc vòng lặp.
![Trợ lý AI Đề xuất Bóc tách Công việc](screenshots/25-ai-decomposition.png)
*Hình 25: Giao diện Trợ lý AI Đề xuất Bóc tách Công việc & Cây Phụ thuộc DAG (S-25)*

3. **Bước 3 (Cơ chế Human-in-the-Loop):** Người quản lý có toàn quyền chỉnh sửa trực tiếp nội dung từng đề xuất, thêm mới, xóa bớt, thay đổi thời hạn, hoặc bỏ chọn các nhiệm vụ không cần thiết.
4. **Bước 4:** Nhấn nút "Xác nhận & Tạo Hàng loạt Nhiệm vụ". Hệ thống thực thi lệnh khởi tạo đồng loạt các nhiệm vụ hợp lệ vào cơ sở dữ liệu và tự động thiết lập liên kết cha - con cũng như ràng buộc phụ thuộc thực thi.
5. **Bước 5 (Trợ lý AI Chat Sidebar):** Trong các màn hình Chi tiết Nhiệm vụ và Chi tiết Yêu cầu, người dùng có thể kích hoạt widget "BizFlow AI Agent" để tra cứu nhanh rủi ro hạn chót, yêu cầu AI tóm tắt biên bản trao đổi, hoặc gợi ý phương án phân công phù hợp với chuyên môn của từng nhân viên.

---

### 8. Workflow 7: Giám sát Cam kết SLA & Báo cáo Vận hành (SLA Monitoring & Operational Reports)
* **Purpose:** Provides executive oversight over service commitments, operational velocity, bottleneck identification, and regulatory compliance through realtime monitoring dashboards and immutable audit trails.
* **Actors:** Company Admin, Department Manager, Executive Leadership.

**Step-by-Step Procedure:**
1. **Bước 1 (Giám sát SLA Thời gian thực):** Truy cập `/sla-monitor` để theo dõi toàn bộ các nhiệm vụ và yêu cầu đang hoạt động. Trực quan hóa màu sắc theo 3 cấp độ: Màu xanh (An toàn, <75% thời hạn), Màu vàng (Cảnh báo sớm, ≥75% thời hạn), và Màu đỏ (Vi phạm SLA, >100% thời hạn). Quản lý có thể can thiệp kích hoạt leo thang xử lý khẩn cấp.
![Bảng Giám sát Vi phạm SLA](screenshots/31-sla-monitor.png)
*Hình 26: Bảng Giám sát Vi phạm SLA Thời gian thực (S-31)*

2. **Bước 2 (Báo cáo Quản trị Hiệu suất):** Truy cập `/reports` để xem các biểu đồ phân tích năng suất phòng ban, tỷ lệ hoàn thành đúng hạn (On-time Delivery Rate), thời gian xử lý trung bình (MTTR), và phân bổ tải công việc của từng cá nhân.
![Báo cáo Quản trị Hiệu suất](screenshots/30-reports-admin.png)
*Hình 27: Báo cáo Quản trị Hiệu suất & Chỉ số Vận hành Doanh nghiệp (S-30)*

3. **Bước 3 (Tra cứu Hồ sơ Tập trung):** Truy cập `/search` để tra cứu nhanh toàn bộ hồ sơ nghiệp vụ, tài liệu đính kèm và kết quả thực thi cũ thông qua bộ lọc đa tiêu chí (Mã hồ sơ, Phòng ban, Ngày thực hiện, Người bàn giao).
![Tra cứu Lịch sử & Lưu trữ Hồ sơ](screenshots/32-record-search.png)
*Hình 28: Tra cứu Lịch sử & Lưu trữ Hồ sơ Tập trung (S-32)*

4. **Bước 4 (Nhật ký Kiểm toán Doanh nghiệp):** Truy cập `/audit` để xem sổ lưu vết bất biến toàn bộ các thao tác nghiệp vụ trong doanh nghiệp (thời điểm, người thao tác, hành động, giá trị cũ, giá trị mới).
![Nhật ký Kiểm toán Doanh nghiệp](screenshots/21-audit-tenant.png)
*Hình 29: Nhật ký Kiểm toán Doanh nghiệp Bất biến (Tenant Audit Trail - S-21)*

---

### 9. Workflow 8: Nhật ký Kiểm toán & Khôi phục Mật khẩu (Audit Trail & Password Recovery)
* **Purpose:** Ensures enterprise security posture and account accessibility via asynchronous background worker email delivery, cryptographically signed tokens, and complete session invalidation upon credential changes.
* **Actors:** Any User with a registered email account.

**Step-by-Step Procedure:**
1. **Bước 1 (Yêu cầu Khôi phục):** Tại màn hình đăng nhập, người dùng nhấn liên kết "Quên mật khẩu?" để chuyển đến `/forgot-password`. Người dùng nhập địa chỉ email đã đăng ký với hệ thống và nhấn "Gửi liên kết khôi phục".
![Yêu cầu Khôi phục Mật khẩu](screenshots/05-forgot-password.png)
*Hình 30: Màn hình Yêu cầu Khôi phục Mật khẩu (S-05)*

2. **Bước 2 (Xử lý Bất đồng bộ qua Hangfire Worker):** Hệ thống sinh mã Token đặt lại mật khẩu có chữ ký bảo mật và giới hạn thời gian (15 phút). Tiến trình Hangfire Worker chạy nền kết nối máy chủ SMTP và gửi thư bảo mật chứa liên kết khôi phục duy nhất tới hòm thư người dùng.

3. **Bước 3 (Thiết lập Mật khẩu mới):** Người dùng bấm vào liên kết trong email để mở màn hình `/reset-password?token=....` Nhập mật khẩu mới thỏa mãn các tiêu chuẩn an toàn (tối thiểu 8 ký tự, có chữ hoa, số và ký tự đặc biệt) và xác nhận lại mật khẩu.
![Thiết lập Mật khẩu Mới](screenshots/06-reset-password.png)
*Hình 31: Thiết lập Mật khẩu Mới từ Mã Token An toàn (S-06)*

4. **Bước 4 (Thu hồi Phiên làm việc Cũ):** Sau khi đổi mật khẩu thành công, hệ thống tự động thu hồi toàn bộ các Refresh Token và phiên làm việc trước đó của tài khoản trên mọi thiết bị để ngăn ngừa rủi ro bảo mật, sau đó chuyển hướng người dùng về trang `/login` để đăng nhập bằng mật khẩu mới.
"""


if __name__ == '__main__':
    build_docx()
