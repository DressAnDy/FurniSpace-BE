# FurniSpace — Realtime Events (FE Implementation Guide)

> **Status:** Backend Phase 1–3 completed (SCRUM-466)  
> **Updated:** 2026-08-13  
> **Audience:** Frontend team  
> **BE spec source:** `docs/06_REALTIME_EVENTS_BE_SPEC.md` (if present in repo)

Backend đã push cross-role realtime events qua SignalR. FE **không cần polling** — listen hub và invalidate React Query cache khi nhận event.

---

## 1. Tóm tắt đã làm xong

| Phase | Scope | Status |
|-------|--------|--------|
| **P0** | Payload chuẩn, payment dual-hub, quotation/payment flows | Done |
| **P1** | Schedule, proposal, production, delivery, order complete | Done |
| **P2** | Hub auth fix, notification dedup, FE event catalog | Done |

**Không có DB migration.** Tất cả thay đổi ở Application/API layer.

---

## 2. Thay đổi quan trọng (FE cần biết)

### 2.1 Event name đã sửa (breaking nếu FE listen tên cũ)

| Trước (BE cũ) | Sau (BE mới — dùng cái này) |
|---------------|----------------------------|
| `proposal.final.selected` | `proposal.selected` |
| `quotation.revision.requested` | `quotation.revision_requested` |
| `production_request.assigned` | `production.request.assigned` |
| `payment.paid` | `payment.updated` |
| Schedule events → `notification.created` | `project_schedule.created` / `.updated` / `.confirmed` / `.completed` |

### 2.2 Event mới (trước đây BE không push)

| Event | Mô tả ngắn |
|-------|------------|
| `quotation.revised` | Sale revise quotation → Customer |
| `production.request.created` | Sale tạo production request |
| `production.request.completed` | Production hoàn thành |
| `order.updated` | Order status thay đổi |
| `order.delivered` | Tất cả items đã giao + customer confirm |
| `order.completed` | Sale complete order/project |
| `order.item.delivery_updated` | Sale cập nhật delivered qty |
| `order.item.delivery_confirmed` | Customer xác nhận nhận hàng item |

### 2.3 Payment hub — thay đổi kết nối

| Trước | Sau |
|-------|-----|
| Chỉ nhận event nếu gọi `JoinPayment(paymentId)` | **Connect hub là đủ** — auto join `user:{accountId}` |
| `?access_token=` không work trên `/hubs/payments` | **Đã fix** — cả 3 hub đều nhận query token |
| `payment.updated` chỉ trên PaymentHub | **Cũng push** trên NotificationsHub (in-app + cache invalidation) |

**Khuyến nghị FE:**
- Luôn mount **NotificationsHub** global (`RealtimeSyncProvider`).
- **PaymentHub:** connect khi vào màn payment; `JoinPayment` vẫn optional (fine-grained update trên payment detail).
- Sale nhận `payment.updated` trên NotificationsHub **không cần** biết `paymentId` trước.

### 2.4 Payload chuẩn hóa

Tất cả event trên `/hubs/notifications` dùng shape thống nhất (camelCase):

```typescript
interface RealtimeNotificationPayload {
  notificationId: string | null;   // null = realtime-only (không lưu DB)
  title: string;
  message: string | null;
  notificationType: string;            // enum name, e.g. "PaymentPaid"
  projectId: string | null;
  referenceType: string | null;
  referenceId: string | null;
  createdAt: string;                   // ISO-8601
  occurredAt: string;                  // ISO-8601
  metadata?: Record<string, unknown>;  // optional — xem §4
}
```

**Realtime-only events** (`project.status.changed`, `project_schedule.*`, `order.item.*`):
- `notificationId = null`
- Vẫn có đủ `projectId`, `referenceType`, `referenceId`, `occurredAt`
- **Không** hiện trong notification bell (không persist DB)

---

## 3. Hub endpoints

| Hub | URL | Auth | Auto-join on connect |
|-----|-----|------|----------------------|
| Notifications | `{API_BASE}/hubs/notifications` | JWT (Bearer / cookie / `?access_token=`) | `user:{accountId}`, `role:{ROLE}` |
| Payments | `{API_BASE}/hubs/payments` | JWT — roles: CUSTOMER, SALES, DESIGNER, ADMIN | `user:{accountId}` |
| Project Chat | `{API_BASE}/hubs/project-chat` | JWT | `user:{accountId}` |

### 3.1 Kết nối (TypeScript)

