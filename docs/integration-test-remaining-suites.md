# Integration Test Remaining Suites (Handoff)

Companion to:

- `docs/FurniSpace_System_Flow_Integration_Test_Context_Updated.md` (§28 suites)
- `docs/integration-test-build-guide.md`
- Existing Core suite under `tests/FurniSpace.API.IntegrationTests/`

## Status snapshot

| Suite | Scope | Owner / status |
| --- | --- | --- |
| A Project Intake | Submit → sales receive → info → start fee → designer | **Done** (Core) |
| B Measurement | Areas + measurement schedules | **Done** (Core) |
| C Proposal (SQL) | Create / scene / publish / revision / select final | **Done** (Core) |
| D Customization | Request → versions → production review → accept / cancel | **Done** (Core) |
| E Quotation & Order | Create quotation, revision, accept → order snapshot, deposit config | **Partial** — accept + deposit create already exist; extend |
| F Payment | Obligation, attempt, webhook, retry, expire, order effect | **Todo** |
| G Production | Staff assign, item lifecycle, adjustment, complete sync | **Todo** |
| H Delivery | Delivery schedules, qty, customer confirm, completion gate | **Todo** |
| I Final payment & Review | Remaining payment, explicit complete, project review | **Todo** |
| J Cross-cutting | Authz matrix, files, notifications, idempotency, concurrency | **Todo** |
| C+ Room Planner / Mongo variants | Scene Mongo save/load, variant apply | **Deferred** — ExternalDependency suite |

## Already implemented (do not rewrite)

```text
tests/FurniSpace.API.IntegrationTests/
  Pipeline/
  Categories/
  Products/
  Projects/          # create/get + Suite A intake + status
  ProjectAreas/      # Suite B
  ProjectSchedules/  # Suite B measurement
  Proposals/         # Suite C SQL
  CustomizationRequests/  # Suite D
  Quotations/        # accept → order
  Payments/          # status-by-code, order deposit, project start fee

tests/FurniSpace.Testing/Seeding/
  CoreAccountSeeder.cs
  ProjectScenarioSeeder.cs
  MeasurementScenarioSeeder.cs
  ProposalScenarioSeeder.cs
  CustomizationScenarioSeeder.cs
  QuotationAcceptScenarioSeeder.cs
  DepositOrderScenarioSeeder.cs
```

Harness: `ApiIntegrationCollection` + `FurniSpaceWebApplicationFactory` (Postgres Testcontainers, Respawn, test auth, fake email/PayOS/search/realtime/storage).

Run Core:

```powershell
dotnet test tests\FurniSpace.API.IntegrationTests\FurniSpace.API.IntegrationTests.csproj -c Release --filter "Category=Core"
```

## Suite E — Quotation & Order (remaining)

**Goal:** cover create/send/revision before accept; deposit configuration side effects.

Suggested tests:

1. Sales creates quotation from selected proposal / items (snapshot `product_version_id`).
2. Customer requests revision → `QUOTATION_REVISION_REQUESTED` + project status.
3. Sales sends revised version (version_no increment).
4. Accept still creates one order atomically (existing test).
5. Sales configures deposit amount; block production until deposit paid (if enforced).
6. Wrong actor / wrong status → 403/400.

Reuse `QuotationAcceptScenarioSeeder`; add `SeedSelectedProposalAsync` if create-quotation needs earlier state.

## Suite F — Payment

**Goal:** provider-fake payment attempt + webhook idempotency.

Suggested tests:

1. Customer creates online attempt on PENDING obligation (PayOS fake / SePay vietqr).
2. Webhook SUCCESS marks payment PAID and applies order effect (deposit paid / start fee paid).
3. Duplicate webhook → no double PAID / no double notification.
4. Concurrent SUCCESS → one SUCCESS transaction.
5. Expire / cancel paths.
6. Customer cannot force PAID via normal API.

Use `FakePayOsClient`; do **not** call real PayOS/SePay.

## Suite G — Production

**Goal:** production request after deposit paid.

