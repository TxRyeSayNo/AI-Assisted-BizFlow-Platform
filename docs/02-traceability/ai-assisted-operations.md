# AI-Assisted Operations — Implementation Contract

Scope: FR-AI-001 through FR-AI-010, SSS §7.11, §20.1, §29 Appendix E, UI-06, UI-09.
Endpoints:
- `POST /api/v1/ai/task-assistance` (`ai.001`, FR-AI-001)
- `POST /api/v1/ai/task-assignment-recommendation` (`ai.002`, FR-AI-002)
- `POST /api/v1/ai/task-parameters` (`ai.003`, FR-AI-003)
- `POST /api/v1/ai/task-breakdown` (`ai.004`, FR-AI-004)
- `POST /api/v1/ai/task-risk` (`ai.005`, FR-AI-005)
- `POST /api/v1/ai/task-summary` (`ai.006`, FR-AI-006)
- `POST /api/v1/ai/request-multi-intent` (`ai.007`, FR-AI-007)
- `POST /api/v1/requests/{id}/split` (`ai.008`, FR-AI-008)
- `POST /api/v1/ai/request-routing` (`ai.009`, FR-AI-009)
- `POST /api/v1/ai/actions/execute` (`ai.010`, FR-AI-010)
- `POST /api/v1/ai/recommendations/{id}/decision` (Recommendation decision tracking)

---

## 1. Architectural and Business Rules (SSS §7.11, Appendix E, BR-021)

- **Strict Advisory Nature (BR-021)**:
  - AI outputs are advisory suggestions. AI does not autonomously alter business state without explicit authorized user action or allowlisted tool invocation dispatch.
  - State transitions, assignment mutations, task creation, and request splitting only occur via authenticated and authorized Application services.

- **Multi-Tenant Catalog Grounding**:
  - All recommendations (department routing, user assignment, service classification, category matching) are strictly validated against the caller's tenant catalog.
  - Hallucinated or cross-tenant IDs are strictly rejected or stripped before recommendations are presented or executed.

- **Subtask Breakdown Acyclic Guarantee (FR-AI-004)**:
  - AI subtask generation validates that dependency references satisfy `DependsOnOrderIndex < OrderIndex`, guaranteeing an acyclic DAG.

- **Advisory Risk Scoring (FR-AI-005)**:
  - Calculated based on deadline proximity (<24h, <48h), priority weight (URGENT/HIGH), checklist complexity, and current overdue status. Returns risk level (`LOW`, `MEDIUM`, `HIGH`, `CRITICAL`) with actionable mitigation recommendations.

- **Request Splitting (FR-AI-008)**:
  - Divides a multi-intent request into multiple independent child requests under the parent request (`ParentRequestId`), preserving tenant isolation, assigning appropriate services, and generating `REQUEST.SPLIT` audit logs.

- **Allowlisted Tool Execution (FR-AI-010)**:
  - Dispatches execution only to registered, allowlisted tools:
    1. `create_task_draft`: Creates task draft via `TaskDraftService`.
    2. `assign_task`: Assigns task via `TaskAssignmentService`.
    3. `add_comment`: Appends collaboration comment via `CommentService`.
    4. `route_request`: Routes request via `RequestRoutingService`.
  - Requires caller to have both `ai.010` and the underlying domain permission (`tasks.create`, `tasks.assign`, `comments.create`, or `requests.route`).

- **Database Entities & Audit Persistence (Tables 34, 35, 36 of 43)**:
  - `AIInteraction`: Records interaction prompt, model name, completion token metrics, latency, and status (`SUCCESS`, `FAILED`).
  - `AIRecommendation`: Records structured recommendation payload, confidence score, rationale, and user decision (`PENDING`, `ACCEPTED`, `REJECTED`, `MODIFIED`).
  - `AIAgentAction`: Records tool invocation name, parameters, execution result, and status (`SUCCESS`, `FAILED`).
  - Enforces database check constraints, tenant query filters, and audit trail traceability.

