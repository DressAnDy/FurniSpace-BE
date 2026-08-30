# API Spec — Production Dashboard (P0 + P1)

> Mục tiêu: đủ API để FE làm Production ops dashboard theo 2 luồng độc lập:
>
> - **A.** Customization feasibility review (trước Order)
> - **B.** Production request → complete → delivery schedule/batch (sau Deposit)
>
> Audience: Backend. FE sẽ consume các endpoint dưới đây (mở rộng từ API hiện có).
>
> Base URL: giống các dashboard hiện tại (`/api/dashboard/...`). Auth: Bearer JWT, role `PRODUCTION` (và `ADMIN` nếu cần read-all).
>
> Response envelope: giữ `ServiceResult<T>` như hiện tại.

**Quyết định đã chốt** (2026-08-30) — chi tiết `docs/production-dashboard-spec-review.md` §6:

1. Default `scope=mine`
2. `CUSTOMIZATION_REVIEW` chỉ trong `scope=all`
3. P0 đủ 3 `workType` kể cả DELIVERY (semantics schedule-based)
4. Bỏ `unavailableItems` khỏi dashboard đợt này
5. Delivery timing = `ProjectSchedule.ScheduledEnd`; không dùng `ProjectPhaseTimeline.DELIVERY`

```json
{
  "status": 200,
  "message": "OK",
  "data": { }
}
```

---

## 0. Breaking change policy

Ưu tiên **backward-compatible**:

1. Giữ field KPI cũ `pendingReview` (deprecated alias) = cùng giá trị `pendingStart` trong 1–2 sprint.
2. Queue item hiện tại vẫn valid; thêm field mới (nullable / optional).
3. `project-phase-deadlines` thêm query param mới, không đổi contract cũ.

Sau khi FE migrate xong, BE có thể deprecate `pendingReview` trong changelog.

---

## 1. Shared query conventions

Dùng chung cho KPI + Queue (trừ khi ghi chú khác).

| Param | Type | Required | Values | Mô tả |
| --- | --- | --- | --- | --- |
| `scope` | string | no | `mine` \| `all` (**default `mine`**) | `mine` = production request / delivery có **assignedTo = current user**. `CUSTOMIZATION_REVIEW` **chỉ** xuất hiện khi `scope=all` (không invent ownership assignee). FE muốn toàn workload → truyền `scope=all`. |
| `dateRange` | string | no | `today` \| `thisWeek` \| `thisMonth` | Kỳ thống kê. Overdue **không** bị cắt bởi dateRange (luôn đếm active quá hạn). |
| `page` | int | no | default `1` | |
| `limit` | int | no | default `20`, max `100` | |

Timezone: dùng timezone server (hoặc VN `Asia/Ho_Chi_Minh`) — ghi rõ trong response header hoặc docs nội bộ.

---

## 2. P0 — KPIs

### `GET /api/dashboard/production/kpis`

#### Query

```
?scope=mine|all
&dateRange=today|thisWeek|thisMonth
```

#### Response `data`

```json
{
  "pendingCustomizationReview": 7,
  "pendingStart": 12,
  "pendingReview": 12,
  "inProduction": 9,
  "readyToComplete": 3,
  "overdueTasks": 4,
  "readyForDelivery": 5,
  "awaitingDeliverySchedule": 2,
  "completedInRange": 8
}
```

> **Out of scope đợt này:** `unavailableItems` — không trả field này. Flow UNAVAILABLE deferred; API `GET /production-items/unavailable` vẫn độc lập.

#### Field definitions