Suggested tests:

1. Sales creates production request + items from order.
2. List available production staff (ACTIVE only).
3. Assign / reassign staff.
4. Item lifecycle (start / issue / complete / cancel → adjustment required).
5. Complete request applies READY/UNAVAILABLE on order items and moves project/order statuses.
6. Idempotent complete.

Seed from deposit-paid order state.

## Suite H — Delivery

**Goal:** delivery after production complete.

Suggested tests:

1. Sales creates one or more DELIVERY schedules.
2. Start delivery → project/order `DELIVERING`.
3. Update `delivered_quantity` (never exceed qty; no lost updates under concurrency).
4. Customer confirms each fully delivered item.
5. Final confirm → `DELIVERED` (schedule COMPLETED alone must not mark order delivered).

## Suite I — Final payment & Review

Suggested tests:

1. Enter final payment only after delivery.
2. Remaining amount 0 → no remaining payment; amount > 0 → exact obligation.
3. Remaining PAID does **not** auto-complete.
4. Sales/Admin explicit complete order + project.
5. Duplicate complete idempotent.
6. Customer review after COMPLETED; reject before complete / duplicate / other customer.

## Suite J — Cross-cutting

Suggested tests:

1. Authz matrix samples across modules (wrong role / wrong project owner).
2. File metadata + fake storage upload link rules.
3. Important notifications once (with capturing dispatcher if added).
4. Idempotent retries listed in system-flow §26.
5. Concurrency samples from §27 (payment, production complete, customization accept, quotation accept).

## Room Planner / Mongo (ExternalDependency)

Out of Core PR gate for now (see build guide Phase P2):

- `PUT/GET /proposal-scenes/{sceneId}/room-planner`
- Variant create/submit/review/apply
- Mongo + SQL consistency on apply failure

Mark tests `[Trait("Category", "ExternalDependency")]` and run in a separate CI job when Mongo Testcontainer is ready.

## Implementation checklist for the next developer

1. Add scenario seeders under `tests/FurniSpace.Testing/Seeding/` (no demo `DataSeeder`).
2. One focused test class per workflow; `[Collection(ApiIntegrationCollection.Name)]` + `Category=Core`.
3. Reset DB in `InitializeAsync` via `_fixture.Database.ResetAsync()`.
4. Authenticate with `IntegrationHttp.Authenticated*` using seeded account IDs/roles.
5. Assert HTTP status **and** Postgres side effects.
6. Prefer SQL seeding over Mongo for Core; keep Mongo in ExternalDependency.
7. Extend CI only after suite is green locally with Docker.

## Source of truth for business rules

Prefer:

1. Application services + existing unit tests under `tests/FurniSpace.Application.Tests/`
2. `docs/FurniSpace_System_Flow_Integration_Test_Context_Updated.md` (status matrix §24, transactions §25)
3. `docs/api-reference.md` for HTTP contracts

When docs and code disagree, **code + unit tests win**; update this handoff if you discover a mismatch.

## Current Implementation Update

Suites E, F, G, H, and J now have Core integration coverage in:

- `tests/FurniSpace.API.IntegrationTests/Quotations/QuotationLifecycleApiIntegrationTests.cs`
- `tests/FurniSpace.API.IntegrationTests/Payments/PaymentAttemptWebhookApiIntegrationTests.cs`
- `tests/FurniSpace.API.IntegrationTests/Production/ProductionWorkflowApiIntegrationTests.cs`
- `tests/FurniSpace.API.IntegrationTests/Delivery/DeliveryWorkflowApiIntegrationTests.cs`
- `tests/FurniSpace.API.IntegrationTests/CrossCutting/CrossCuttingApiIntegrationTests.cs`

Suite I final-payment and explicit order/project completion coverage is in
`tests/FurniSpace.API.IntegrationTests/Orders/FinalPaymentReviewApiIntegrationTests.cs`.
Project review remains pending because the API layer does not expose a project review endpoint yet.

Room Planner / Mongo remains deferred to an `ExternalDependency` suite.