- **Security & Permissions**:
  - Seeded in migration `20261008030000_AiAssistedOperationsFoundation`:
    - `ai.001` (`01a14000-0000-7000-8000-000000000010`): Task assistance
    - `ai.002` (`01a14000-0000-7000-8000-000000000011`): Assignment recommendation
    - `ai.003` (`01a14000-0000-7000-8000-000000000012`): Parameter extraction
    - `ai.004` (`01a14000-0000-7000-8000-000000000013`): Task breakdown
    - `ai.005` (`01a14000-0000-7000-8000-000000000014`): Risk assessment
    - `ai.006` (`01a14000-0000-7000-8000-000000000015`): Task summary
    - `ai.007` (`01a14000-0000-7000-8000-000000000016`): Multi-intent analysis
    - `ai.008` (`01a14000-0000-7000-8000-000000000017`): Request split
    - `ai.009` (`01a14000-0000-7000-8000-000000000018`): Request routing
    - `ai.010` (`01a14000-0000-7000-8000-000000000019`): Action dispatch
  - Granted by default to `COMPANY_ADMIN`, `MANAGER`, and `EMPLOYEE`.

---

## 2. API Contracts

### `POST /api/v1/ai/task-assistance`
- **Request Body**:
  ```json
  {
    "taskTitle": "Upgrade database server",
    "taskDescription": "Migrate database from Postgres 16 to 18",
    "category": "INFRASTRUCTURE"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "suggestedTitle": "Upgrade database server",
    "suggestedDescription": "Migrate database from Postgres 16 to 18...",
    "suggestedPriority": "HIGH",
    "suggestedChecklist": ["Backup data", "Run migrations", "Verify indices"],
    "rationale": "Database migration requires strict checklist..."
  }
  ```

### `POST /api/v1/ai/task-assignment-recommendation`
- **Request Body**:
  ```json
  {
    "taskId": "01a14000-0000-7000-8000-000000000001",
    "departmentId": "01a10000-0000-7000-8000-000000000010"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "recommendedUserId": "01a10000-0000-7000-8000-000000000001",
    "recommendedUserName": "Alice Engineer",
    "confidenceScore": 0.95,
    "rationale": "Member has lowest active workload..."
  }
  ```

### `POST /api/v1/ai/task-parameters`
- **Request Body**:
  ```json
  {
    "unstructuredText": "Please fix server outage urgently by tomorrow 5pm"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "extractedTitle": "Fix server outage",
    "extractedDescription": "...",
    "extractedPriority": "URGENT",
    "extractedDeadline": "2026-10-09T17:00:00Z"
  }
  ```

### `POST /api/v1/ai/task-breakdown`
- **Request Body**:
  ```json
  {
    "taskTitle": "Deploy microservice platform",
    "taskDescription": "Setup k8s cluster, deploy api and frontend"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "subtasks": [
      { "orderIndex": 1, "title": "Setup k8s cluster", "dependsOnOrderIndex": null },
      { "orderIndex": 2, "title": "Deploy API", "dependsOnOrderIndex": 1 },
      { "orderIndex": 3, "title": "Deploy Frontend", "dependsOnOrderIndex": 2 }
    ]
  }
  ```

### `POST /api/v1/ai/task-risk`
- **Request Body**:
  ```json
  {
    "taskId": "01a14000-0000-7000-8000-000000000001"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "riskLevel": "HIGH",
    "riskScore": 0.75,
    "riskFactors": ["Urgent priority", "Deadline is less than 24 hours"],
    "mitigationSuggestions": ["Assign secondary reviewer"]
  }
  ```

### `POST /api/v1/ai/task-summary`
- **Request Body**:
  ```json
  {
    "taskId": "01a14000-0000-7000-8000-000000000001"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "summary": "Task is 75% complete with 3 completed checklist items.",
    "currentStatus": "IN_PROGRESS",
    "blockersIdentified": []
  }
  ```

### `POST /api/v1/ai/request-multi-intent`
- **Request Body**:
  ```json
  {
    "requestId": "01a12000-0000-7000-8000-000000000055",
    "title": "Onboard employee and grant VPN access",
    "description": "HR needs to onboard employee and IT needs to grant VPN access"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "requestId": "01a12000-0000-7000-8000-000000000055",
    "isMultiIntent": true,
    "confidenceScore": 0.92,
    "intents": [
      {
        "title": "HR Onboarding",
        "description": "Prepare paperwork and credentials",
        "suggestedServiceName": "HR Services"
      },
      {
        "title": "VPN Access Provisioning",
        "description": "Grant IT network VPN credentials",
        "suggestedServiceName": "IT Security"
      }
    ],
    "reasoning": "Request combines HR onboarding and IT access provisioning."
  }
  ```

