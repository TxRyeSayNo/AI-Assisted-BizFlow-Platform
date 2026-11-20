# Complete English Markdown generator for BizFlow Final Release Document v1.0.0
import os

OUT_MD_EN = os.path.join("docs", "BizFlow_Final_Release_Document_EN.md")

md_content_en = """# FPT UNIVERSITY

<br/>

# <span style="color:#C00000">AI-Assisted BizFlow Platform</span>
## <span style="color:#C00000">Final Release Document</span>

<br/>

**– Hanoi, November 2026 –**

---

## Table of Contents

- [0. Release Notes v1.0.0](#0-release-notes-v100)
  - [0.1 Executive Summary](#01-executive-summary)
  - [0.2 Release Scope](#02-release-scope)
  - [0.3 Bug Fixes & Resolved Issues](#03-bug-fixes--resolved-issues)
  - [0.4 Known Issues & Limitations](#04-known-issues--limitations)
  - [0.5 Deployment & Rollback Strategy](#05-deployment--rollback-strategy)
  - [0.6 Post-Release Monitoring & Operational Metrics](#06-post-release-monitoring--operational-metrics)
  - [0.7 Sign-off & Release Approval](#07-sign-off--release-approval)
  - [0.8 Compatibility & Breaking Changes Notes](#08-compatibility--breaking-changes-notes)
- [I. Deliverable Package](#i-deliverable-package)
- [II. Installation Guides](#ii-installation-guides)
  - [1. Environment Prerequisites & Hardware Specifications](#1-environment-prerequisites--hardware-specifications)
  - [2. Configuration & Environment Variables Reference](#2-configuration--environment-variables-reference)
  - [3. Infrastructure & Backing Services Deployment](#3-infrastructure--backing-services-deployment)
  - [4. Backend Service Setup & Execution (.NET 10 Web API)](#4-backend-service-setup--execution-net-10-web-api)
  - [5. Frontend Application Setup & Execution (Angular 22 SPA)](#5-frontend-application-setup--execution-angular-22-spa)
  - [6. Automated Test Suite Execution & Quality Metrics](#6-automated-test-suite-execution--quality-metrics)
  - [7. Baseline Test Accounts & Access Credentials Matrix](#7-baseline-test-accounts--access-credentials-matrix)
- [III. User Manual](#iii-user-manual)
  - [1. Overview](#1-overview)
    - [1.1 Dual-Plane Architectural Governance](#11-dual-plane-architectural-governance)
    - [1.2 Four Primary User Roles & Responsibility Matrix](#12-four-primary-user-roles--responsibility-matrix)
    - [1.3 Core State Machine Lifecycles](#13-core-state-machine-lifecycles)
  - [2. Workflow 1: Company Registration & Tenant Provisioning](#2-workflow-1-company-registration--tenant-provisioning)
  - [3. Workflow 2: Authentication & Workspace Navigation](#3-workflow-2-authentication--workspace-navigation)
  - [4. Workflow 3: Initial Tenant & Service Configuration](#4-workflow-3-initial-tenant--service-configuration)
  - [5. Workflow 4: End-to-End Task Lifecycle Management](#5-workflow-4-end-to-end-task-lifecycle-management)
  - [6. Workflow 5: End-to-End Service Request Management](#6-workflow-5-end-to-end-service-request-management)
  - [7. Workflow 6: AI Assistance & Task Decomposition](#7-workflow-6-ai-assistance--task-decomposition)
  - [8. Workflow 7: SLA Monitoring & Operational Reports](#8-workflow-7-sla-monitoring--operational-reports)
  - [9. Workflow 8: Password Recovery & Session Invalidation](#9-workflow-8-password-recovery--session-invalidation)

---

# 0. Release Notes v1.0.0

## 0.1 Executive Summary

Release v1.0.0 (November 15, 2026) encapsulates all core functional and non-functional capabilities of the **AI-Assisted BizFlow Platform** on modern **.NET 10** and **Angular 22** architecture. The platform operates on a robust multi-tenant model with two strictly segregated governance planes (Platform Plane and Tenant Plane), encompassing 8 business workflows; Workflow 8 (password recovery) and multi-channel deadline notification dispatch carry identified operational limitations (see Section 0.4). The domain layer enforces strict state machine lifecycles: a 10-state machine for Tasks and a 12-state machine for Service Requests, integrated with an AI-powered task decomposition engine operating under mandatory human-in-the-loop oversight.

A rigorous technical audit verified **108 out of 132 requirements** (81.8% VERIFIED core, 16 PARTIAL, 6 NOT_IMPL, and 2 OUT_OF_SCOPE per `docs/requirements/srs/REQUIREMENTS_TRACEABILITY.md`). The user interface comprises exactly **33 functional screens** (S-01 through S-33) strictly aligned with `docs/BizFlow_Luong_Man_Hinh.docx`, meeting MVP operational readiness.

The project maintains an active registry of 13 known technical issues (KISS-01 through KISS-13); notably, one Critical vulnerability remains in backend authentication (KISS-09 / BKL-SEC-01), with client-side UI enforcement acting as an interim mitigation.

**Overall release readiness evaluation: CONDITIONAL GO – qualified for academic capstone defense; resolution of KISS-09 is mandatory prior to live production staging.**

---

## 0.2 Release Scope

Delivered functional scope matrix for Release v1.0.0:

| Feature / Capability | Requirement ID | Screen ID | Actual Route | Status |
| :--- | :--- | :--- | :--- | :--- |
| Public Enterprise Registration | FR-TEN-001, FR-TEN-005 | S-04 | `/register-company` | Done |
| Tenant Provisioning & Approval Queue | FR-TEN-002 | S-08 | `/platform/companies` | Done |
| Global Platform Immutable Audit Trail | FR-AUD-001 | S-09 | `/platform/audit` | Done |
| Unified Multi-Tenant Authentication | FR-AUTH-001, FR-AUTH-005 | S-01, S-02 | `/login` | Done |
| Self-Service Password Recovery | FR-AUTH-003 | S-05, S-06 | `/forgot-password`, `/reset-password` | Partial |
| Role-Tailored Operational Workspace | FR-WS-001 | S-07 | `/dashboard` | Done |
| Task Registry & Creation Flow | FR-TASK-001, FR-TASK-002 | S-10, S-11 | `/tasks`, `/tasks/new` | Done |
| AI-Assisted Task Decomposition | FR-AI-013 | S-12 | `/tasks/ai-decomposition` | Done |
| Task Detail & 10-State Lifecycle | FR-TASK-003 | S-13 | `/tasks/:id` | Done |
| Service Request Registry & Creation | FR-REQ-001, FR-REQ-002 | S-14, S-15 | `/requests`, `/requests/new` | Done |
| Service Request Detail & 12-State Lifecycle | FR-REQ-003 | S-16 | `/requests/:id` | Done |
| Multi-Level Request Approval Queue | FR-REQ-004 | S-17 | `/approvals` | Partial |
| Real-Time SLA Violation Monitoring | FR-SLA-003 | S-18 | `/sla-monitor` | Done |
| Executive Operational Analytics | FR-REP-001 | S-19 | `/reports` | Done |
| Centralized Archival & Document Retrieval | FR-ARC-001 | S-20 | `/records/search` | Done |
| Event Notification Center | FR-NOTIF-001 | S-21 | `/notifications` | Partial |
| Tenant-Level Immutable Audit Trail | FR-AUD-002 | S-22 | `/audit` | Done |
| Tenant General Parameters & Branding | FR-SET-001 | S-23 | `/settings` | Done |
| Enterprise Internal Service Catalog | FR-SVC-001 | S-24 | `/settings/services` | Done |
| Organizational Departments & Hierarchy | FR-ORG-002 | S-25 | `/settings/organization/departments` | Done |
| User Directory & Account Governance | FR-ORG-001 | S-26 | `/settings/organization/users` | Done |
| Granular RBAC Roles & Permissions | FR-SEC-001 | S-27 | `/settings/organization/roles` | Done |
| Visual Workflow Designer & Library | FR-WF-001 | S-28, S-29 | `/settings/workflows`, `/settings/workflows/:id/versions/:v` | Done |
| SLA Profiles & Version Management | FR-SLA-001, FR-SLA-002 | S-30, S-31 | `/settings/sla-profiles`, `/settings/sla-profiles/:id/versions` | Done |
| RBAC Access Denied 403 Barrier | FR-SEC-002 | S-32 | `/access-denied` | Done |
| Intelligent AI Assistant Robot Widget | FR-AI-014 | S-33 | Shared RobotWidget | Done |

---

## 0.3 Bug Fixes & Resolved Issues

Technical defects and architectural remediations resolved prior to Release v1.0.0:

| Defect ID | Defect Description | Severity | Module | Resolution Strategy | Commit / PR | Verified By |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **BKL-DB-01** | Company table lacked mandatory v1.2 legal identity fields (TaxId, ContactPhone, ContactPersonName) and legal document store. | High | Tenancy | Implemented migration `20261021100000_CompanyRegistration6FieldsAndEvidence.cs` adding 3 columns and `CompanyRegistrationDocuments` table. | `6c8066f` | [CẦN XÁC NHẬN NGƯỜI VERIFY] |
| **BKL-FNC-01** | Public registration form lacked real-time workspace code collision checks and secure MinIO upload handling. | High | Onboarding | Added real-time workspace code check endpoint with 400ms debounce and presigned MinIO upload workflow. | `6c8066f` | [CẦN XÁC NHẬN NGƯỜI VERIFY] |
| **BKL-FNC-03** | Platform Admin could not inspect uploaded business license documents during registration review. | High | Platform | Implemented secure 15-minute time-bound presigned URL retrieval endpoint for Platform Admin verification. | `6c8066f` | [CẦN XÁC NHẬN NGƯỜI VERIFY] |
| **BKL-FNC-04** | AI task decomposition automatically persisted records on preview, violating human control principles. | Medium | AI Assistant | Segregated into preview-only (transient DTO computation) and confirm (explicit user confirmation persistence) endpoints. | `c033ca2` | [CẦN XÁC NHẬN NGƯỜI VERIFY] |
| **BKL-DB-02** | Lacked unique constraints to prevent duplicate deadline notification dispatch schedules across channels. | Medium | Notifications | Created migration `20261102143000_DeadlineNotificationDispatchRecords.cs` adding unique constraint on `(TenantId, TaskId, UserId, TriggerType, ScheduledAt)`. | `6b3663b` | [CẦN XÁC NHẬN NGƯỜI VERIFY] |
| **BKL-FNC-05** | Chatbot widget open/close toggle state did not persist across navigation tabs. | Low | Frontend | Stored widget visibility state in `sessionStorage` to prevent unwanted expansion upon navigation. | `4508805` | [CẦN XÁC NHẬN NGƯỜI VERIFY] |

**Summary: 6 out of 7 major defects resolved; 1 defect (BKL-SEC-01) remains open under KISS-09.**

---

## 0.4 Known Issues & Limitations

Inventory of known issues, technical root causes, mitigations, and scheduled remediation roadmap:

| Issue ID | Description | Severity | Technical Root Cause | Workaround / Mitigation | Roadmap |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **KISS-01** | Magic Link authentication does not dispatch physical emails. | Medium | Production SMTP service not configured; magic link button hidden on primary login view. | Authenticate via Workspace Code + Username + Password or Google Sign-In. | Targeted for Sprint v1.0.1. |
| **KISS-02** | Self-service password recovery is disabled by default. | Medium | `ResetEmail__Enabled` defaults to false to prevent runtime mailer exceptions; API returns `AUTH.RESET_UNAVAILABLE`. | System Administrator configures TLS-enabled SMTP host and enables `ResetEmail__Enabled=true`. | Admin SMTP configuration UI. |
| **KISS-03** | Baseline database seed does not provision a Super Admin account. | Medium | `deploy/seed_enterprise_simulation.sql` sets `IsPlatformAdministrator = FALSE` for all users. | **Step 4.4b**: Run manual psql script to insert Super Admin and PLATFORM_ADMIN role. | Official seed script inclusion. |
| **KISS-04** | Nginx Reverse Proxy enforces default 1MB upload limit. | Medium | `deploy/nginx.conf` lacks explicit `client_max_body_size` directive (defaults to 1MB) while Backend allows up to 500MB. | Inject `client_max_body_size 500M;` into server block, or utilize local development mode. | App container image update. |
| **KISS-05** | Dynamic multi-tier approval routing is partially supported. | Medium | JSON graph schema supports complex conditions but frontend visual triggers are limited. | Utilize department-level approvals and single-tier review in `/approvals` (SUBMITTED filter). | Workflow Designer expansion. |
| **KISS-06** | AI Assistant services rely on external OpenAI API connectivity. | Low | When internet connectivity is absent or `OpenAI__ApiKey` is missing, falls back to heuristic engine. | System gracefully executes heuristic categorization based on internal service catalog. | Local LLM integration. |
| **KISS-07** | Certain UI labels and API error messages display in English. | Low | Inherited from domain token assets and standard error codes. | Consult technical glossary and API specification mapping. | Full i18n localization. |
| **KISS-08** | Development credentials stored in `appsettings.Development.json`. | Low | Local development template configuration for evaluation convenience. | Permissible solely in dev/evaluation environments; production utilizes external Secret Manager. | Clean production container image. |
| **KISS-09** | Omitting Workspace Code permits tenant login when identifier belongs to a single tenant (BKL-SEC-01 open). | Critical | `AuthenticationStore.FindCandidatesAsync` does not enforce account plane segregation. | Frontend web UI strictly requires Workspace Code for standard users; risk confined to direct API calls. | Patch in v1.0.1. |
| **KISS-10** | Multi-channel deadline escalation (SignalR -> Web Push -> Email) incomplete (BKL-FNC-06). | Medium | Background scheduling worker for multi-tier channel dispatch is not yet finalized. | Monitor approaching deadlines via `/sla-monitor` and `/notifications`. | Patch in v1.0.1. |
| **KISS-11** | Automated test suite contains known failures: 2/270 unit tests, 4/6 E2E tests, 89 Prettier formatting errors. | Low | Outdated mock test locators and cancelled request handler in users.spec.ts. | Does not impact runtime execution; application builds successfully. | Patch in v1.0.1. |
| **KISS-12** | Docker deployment: local file uploads stored on `/tmp` (tmpfs) and lost upon container restart. | Medium | `deploy/docker-compose.app.yml` does not mount persistent MinIO storage for API container. | Utilize local development mode (Sections II.4-II.5) during capstone evaluation. | Patch in v1.0.1. |
| **KISS-13** | Docker container hardcodes `ResetEmail__Enabled="false"`. | Low | Designed as safe default in containerized demo overlay. | Utilize development mode and configure external SMTP parameters. | — |

---

## 0.5 Deployment & Rollback Strategy

Standard containerized release deployment and disaster recovery procedure based on `deploy/docker-compose.app.yml`:

- **Step 1: Environment Variables Preparation:** Generate a cryptographically strong 32-byte JWT Signing Key using `openssl rand -base64 32` and configure `JWT_SIGNING_KEY_BASE64` in `deploy/.env` (the placeholder in `.env.example` is not valid Base64; the API will fail to start if omitted). Ensure `BIZFLOW_DATABASE_CONNECTION` password matches `POSTGRES_PASSWORD`.
- **Step 2: Pre-Release Database Backup:** Execute full schema and data dump in custom binary format (-Fc):
  ```bash
  docker compose -f deploy/docker-compose.yml exec -T postgres pg_dump -U bizflow -d bizflow -Fc > backup_pre_v1.0.0.dump
  ```
- **Step 3: Preserve Active Images for Instant Rollback:** Tag active images with the fallback tag `:prev`:
  ```bash
  docker tag bizflow/api:local bizflow/api:prev
  docker tag bizflow/web:local bizflow/web:prev
  ```
- **Step 4: Build Application Container Images:** Compile and assemble packaged application artifacts:
  ```bash
  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml build
  ```
- **Step 5: Apply Database Schema Migrations:** Execute schema updates using the standalone `migrate` service (profile tools):
  ```bash
  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml --profile tools run --rm migrate
  ```
- **Step 6: Launch Application Services:** Start application daemon containers in detached mode:
  ```bash
  docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d api web
  ```
- **Step 7: Smoke Test Execution & Health Verification:** Validate service health endpoints exposed via Web port 8080:
  ```bash
  curl -f http://localhost:8080/health/live
  curl -f http://localhost:8080/health/ready
  curl -f http://localhost:8080/login
  ```
  Authenticate via browser using seeded credentials: `admin@bizflow.local` (Workspace Code: `bizflow-corp` | Password: `Password123!`).
- **Step 8: Expected Service Downtime:** **[CẦN ĐO THỰC TẾ]**

### Rollback Strategy for Critical Failure Scenarios:

| Failure Scenario | Rollback Procedure | Execution Command | Recovery Objective (RTO) |
| :--- | :--- | :--- | :--- |
| **Application / UI Fault** | Revert to previous Docker image (:prev) | `docker tag bizflow/api:prev bizflow/api:local && docker tag bizflow/web:prev bizflow/web:local && docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d --no-build api web` | [CẦN ĐO THỰC TẾ] |
| **Database Migration Failure** | Rollback schema to preceding stable migration (TaskParentTaskAndAiLearnedChoice) | `dotnet ef database update 20261111090436_TaskParentTaskAndAiLearnedChoice --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure` | [CẦN ĐO THỰC TẾ] |
| **Data Corruption Catastrophe** | Restore clean state from pre-release custom binary backup (-Fc) | `docker compose -f deploy/docker-compose.yml exec -T postgres pg_restore -U bizflow -d bizflow --clean --if-exists < backup_pre_v1.0.0.dump` | [CẦN ĐO THỰC TẾ] |

*Note: Nginx Reverse Proxy in deploy/nginx.conf enforces a 1MB upload ceiling (KISS-04). For capstone evaluation requiring large attachments, utilize local development mode.*

---

## 0.6 Post-Release Monitoring & Operational Metrics

Key operational metrics and health thresholds for continuous post-release monitoring:

| Monitoring Target | Standard Threshold | Observation Source | Incident Response Action | Owner | Interval |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Readiness Probe (/health/ready)** | HTTP 200 OK (< 200ms) | Docker Probe: `http://localhost:8080/health/ready` | If 503 returned: inspect PostgreSQL connection pool and migration state. | [CẦN PHÂN CÔNG] | Every 15 min for 24h |
| **5xx Server Error Rate** | < 0.5% of total requests | Serilog / Docker API logs | Examine unhandled exceptions in backend container logs. | [CẦN PHÂN CÔNG] | Every 15 min for 24h |
| **Hangfire Failed Jobs** | 0 consecutive failures | PostgreSQL tables `hangfire.job` / `hangfire.state` | Check `ResetEmail__*` SMTP host parameters and DB pool. | [CẦN PHÂN CÔNG] | Every 15 min for 24h |
| **AI Assistant Error Rate** | < 2% of total prompts | `OpenAiModelClient` logs | Engine falls back to heuristic rule-base; inspect OpenAI quota. | [CẦN PHÂN CÔNG] | Every 15 min for 24h |
| **Container Resource Utilization** | CPU < 80%, RAM < 85% | `docker stats` | Restart daemon or increase memory allocation ceiling. | [CẦN PHÂN CÔNG] | Every 15 min for 24h |

---

## 0.7 Sign-off & Release Approval

Formal sign-off matrix for Release v1.0.0 authorization:

| Project Role | Full Name | Approval Date | Signature / Status |
| :--- | :--- | :--- | :--- |
| **Product Owner** | [CẦN BỔ SUNG] | 15/11/2026 | [CẦN KÝ] |
| **Technical Lead** | [CẦN BỔ SUNG] | 15/11/2026 | [CẦN KÝ] |
| **QA / Test Lead** | [CẦN BỔ SUNG] | 15/11/2026 | [CẦN KÝ] |
| **Release Manager** | [CẦN BỔ SUNG] | 15/11/2026 | [CẦN KÝ] |
| **Academic Advisor (GVHD)** | [CẦN BỔ SUNG] | 15/11/2026 | [CẦN KÝ] |

---

## 0.8 Compatibility & Breaking Changes Notes

Critical compatibility guidelines and breaking behavioral changes in Release v1.0.0:

1. **Database Schema Lineage:** The system incorporates exactly **38 Entity Framework Core migrations**. Schema updates must be executed sequentially from initial creation up to `20261111100000_GranularUserManagementPermissions`. Non-linear or skipped migration rollback is strictly prohibited.
2. **Divergence Between Docker and Development Environments:**
   - **Network Endpoints:** Docker overlay serves traffic through unified Web port 8080 (`http://localhost:8080`); Development environment operates segregated ports for Frontend 4200 (`http://localhost:4200`) and Backend API 5154 (`http://localhost:5154`).
   - **File Attachment Storage:** Docker setup utilizes `/tmp` (tmpfs) temporary storage, meaning local uploads are wiped upon container destruction (KISS-12); Development setup connects to MinIO S3 bucket or host persistent volume.
   - **Password Recovery:** Docker deployment hardcodes `ResetEmail__Enabled="false"` (KISS-13); Development mode enables mail dispatch when configured with valid SMTP parameters.
3. **Authentication Breaking Change:** Standard enterprise users must explicitly supply their WorkspaceKey during authentication to prevent tenant ambiguity (KISS-09).

---

# I. Deliverable Package

| No. | File Name | Version | Date | Description & Technical Notes |
| :--- | :--- | :--- | :--- | :--- |
| 1 | `Fall26_SE20B02_G3_DB_final.sql` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.0.0 | 15/11/2026 | Complete PostgreSQL 18 database script incorporating all 38 EF Core migrations, table partitions, foreign keys, indexes, and seeded enterprise simulation data. |
| 2 | `Fall26_SE20B02_G3_SRS_final.docx` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.1 | 15/11/2026 | Software Requirements Specification document detailing 16 core Use Cases (UC-001 through UC-016), functional requirement modules (FR-AUTH, FR-TEN, FR-TASK, FR-REQ, FR-SLA, FR-AI, FR-AUD), and non-functional specifications. |
| 3 | `Fall26_SE20B02_G3_SDS_final.docx` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.1 | 15/11/2026 | Software Design Specification document detailing Clean Architecture layers, ERD schemas, sequence diagrams, Task/Request state machines, and RESTful API catalogs. |
| 4 | `Fall26_SE20B02_G3_Final_Product_Backlog.xlsx` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.0.0 | 15/11/2026 | Final product backlog reflecting completion tracking across 3 iterations. Audited verification confirms 108/132 requirements verified (81.8%) and 33 screens operational for MVP. |
| 5 | `Fall26_SE20B02_G3_Issues_Report.xlsx` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.0.0 | 15/11/2026 | Comprehensive issue and defect registry tracking all lifecycle defects, root causes, severity ratings, resolution strategies, and verification statuses. |
| 6 | `Fall26_SE20B02_G3_Screen_Flow_Guide.docx` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.0.0 | 15/11/2026 | Screen flow guide detailing navigation transitions across all 33 user interface screens (S-01 to S-33), 4 user roles, and 8 business workflows. |
| 7 | `Fall26_SE20B02_G3_Backend_Source_Code.zip` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.0.0 | 15/11/2026 | Complete backend solution source code developed on .NET 10, C# 13, Entity Framework Core 10, ASP.NET Core Web API, Hangfire Background Worker, and SignalR Hub. |
| 8 | `Fall26_SE20B02_G3_Frontend_Source_Code.zip` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.0.0 | 15/11/2026 | Modern frontend single-page application source code developed on Angular 22, TypeScript, Tailwind CSS, Standalone Components, Angular Signals, and Vitest / Playwright. |
| 9 | `Fall26_SE20B02_G3_Deployment_Package.zip` [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3] | v1.0.0 | 15/11/2026 | Containerized deployment package containing docker-compose.yml, docker-compose.app.yml, Dockerfiles, Nginx Reverse Proxy configs, .env.example, and verification health check probes. |

*Note: Fall26_SE20B02_G3 represents the designated student capstone project group identifier. [CẦN XÁC NHẬN MÃ NHÓM: Fall26_SE20B02_G3]*

**Other related deliverables:**
- **Tagged source codes:** `https://github.com/TxRyeSayNo/AI-Assisted-BizFlow-Platform/releases/tag/v1.0.0 [CẦN TẠO TAG: git tag -a v1.0.0 -m 'Release v1.0.0']`
- **Demonstration video:** `[CẦN DÁN LINK THẬT: Video demo YouTube 11 ký tự, ví dụ: https://youtu.be/XXXXXXXXXXX]`

---

# II. Installation Guides

## 1. Environment Prerequisites & Hardware Specifications

Prior to installation, the target host or developer workstation must satisfy the following hardware and software prerequisites:

| Component | Minimum Requirement | Recommended Specification |
| :--- | :--- | :--- |
| **Operating System** | Windows 10/11 x64, macOS 13+, or Ubuntu 22.04 LTS | Windows 11 Pro 64-bit with WSL2 enabled |
| **Hardware Capacity** | 4-core CPU @ 2.0 GHz, 8 GB RAM, 10 GB available SSD | 8-core CPU @ 3.2 GHz, 16 GB+ RAM, NVMe SSD storage |
| **.NET SDK** | .NET SDK 10.0.100+ | .NET SDK 10.0.401 (pinned in global.json) |
| **Node.js & npm** | Node.js v22.17.0 LTS / npm 10.8+ | Node.js v22.22.3 LTS & npm 11.6.2 (pinned in frontend/.nvmrc) |
| **Container Engine** | Docker Desktop 4.25+ (Linux containers mode) | Docker Desktop 4.34+ (Linux engine, 4GB+ allocated RAM) |
| **Backing Services** | PostgreSQL 17.x, Redis 7.x, S3-Compatible Storage | PostgreSQL 18.3-alpine, Redis 7-alpine, MinIO S3 |

---

## 2. Configuration & Environment Variables Reference

Consolidated specification of environment variables consumed by the backend application (defined in `deploy/.env.example`, `Program.cs`, `PasswordResetRegistration.cs`):

| Environment Variable | Required | Purpose & Default Value | Fallback Mechanism |
| :--- | :--- | :--- | :--- |
| `ConnectionStrings__BizFlow` | Yes | PostgreSQL connection string (default: Port 5432, DB: bizflow, User: bizflow). | None. Startup halts immediately if missing. |
| `Jwt__Issuer` | Yes | JWT token issuing authority name (default: BizFlow). | None. Startup halts immediately if missing. |
| `Jwt__Audience` | Yes | JWT token audience identifier (default: BizFlow.Web). | None. Startup halts immediately if missing. |
| `Jwt__SigningKeyBase64` | Yes | HMAC-SHA256 Base64-encoded secret signing key (minimum 32 bytes). | None. Startup halts immediately if missing. |
| `Redis__ConnectionString` | No | Redis cache endpoint connection string (e.g. localhost:6379). | Automatically falls back to in-memory IMemoryCache. |
| `Storage__Provider / Endpoint / PublicEndpoint / Bucket / AccessKey / SecretKey` | No | MinIO/S3 object storage configuration for file attachments. | Falls back to local file storage only when Provider is not MinIO/S3. In Development, appsettings.Development.json enables MinIO; credentials must match deploy/.env. |
| `OpenAI__ApiKey / Model / BaseUrl / TimeoutSeconds` | No | OpenAI API authentication key and model name (default: gpt-4o-mini). | Automatically activates heuristic rule-based categorization. |
| `ResetEmail__Enabled / Host / Port / Username / Password / FromAddress / FrontendOrigin` | No | SMTP configuration for self-service password recovery emails. | Defaults to Enabled=false; API returns AUTH.RESET_UNAVAILABLE if disabled. |
| `PasswordReset__TokenLifetimeMinutes` | No | Validity lifespan of password recovery tokens (default: 15 minutes). | Defaults to 15 minutes if unconfigured. |

> **[SECURITY WARNING / EVALUATION ENVIRONMENT]** All passwords, database credentials, and cryptographic signing keys in this document are DEMO / EVALUATION SAMPLES. UNDER NO CIRCUMSTANCES should these values be reused in Production environments!

---

## 3. Infrastructure & Backing Services Deployment

The platform utilizes Docker Compose to provision PostgreSQL 18.3-alpine, Redis 7-alpine, and MinIO S3-compatible storage (image pgsty/silo).

**Step 3.1: Copy environment template configuration:**
- PowerShell 7: `Copy-Item deploy\.env.example deploy\.env`
- Bash: `cp deploy/.env.example deploy/.env`

**Step 3.2: Spin up infrastructure containers in detached mode:**
```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d
```
Verify container health (default container names: `bizflow-local-postgres-1`, `bizflow-local-redis-1`, `bizflow-local-minio-1`):
```bash
docker compose ps
```

---

## 4. Backend Service Setup & Execution (.NET 10 Web API)

**Step 4.1: Restore .NET local tools and NuGet packages:**
```bash
dotnet tool restore
dotnet restore BizFlow.slnx
```

**Step 4.2: Export runtime database connection and security parameters:**
*Note: If POSTGRES_PORT is customized in deploy/.env, adjust the Port parameter accordingly.*

PowerShell 7:
```powershell
$env:ConnectionStrings__BizFlow = 'Host=127.0.0.1;Port=5432;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret'
$env:Jwt__Issuer = 'BizFlow'
$env:Jwt__Audience = 'BizFlow.Web'
$env:Jwt__SigningKeyBase64 = 'Loizdg9h99sl7lZJSV1LJwNCNTIdi7PjAtR3yib+brk='
$env:Storage__AccessKey = 'bizflow'
$env:Storage__SecretKey = 'replace-with-a-unique-local-secret'
```

Bash:
```bash
export ConnectionStrings__BizFlow='Host=127.0.0.1;Port=5432;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret'
export Jwt__Issuer='BizFlow'
export Jwt__Audience='BizFlow.Web'
export Jwt__SigningKeyBase64='Loizdg9h99sl7lZJSV1LJwNCNTIdi7PjAtR3yib+brk='
export Storage__AccessKey='bizflow'
export Storage__SecretKey='replace-with-a-unique-local-secret'
```

**Step 4.3: Apply EF Core database schema migrations (38 migrations):**
```bash
dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure
```

**Step 4.4 (MANDATORY FOR EVALUATORS): Seed enterprise simulation dataset:**
```bash
docker compose -f deploy/docker-compose.yml exec -T postgres psql -U bizflow -d bizflow < deploy/seed_enterprise_simulation.sql
```

**Step 4.4b: Provision Super Admin Account (Required for Workflow 1 evaluation):**
Because the baseline SQL seed does not provision a platform superadmin (KISS-03), evaluators must open a terminal session:
```bash
docker compose -f deploy/docker-compose.yml exec -T postgres psql -U bizflow -d bizflow
```
and paste the following SQL block to create the Super Admin user and assign the PLATFORM_ADMIN system role:
```sql
INSERT INTO "User" ("UserId","TenantId","IsPlatformAdministrator","EmployeeCode","NormalizedEmployeeCode","Email","NormalizedEmail","PasswordHash","SecurityStamp","FullName","DepartmentId","Status","AccessFailedCount","MustChangePassword","CreatedAt","UpdatedAt")
VALUES ('019f8000-0000-7000-8000-0000000000ff', NULL, TRUE, 'SUPERADMIN', 'SUPERADMIN', 'platform@bizflow.local', 'PLATFORM@BIZFLOW.LOCAL', 'AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==', 'STAMP_SUPERADMIN', 'Platform Administrator', NULL, 'ACTIVE', 0, FALSE, NOW(), NOW())
ON CONFLICT ("UserId") DO NOTHING;
INSERT INTO "UserRole" ("UserId","RoleId") SELECT '019f8000-0000-7000-8000-0000000000ff', "RoleId" FROM "Role" WHERE "Name"='PLATFORM_ADMIN' AND "IsSystem" AND "TenantId" IS NULL ON CONFLICT DO NOTHING;
```
*Note: If accessing /platform/audit results in a 403 Forbidden, verify that the platform.audit.read permission is mapped to the PLATFORM_ADMIN role [CẦN XÁC NHẬN].*

**Step 4.5: Launch Backend Web API service:**
```bash
dotnet run --project backend/BizFlow.Api
```
The Web API listens at `http://localhost:5154`. Validate probe readiness at `http://localhost:5154/health/ready` and inspect OpenAPI Swagger specifications at `http://localhost:5154/openapi/v1.json` (Development mode only).

---

## 5. Frontend Application Setup & Execution (Angular 22 SPA)

**Step 5.1: Open a secondary terminal, navigate to the frontend directory, and install dependencies from package-lock.json:**
```bash
cd frontend
npm ci
```

**Step 5.2: Launch Angular development server:**
```bash
npm start
```
The single-page application is available at `http://localhost:4200`. The dev server automatically proxies `/api` requests to the Backend API on port 5154.

---

## 6. Automated Test Suite Execution & Quality Metrics

Recorded automated testing metrics across the codebase (recorded November 15, 2026):

| Test Suite | Execution Command | Recorded Outcome | Technical Notes |
| :--- | :--- | :--- | :--- |
| **Backend Unit Tests** | `dotnet test BizFlow.slnx --filter "Category=Unit"` | 495/495 (as of 01/11/2026 report, pending re-run) [CẦN CHẠY LẠI] | Requires comprehensive test re-execution |
| **Backend Integration Tests** | `dotnet test BizFlow.slnx --filter "Category=Integration"` | [CHƯA CHẠY] | Requires active Testcontainers Docker runtime |
| **Frontend Unit Tests (Vitest)** | `cd frontend && npm test -- --watch=false` | 268/270 tests PASS (99.2%) | 2 known failures: users.spec.ts (pending retrieval cancellation) and requests.spec.ts in concurrent suite |
| **End-to-End Mock Tests (Playwright)** | `cd tests/BizFlow.E2ETests && npm test` | 2/6 tests PASS (33.3%) | 4 outdated mock tests failing against localized Vietnamese UI |
| **Source Code Prettier Check** | `cd frontend && npm run format:check` | 89 unformatted files detected | Will fail CI pipeline format:check gate |

---

## 7. Baseline Test Accounts & Access Credentials Matrix

Baseline demonstration accounts provisioned from `deploy/seed_enterprise_simulation.sql` and Step 4.4b:

**Workspace Code: `bizflow-corp` | Shared Password: `Password123!`**

| Role | Email / Account | Employee Code | Full Name & Title | Department | Workspace | Password |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Super Admin** | `platform@bizflow.local` | SUPERADMIN | Platform Administrator | NULL (Platform Plane) | Workspace: leave blank | Password123! |
| **Company Admin** | `admin@bizflow.local` | ADMIN001 | Phạm Minh Đức (CEO) | Executive Leadership (EXEC) | `bizflow-corp` | Password123! |
| **Executive Board** | `huong.nguyen@bizflow.local` | EXEC001 | Nguyễn Thị Mai Hương (COO) | Executive Leadership (EXEC) | `bizflow-corp` | Password123! |
| **Department Manager** | `manager@bizflow.local` | MGR001 | Lê Hoàng Nam (CTO) | Technology & Digital Transformation (TECH) | `bizflow-corp` | Password123! |
| **DevOps Engineer** | `huy.do@bizflow.local` | TECH002 | Đỗ Quang Huy (DevOps) | Technology & Digital Transformation (TECH) | `bizflow-corp` | Password123! |
| **Employee (Dev)** | `employee@bizflow.local` | EMP001 | Nguyễn Văn Nhân Viên (Dev) | Technology & Digital Transformation (TECH) | `bizflow-corp` | Password123! |
| **Department Manager** | `dat.nguyen@bizflow.local` | SALES_MGR | Nguyễn Tiến Đạt (Sales Lead) | B2B Sales & Partnerships (SALES) | `bizflow-corp` | Password123! |
| **Employee (Sales)** | `kiet.hoang@bizflow.local` | SALES001 | Hoàng Tuấn Kiệt (B2B Specialist) | B2B Sales & Partnerships (SALES) | `bizflow-corp` | Password123! |
| **Department Manager** | `thao.vu@bizflow.local` | MKT_MGR | Vũ Phương Thảo (Marketing Lead) | Marketing & Communications (MKT) | `bizflow-corp` | Password123! |
| **Employee (Content)** | `linh.le@bizflow.local` | MKT001 | Lê Mỹ Linh (Content Creator) | Marketing & Communications (MKT) | `bizflow-corp` | Password123! |

---

# III. User Manual

## 1. Overview

The AI-Assisted BizFlow Platform is a comprehensive multi-enterprise task and service orchestration system supporting automated state machine transitions, strict SLA deadline tracking, and human-in-the-loop AI-assisted task decomposition.

### 1.1 Dual-Plane Architectural Governance

The platform enforces strict administrative boundary segregation between two operational planes:
- **Platform Plane (Global Governance):** Reserved exclusively for Platform Administrators via `/platform/companies` and `/platform/audit`. Responsible for evaluating new enterprise registration dossiers, atomic tenant database provisioning, and inspecting immutable platform-wide audit trails.
- **Tenant Plane (Enterprise Operations):** Dedicated to tenant members via `/dashboard` (or `/workspace`). Data across enterprises is strictly isolated by `TenantId`, governing departmental structures, RBAC permissions, service catalogs, workflows, and SLA profiles.

### 1.2 Four Primary User Roles & Responsibility Matrix

| User Role | Governance Scope | Primary Responsibilities | Primary Accessible Routes |
| :--- | :--- | :--- | :--- |
| **Super Admin** | Platform Plane | Audit registration dossiers, approve tenant provisioning, monitor platform audit trail. | `/platform/companies`, `/platform/audit` |
| **Company Admin** | Tenant Plane | Configure tenant parameters, departments, user directory, RBAC roles, services, workflows, and SLAs. | `/dashboard`, `/settings`, `/settings/organization/*`, `/settings/services`, `/settings/workflows`, `/settings/sla-profiles`, `/audit` |
| **Department Manager** | Department Scope | Create tasks, AI decomposition, assign work, review service requests, confirm task completion, track SLAs. | `/dashboard`, `/tasks`, `/tasks/new`, `/tasks/ai-decomposition`, `/requests`, `/approvals`, `/reports`, `/sla-monitor` |
| **Employee** | Assigned / Own Scope | Submit service requests, accept assigned tasks, execute work, upload evidence files, search records, receive alerts. | `/dashboard`, `/tasks`, `/requests`, `/requests/new`, `/notifications` |

### 1.3 Core State Machine Lifecycles

The domain layer enforces strict state machine lifecycle management. Priority levels across Tasks and Requests comprise 4 standardized tiers: Low, Medium, High, Critical.

#### Table 1: Task State Machine Transition Matrix (TaskLifecyclePolicy - 10 States)

| Current State | Next State | Condition & Required Evidence | Required Permission |
| :--- | :--- | :--- | :--- |
| Draft or Rejected | Assigned | Requires valid assigned department or individual user | `tasks.assign` |
| Assigned | Accepted | Assignee formally confirms task acceptance | `tasks.accept` |
| Assigned | Rejected | Assignee rejects task, mandatory rejection justification | `tasks.accept` |
| Accepted or Overdue | InProgress | Assignee initiates execution | `tasks.execute` |
| InProgress | Submitted | Mandatory submission of delivery evidence files | `tasks.submit` |
| Submitted | Confirmed | Designated reviewer verifies and approves outcome | `tasks.confirm` |
| Submitted | InProgress | Reviewer requests rework, returning task to active execution | `tasks.confirm` |
| Confirmed | Completed | System automatically concludes task lifecycle | System Only |
| InProgress or Submitted | Overdue | Deadline breached or SLA violation detected | System Only |
| Any (except Completed, Cancelled) | Cancelled | Task cancelled with mandatory audit justification | `tasks.cancel` |

#### Table 2: Service Request Transition Matrix (RequestLifecyclePolicy - 12 States)

| Current State | Next State | Condition & Required Evidence | Required Permission |
| :--- | :--- | :--- | :--- |
| Draft | Submitted | Requester submits formal service request | `requests.submit` |
| Submitted | Routed | Request routed to responsible functional department | `requests.route` |
| Routed | Received | Department acknowledges and queues request | `requests.receive` |
| Received | InProgress | Specialist commences request fulfillment | `requests.process` |
| InProgress | WaitingForInformation | Suspended awaiting additional information from requester | `requests.process` |
| WaitingForInformation | InProgress | Fulfillment resumes upon receipt of required information | `requests.process` |
| InProgress | Resolved | Specialist inputs resolution summary and marks resolved | `requests.resolve` |
| Resolved | Confirmed | Requester confirms satisfaction with resolution | `requests.confirm` |
| Resolved | InProgress | Requester rejects resolution, returning to in-progress | `requests.confirm` |
| Confirmed | Closed | System permanently closes fulfilled request | System Only |
| Submitted or Routed | Rejected | Request rejected with mandatory justification | `requests.reject` |
| InProgress, Received... | Overdue | SLA commitment target elapsed | System Only |
| Any (except Closed, Cancelled, Rejected) | Cancelled | Request cancelled with mandatory justification | `requests.cancel` |

---

## 2. Workflow 1: Company Registration & Tenant Provisioning

**Objective:** Receive new enterprise registration applications, verify legal credentials, and execute atomic tenant workspace provisioning.  
**Actors:** Enterprise Representative (Guest), Super Admin (Platform Owner).  
**Step-by-Step Procedure:**
1. **Step 1:** Enterprise representative visits public registration portal at `/register-company`. Form requires 6 mandatory legal fields: Company Name, Tax Identification Code, Desired Workspace Code, Legal Representative Name, Contact Phone, and Official Notification Email.
2. **Step 2:** Representative uploads Business Registration License (.pdf or .png, max 10MB). System invokes real-time workspace code collision check with 400ms debounce and uploads document directly to MinIO bucket via presigned URL. Clicks 'Submit Registration' [Status: Initial -> PendingReview].
3. **Step 3:** Super Admin signs in to Platform Plane and navigates to `/platform/companies` to view pending registrations. *(Note: Provision Super Admin via Step 4.4b prior to performing Step 3)*. Super Admin inspects dossier details and clicks license file to view document via 15-minute secure presigned URL.
4. **Step 4:** Super Admin executes approval action: opens approval dialog and supplies mandatory fields: Tenant Key (lowercase slug, e.g. 'acme-corp'), Initial Admin Employee Code, Admin Email, Admin Full Name, Initial Admin Password (min 8 characters), and IANA Timezone (e.g. 'Asia/Ho_Chi_Minh'). System executes atomic transaction creating Company, Tenant, initial Admin account, calendar, and audit record. (Note: No automatic activation emails are sent; Admin uses credentials established by Super Admin). If rejecting, Super Admin provides justification (5-1000 characters) [Status: PendingReview -> Approved / Rejected].

---

## 3. Workflow 2: Authentication & Workspace Navigation

**Objective:** Securely authenticate platform users, resolve multi-tenant account associations, and route to role-tailored workspaces.  
**Actors:** All system users.  
**Step-by-Step Procedure:**
1. **Step 1:** User navigates to unified login screen at `/login`. UI offers two authentication mechanisms: (1) Workspace Code + Username/EmployeeCode/Email + Password; (2) Google Sign-In via Firebase Popup (requires Firebase configuration).
2. **Step 2:** If a user logs in via Google and the email address belongs to multiple tenant enterprises, the Workspace Selector dialog (S-03) is displayed for tenant selection. JWT session token is issued strictly after TenantId resolution. *(Image placeholder: [SCREENSHOT REQUIRED])*
3. **Step 3:** System redirects user to tailored Workspace (`/dashboard` or `/workspace`) based on assigned role:
   - **Department Manager:** Dashboard surfaces departmental performance metrics, pending task assignments, SLA violation alerts, and approval queues.
   - **Employee:** Dashboard focuses on individual task queue (My Assigned Tasks) and personal service requests.

---

## 4. Workflow 3: Initial Tenant & Service Configuration

**Objective:** Establish organizational foundation including tenant parameters, departments, user accounts, RBAC matrix, service catalog, visual workflows, and SLA profiles.  
**Actors:** Company Admin (Tenant Administrator).  
**Step-by-Step Procedure:**
1. **Step 1:** Configure general tenant parameters at `/settings` (S-23) to update branding logo, operational IANA timezone, and password policies.
2. **Step 2:** Manage departmental organizational structure at `/settings/organization/departments` (S-25) to build department tree, designate Department Leads, and set group notification emails.
3. **Step 3:** Manage staff accounts at `/settings/organization/users` (S-26) to invite new employees, assign department affiliations, and grant initial roles.
4. **Step 4:** Configure granular RBAC permissions at `/settings/organization/roles` (S-27) to customize permission matrices (`tasks.create`, `tasks.assign`, `requests.route`, `sla.read`...).
5. **Step 5:** Define internal service catalog at `/settings/services` (S-24) to create request types (equipment provisioning, leave requests, IT support), map fulfillment departments, and associate SLA profiles.
6. **Step 6:** Workflow management and visual design: Access `/settings/workflows` (S-28) to inspect active workflows, and `/settings/workflows/:id/versions/:v` (S-29) to design state transitions on the visual canvas. Published workflows enforce `WorkflowAllowsTransition` validation rules.
7. **Step 7:** Establish SLA commitment profiles: Navigate to `/settings/sla-profiles` (S-30) to configure Response and Resolution targets, operating calendars (excluding weekends/holidays), and audit version history at `/settings/sla-profiles/:id/versions` (S-31).

---

## 5. Workflow 4: End-to-End Task Lifecycle Management

**Objective:** Coordinate entire task lifecycle including creation, assignment, acceptance, execution, evidence submission, and formal verification across the 10-state lifecycle.  
**Actors:** Department Manager, Assignee (Employee), Quality Reviewer.  
**Step-by-Step Procedure:**
1. **Step 1:** Task Creation: Department Manager navigates to `/tasks/new` (S-11) to define task title, description, priority (Low, Medium, High, Critical), completion deadline, checklist items, and applicable SLA profile [Status: Initial -> Draft].
2. **Step 2:** Task Assignment: Manager delegates task to specific team member or routes to unassigned department queue at `/tasks` (S-10) [Status: Draft -> Assigned].
3. **Step 3:** Task Acceptance: Employee inspects personal queue at `/tasks` (S-10). Opens task detail view at `/tasks/:id` (S-13) and clicks 'Accept Task' (S-13b) [Status: Assigned -> Accepted]. If unable to execute, clicks 'Reject' with mandatory justification [Status: Assigned -> Rejected].
4. **Step 4:** Execution & Progress Tracking: Assignee clicks 'Start Execution' (S-13c) [Status: Accepted -> InProgress]. During execution, assignee checks off completed checklist items, posts discussion notes, and attaches preliminary files. *(Image placeholder: [SCREENSHOT REQUIRED])*
5. **Step 5:** Submission & Evidence Delivery: Upon completion, assignee clicks 'Submit Result' (S-13d). Modal dialog requires summary of deliverables and mandatory evidence attachment (.pdf, .zip up to 500MB per backend limit) [Status: InProgress -> Submitted]. *(Image placeholder: [SCREENSHOT REQUIRED])*
6. **Step 6:** Review & Final Acceptance: Designated reviewer opens `/tasks/:id` (S-13) to inspect deliverables and evidence (S-13e): If acceptable, clicks 'Confirm & Accept' [Status: Submitted -> Confirmed], triggering automatic conclusion [Status: Confirmed -> Completed]. If deficient, clicks 'Request Rework' with mandatory instructions; task reverts to InProgress.

---

## 6. Workflow 5: End-to-End Service Request Management

**Objective:** Receive internal service requests from employees, facilitate multi-tier approvals, route to specialized fulfillment departments, and verify resolution.  
**Actors:** Requester (Employee), Approver (Manager), Dispatcher / Specialist.  
**Step-by-Step Procedure:**
1. **Step 1:** Service Request Submission: Employee navigates to `/requests/new` (S-15), selects catalog service item, details requirements, specifies priority (Low, Medium, High, Critical), and attaches relevant files [Status: Initial -> Submitted].
2. **Step 2:** Request Review & Approval: Submitted request surfaces in Manager Approval Queue at `/approvals` (S-17). Management evaluates request and approves or rejects with mandatory justification [Status: Submitted -> Rejected if denied].
3. **Step 3:** Departmental Routing & Acknowledgment: Approved request routes to fulfillment department at `/requests` (S-14) [Status: Submitted -> Routed]. Dispatcher acknowledges receipt [Status: Routed -> Received] and assigns specialist [Status: Received -> InProgress].
4. **Step 4:** Linked Task Sub-Division: For complex fulfillment operations, specialist may instantiate Linked Sub-Tasks directly from the request detail view to track specific execution phases. *(Image placeholder: [SCREENSHOT REQUIRED])*
5. **Step 5:** Resolution & Requester Confirmation: Specialist inputs resolution summary and clicks 'Resolve Request' [Status: InProgress -> Resolved]. Requester receives notification to verify outcome at `/requests/:id` (S-16): If satisfied, clicks 'Confirm Resolution' [Status: Resolved -> Confirmed], triggering permanent closure [Status: Confirmed -> Closed]. If unsatisfied, clicks 'Feedback' with justification, returning request to InProgress.

---

## 7. Workflow 6: AI Assistance & Task Decomposition

**Objective:** Accelerate project task planning via AI under strict Human-in-the-Loop oversight. AI suggestions remain transient drafts until explicitly confirmed and persisted by authorized managers.  
**Actors:** Department Manager, Company Admin.  
**Step-by-Step Procedure:**
1. **Step 1:** On the `/tasks` page (S-10), user clicks the 'AI Decompose' button to open `/tasks/ai-decomposition` (S-12), and inputs overarching project objectives. *(Image placeholder: [SCREENSHOT REQUIRED])*
2. **Step 2:** System redirects to AI Task Decomposition view at `/tasks/ai-decomposition` (S-12). AI parses goal and returns structured sub-tasks: task titles, descriptions, estimated duration, priority, and Directed Acyclic Graph (DAG) dependencies.
3. **Step 3 (Human-in-the-Loop Validation):** Preview endpoint performs transient computation and emits read-only DTOs without writing to the database. Manager directly edits task details inline, removes redundant sub-tasks, or adds manual items. *(Image placeholder: [SCREENSHOT REQUIRED])*
4. **Step 4:** Manager clicks 'Confirm & Batch Create Tasks'. System validates permissions and transactionally creates validated tasks in database, wiring parent-child hierarchy and execution dependencies. *(Image placeholder: [SCREENSHOT REQUIRED])*
5. **Step 5 (BizFlow Robot Chat Widget S-33):** Within Task and Request detail screens, users open the BizFlow AI Agent drawer (S-33) to query deadline risks, summarize discussion history, or seek assignment recommendations. *(Image placeholder: [SCREENSHOT REQUIRED])*

---

## 8. Workflow 7: SLA Monitoring & Operational Reports

**Objective:** Provide real-time SLA commitment tracking, operational bottleneck detection, executive productivity analytics, and immutable audit trailing.  
**Actors:** Company Admin, Department Manager, Executive Leadership.  
**Step-by-Step Procedure:**
1. **Step 1: Real-Time SLA Monitoring:** Navigate to `/sla-monitor` (S-18) to track active tasks and service requests across 3 visual tiers: Green (Safe, < 75% elapsed), Yellow (Approaching Breach, >= 75% elapsed), and Red (Breached, > 100% elapsed). Managers may initiate emergency escalation interventions.
2. **Step 2: Executive Performance Analytics:** Access `/reports` (S-19) to review departmental throughput, on-time delivery rates, mean resolution times, and workload distribution.
3. **Step 3: Centralized Archival & Records Retrieval:** Navigate to `/records/search` (S-20) to filter and retrieve historical task records, evidence attachments, and resolution artifacts.
4. **Step 4: Immutable Enterprise Audit Trail:** Access `/audit` (S-22) to review tamper-resistant transaction logs recording all business mutations (timestamp, actor, action, previous state, new state).
5. **Step 5: Event Notification Center:** Visit `/notifications` (S-21) to review deadline alerts, assignment updates, and status change notices.

---

## 9. Workflow 8: Password Recovery & Session Invalidation

**Objective:** Restore account access via asynchronous background mailer (Hangfire Worker) and automatically invalidate active sessions upon password reset to safeguard account security.  
**Actors:** All registered enterprise users.  
**Note:** This feature is disabled by default in baseline configuration (`ResetEmail__Enabled=false`, API returns `AUTH.RESET_UNAVAILABLE`) until operators configure a TLS-capable SMTP server.  
**Step-by-Step Procedure:**
1. **Step 1: Password Recovery Request:** On login screen, user clicks 'Forgot password?' navigating to `/forgot-password` (S-05). Enters registered corporate email and clicks 'Send Recovery Link'.
2. **Step 2: Asynchronous Hangfire Worker Processing:** System generates a cryptographically signed recovery token with 15-minute expiration. Background worker dispatches email containing secure single-use recovery link.
3. **Step 3: New Password Creation:** User opens link navigating to `/reset-password?token=...` (S-06). Enters new password complying with security policy (min 12 characters, >= 4 unique characters) and confirms password.
4. **Step 4: Session Invalidation & Re-Authentication:** Upon successful reset, backend invalidates all existing Refresh Tokens and sessions across devices. User is redirected to `/login` to authenticate with new credentials.
"""

with open(OUT_MD_EN, "w", encoding="utf-8") as f:
    f.write(md_content_en)

print(f"Generated English Markdown at {OUT_MD_EN}, size: {os.path.getsize(OUT_MD_EN)} bytes")