| Field | Definition (đếm) |
| --- | --- |
| `pendingCustomizationReview` | Customization versions: `status=REVIEWING` AND `feasibilityStatus=PENDING`. Không gồm DRAFT. Chỉ meaningful / đếm khi `scope=all` (khi `scope=mine` → `0`). |
| `pendingStart` | Production requests `status=PENDING` (chưa start). |
| `pendingReview` | **Deprecated alias** của `pendingStart` (giữ để FE cũ không vỡ). |
| `inProduction` | Production requests `status=IN_PRODUCTION`. |
| `readyToComplete` | Request `IN_PRODUCTION` và mọi item đã `COMPLETED` hoặc `CANCELLED` (chưa gọi complete request). |
| `overdueTasks` | Request active (`PENDING` \| `IN_PRODUCTION`) có phase PRODUCTION deadline `< now` và chưa complete. |
| `readyForDelivery` | Order ở trạng thái sẵn sàng giao / đang giao (vd. từ `READY_FOR_DELIVERY`) theo ownership scope — xem queue DELIVERY. |
| `awaitingDeliverySchedule` | Subset: Order `READY_FOR_DELIVERY` và chưa có schedule type `DELIVERY` active (không CANCELLED). |
| `completedInRange` | Production requests chuyển `COMPLETED` trong `dateRange`. |

#### Scope rules

| KPI | `scope=all` | `scope=mine` |
| --- | --- | --- |
| Customization pending | Global queue | **0** (không gồm customization) |
| pendingStart / inProduction / readyToComplete / overdue | Tất cả request | `assignedTo = currentUser` |
| readyForDelivery / awaitingDeliverySchedule | Tất cả eligible | ProductionRequest `assignedTo = currentUser` |
| completedInRange | Tất cả | `assignedTo = currentUser` |

#### Errors

| Status | Khi |
| --- | --- |
| 401 | Chưa auth |
| 403 | Role không phải PRODUCTION/ADMIN |

---

## 3. P0 — Unified work queue

### `GET /api/dashboard/production/queue`

Mở rộng endpoint hiện có (không tạo endpoint mới trừ khi BE muốn tách — FE chấp nhận cả 2 nếu contract giống).

#### Query

```
?scope=mine|all
&dateRange=today|thisWeek|thisMonth
&workType=CUSTOMIZATION_REVIEW|PRODUCTION_REQUEST|DELIVERY
&status=
&priority=HIGH|MEDIUM|LOW|URGENT
&dueBucket=OVERDUE|TODAY|THIS_WEEK|LATER
&search=
&page=1
&limit=20
```

| Param | Mô tả |
| --- | --- |
| `workType` | Filter 1 loại việc. Omit = tất cả. |
| `status` | Status theo từng workType (xem bảng dưới). |
| `dueBucket` | Filter theo bucket deadline PRODUCTION (hoặc DELIVERY khi workType=DELIVERY). |
| `search` | Match `projectCode`, `projectName`, `customerName`, `productionCode`, entity code. |

#### Response `data`

