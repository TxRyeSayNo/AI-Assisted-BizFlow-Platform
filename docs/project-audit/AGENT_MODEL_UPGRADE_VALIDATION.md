# Personal Agent model upgrade — 24 November 2026

The development API uses `gpt-6-sol`, Responses API, medium reasoning effort and a
90-second provider timeout. The configured API key was verified against that model.
Runtime logs record the actual returned model, endpoint, latency and token counts,
without recording prompts or provider credentials. Provider response storage is disabled.
The integration follows [OpenAI's Responses migration guide](https://developers.openai.com/api/docs/guides/migrate-to-responses)
and [GPT-6 Sol model documentation](https://developers.openai.com/api/docs/models/gpt-6-sol).

## Changes

- One general business assistant instruction replaces the meeting-specific extraction
  prompt. The model selects from discoverable tool descriptions and typed input contracts.
  There is no server keyword router deciding which business scenario to run.
- Read capabilities now include employee lookup, request search, task details and request
  details. They reuse the existing application queries and their live access policies.
  Employee lookup returns identity and department, omitting email addresses.
- Existing task search, own inbox, personal memory and task decomposition remain available.
  All writes still require a bound preview and explicit approval.
- Meeting communication and general announcement tool descriptions now have explicit,
  nonoverlapping scope. The live test initially caught the model using a general announcement
  to bypass missing meeting logistics; the tool descriptions were corrected before the
  successful evaluation. No canned response or intent keyword match was added.
- The prompt tells the model to resolve names before asking for IDs, preserve follow-up
  facts, clarify ambiguity, respect pagination, distinguish facts from advice, and state
  unsupported capabilities. It does not grant permissions or implement new actions by itself.
- Legacy placeholder role metadata is withheld from the model; it must not mistake every
  user for an employee. Tenant/user/conversation isolation and user-approved preferences remain.
- Responses parsing handles typed message output and usage, and rejects incomplete,
  refused or empty output before it can become an action. Legacy Chat Completions remains
  configurable with `OpenAI:ApiMode=chat`.

## Validation

- Backend unit tests: 670 passed.
- Integration tests for AI, API foundation and deadline reminders: 33 passed using disposable
  PostgreSQL. New coverage includes the Responses request/response contract, incomplete
  responses, a real four-tool lookup chain, and denial of a foreign tenant's task details.
- The live evaluation script passed 8/8 business checks with the configured model. See
  [the machine-readable results](../../artifacts/agent-business-evaluation.json).
  Checks cover meeting follow-ups and correction, announcement revision, an unseen task
  draft, prioritizing accessible work, finding people, finding requests, unsupported
  email/scheduling, and advice without unsolicited writes.
- Live evaluation confirms zero write previews. It cancels every prepared action and never
  calls the action execution endpoint. Notification delivery/confirmation behavior is tested
  separately against disposable PostgreSQL with controlled model output.

Run the relevant automated checks from the repository root:

```powershell
dotnet test tests/BizFlow.UnitTests --no-restore
$env:DOCKER_HOST='npipe://./pipe/dockerDesktopLinuxEngine'
dotnet test tests/BizFlow.IntegrationTests --no-restore --filter 'FullyQualifiedName~.AI.|FullyQualifiedName~ApiFoundationTests|FullyQualifiedName~DeadlineReminderApiTests'
```

To repeat the live evaluation, set `BIZFLOW_EVAL_IDENTIFIER`, `BIZFLOW_EVAL_PASSWORD`
and `BIZFLOW_EVAL_TENANT` for an explicitly selected local test account, then run
`python scripts/evaluate_personal_agent.py`. It uses `http://localhost:5154` unless
`BIZFLOW_EVAL_URL` overrides it. The report contains check outcomes and timings, not
credentials or business content. Live evaluation incurs provider usage charges.

## Limits of this evidence

These are focused checks, not proof that the agent understands every business request.
The live read checks validate successful read-only conversation outcomes; controlled
integration tests establish query execution and access boundaries. There is no measured
before/after model benchmark claiming a particular intelligence improvement.
The agent has a finite tool catalog and a five-model-call budget per turn. It prepares one
write at a time and cannot autonomously build tools, send email, schedule delivery or
perform unsupported task mutations. Task draft creation supports title, description and
priority; it does not set deadlines or assign users. Notification tools send in-app only.
No browser end-to-end suite or full integration suite was run for this backend-only upgrade.
