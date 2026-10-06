# Service catalog implementation

Scope: DEV-D5 / FR-SVC-001's catalog/create flow, canonical API-SVC-01/02/03/05 and Appendix C Service/ServiceCategory. The API and GRID catalog/create UI are implemented, including adding categories to existing services. The metadata-update increment has its own [contract and validation record](service-metadata-update.md). Request selection/runtime is not implemented.

## Implemented model and API contract

`Service` and `ServiceCategory` preserve the specified business fields, with UUIDv7 identities, native status enums, required foreign keys, nullable existing workflow/SLA version references, scoped unique indexes and xmin concurrency. Their original migration brought the model to 22 of the approved 43 application tables; the later Request foundation brings the total to 23. No routing rule, approval binding, request or runtime state is invented by this service slice.

`POST /api/v1/services` accepts `code`, `name`, optional plain-text `description`, `active` (default true), and optional initial `categories:[{code,name,active}]`. Initial categories and actor-attributed `SERVICE.CREATED` audit commit with the service. Codes/names are trimmed; code length is 1–80 and name length 1–200. Codes use exact case-sensitive uniqueness in their owning tenant/service; no case-folding requirement is added. Category-code duplicates after trimming are rejected before persistence. Empty category collections are allowed because the service dictionary defines no minimum; a future Request still must satisfy its category requirement. Creation leaves workflow/SLA references null and does not start runtime work.

Description is plain text, not an HTML document: unsupported control characters are rejected, line breaks/tabs are retained, and consumers must render using contextual text escaping. Literal markup is preserved as text, never treated as trusted HTML. Names/codes reject controls. SQL guards enforce these basics in addition to EF/Domain validation.

Live `service.create` authorization is required; its default grant belongs only to the COMPANY_ADMIN system role. Custom grants work without trusting role names. Creation returns 201 with the service and generated category IDs; concurrent duplicate service codes return safe `409 SERVICE.DUPLICATE_CODE`, with no partial categories/audit. Unsupported tenant, status, version or nested ownership fields are rejected rather than discarded.

`GET /api/v1/services` follows the canonical authenticated-tenant-member contract, using the existing membership authorizer rather than inventing an administrator-only read grant. Filters: page (default 1), pageSize (default 25, max 100), literal name/code search (max 200), canonical status. It returns tenant-owned service metadata and categories, with a consistent count/page/category snapshot. Status defaults to no filter; clients selecting new request services will need explicit ACTIVE selection and server-side request validation. API responses are no-store.

## Integrity boundaries

EF filters derive category tenancy through the owning service. Writes reject foreign tenants, foreign persisted parents, forged detached ownership, category edits/deletes and attempts to bind versions through creation or metadata replacement. PostgreSQL protects identity/tenant/creation-time and category parent immutability, validates same-tenant version references and requires a published workflow reference. These reference checks are not a complete workflow-compatibility or binding use case. Migration rollback refuses populated service/category data or configured custom grants.

## Adding categories to existing services (API-SVC-05)

`POST /api/v1/services/{id}/categories` accepts only `code`, `name` and optional `active` (default true). It uses the same live `service.create` permission as initial category creation. The tenant-owned parent is locked, authorization is checked again after acquiring the lock, and foreign/missing parents share a safe 404 with caller-scoped denial audit. No caller-supplied tenant, parent identity or status field is accepted.

Service create/list responses now include an additive `eTag` property holding the quoted PostgreSQL xmin version. Creation responses also carry the ETag header. Adding a category requires a single strong quoted numeric `If-Match`; missing, weak, wildcard or malformed tags return `422 SERVICE.ETAG_REQUIRED`. A stale tag returns `409 SERVICE.VERSION_CONFLICT` with the current authorized ETag/category codes. The UI never silently adopts that tag or retries the mutation.

The new category, parent's UpdatedAt/xmin and actor-attributed `SERVICE.CATEGORY_CREATED` audit commit in one transaction. Even an unchanged clock advances the parent version. Existing category identities, metadata, active state and workflow/SLA bindings are preserved. Category-code uniqueness is exact/case-sensitive within its service; duplicate codes return `409 SERVICE.CATEGORY_CODE_EXISTS`. A success returns 201 with the updated full service projection and its new ETag header. No additional tables, migration, business states or permission codes are introduced.

The GRID detail view exposes “Add category to service” only to authorized creators. A separate editor retains input after validation/duplicate rejection and blocks overlapping mutations. Stale or uncertain outcomes require a successful catalog reload and explicit service reselection before another save; unsuccessful reloads do not release that gate. Manual reconciliation is not an idempotency guarantee. Successful responses update only the affected catalog row. Existing category editing/deletion remains unimplemented.

## GRID catalog and creation

Authenticated tenant members can open the catalog from Workspace. Authorized creators can also enter through Settings; that navigation grant does not broaden other settings-feature guards. The GRID screen includes literal name/code and status filters, paging, loading/empty/error/denied states, escaped plain-text details and categories. Only callers with `service.create` see the creation form. Server authorization remains mandatory.

The form saves service metadata, active flag and initial category code/name/active flags in one request. It validates trimmed blank/duplicate codes, blocks double submission, retains input for duplicate-code/validation errors and never retries a POST automatically. An uncertain save requires a successful catalog refresh before another attempt; this is manual reconciliation, not idempotency proof. Workflow/SLA IDs are displayed only when actually present, otherwise "Not configured". No routing or runtime operation is implied by ACTIVE metadata.

## Verification and remaining work

Eight Domain tests were added first and initially failed because the Services namespace did not exist. The first persistence run passed 194 unit and 162 integration tests, including four new scoped-uniqueness, tenant/parent-forgery, direct-SQL integrity and safe rollback tests. The subsequent API run stopped at two xUnit2031 analyzer errors; both assertions were corrected without changing their meaning. The final complete suite passed 194 unit and 171 integration tests, including platform/suspension and grant-preserving rollback checks. EF reports no pending model changes. Current evidence is recorded in [implementation status](implementation-status.md).

The automatic approval reviewer temporarily reported a usage limit before UI-folder creation. The normal approval path subsequently recovered, and the folder/UI were created only after that path succeeded. All 87 Angular tests, the production build, source formatting and six supplemental mocked browser tests pass. All 32 real desktop/mobile flows passed, including service/category creation, persisted catalog retrieval, filters/paging, safe text and employee read-only tenant isolation. Complete creation forms and catalog screenshots were visually inspected at desktop and 375px mobile width. The final harness exited 0 and removed its disposable database container.

Category-add validation subsequently passed 195 unit and 177 integration tests. New checks cover exact If-Match handling, concurrent additions, frozen-clock parent version advancement, binding/metadata preservation, foreign-resource denial, permission revocation during a lock wait and whole-transaction audit rollback. All 92 Angular tests, production build and source formatting pass; EF confirms no pending model changes. Browser validation for this increment is recorded separately in implementation status.

Metadata updates are tracked separately in [API-SVC-03 implementation](service-metadata-update.md). Remaining service work includes routing, workflow/approval binding and request consumers. No full service-module or request workflow acceptance is claimed. Original planning documents remain unchanged.