```json
{
  "items": [
    {
      "id": "cvr_01J...",
      "workType": "CUSTOMIZATION_REVIEW",
      "entityId": "cvr_01J...",
      "projectId": "prj_01J...",
      "projectCode": "PRJ-2026-014",
      "projectName": "Căn hộ Thảo Điền",
      "customerName": "Nguyễn A",
      "assigneeName": null,
      "group": "Production",
      "phase": "CUSTOMIZATION_FEASIBILITY",
      "status": "REVIEWING",
      "priority": "HIGH",
      "action": "Review feasibility",
      "actionPath": "/production/customization-reviews?versionId=cvr_01J...",
      "links": {
        "versionId": "cvr_01J...",
        "customizationRequestId": "cr_01J...",
        "projectId": "prj_01J...",
        "productionRequestId": null,
        "orderId": null
      },
      "dueAt": null,
      "dueBucket": null,
      "warning": "Material TBD",
      "lastUpdatedAt": "2026-08-30T10:15:00Z"
    },
    {
      "id": "pr_01J...",
      "workType": "PRODUCTION_REQUEST",
      "entityId": "pr_01J...",
      "projectId": "prj_01J...",
      "projectCode": "PRJ-2026-014",
      "projectName": "Căn hộ Thảo Điền",
      "customerName": "Nguyễn A",
      "assigneeName": "Trần Production",
      "group": "Production",
      "phase": "IN_PRODUCTION",
      "status": "IN_PRODUCTION",
      "priority": "URGENT",
      "action": "Update items / complete",
      "actionPath": "/production/requests/pr_01J...",
      "links": {
        "versionId": null,
        "customizationRequestId": null,
        "projectId": "prj_01J...",
        "productionRequestId": "pr_01J...",
        "orderId": "ord_01J..."
      },
      "dueAt": "2026-09-05T17:00:00Z",
      "dueBucket": "THIS_WEEK",
      "warning": null,
      "lastUpdatedAt": "2026-08-29T08:00:00Z"
    },
    {
      "id": "ord_01J...:delivery",
      "workType": "DELIVERY",
      "entityId": "ord_01J...",
      "projectId": "prj_01J...",
      "projectCode": "PRJ-2026-014",
      "projectName": "Căn hộ Thảo Điền",
      "customerName": "Nguyễn A",
      "assigneeName": "Trần Production",
      "group": "Production",
      "phase": "READY_FOR_DELIVERY",
      "status": "AWAITING_SCHEDULE",
      "priority": "MEDIUM",
      "action": "Create DELIVERY schedule / batch",
      "actionPath": "/production/ready-for-delivery?orderId=ord_01J...",
      "links": {
        "versionId": null,
        "customizationRequestId": null,
        "projectId": "prj_01J...",
        "productionRequestId": "pr_01J...",
        "orderId": "ord_01J..."
      },
      "dueAt": null,
      "dueBucket": null,
      "warning": "No DELIVERY schedule",
      "lastUpdatedAt": "2026-08-28T12:00:00Z"
    }
  ],
  "countsByWorkType": {
    "CUSTOMIZATION_REVIEW": 7,
    "PRODUCTION_REQUEST": 21,
    "DELIVERY": 5
  },
  "countsByStatus": {
    "REVIEWING": 7,
    "PENDING": 12,
    "IN_PRODUCTION": 9,
    "READY_TO_COMPLETE": 3,
    "AWAITING_SCHEDULE": 2,
    "SCHEDULED": 1,
    "IN_PROGRESS": 1,
    "AWAITING_CUSTOMER_CONFIRMATION": 1
  },
  "countsByGroup": {
    "Production": 33
  },
  "page": 1,
  "limit": 20,
  "total": 33
}
```

#### `workType` × `status` mapping

| workType | status values (queue) | Nguồn thật |
| --- | --- | --- |
| `CUSTOMIZATION_REVIEW` | `REVIEWING` | version `REVIEWING` + feasibility `PENDING`. **Chỉ khi `scope=all`.** |
| `PRODUCTION_REQUEST` | `PENDING`, `IN_PRODUCTION`, `READY_TO_COMPLETE`, `COMPLETED` | Request status. `READY_TO_COMPLETE` = derived (IN_PRODUCTION + all items terminal). |
| `DELIVERY` | `AWAITING_SCHEDULE`, `SCHEDULED`, `IN_PROGRESS`, `AWAITING_CUSTOMER_CONFIRMATION` | Xem bảng DELIVERY dưới. |

#### DELIVERY workType (P0 — đã chốt)

Xuất hiện từ Order = `READY_FOR_DELIVERY` (và các bước delivery active tiếp theo).

| Điều kiện | `status` |
| --- | --- |
| Order `READY_FOR_DELIVERY` + chưa có schedule DELIVERY | `AWAITING_SCHEDULE` |
| Có DELIVERY schedule `CONFIRMED` + chưa có batch | `SCHEDULED` |
| Có Delivery batch `IN_PROGRESS` | `IN_PROGRESS` |
| Giao xong vật lý nhưng Order còn chờ Customer confirm | `AWAITING_CUSTOMER_CONFIRMATION` |

| Field | Rule |
| --- | --- |
| `scope=mine` ownership | Production assignee của `ProductionRequest` liên quan |
| `dueAt` | `ProjectSchedule.ScheduledEnd` (type DELIVERY); `null` nếu chưa có schedule |
| Overdue / `dueBucket=OVERDUE` | `ScheduledEnd < now` **và** delivery chưa hoàn tất (chưa qua customer confirm / chưa exit active queue) |

#### `actionPath` contract (FE routes)

BE **nên** trả path FE (không phải raw BE API path):

