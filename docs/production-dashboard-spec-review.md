# Production Dashboard Spec — Gap Review

So sánh `docs/api-spec-production-dashboard.md` với backend hiện tại (`ProductionDashboardController`, `DashboardQueueService`, `DashboardQueueReadRepository`, phase-deadline DTOs).

**Verdict:** Đã implement P0a/P0b/P1 theo quyết định §6 (2026-08-30). Không migration. Mở rộng 3 endpoint sẵn có.

| Câu hỏi | Kết luận |
| --- | --- |
| Có cần migration? | **Không** |
| Có trùng API? | **3/3 endpoint đã có** — chỉ mở rộng |
| Impact lớn? | **Trung bình**, khu vực read-layer |
| Quyết định mục 6? | **Đã chốt** (xem §6) |

---

## 1. Trùng / sẵn có?

| Spec | Hiện có | Hành động | Ghi chú |
| --- | --- | --- | --- |
| `GET /api/dashboard/production/kpis` | Có — `ProductionDashboardController` | Mở rộng fields | Không thêm `unavailableItems` (đã bỏ) |
| `GET /api/dashboard/production/queue` | Có — cùng controller | Mở rộng item + filter | P0 đủ 3 `workType` kể cả DELIVERY |
| `GET /api/dashboard/project-phase-deadlines` | Có — `ProjectPhaseDeadlineDashboardController` | Thêm `productionId`; **không** dùng `phase=DELIVERY` timeline | Delivery timing → `ProjectSchedule` |
| Customization queue riêng | `GET /api/production/customization-versions` | Giữ + aggregate vào dashboard | Không thay thế |
| Unavailable items riêng | `GET /production-items/unavailable` | Giữ API riêng; **không** đưa vào dashboard đợt này | Flow UNAVAILABLE deferred |

**Bug / stub sẵn có — phải sửa khi đụng KPI:** `ReadyToComplete` trong `DashboardQueueReadRepository` đang `CountAsync(request => false)` → luôn **0**. Fix bắt buộc theo định nghĩa: `IN_PRODUCTION` + mọi item terminal.

---

## 2. Có cần migration?

**Không.**

- P0 KPI/queue: read projection trên entities hiện có.
- Delivery due: `ProjectSchedule.ScheduledEnd` (type `DELIVERY`) — không ghi `ProjectPhaseTimeline.DELIVERY`.
- Không thêm cột “resolved” cho unavailable (metric đã bỏ khỏi dashboard).

---

## 3. Gap theo field / filter

### 3.1 KPI — `GET /api/dashboard/production/kpis`

| Field | BE hôm nay | Effort |
| --- | --- | --- |
| `pendingReview` | Có (= PENDING requests) | Giữ alias = `pendingStart` |
| `pendingStart` | Thiếu tên | Thêm |
| `inProduction` | Có | Giữ |
| `readyToComplete` | Có nhưng luôn 0 (stub) | **Fix query** |
| `overdueTasks` | Có | Refine (PRODUCTION deadline) |
| `pendingCustomizationReview` | Thiếu | Count version `REVIEWING` + feasibility `PENDING` |
| `readyForDelivery` | Thiếu | Order `READY_FOR_DELIVERY` (+ ownership) |
| `awaitingDeliverySchedule` | Thiếu | Subset chưa có schedule DELIVERY |
| `completedInRange` | Thiếu | `ActualCompletionDate` + `dateRange` |
| `unavailableItems` | — | **Out of scope** đợt này |

### 3.2 Queue — `GET /api/dashboard/production/queue`

| Spec | BE hôm nay | Effort |
| --- | --- | --- |
| `workType` `PRODUCTION_REQUEST` | Implicit | Thêm field |
| `workType` `CUSTOMIZATION_REVIEW` | Không | Union; chỉ khi `scope=all` |
| `workType` `DELIVERY` | Không | P0 — semantics §6.3 |
| `links` / `entityId` | Không | Mapper |
| `countsByWorkType` / `countsByStatus` | Chỉ `countsByGroup` | Aggregate full filtered set |
| Filter `workType` / `status` / `dueBucket` | Một phần | Query DTO |
| Default `scope` | `mine` | **Giữ `mine`** (đã chốt) |
| Sort overdue → priority → `dueAt` | `OrderBy UpdatedAt` | Đổi sort rule |

