# FurniSpace Payment Service Guide

This guide explains how FurniSpace collects payments through **SePay (VietQR / bank transfer)** and **PayOS (payment link / checkout)**, persists records in PostgreSQL, and notifies clients over SignalR.

Related specs:

- `support-docs/FurniSpace_SePay_Service_Implementation_Plan (1).md`
- `support-docs/FurniSpace_PayOS_Service_Implementation_Plan.md`

## 1. Scope

Current implementation supports:

| Area | Status |
|---|---|
| Payment query APIs | Implemented |
| SePay VietQR generation | Implemented |
| SePay incoming-transfer webhook | Implemented |
| PayOS payment link creation | Implemented |
| PayOS webhook verification | Implemented |
| PayOS admin webhook confirm | Implemented |
| Payment realtime hub (`payment.updated`) | Implemented |
| Test payment creation (ADMIN) | Implemented |
| Partial payment (multiple transfers / links) | Implemented |
| Project start fee / order deposit / remaining APIs | **Not implemented** |
| Order / project status side effects on `PAID` | **Not implemented** |

The project does not use CQRS. The request flow follows the current backend pattern:

```text
API Controller
  -> Application service (PaymentService / webhook handlers)
  -> Infrastructure repository
  -> PostgreSQL (payments, payment_transactions)
  -> ServiceResult<T>

Webhook (provider -> API)
  -> SePayWebhookHandler / PayOsWebhookHandler
  -> Update payment + transaction
  -> IPaymentRealtimeService (SignalR)
```

**Important:** Webhooks are the source of truth for payment confirmation. Return URLs (PayOS) and manual refresh are for UI only.

## 2. Environment Variables

### 2.1 SePay

```env
SEPAY_ENABLED=true
SEPAY_ENVIRONMENT=production

SEPAY_BANK_CODE=...
SEPAY_BANK_ACCOUNT_NO=...
SEPAY_BANK_ACCOUNT_NAME=...

SEPAY_WEBHOOK_SECRET=...
SEPAY_WEBHOOK_URL=https://your-domain.com/api/webhooks/sepay

SEPAY_PAYMENT_CODE_PREFIX=FS
SEPAY_PAYMENT_CODE_REGEX=FS[0-9]{8,10}

SEPAY_VIETQR_ENABLED=true
SEPAY_VIETQR_BASE_URL=https://vietqr.app/img

SEPAY_STRICT_AMOUNT_CHECK=true
SEPAY_ALLOW_PARTIAL_PAYMENT=true
SEPAY_ALLOW_OVERPAYMENT=false
```

SePay binds through `SePayOptions` + `SePayOptionsConfiguration.ApplyEnvironmentOverrides`.

### 2.2 PayOS

```env
PAYOS_ENABLED=true
PAYOS_ENVIRONMENT=production

PAYOS_CLIENT_ID=...
PAYOS_API_KEY=...
PAYOS_CHECKSUM_KEY=...

PAYOS_API_BASE_URL=https://api-merchant.payos.vn

PAYOS_RETURN_URL=http://localhost:3000/payment/payos/success
PAYOS_CANCEL_URL=http://localhost:3000/payment/payos/cancel
PAYOS_WEBHOOK_URL=https://your-domain.com/api/webhooks/payos

PAYOS_DESCRIPTION_PREFIX=FS
PAYOS_MAX_DESCRIPTION_LENGTH=25
```

PayOS binds through `PayOsOptions` + `PayOsOptionsConfiguration.ApplyEnvironmentOverrides`.

**Never expose** `PAYOS_CLIENT_ID`, `PAYOS_API_KEY`, `PAYOS_CHECKSUM_KEY`, or `SEPAY_WEBHOOK_SECRET` to the frontend.

## 3. NuGet Package

PayOS SDK (Application layer):

```xml
<PackageReference Include="payOS" Version="2.1.0" />
```

SePay does not use an external SDK. VietQR URLs are built locally (`SePayVietQrUrlBuilder`).

## 4. REST Endpoints

### 4.1 Create test payment (development)

```http
POST /api/test/payments
Authorization: Bearer <admin-access-token>
Content-Type: application/json
```

Body:

```json
{
  "projectId": "uuid",
  "amount": 10000000,
  "paymentType": "OTHER",
  "note": "optional",
  "expiredAt": null
}
```

