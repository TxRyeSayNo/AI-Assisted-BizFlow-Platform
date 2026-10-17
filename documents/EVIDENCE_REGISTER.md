# Evidence Register — BizFlow SRS

Version 0.1 · 2026-10-16 · Draft

## Evidence records

|Evidence ID|Source|Observed evidence|Used for|Evidence type|Confidence / limitation|
|---|---|---|---|---|---|
|EVD-001|AI-Assisted_BizFlow_Software_System_Specification_v1.2_FINAL.docx|SSS document, v1.2, last updated 2026-09-28; labels itself “Baseline v1.2 – Scope Locked / Implementation Ready”.|Primary source for intended domain scope, 62 canonical FRs, business rules, state models, API catalogue, NFRs, security and AI guardrails.|Document source; not source-code evidence.|High for what the document states; no independent approval artifact checked.|
|EVD-002|User conversation, 2026-10-16|User describes multi-business internal task/request management; five-person team; base skeleton frontend/backend; SSS available; demo date 2026-10-19.|Project context and current delivery constraint.|Direct user-provided information.|High for user intent; implementation detail not independently verified.|
|EVD-003|User decision, 2026-10-16|User accepts Option C for MVP and wants a future paid enterprise upgrade for Option A.|Sets one primary workspace per company for MVP; multi-workspace is Future Enterprise, billing undecided.|Explicit product decision.|High.|
|EVD-004|GitHub repository listing|`ThangHoQuoc/f`, default branch `main`, returned as the only repository accessible to the connected GitHub account during this discovery.|Shows a repository is connected, but not the target BizFlow source.|Repository metadata/root contents.|High for observed repo metadata; does not prove there is no other repository outside the connected account.|
|EVD-005|GitHub `manage.py`|https://github.com/ThangHoQuoc/f/blob/main/manage.py|Django command-line utility; sets `DJANGO_SETTINGS_MODULE` to `ready2go.settings`. Supports the assessment that repository is not the intended Angular/.NET project.|Source file content fetched read-only.|High.|
|EVD-006|Local execution environment|Only `/mnt/data/Pasted text(4).txt` was present before materializing the referenced SSS; no BizFlow Git working tree or source archive was present.|Local `git status`, source tree, migrations, tests, package manifests and runtime configuration could not be inspected.|Filesystem inspection.|High for current execution workspace; environment-specific.|
|EVD-007|SSS sections 3, 7–10, 16–21, 25–27 and Appendices A–G|Source baseline describes 13 modules, five actors, 62 FRs, 22 BRs, Task/Request state machines, 500 MB attachment limit, AI guardrails and testing plan.|Used to build functional register, rule catalogue, security requirements, verification scenarios and scope priorities.|Extracted from docx body and tables.|High for document contents; implementation not verified.|
|EVD-008|SSS Technology Verification References|Appendix G of SSS v1.2 has a snapshot described as verified on 2026-09-28 with official URLs for framework/database/SDK details.|Recorded as the source’s technology statement only; not re-verified against application manifests because the application repository is unavailable.|Source document.|High for contents; current project versions not verified.|
|EVD-009|SSS Appendix C and Section 32 scorecard|Appendix C yields 38 distinct “Entity – ...” headings in the extracted document; Section 32 scorecard states “39 entities”.|Records an internal source-document count discrepancy; do not invent or silently add the missing entity.|Cross-section consistency check.|High for the extracted text; the reason remains unresolved.|

## Discovery result and limitation

**Repository discovery result:** GitHub repository accessible to the connected account: https://github.com/ThangHoQuoc/f (default branch main, public metadata). Root listing includes `db.sqlite3`, `manage.py`, `media/`, `ready2go/`, `tourapp/`; `manage.py` is Django and sets `DJANGO_SETTINGS_MODULE` to `ready2go.settings`. This does not match the intended BizFlow Angular/.NET/PostgreSQL stack and is not treated as the BizFlow application repository.

**Conclusion:** no matching BizFlow application repository was discovered through the connected GitHub account. The SRS therefore describes the agreed/source-specified product requirements, not the actual behavior of the codebase. No current Git branch or working-tree cleanliness claim is made. No application code was changed, and no commits or pushes were performed by this task.

## Evidence hierarchy used

1. Explicit user decision on 2026-10-16 is authoritative for the newly decided MVP/future workspace direction.
2. SSS v1.2 is the canonical detailed product/technical baseline for the original BizFlow scope unless a newer approved source is supplied.
3. Repository metadata and `manage.py` from `ThangHoQuoc/f` were used only to identify that the accessible repo does not match the intended application; its business code was not treated as BizFlow evidence.
4. No implementation facts were inferred from the SSS alone. Implementation coverage and all test execution statuses remain Not Verified.