```typescript
import * as signalR from "@microsoft/signalr";

const accessToken = "..."; // từ login / cookie bridge

// Global — mount 1 lần trong RealtimeSyncProvider
const notificationConnection = new signalR.HubConnectionBuilder()
  .withUrl(`${API_BASE}/hubs/notifications`, {
    accessTokenFactory: () => accessToken,
  })
  .withAutomaticReconnect()
  .build();

// Payment — mount khi vào payment screens (optional JoinPayment)
const paymentConnection = new signalR.HubConnectionBuilder()
  .withUrl(`${API_BASE}/hubs/payments`, {
    accessTokenFactory: () => accessToken,
  })
  .withAutomaticReconnect()
  .build();

await notificationConnection.start();
await paymentConnection.start();

// Optional: join payment group khi mở payment detail
await paymentConnection.invoke("JoinPayment", paymentId);
```

### 3.2 Client methods (PaymentHub)

| Method | Params | Mô tả |
|--------|--------|-------|
| `JoinPayment` | `paymentId: Guid` | Join group `payment:{paymentId}` — cần quyền access |
| `LeavePayment` | `paymentId: Guid` | Rời group |

---

## 4. referenceType & metadata

### 4.1 referenceType → invalidate query

| referenceType | Invalidate |
|---------------|------------|
| `PROJECT` | Project list, project detail |
| `PROJECT_SCHEDULE` | Schedule list, schedule detail |
| `PROPOSAL` | Proposal list, proposal detail |
| `QUOTATION` | Quotation list, quotation detail |
| `PAYMENT` | Payment list, payment detail, start-fee status |
| `ORDER` | Order list, order detail |
| `PRODUCTION_REQUEST` | Production request list |

Logic invalidate hiện tại của FE (theo `projectId` + `referenceType` + `referenceId`) vẫn đúng.

### 4.2 metadata (optional, forward-compatible)

```typescript
interface RealtimeMetadata {
  paymentType?: "PROJECT_START_FEE" | "DEPOSIT" | "REMAINING_PAYMENT";
  orderId?: string;
  orderItemId?: string;
  quotationId?: string;
  proposalId?: string;
  scheduleId?: string;
  productionRequestId?: string;
  assignedToAccountId?: string;
  newProjectStatus?: string;   // e.g. "PROPOSAL_SELECTED", "DELIVERING"
  orderStatus?: string;
}
```

Dùng khi cần invalidate granular hơn trong tương lai. MVP: invalidate theo `referenceType` là đủ.

---

## 5. Event catalog (đầy đủ)

### 5.1 In-app + Realtime (bell + cache refresh)

Listen trên `/hubs/notifications`. Persist DB → có `notificationId`.

| Event name | Khi nào BE push | Ai nhận (typical) |
|------------|-----------------|-------------------|
| `project.request.submitted` | Customer submit project | Sales, Admin |
| `project.request.accepted` | Admin/Sales accept | Customer |
| `project.more_information.requested` | Cần thêm info | Customer |
| `project.basic_information.updated` | Customer cập nhật info | Sales |
| `project.designer.assigned` | Assign designer | Designer |
| `proposal.published` | Designer publish proposal | Customer |
| `proposal.selected` | Customer chọn proposal | Sales, Designer |
| `quotation.sent` | Sales gửi quotation | Customer |
| `quotation.revised` | Sales revise quotation | Customer |
| `quotation.revision_requested` | Customer yêu cầu sửa | Sales |
| `quotation.rejected` | Customer reject | Sales |
| `quotation.accepted` | Customer accept | Sales |
| `payment.created` | Tạo payment (start fee / deposit / remaining) | Customer (+ metadata) |
| `payment.updated` | Payment paid / status change | Customer + Sale |
| `payment.processing` | Payment đang xử lý | Customer |
| `payment.expired` | Payment hết hạn | Customer |
| `payment.cancelled` | Payment bị hủy | Customer |
| `payment.transaction.failed` | Thanh toán thất bại | Customer |
| `payment.transaction.cancelled` | Transaction bị hủy | Customer |
| `order.updated` | Order status change | Customer, Sale |
| `order.delivered` | Order fully delivered | Customer, Sale |
| `order.completed` | Order/project completed | Customer, Sale |
| `production.request.created` | Tạo production request | Sale, Production staff |
| `production.request.assigned` | Assign production | Assigned staff, Sale |
| `production.request.completed` | Production xong | Sale, Customer |

> **Lưu ý:** `order.deposit.paid` vẫn tồn tại nội bộ BE nhưng flow deposit paid giờ cũng emit `payment.updated` — FE nên listen `payment.updated` cho deposit.