| workType | `actionPath` mẫu |
| --- | --- |
| `CUSTOMIZATION_REVIEW` | `/production/customization-reviews?versionId={versionId}` |
| `PRODUCTION_REQUEST` | `/production/requests/{productionRequestId}` |
| `DELIVERY` | `/production/ready-for-delivery?orderId={orderId}` hoặc `?productionRequestId={id}` |

Đồng thời trả `links` object để FE tự build nếu cần.

#### Sort mặc định

1. `dueBucket=OVERDUE` trước
2. Priority: `URGENT` > `HIGH` > `MEDIUM`/`NORMAL` > `LOW`
3. `dueAt` ASC (nulls last)
4. `lastUpdatedAt` DESC

#### Compatibility với FE hiện tại

Field cũ bắt buộc giữ: `id`, `projectId`, `projectCode`, `projectName`, `customerName`, `assigneeName`, `group`, `phase`, `status`, `priority`, `action`, `actionPath`, `dueAt`, `dueBucket`, `warning`, `lastUpdatedAt`, `countsByGroup`, `page`, `limit`, `total`.

Field mới optional: `workType`, `entityId`, `links`, `countsByWorkType`, `countsByStatus`.

---

## 4. P1 — Phase deadline risks (Production-aware)

### `GET /api/dashboard/project-phase-deadlines`

#### Query (mở rộng)

```
?phase=PROPOSAL|PRODUCTION
&status=OVERDUE|ON_TRACK|COMPLETED_ON_TIME|COMPLETED_LATE
&salesId=
&designerId=
&productionId=
&from=
&to=
&page=1
&limit=20
```

| Param mới / đổi | Mô tả |
| --- | --- |
| `productionId` | Filter theo staff Production được assign trên production request. |
| `phase=DELIVERY` | **Không support đợt này.** Delivery timing lấy từ `ProjectSchedule.DELIVERY.ScheduledEnd` (queue DELIVERY). Nếu FE gửi `phase=DELIVERY` → `400` rõ ràng. |

**Deadline sources (đã chốt):**

```text
Production Deadline → ProjectPhaseTimeline(PRODUCTION)
Delivery timing    → ProjectSchedule.DELIVERY.ScheduledEnd
                     (không ghi ProjectPhaseTimeline.DELIVERY)
```

#### Response item (giữ shape cũ, không phá)

```json
{
  "projectId": "prj_01J...",
  "projectCode": "PRJ-2026-014",
  "projectName": "Căn hộ Thảo Điền",
  "phase": "PRODUCTION",
  "dueDate": "2026-09-05T17:00:00Z",
  "completedAt": null,
  "projectStatus": "IN_PRODUCTION",
  "assignedSalesId": "acc_s1",
  "assignedSalesName": "Sales B",
  "assignedDesignerId": "acc_d1",
  "assignedDesignerName": "Designer C",
  "assignedProductionId": "acc_p1",
  "assignedProductionName": "Trần Production",
  "status": "OVERDUE",
  "group": "Overdue",
  "days": 3
}
```

#### Role access

| Role | Default filter khi omit assignee ids |
| --- | --- |
| `PRODUCTION` | Projects/requests liên quan Production (có production request / phase PRODUCTION). Optional: FE truyền `productionId=currentUser`. |
| `ADMIN` | Full |
| `SALES` / `DESIGNER` | Giữ behavior hiện tại |

#### FE usage (Production)

- Widget “Deadline risk”: `phase=PRODUCTION` (+ `productionId` khi cần).
- Delivery overdue / due: dùng **queue** `workType=DELIVERY` + `dueBucket` / `dueAt` từ schedule — không gọi `phase=DELIVERY` trên endpoint này.
- Tab Overdue production: deep-link KPI `overdueTasks` → `status=OVERDUE&phase=PRODUCTION`.

---

## 5. Acceptance criteria (BE)

### KPI

- [ ] Field mới: `pendingCustomizationReview`, `pendingStart`, `readyForDelivery`, `awaitingDeliverySchedule`, `completedInRange` (+ fix `readyToComplete`).
- [ ] **Không** trả `unavailableItems`.
- [ ] `pendingReview` vẫn trả về (= `pendingStart`) trong giai đoạn chuyển.
- [ ] Default `scope=mine`; customization KPI = 0 khi `mine`.
- [ ] `overdueTasks` chỉ đếm request active quá **PRODUCTION** phase deadline.
- [ ] Role PRODUCTION gọi được; role khác 403 (trừ ADMIN).