### 3.3 Phase deadlines (P1)

| Spec | Quyết định | Effort |
| --- | --- | --- |
| `productionId` filter | Làm | Filter theo ProductionRequest assignee |
| `assignedProductionId` / `Name` | Đã có | Giữ |
| `phase=DELIVERY` trên timeline | **Không làm** | Delivery due nằm ở queue/KPI schedule; widget deadline Production dùng `phase=PRODUCTION` |

---

## 4. Phase build (sau khi chốt §6)

### Phase 0 — Quyết định

**Done.** Xem §6.

### Phase 1 — P0a KPI (1–2 ngày)

- Fix `ReadyToComplete` stub.
- Thêm `pendingStart` (+ alias `pendingReview`), `pendingCustomizationReview`, `completedInRange`.
- Thêm `readyForDelivery`, `awaitingDeliverySchedule`.
- **Không** thêm `unavailableItems`.
- Default `scope=mine`; customization KPI chỉ meaningful khi `scope=all` (hoặc luôn đếm global — document rõ).

### Phase 2 — P0b Unified queue (2–3 ngày)

- Union 3 `workType`: `PRODUCTION_REQUEST`, `CUSTOMIZATION_REVIEW`, `DELIVERY`.
- `CUSTOMIZATION_REVIEW` chỉ xuất hiện khi `scope=all`.
- DELIVERY theo semantics §6.3 (`AWAITING_SCHEDULE` / `SCHEDULED` / `IN_PROGRESS` / `AWAITING_CUSTOMER_CONFIRMATION`).
- `links`, `countsByWorkType` / `countsByStatus`, filters, sort.

### Phase 3 — P1 Phase deadlines (≤1 ngày)

- Thêm `productionId` query filter.
- **Không** support `phase=DELIVERY` trên endpoint này (hoặc 400 rõ nếu FE gửi) — delivery timing dùng schedule trong queue.
- FE overdue widget: `phase=PRODUCTION` + `productionId`.

---

## 5. Impact

### Thấp–trung bình

- Không đổi domain write flow production/delivery/payment.
- Route giữ nguyên → FE migrate dần.
- Rủi ro chính: union 3 nguồn + counts/pagination đúng trên full filtered set.

### Cần chú ý

- Giữ default `scope=mine` — không breaking silent.
- `actionPath` FE route có thể phá deep-link cũ nếu đổi — đồng bộ FE cùng PR hoặc giữ tương thích tạm.
- Shared `DashboardQueueItemDto`: field mới optional.

---

## 6. Quyết định đã chốt

### 6.1 Default `scope` của Production dashboard

```text
scope = mine
```

Giữ behavior hiện tại. Khi FE cần xem toàn bộ workload thì truyền `scope=all`. Không đổi default sang `all` (tránh FE silently thấy nhiều dữ liệu hơn dự kiến).

### 6.2 `scope=mine` với Customization Review

```text
CUSTOMIZATION_REVIEW
→ chỉ nằm trong scope=all

scope=mine
→ không filter / không trả customization theo assignee
```

MVP không invent ownership cho customization nếu domain chưa có assignment tương đương `ProductionRequest`.

| KPI / queue item | `scope=all` | `scope=mine` |
| --- | --- | --- |
| Customization review | Có (global PENDING review) | Không gồm |
| Production request | Tất cả | `assignedTo = currentUser` |
| Delivery | Tất cả (eligible) | Assignee của ProductionRequest liên quan |

### 6.3 Unified queue — đủ 3 `workType` trong P0

```text
P0:
- PRODUCTION_REQUEST
- CUSTOMIZATION_REVIEW
- DELIVERY
```

#### DELIVERY semantics (đã chốt)