### 5.2 Realtime-only (cache refresh, không bell)

| Event name | Khi nào | Ai nhận |
|------------|---------|---------|
| `project.status.changed` | Project status đổi | Stakeholders (Customer, Sale, …) |
| `project_schedule.created` | Tạo schedule | Customer, Sale, Designer |
| `project_schedule.updated` | Sửa schedule | Customer, Sale, Designer |
| `project_schedule.confirmed` | Customer confirm | Sale, Designer |
| `project_schedule.completed` | Schedule hoàn thành | Customer, Sale, Designer |
| `order.item.delivery_updated` | Sale update delivered qty | Customer |
| `order.item.delivery_confirmed` | Customer confirm item | Sale |

### 5.3 Payment hub event

| Event | Hub | Payload |
|-------|-----|---------|
| `payment.updated` | `/hubs/payments` | `PaymentUpdatedRealtimeDto` (xem §6) |

Cũng mirror trên NotificationsHub dưới tên `payment.updated` với payload §2.4.

---

## 6. Payment hub payload

Event: `payment.updated`

```typescript
interface PaymentUpdatedRealtimePayload {
  paymentId: string;
  projectId: string;
  paymentCode: string;
  status: "PENDING" | "PROCESSING" | "PAID" | "EXPIRED" | "CANCELLED" | null;
  amount: number;
  paidAmount: number;
  remainingAmount: number;
  paymentTransactionId: string;
  transactionAmount: number;
  appliedAmount: number;
  paidAt: string | null;      // ISO-8601
  occurredAt: string;         // ISO-8601
}
```

**FE handler gợi ý:**

```typescript
paymentConnection.on("payment.updated", (payload: PaymentUpdatedRealtimePayload) => {
  queryClient.invalidateQueries({ queryKey: ["payments"] });
  queryClient.invalidateQueries({ queryKey: ["payments", payload.paymentId] });
  queryClient.invalidateQueries({ queryKey: ["projects", payload.projectId] });
  queryClient.invalidateQueries({ queryKey: ["projects", payload.projectId, "start-fee-status"] });
});
```

---

## 7. Luồng nghiệp vụ → event (test matrix)

Test với **2 browser / 2 role**. UI cập nhật trong ~3s, không refresh.

| # | Actor | Action | Observer | Event bắt buộc |
|---|-------|--------|----------|----------------|
| 1 | Sale | Create start fee | Customer | `payment.created` |
| 2 | Customer | Pay start fee | Sale | `payment.updated` |
| 3 | Sale/Designer | Create schedule | Customer | `project_schedule.created` |
| 4 | Customer | Confirm schedule | Sale, Designer | `project_schedule.confirmed` |
| 5 | Sale/Designer | Complete schedule | Customer | `project_schedule.completed` |
| 6 | Customer | Select proposal | Sale | `proposal.selected` + `project.status.changed` |
| 7 | Sale | Send quotation | Customer | `quotation.sent` |
| 8 | Customer | Accept quotation | Sale | `quotation.accepted` |
| 9 | Customer | Pay deposit | Sale | `payment.updated` |
| 10 | Sale | Assign production | Production | `production.request.assigned` |
| 11 | Production | Complete request | Sale | `production.request.completed` + `order.updated` |
| 12 | Sale | Update delivery qty | Customer | `order.item.delivery_updated` |
| 13 | Customer | Confirm delivery | Sale | `order.item.delivery_confirmed` |
| 14 | Sale | Create remaining payment | Customer | `payment.created` |
| 15 | Customer | Pay remaining | Sale | `payment.updated` |
| 16 | Sale | Complete order | Customer | `order.completed` + `project.status.changed` |

---

## 8. FE implementation checklist

### 8.1 Global setup (đã có — verify lại)

- [ ] `RealtimeSyncProvider` mount NotificationsHub khi user logged in
- [ ] Reconnect on token refresh (`withAutomaticReconnect` + re-start sau login)
- [ ] Pass `accessTokenFactory` hoặc cookie credentials

### 8.2 Event listeners (`useNotifications.ts`)

Verify listen **đúng tên event** (§5):

```typescript
const IN_APP_EVENTS = [
  "payment.created",
  "payment.updated",           // NOT payment.paid
  "proposal.selected",         // NOT proposal.final.selected
  "quotation.revision_requested", // underscore, NOT dot
  "quotation.revised",
  "production.request.created",
  "production.request.assigned",  // NOT production_request.assigned
  "production.request.completed",
  "order.updated",
  "order.delivered",
  "order.completed",
  // ... các event còn lại §5.1
];

const REALTIME_ONLY_EVENTS = [
  "project.status.changed",
  "project_schedule.created",
  "project_schedule.updated",
  "project_schedule.confirmed",
  "project_schedule.completed",
  "order.item.delivery_updated",
  "order.item.delivery_confirmed",
];
```