- Role: **ADMIN** only.
- Creates a row in `payments` with status `PENDING`.
- Generates human-readable `payment_code` (example: `FS12345678`).

Production business endpoints (project start fee, deposit, remaining) are planned but not implemented yet.

### 4.2 Query payments

```http
GET /api/payments/{paymentId}
GET /api/payments?projectId=&orderId=&status=&paymentType=
GET /api/payments/{paymentId}/transactions
GET /api/payments/code/{paymentCode}/status
Authorization: Bearer <access-token>
```

Roles: `CUSTOMER`, `SALES`, `DESIGNER`, `ADMIN`.

Access is scoped to project participants (customer, assigned sales/designer, or admin).

### 4.3 SePay — generate VietQR

```http
POST /api/payments/{paymentId}/sepay/vietqr
Authorization: Bearer <access-token>
```

Response includes:

- `vietQrUrl` — use as `<img src="..." />`
- `amount` — current `remainingAmount`
- `paymentCode` — transfer content (`FS...`)
- Bank fields from SePay config

Eligible payment statuses: `PENDING`, `PROCESSING`, `PARTIALLY_PAID`.

### 4.4 PayOS — create payment link

```http
POST /api/payments/{paymentId}/payos/payment-link
Authorization: Bearer <access-token>
Content-Type: application/json
```

Body (all fields optional):

```json
{
  "amount": 10000000,
  "returnUrl": "http://localhost:3000/payment/payos/success",
  "cancelUrl": "http://localhost:3000/payment/payos/cancel"
}
```

- If `amount` is omitted → uses `payments.remaining_amount`.
- If URLs are omitted → uses `PAYOS_RETURN_URL` / `PAYOS_CANCEL_URL`.
- Creates `payment_transactions` with status `PENDING` before calling PayOS API.
- PayOS `orderCode` (numeric) is stored in `provider_reference_code`.

Response includes `checkoutUrl`, `qrCode`, `orderCode`, `paymentTransactionId`.

### 4.5 Webhooks (providers call these — not FE)

```http
POST /api/webhooks/sepay
POST /api/webhooks/payos
```

- `[AllowAnonymous]` — authenticated by provider signature/checksum.
- SePay requires `X-SePay-Signature` and `X-SePay-Timestamp` (configurable via env).
- PayOS body is verified through the official SDK (`Webhooks.VerifyAsync`).
- Swagger cannot test these without valid signatures.

### 4.6 PayOS admin — confirm webhook URL

```http
POST /api/admin/payments/payos/confirm-webhook
Authorization: Bearer <admin-access-token>
Content-Type: application/json
```

Body:

```json
{
  "webhookUrl": "https://abc-123.ngrok-free.app/api/webhooks/payos"
}
```

Optional if webhook URL is configured manually on PayOS dashboard. Useful for local ngrok testing.

## 5. Access Rules

Payment access uses project assignment rules (`ProjectAssignmentAccessEvaluator`):

| Role | Access |
|---|---|
| `ADMIN` | Any payment |
| `CUSTOMER` | Payments for projects where they are `customer_id` |
| `SALES` | Payments for projects where they are `assigned_sales_id` |
| `DESIGNER` | Payments for projects where they are `assigned_designer_id` |

Webhook endpoints have no JWT. All other payment endpoints require JWT.

## 6. SePay Flow

```text
1. Backend creates payment (test API or future business API)
2. FE calls POST .../sepay/vietqr
3. FE displays QR image (vietQrUrl) + amount + paymentCode
4. Customer transfers via bank app (content must contain FS...)
5. SePay POST /api/webhooks/sepay
6. Backend verifies HMAC, creates payment_transaction (CHARGE, SUCCESS)
7. Backend updates payments.paid_amount / remaining_amount / status
8. Backend pushes SignalR event payment.updated
```

**Partial payment:** Customer can transfer less than QR amount. Each successful webhook creates a new transaction. FE should refresh VietQR after `PARTIALLY_PAID` so `amount` matches new `remainingAmount`.

**Same payment code:** Partial and follow-up transfers reuse `payment_code` (`FS...`).

**Overpayment:** Rejected when `SEPAY_STRICT_AMOUNT_CHECK=true` and `SEPAY_ALLOW_OVERPAYMENT=false`.

## 7. PayOS Flow