### `POST /api/v1/requests/{id}/split`
- **Request Body**:
  ```json
  {
    "splits": [
      {
        "title": "HR Onboarding",
        "description": "Prepare paperwork",
        "serviceId": "01a11000-0000-7000-8000-000000000001",
        "categoryId": "01a11000-0000-7000-8000-000000000002"
      },
      {
        "title": "VPN Access Provisioning",
        "description": "Grant IT credentials",
        "serviceId": "01a11000-0000-7000-8000-000000000003",
        "categoryId": "01a11000-0000-7000-8000-000000000004"
      }
    ]
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "parentRequestId": "01a12000-0000-7000-8000-000000000055",
    "splitCount": 2,
    "childRequestIds": [
      "01a14000-0000-7000-8000-000000000021",
      "01a14000-0000-7000-8000-000000000022"
    ]
  }
  ```

### `POST /api/v1/ai/request-routing`
- **Request Body**:
  ```json
  {
    "requestId": "01a12000-0000-7000-8000-000000000055",
    "title": "Network outage on floor 3",
    "description": "Switch is flashing amber and offline",
    "serviceId": "01a11000-0000-7000-8000-000000000001"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "requestId": "01a12000-0000-7000-8000-000000000055",
    "recommendedDepartmentId": "01a10000-0000-7000-8000-000000000010",
    "recommendedDepartmentName": "IT Operations",
    "confidenceScore": 0.95,
    "rationale": "Network hardware incidents route to IT Operations."
  }
  ```

### `POST /api/v1/ai/actions/execute`
- **Request Body**:
  ```json
  {
    "toolName": "create_task_draft",
    "argumentsJson": "{\"title\":\"Hardware replacement\",\"description\":\"Swap switch on floor 3\",\"priority\":\"HIGH\"}"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "toolName": "create_task_draft",
    "status": "SUCCESS",
    "resultJson": "{\"taskId\":\"01a14000-0000-7000-8000-000000000031\"}",
    "errorMessage": null
  }
  ```

---

## 3. Frontend Integration

- **Task Creation (`bf-task-create`)**:
  - `assistWithAi()`: Calls `POST /api/v1/ai/task-assistance` to generate structured title, description, priority, and checklist.
  - `breakdownWithAi()`: Calls `POST /api/v1/ai/task-breakdown` to decompose complex tasks into ordered checklist items.
- **Task Detail (`bf-task-detail`)**:
  - `evaluateRisk()`: Calls `POST /api/v1/ai/task-risk` to display risk card with level, score, risk factors, and mitigation advice.
  - `summarizeProgress()`: Calls `POST /api/v1/ai/task-summary` to produce a high-level executive summary of task execution.
- **Request Detail (`bf-request-detail`)**:
  - `recommendRouting()`: Calls `POST /api/v1/ai/request-routing` to pre-populate target department in the routing drawer.
  - `analyzeMultiIntent()`: Calls `POST /api/v1/ai/request-multi-intent` to detect multi-intent requests.
  - `confirmSplit()`: Calls `POST /api/v1/requests/{id}/split` from the AI Split dialog to split into independent child requests.
- **Styling**:
  - Compact, elegant AI assistant cards with violet gradients (`#faf5ff` to `#ffffff`), glowing badges (`✨ AI Assistant`), and distinct callout blocks.
  - Complies strictly with Angular component style budgets (< 4.0 kB warning / 8.0 kB error).

---

## 4. Automated Test Evidence

- **Backend Unit Tests**:
  - Test file: `tests/BizFlow.UnitTests/AI/AiAssistanceServiceTests.cs` (16 test cases covering all 10 operations, error paths, DAG acyclic validation, permission checks, and tenant isolation).
  - Total backend tests: **404 passed, 0 failed**.
- **Frontend Unit Tests**:
  - Test files: `task-create.spec.ts`, `task-detail.spec.ts`, `request-detail.spec.ts` (17 tests in request detail including AI split and AI routing).
  - Total frontend tests: **196 passed across 36 test files, 0 failed**.
- **Build Verification**:
  - `dotnet build`: Succeeded, 0 errors, 0 warnings.
  - `npm run build`: Succeeded with code 0, bundle size 322.70 kB initial, all component budgets met.