| Quy tắc | Chi tiết |
| --- | --- |
| Xuất hiện từ | Order = `READY_FOR_DELIVERY` (và các trạng thái delivery tiếp theo còn active theo status dưới) |
| Ownership `scope=mine` | Production assignee của `ProductionRequest` |
| `dueAt` | `ProjectSchedule.ScheduledEnd` (type DELIVERY); `null` nếu chưa có schedule |
| Overdue | `ScheduledEnd < now` **và** delivery chưa `COMPLETED` |

| Điều kiện | Queue `status` |
| --- | --- |
| `READY_FOR_DELIVERY` + chưa có schedule DELIVERY | `AWAITING_SCHEDULE` |
| Có DELIVERY schedule `CONFIRMED` + chưa có batch | `SCHEDULED` |
| Có Delivery batch `IN_PROGRESS` | `IN_PROGRESS` |
| Giao xong vật lý nhưng Order còn chờ Customer confirm | `AWAITING_CUSTOMER_CONFIRMATION` |

> Khác bản draft cũ (`READY_FOR_BATCH` / `COMPLETED` trên queue): dùng bộ status trên. `COMPLETED` delivery không cần nằm trong active queue (hoặc chỉ khi FE filter lịch sử — optional, không bắt P0).

### 6.4 `unavailableItems`

```text
BỎ khỏi scope hiện tại
```

Không thêm KPI, không filter, không AC liên quan unavailable trong Production Dashboard đợt này.

Lý do: flow UNAVAILABLE chưa chốt hoàn chỉnh — dashboard không định nghĩa metric cho business flow đang deferred. API `GET /production-items/unavailable` vẫn tồn tại độc lập.

### 6.5 Nguồn deadline DELIVERY / phase deadlines

```text
Không dùng ProjectPhaseTimeline.DELIVERY lúc này.

Production Deadline → ProjectPhaseTimeline(PRODUCTION)
Delivery timing    → ProjectSchedule.DELIVERY.ScheduledEnd
```

- Không tạo / ghi Delivery phase deadline riêng trên `project_phase_timelines`.
- Endpoint `project-phase-deadlines`: mở rộng `productionId`; **không** yêu cầu `phase=DELIVERY` trong P1.
- Sau này nếu business cần “toàn project giao xong trước ngày X” độc lập từng schedule → mới cân nhắc `ProjectPhaseTimeline.DELIVERY`.

### Quyết định phụ (chưa bắt buộc chốt để start P0a)

- Timezone: server UTC vs `Asia/Ho_Chi_Minh`.
- `actionPath`: FE route vs FE tự build từ `links`.
- `overdueTasks` (KPI production): có tính `PENDING` chưa start hay chỉ `IN_PRODUCTION` — draft spec đang gồm cả hai.

---

## 7. Không làm / ngoài scope (đợt này)

- Migration schema.
- Endpoint KPI/queue mới (chỉ mở rộng 3 route hiện có).
- KPI / filter / AC `unavailableItems`.
- Ghi `ProjectPhaseTimeline.DELIVERY` / `phase=DELIVERY` trên deadline dashboard.
- SignalR khi KPI đổi.
- Chat project cho role PRODUCTION.
- Chart trend / breakdown priority riêng (P2).

---

## 8. Tóm tắt deliverable

| Priority | Endpoint | Việc cần làm |
| --- | --- | --- |
| P0 | `GET /api/dashboard/production/kpis` | Fix stub + fields mới (không `unavailableItems`); `scope` default `mine` |
| P0 | `GET /api/dashboard/production/queue` | 3 `workType` + DELIVERY semantics §6.3 + links/counts/filters |
| P1 | `GET /api/dashboard/project-phase-deadlines` | Thêm `productionId`; không `phase=DELIVERY` timeline |

### Sources

- `docs/api-spec-production-dashboard.md` (cần đồng bộ theo §6)
- Quyết định product/BE chốt 2026-08-30 (mục 6)
- `ProductionDashboardKpisDto`, `DashboardQueueReadRepository`, `ProjectPhaseDeadlineRiskDtos`, `ProjectPhaseType`, `ProjectSchedule`