```text
1. Backend creates payment
2. FE calls POST .../payos/payment-link (optional amount for partial pay)
3. FE redirects/opens checkoutUrl
4. Customer pays on PayOS
5. PayOS POST /api/webhooks/payos
6. Backend verifies checksum, finds transaction by orderCode
7. Transaction PENDING -> SUCCESS; payment summary updated
8. Backend pushes SignalR event payment.updated
```

**Partial payment:** Call `payment-link` again with `amount <= remainingAmount`. Each call creates a new PENDING transaction with a new numeric `orderCode`.

**Return URL:** UI redirect only. Do not mark payment as paid based on return URL alone.

## 8. Payment Status Model

Payment summary (`payments`):

| paid_amount vs amount | status |
|---|---|
| `0` | `PENDING` |
| `0 < paid < amount` | `PARTIALLY_PAID` |
| `paid >= amount` | `PAID` |

Other statuses: `CANCELLED`, `EXPIRED`, `REFUNDED`, `FAILED`, `PROCESSING`.

`expiredAt` is optional on create. If set and in the past, VietQR / payment-link generation returns `PAYMENT_EXPIRED`.

## 9. Database Persistence

### 9.1 `payments`

Key fields:

```text
payment_id
project_id
order_id (optional)
payment_code          -- human code FS...
amount
paid_amount
remaining_amount
currency
status
expired_at (optional)
paid_at
payment_type
```

### 9.2 `payment_transactions`

Key fields:

```text
payment_transaction_id
payment_id
transaction_code
transaction_type      -- CHARGE | REFUND | ADJUSTMENT
amount
payment_provider      -- SEPAY | PAYOS
payment_method        -- QR_CODE | PAYMENT_LINK
provider_reference_code
  -- SePay: not primary match key (match via payment_code in transfer content)
  -- PayOS: numeric orderCode as string
provider_transaction_id
status                -- PENDING | SUCCESS | FAILED | CANCELLED
raw_provider_payload  -- webhook raw body on success
transaction_time
```

Provider matching:

| Provider | Webhook match |
|---|---|
| SePay | Extract `FS...` from transfer content → `payments.payment_code` |
| PayOS | `payment_provider=PAYOS` + `provider_reference_code=orderCode` |

## 10. Realtime (SignalR)

Payment updates use a dedicated hub (not the notifications hub).

| Item | Value |
|---|---|
| Hub path | `/hubs/payments` |
| Hub class | `FurniSpace.API.Hubs.PaymentHub` |
| Join method | `JoinPayment(paymentId)` |
| Leave method | `LeavePayment(paymentId)` |
| Event name | `payment.updated` |
| Group | `payment:{paymentId}` |

Event payload (`PaymentUpdatedRealtimeDto`):

```json
{
  "paymentId": "uuid",
  "projectId": "uuid",
  "paymentCode": "FS12345678",
  "status": "PARTIALLY_PAID",
  "amount": 30000000,
  "paidAmount": 10000000,
  "remainingAmount": 20000000,
  "paymentTransactionId": "uuid",
  "transactionAmount": 10000000,
  "appliedAmount": 10000000,
  "paidAt": null,
  "occurredAt": "2026-07-08T10:00:00Z"
}
```

Auth: same JWT as REST (`access_token` cookie or `?access_token=` for WebSocket). See `docs/signalr-notification-guide.md` for token transport details.

**Recommended FE pattern:** connect to `/hubs/payments`, call `JoinPayment`, listen for `payment.updated`. Do not poll status in a loop unless SignalR is unavailable.

## 11. Error Codes

Common codes (`PaymentErrorCodes`):

| Code | Meaning |
|---|---|
| `PAYMENT_NOT_FOUND` | Payment id/code not found |
| `INVALID_PAYMENT_STATUS` | Payment not collectable (e.g. already PAID) |
| `PAYMENT_EXPIRED` | `expiredAt` passed |
| `INVALID_PAYMENT_AMOUNT` | Amount <= 0 or no remaining |
| `PAYMENT_AMOUNT_EXCEEDS_REMAINING` | PayOS link amount too high |
| `SEPAY_DISABLED` | SePay off in config |
| `PAYOS_DISABLED` | PayOS off in config |
| `PAYOS_CREATE_LINK_FAILED` | PayOS API / URL config error |
| `PAYOS_AMOUNT_MISMATCH` | Webhook amount != transaction amount |

## 12. Implementation Files

**API**