### Queue

- [ ] Đủ 3 `workType` trong P0: `PRODUCTION_REQUEST`, `CUSTOMIZATION_REVIEW`, `DELIVERY`.
- [ ] `CUSTOMIZATION_REVIEW` chỉ khi `scope=all`.
- [ ] DELIVERY statuses: `AWAITING_SCHEDULE` \| `SCHEDULED` \| `IN_PROGRESS` \| `AWAITING_CUSTOMER_CONFIRMATION`; `dueAt` từ schedule `ScheduledEnd`.
- [ ] `actionPath` / `links` đủ để FE mở đúng page không đoán ID.
- [ ] `countsByWorkType` (+ ideally `countsByStatus`) khớp total filter hiện tại (không chỉ page hiện tại).
- [ ] Pagination ổn định; sort theo rule mục 3.
- [ ] Item cũ không có `workType` vẫn parse được (FE treat missing = `PRODUCTION_REQUEST`).

### Phase deadlines

- [ ] `productionId` filter đúng.
- [ ] `phase=DELIVERY` → `400` (không dùng timeline DELIVERY).
- [ ] `assignedProductionId` / `Name` filled khi có assign.

---

## 6. Out of scope (P2 / deferred)

- KPI / filter `unavailableItems` (flow UNAVAILABLE chưa chốt).
- Ghi / đọc `ProjectPhaseTimeline.DELIVERY`.
- Chart trend theo tuần (`completedInRange` time-series).
- Breakdown priority riêng endpoint.
- SignalR push khi KPI đổi.
- Chat project cho role PRODUCTION.

---

## 7. FE migration note (sau khi BE ship)

1. Map KPI mới trên `ProductionDashbroad` (card Customization / Ready delivery — **không** Unavailable).
2. Queue tabs theo `workType` hoặc `countsByStatus` từ API; `scope=all` khi cần customization tab.
3. Wire `useProjectPhaseDeadlineRisks({ phase: 'PRODUCTION', productionId })` — không gọi `phase=DELIVERY`.
4. Delivery due/overdue từ queue `workType=DELIVERY`.
5. Deprecate đọc `pendingReview` → dùng `pendingStart`.

---

## 8. Sample cURL

```bash
# KPIs
curl -s -H "Authorization: Bearer $TOKEN" \
  "$API/api/dashboard/production/kpis?scope=mine&dateRange=thisWeek"

# Queue — chỉ customization chờ review
curl -s -H "Authorization: Bearer $TOKEN" \
  "$API/api/dashboard/production/queue?workType=CUSTOMIZATION_REVIEW&status=REVIEWING&page=1&limit=20"

# Queue — production overdue
curl -s -H "Authorization: Bearer $TOKEN" \
  "$API/api/dashboard/production/queue?workType=PRODUCTION_REQUEST&dueBucket=OVERDUE"

# Deadline risks — Production staff
curl -s -H "Authorization: Bearer $TOKEN" \
  "$API/api/dashboard/project-phase-deadlines?phase=PRODUCTION&status=OVERDUE&productionId=$ACCOUNT_ID&page=1&limit=20"
```

---

## 9. Tóm tắt deliverable cho BE

| Priority | Endpoint | Việc cần làm |
| --- | --- | --- |
| P0 | `GET /api/dashboard/production/kpis` | Mở rộng fields (giữ `pendingReview`); không `unavailableItems`; default `scope=mine` |
| P0 | `GET /api/dashboard/production/queue` | 3 `workType` + DELIVERY semantics + `links` / counts / filters |
| P1 | `GET /api/dashboard/project-phase-deadlines` | Thêm `productionId`; reject `phase=DELIVERY` (timing giao = schedule) |

Quyết định đã chốt: `docs/production-dashboard-spec-review.md` §6.

Nguồn tham chiếu FE hiện tại: `src/services/api/dashboard.ts`, `src/features/ProductionPages/ProductionDashbroad/ProductionDashbroad.tsx`.