Handler chung:

```typescript
function handleRealtimeEvent(eventName: string, payload: RealtimeNotificationPayload) {
  // 1. Invalidate cache theo referenceType
  invalidateByReference(payload.projectId, payload.referenceType, payload.referenceId);

  // 2. In-app only: refresh bell nếu có notificationId
  if (payload.notificationId) {
    queryClient.invalidateQueries({ queryKey: ["notifications"] });
    queryClient.invalidateQueries({ queryKey: ["notifications", "unread-count"] });
  }
}
```

### 8.3 Payment screens (`usePayments.ts`)

- [ ] Connect PaymentHub khi mount payment detail/list
- [ ] Listen `payment.updated` — **không bắt buộc** `JoinPayment` nữa cho cross-role sync
- [ ] `JoinPayment` vẫn dùng nếu cần realtime trên payment detail page (optional)

### 8.4 Pages affected (invalidate targets)

| Page / Hook | Events trigger invalidate |
|-------------|---------------------------|
| Customer project list/detail | `project.status.changed`, `payment.*`, `proposal.*`, `quotation.*` |
| Sale project overview | `payment.updated`, `proposal.selected`, `quotation.*`, `production.*` |
| Start fee status | `payment.updated`, `project.status.changed` |
| Schedule tabs | `project_schedule.*` |
| Quotation pages | `quotation.sent/revised/revision_requested/accepted/rejected` |
| Order / delivery tracking | `order.*`, `order.item.*` |
| Production requests | `production.request.*` |
| Notification bell | Tất cả in-app events (có `notificationId`) |

### 8.5 Không dùng polling

BE đã push đủ event. **Remove** mọi `refetchInterval` fallback trên các trang trên.

---

## 9. Dedup behavior (INF-08)

BE dedup in-app notifications khi webhook retry:
- Key: `receiverId + notificationType + referenceType + referenceId`
- Trùng → skip persist + skip SignalR push

**FE impact:** Bell không bị duplicate khi payment webhook gọi 2 lần. Realtime-only events **không** dedup (có thể nhận 2 lần nếu action lặp — hiếm).

---

## 10. Out of scope / chưa làm

| Item | Ghi chú |
|------|---------|
| Admin dashboard live updates | Optional — chưa push role ADMIN broadcast |
| Redis SignalR backplane | Single instance OK cho MVP |
| `production_item.cancelled` | BE có event riêng — FE chưa trong spec gốc, có thể listen thêm |
| `customization_request.*` | BE có — ngoài spec FE gốc |
| `notification.created` | Default fallback cho template thiếu event name — không dùng làm primary handler |

---

## 11. Troubleshooting

| Symptom | Nguyên nhân | Fix |
|---------|-------------|-----|
| Payment hub 401 | Token hết hạn / thiếu query token | Refresh token; dùng `accessTokenFactory` |
| Sale không thấy payment update | Listen tên cũ `payment.paid` | Đổi → `payment.updated` |
| Schedule không refresh | Listen `notification.created` | Đổi → `project_schedule.*` |
| Proposal select không refresh Sale | Listen `proposal.final.selected` | Đổi → `proposal.selected` |
| Bell duplicate (hiếm) | Webhook retry trước Phase 3 | Đã fix BE dedup — redeploy BE mới |

---

## 12. Related BE files (reference)

| File | Role |
|------|------|
| `src/FurniSpace.API/Hubs/NotificationsHub.cs` | Notification hub |
| `src/FurniSpace.API/Hubs/PaymentHub.cs` | Payment hub |
| `src/FurniSpace.Application/Services/Notifications/NotificationDispatcher.cs` | Push + persist + dedup |
| `src/FurniSpace.Application/Common/Notifications/NotificationTemplateProvider.cs` | Event name mapping |
| `src/FurniSpace.Application/Common/Payments/PaymentNotificationSupport.cs` | Payment notify Sale + Customer |
| `src/FurniSpace.Application/Common/Orders/OrderNotificationSupport.cs` | Order/delivery notify |
| `src/FurniSpace.Application/DTOs/Notifications/RealtimeNotificationPayloadDto.cs` | Payload shape |

---

## 13. Record of Changes

| Date | Change |
|------|--------|
| 2026-08-13 | Initial FE guide — Phase 1–3 BE complete |
