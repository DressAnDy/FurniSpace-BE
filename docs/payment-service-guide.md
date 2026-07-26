# FurniSpace Payment Service Guide

This guide explains how FurniSpace collects payments through **SePay (VietQR / bank transfer)** and **PayOS (payment link / checkout)**, persists records in PostgreSQL, dispatches in-app notifications, and notifies clients over SignalR.

Related specs:

- `support-docs/jira ref/FurniSpace_Payment_Management_Stories_Final_Corrected.md`
- `support-docs/FurniSpace_SePay_Service_Implementation_Plan (1).md`
- `support-docs/FurniSpace_PayOS_Service_Implementation_Plan.md`

## 1. Scope

Current implementation supports:

| Area | Status |
|---|---|
| Payment list / summary / detail (CP1–CP3) | Implemented |
| Payment transaction history (CP4) | Implemented |
| Unified customer transaction attempt (CP5) | Implemented |
| Active transaction + cancel attempt (CP6–CP7) | Implemented |
| Inline expired-payment sync (CP8) | Implemented |
| Customer payment notifications (CP9) | Implemented |
| SePay VietQR + webhook | Implemented |
| PayOS payment link + webhook | Implemented |
| Project start fee / order deposit / remaining payment creation | Implemented |
| Order / project side effects on `PAID` | Partial (deposit + start fee) |
| **Partial payment** | **Removed — full payment only** |

Request flow:

```text
API Controller (PaymentsController)
  -> Application service (PaymentService / webhook handlers)
  -> Infrastructure repository
  -> PostgreSQL (payments, payment_transactions)
  -> ServiceResult<T>

Webhook (provider -> API)
  -> SePayWebhookHandler / PayOsWebhookHandler
  -> Update payment + transaction
  -> PaymentBusinessEffectService (order/project effects + notifications)
  -> IPaymentRealtimeService (SignalR)
```

**Important:** Webhooks are the source of truth for payment confirmation. Return URLs (PayOS) and manual refresh are for UI only.

## 2. Environment Variables

See previous sections for `SePayOptions` and `PayOsOptions`. Key PayOS URLs for production FE:

```env
PAYOS_RETURN_URL=https://furni-space-fe.vercel.app/payments/result
PAYOS_CANCEL_URL=https://furni-space-fe.vercel.app/payments/cancel
```

**Never expose** PayOS keys or `SEPAY_WEBHOOK_SECRET` to the frontend.

## 3. REST Endpoints (`/api/payments`)

Base controller: `PaymentsController`. All routes require JWT unless noted.

### 3.1 Customer payment management (primary FE flow)

```http
GET    /api/payments?page=1&pageSize=20&projectId=&orderId=&status=&paymentType=
GET    /api/payments/summary
GET    /api/payments/{paymentId}
GET    /api/payments/{paymentId}/transactions
POST   /api/payments/{paymentId}/transactions          # CUSTOMER only
GET    /api/payments/{paymentId}/transactions/active   # CUSTOMER only
PATCH  /api/payments/{paymentId}/transactions/{paymentTransactionId}/cancel  # CUSTOMER only
```

**List response** includes enriched items (`projectCode`, `projectName`, `orderCode`, `isPayable`) and pagination (`page`, `pageSize`, `totalItems`, `totalPages`).

**Detail response** includes nested `project`, `order`, `latestTransaction`, and `isPayable`.

**Create transaction (CP5)** — CUSTOMER only:

PayOS:

```json
{
  "paymentProvider": "PAYOS",
  "paymentMethod": "PAYMENT_LINK",
  "returnUrl": "https://furni-space-fe.vercel.app/payments/result",
  "cancelUrl": "https://furni-space-fe.vercel.app/payments/cancel"
}
```

SePay:

```json
{
  "paymentProvider": "SEPAY",
  "paymentMethod": "QR_CODE"
}
```

- Transaction amount/currency are copied from the Payment (customer cannot override).
- Reuses an existing PENDING attempt with the same provider/method when still valid (same `paymentTransactionId`).
- PayOS requires valid HTTPS `returnUrl` and `cancelUrl`.

### 3.2 Business payment creation (Sales/System)

```http
POST /api/projects/{projectId}/payments/project-start-fee
POST /api/projects/orders/{orderId}/payments/deposit
POST /api/projects/orders/{orderId}/payments/remaining
POST /api/test/payments   # ADMIN only — development
```

### 3.3 Legacy collection endpoints (deprecated for new FE)

```http
POST /api/payments/{paymentId}/sepay/vietqr
POST /api/payments/{paymentId}/payos/payment-link
GET  /api/payments/code/{paymentCode}/status
```

Prefer `POST /api/payments/{paymentId}/transactions` for customer checkout.

### 3.4 Webhooks (providers call these)

```http
POST /api/webhooks/sepay
POST /api/webhooks/payos
POST /api/admin/payments/payos/confirm-webhook   # ADMIN
```

## 4. Access Rules

| Role | List/detail/transactions | Summary | Create online attempt | Cancel attempt |
|---|---|---|---|---|
| `CUSTOMER` | Own payments (`paid_by`) | Yes | Yes | Yes |
| `SALES` | Assigned project/order scope | Yes | No | No |
| `DESIGNER` | Assigned project (read-only) | No | No | No |
| `ADMIN` | All | Yes | No | No |

## 5. Payment Model (full amount only)

### 5.1 `payment_status`

```text
PENDING | PROCESSING | PAID | CANCELLED | EXPIRED | REFUNDED
```

- No `PARTIALLY_PAID` or `FAILED` on Payment.
- `payments` does **not** store `paid_amount` or `remaining_amount`.
- One Payment may have multiple transaction attempts; at most one SUCCESS transaction.

### 5.2 Expiry sync (CP8)

Before list, detail, and create-attempt operations, backend synchronizes expired payments inline.

## 6. Notifications (CP9)

Customer notifications via `INotificationDispatcher`: `PaymentCreated`, `PaymentProcessing`, `PaymentPaid`, `PaymentExpired`, `PaymentTransactionFailed`, `PaymentTransactionCancelled`.

## 7. Implementation Files

- `src/FurniSpace.API/Controllers/Payments/PaymentsController.cs`
- `src/FurniSpace.Application/Services/Payments/PaymentService.cs`
- `src/FurniSpace.Application/Common/Payments/*`
- `src/FurniSpace.Infrastructure/Repositories/Repository/PaymentRepository.cs`

See also `docs/backend-api-dev-guide.md` section 14 (Payment Management).