- `src/FurniSpace.API/Controllers/Payments/PaymentsController.cs`
- `src/FurniSpace.API/Controllers/Payments/TestPaymentsController.cs`
- `src/FurniSpace.API/Controllers/Payments/SePayWebhookController.cs`
- `src/FurniSpace.API/Controllers/Payments/PayOsWebhookController.cs`
- `src/FurniSpace.API/Controllers/Payments/PayOsAdminPaymentsController.cs`
- `src/FurniSpace.API/Hubs/PaymentHub.cs`
- `src/FurniSpace.API/Realtime/SignalRPaymentRealtimeService.cs`

**Application**

- `src/FurniSpace.Application/Services/Payments/PaymentService.cs`
- `src/FurniSpace.Application/Services/Payments/SePayWebhookHandler.cs`
- `src/FurniSpace.Application/Services/Payments/PayOsWebhookHandler.cs`
- `src/FurniSpace.Application/Services/Payments/PayOsClientService.cs`
- `src/FurniSpace.Application/Common/Payments/SePay*.cs`
- `src/FurniSpace.Application/Common/Payments/PayOs*.cs`
- `src/FurniSpace.Application/DTOs/Payments/*`
- `src/FurniSpace.Application/Interfaces/Payments/*`

**Infrastructure**

- `src/FurniSpace.Infrastructure/Repositories/IRepository/IPaymentRepository.cs`
- `src/FurniSpace.Infrastructure/Repositories/Repository/PaymentRepository.cs`

**Tests**

- `tests/FurniSpace.Application.Tests/Payments/*`

## 13. Local Testing Checklist

### 13.1 Common setup

1. Run API (Docker or `dotnet run`).
2. Apply migrations / ensure `payments` and `payment_transactions` exist.
3. Login as ADMIN, create test payment:

```http
POST /api/test/payments
{ "projectId": "<uuid>", "amount": 10000, "paymentType": "OTHER" }
```

4. Connect SignalR hub `/hubs/payments` and `JoinPayment(paymentId)`.

### 13.2 SePay

1. Configure SePay env (bank account, webhook secret).
2. Start ngrok: `ngrok http 50000` (or your API port).
3. Register webhook URL in SePay dashboard: `{ngrok}/api/webhooks/sepay`.
4. `POST /api/payments/{id}/sepay/vietqr` → display QR.
5. Transfer real/test money with content containing `FS...`.
6. Verify webhook logs, transaction `SUCCESS`, payment `PAID`, SignalR `payment.updated`.

Swagger webhook test will fail without valid HMAC headers — expected.

### 13.3 PayOS

1. Configure PayOS credentials in env.
2. Start ngrok and set `PAYOS_WEBHOOK_URL`.
3. `POST /api/admin/payments/payos/confirm-webhook` or configure dashboard.
4. `POST /api/payments/{id}/payos/payment-link` → open `checkoutUrl`.
5. Complete payment (PayOS uses real money; minimum ~2000 VND).
6. Verify webhook, transaction `SUCCESS`, SignalR event.

### 13.4 Partial payment

- SePay: transfer half amount → `PARTIALLY_PAID` → regenerate VietQR → transfer remainder.
- PayOS: create link for partial amount → after webhook, create new link for `remainingAmount`.

## 14. Operational Notes

- Do not commit PayOS keys or SePay webhook secrets.
- Webhook URLs must be **HTTPS** in production.
- SePay and PayOS can run **in parallel**; FE lets the customer choose provider.
- Refunds are not implemented yet; `payment_transactions.transaction_type=REFUND` exists in schema for future use.
- No `PaymentWebhookLog` audit table — successful webhooks store payload on `payment_transactions.raw_provider_payload`; failures are logged via `ILogger`.
- After `PAID`, additional provider transfers are ignored (idempotent / non-collectable state).

## 15. Not Implemented Yet (Phase 2)

Planned business APIs:

```http
POST /api/projects/{projectId}/payments/project-start-fee
POST /api/orders/{orderId}/payments/deposit
POST /api/orders/{orderId}/payments/remaining
```

Planned side effects when payment reaches `PAID`:

- Project start fee → project eligible for designer assignment
- Deposit → `orders.status = DEPOSIT_PAID`
- Remaining → order/project completion

See `support-docs/jira ref/FurniSpace_Order_Deposit_Payment_Stories.md` for product stories.
