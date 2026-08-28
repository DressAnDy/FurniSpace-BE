# FurniSpace Sales Mobile Integration Guide

Tài liệu này mô tả luồng nghiệp vụ, API request/response và cách mobile app dành cho role `SALES` nên điều phối các màn hình.

Nguồn đối chiếu:

- Controller và DTO trong `src/`
- `docs/backend-api-dev-guide.md`
- `docs/api-reference.md`
- `docs/payment-service-guide.md`
- `docs/signalr-notification-guide.md`

> Lưu ý: tài liệu này ưu tiên hành vi hiện tại của code. Không dùng tài liệu flow cũ để suy ra các bước adjustment, giao hàng từng phần hoặc tự cập nhật trạng thái thanh toán.

---

## 1. Vai trò của Sales

Sales là người điều phối dự án từ lúc tiếp nhận yêu cầu đến khi hoàn tất:

1. Xem hàng đợi và tiếp nhận project mới.
2. Kiểm tra thông tin, yêu cầu Customer bổ sung nếu thiếu.
3. Thu project start fee.
4. Chuyển project sang chờ Designer và assign Designer.
5. Theo dõi đo đạc, proposal và deadline.
6. Hoàn thiện, gửi và revise quotation.
7. Theo dõi Customer accept quotation và thanh toán deposit.
8. Tạo production request, chọn Production staff.
9. Theo dõi sản xuất, tạo lịch giao hàng và thực hiện giao hàng.
10. Theo dõi remaining payment, complete Order rồi complete Project.

Sales chỉ được sửa các project đã assign cho chính mình. Ngoại lệ: Sales có thể xem project chưa assign để nhận lead. `ADMIN` có quyền thay Sales ở các thao tác hỗ trợ.

Sales không được:

- Tự đánh dấu payment online là `PAID`; webhook của provider là nguồn xác nhận.
- Accept quotation hoặc confirm delivery thay Customer.
- Start/complete production request hay sửa production item thay Production.
- Gọi status API với trạng thái bất kỳ; API này chỉ hỗ trợ một số transition đầu flow.

---

## 2. Quy ước tích hợp

### 2.1 Base URL

Backend không có một global prefix chung. Mobile phải dùng đúng path:

- `/auth`, `/projects`, `/orders`, `/quotations`
- `/api/payments`, `/api/dashboard/sales`
- `/production-requests`, `/project-schedules`

Không tự động thêm `/api` vào mọi endpoint.

### 2.2 Authentication

Các API Sales yêu cầu session có role `SALES`.

```http
Authorization: Bearer {access_token}
Content-Type: application/json
```

Backend cũng đọc cookie `access_token` và `refresh_token`.

Điểm cần chốt trước khi làm mobile native:

- `POST /auth/login`, `/auth/refresh` và `/auth/verify-email` hiện đặt token trong cookie HttpOnly.
- JSON response không chứa raw access token hoặc refresh token.
- Mobile phải dùng HTTP client có persistent cookie jar và cho phép nhận/gửi cookie `Secure`.
- Nếu app bắt buộc dùng Bearer token trực tiếp, backend cần bổ sung một contract dành cho native client; contract hiện tại chưa cấp token trong JSON.

### 2.3 Response envelope

Mọi response nghiệp vụ có dạng:

```json
{
  "status": 200,
  "message": "Success",
  "data": {},
  "errors": null,
  "errorCode": null
}
```

Mobile xử lý theo thứ tự:

1. HTTP status.
2. `errorCode` để điều hướng nghiệp vụ.
3. `errors` để hiển thị lỗi validation theo field/form.
4. `message` chỉ dùng làm fallback.

Status thường gặp:

- `200`: thành công.
- `201`: tạo mới thành công.
- `400`: body/query sai hoặc transition không hợp lệ.
- `401`: hết/thiếu session; thử refresh một lần.
- `403`: đúng role nhưng không sở hữu project/resource.
- `404`: resource không tồn tại.
- `409`: xung đột trạng thái, lịch hoặc assignment.
- `429`: vượt rate limit.

### 2.4 JSON

- Property: `camelCase`.
- Enum: chuỗi `SCREAMING_SNAKE_CASE`.
- UUID: string.
- `DateOnly`: `YYYY-MM-DD`.
- Date/time: ISO-8601 UTC.
- Số tiền: number VND; không format chuỗi trước khi gửi.

---

## 3. Luồng tổng thể

```text
Login
  -> Sales dashboard / action queue cho project đã nhận
  -> GET /projects?status=SUBMITTED để mở inbox lead chưa assign
  -> Open unassigned SUBMITTED project
  -> Claim project
     project = IN_CONSULTATION
     Sales chat được tạo tự động
  -> Kiểm tra basic information
     -> thiếu: request information
        project = NEED_BASIC_INFORMATION
        chờ Customer cập nhật
        -> gọi lại sales-assignment để resume IN_CONSULTATION
     -> đủ: tạo/theo dõi PROJECT_START_FEE khi còn IN_CONSULTATION
     -> webhook xác nhận PAID
        backend tự chuyển project = WAITING_FOR_DESIGNER_ASSIGNMENT
  -> Lấy danh sách Designer khả dụng
  -> Assign Designer + chọn tình trạng dữ liệu không gian
     -> INSUFFICIENT: project = MEASUREMENT_REQUIRED
     -> SUFFICIENT: project = SPACE_VERIFIED
  -> Nếu cần đo:
     tạo MEASUREMENT schedule, assign đúng Designer của project
     -> Customer xác nhận schedule
     -> assigned staff/Sales/Admin hoàn tất schedule + file đo đạc
     -> project = SPACE_VERIFIED
  -> project = PROPOSAL_CONSULTING
  -> Designer tạo/publish proposal
  -> Customer select-final
     backend tự tạo quotation DRAFT
  -> Sales cập nhật item financials + header
  -> Sales send quotation
     project = QUOTATION_SENT
  -> Customer có thể request revision
     project = QUOTATION_REVISION_REQUESTED
     -> Sales revise -> sửa -> send lại
  -> Customer accept
     backend tạo Order CREATED
     project = ORDER_CONFIRMED
  -> Tạo/reuse DEPOSIT payment
     order = DEPOSIT_PENDING
     -> webhook PAID
     -> order = DEPOSIT_PAID
  -> Sales tạo production request
     -> Production staff đã được chọn trong request
     -> có thể reassign bằng endpoint assign
     -> Production start/complete
     -> order/project chuyển sang trạng thái giao hàng phù hợp
  -> Tạo DELIVERY schedule
  -> Sales/Production start-delivery
  -> Sales/Production complete-delivery
  -> Customer confirm-delivery
     -> còn tiền: order = FINAL_PAYMENT_PENDING
        backend tạo/reuse REMAINING_PAYMENT
     -> không còn tiền: order = COMPLETED
  -> Webhook xác nhận remaining payment PAID
  -> Backend có thể tự complete Order; Sales refetch và chỉ complete fallback
  -> Sales complete Project
```

Các side effect đổi trạng thái ở quotation, order, payment và production do service tương ứng thực hiện. Mobile không gọi `PATCH /projects/{id}/status` để ép các trạng thái như `QUOTATION_SENT`, `ORDER_CONFIRMED`, `IN_PRODUCTION` hoặc `DELIVERED`.

---

## 4. Màn hình và API đề xuất

### 4.1 Khởi động app

1. `POST /auth/login`
2. `GET /auth/me`
3. Kết nối `/hubs/notifications`
4. Gọi song song:
   - `GET /api/dashboard/sales/kpis?scope=mine`
   - `GET /api/dashboard/sales/action-queue?scope=mine&page=1&limit=20`
   - `GET /projects?status=SUBMITTED&page=1&limit=20`
   - `GET /notifications/me/unread-count`

### 4.2 Dashboard Sales

#### `GET /api/dashboard/sales/kpis`

Query:

- `scope`: `mine` mặc định; `team`; `all` chủ yếu dành cho Admin.
- `dateRange`: `today`, `thisWeek`, `thisMonth`.
- `search`: project code/name hoặc customer name.

Response `data`:

```json
{
  "newRequests": 0,
  "waitingCustomer": 2,
  "paymentFollowUp": 1,
  "overdueTasks": 1,
  "activeProjects": 8
}
```

Giới hạn hiện tại của backend: `scope=mine` chỉ lấy project có
`assignedSalesId = currentUserId`; `scope=team` cũng loại project chưa assign.
Vì claim project sẽ lập tức đổi `SUBMITTED -> IN_CONSULTATION`, KPI
`newRequests` của Sales thực tế thường bằng `0`. Mobile phải dùng
`GET /projects?status=SUBMITTED` làm inbox lead chưa nhận. Nếu muốn dashboard
hiển thị lead chưa assign, backend cần sửa projection/scope.

#### `GET /api/dashboard/sales/action-queue`

Query đầy đủ:

```text
scope=mine
group=Intake
dateRange=thisWeek
priority=HIGH
search=PRJ-2026
page=1
limit=20
```

Các group thường dùng:

- `Intake`
- `Design`
- `Proposal and Quotation`
- `Order and Payment`
- `Delivery`

Response `data`:

```json
{
  "items": [
    {
      "id": "project-or-order-key",
      "projectId": "uuid",
      "projectCode": "PRJ-2026-0001",
      "projectName": "Cafe District 1",
      "customerName": "Nguyen Van A",
      "assigneeName": "Sales One",
      "group": "Intake",
      "phase": "SUBMITTED",
      "status": "SUBMITTED",
      "priority": "HIGH",
      "action": "Review request",
      "actionPath": "/projects/{projectId}",
      "dueAt": "2026-08-24T23:59:59Z",
      "dueBucket": "TODAY",
      "warning": null,
      "lastUpdatedAt": "2026-08-24T10:00:00Z"
    }
  ],
  "countsByGroup": {
    "Intake": 3,
    "Order and Payment": 1
  },
  "page": 1,
  "limit": 20,
  "total": 4
}
```

Mobile không nên tự suy ra next action từ status nếu queue đã trả `action`, `priority`, `warning` và `dueBucket`.

---

## 5. Project intake

### 5.1 Danh sách project

#### `GET /projects`

Query:

```text
status=SUBMITTED
assignedSalesId={salesAccountId}
assignedDesignerId={designerId}
search=keyword
page=1
limit=20
```

Response `data`:

```json
{
  "items": [
    {
      "projectId": "uuid",
      "customerId": "uuid",
      "assignedSalesId": null,
      "assignedDesignerId": null,
      "projectCode": "PRJ-2026-0001",
      "projectName": "Cafe District 1",
      "businessType": "Cafe",
      "status": "SUBMITTED",
      "submittedAt": "2026-08-24T08:00:00Z"
    }
  ],
  "page": 1,
  "limit": 20,
  "total": 1
}
```

Lưu ý quyền đọc:

- Sales có thể xem danh sách chung.
- Sales xem được detail của project chưa assign.
- Khi project đã assign, chỉ Sales được assign mới xem và thao tác detail.
- Với tab “Của tôi”, mobile nên truyền `assignedSalesId` bằng `accountId` từ `/auth/me`.

#### `GET /projects/{projectId}`

Không có body.

Response `data` chính:

```json
{
  "projectId": "uuid",
  "customerId": "uuid",
  "assignedSalesId": "uuid",
  "assignedDesignerId": null,
  "projectCode": "PRJ-2026-0001",
  "projectName": "Cafe District 1",
  "businessType": "Cafe",
  "projectAddress": "123 Nguyen Hue, HCMC",
  "businessPurpose": "New cafe opening",
  "furnitureRequirement": "Tables, chairs, bar counter",
  "description": "Industrial style",
  "totalAreaSqm": 120.5,
  "numberOfFloors": 1,
  "budgetMin": 50000000,
  "budgetMax": 120000000,
  "targetCompletionDate": "2026-12-31",
  "status": "IN_CONSULTATION",
  "submittedAt": "2026-08-24T08:00:00Z"
}
```

### 5.2 Nhận project

#### `PATCH /projects/{projectId}/sales-assignment`

Request:

```json
{
  "note": "Taking this lead"
}
```

Response `data`:

```json
{
  "projectId": "uuid",
  "assignedSalesId": "current-sales-uuid",
  "status": "IN_CONSULTATION",
  "salesAssignedAt": "2026-08-24T09:00:00Z",
  "salesChat": {
    "chatId": "uuid",
    "projectId": "uuid",
    "chatType": "SALES",
    "status": "OPEN",
    "title": "Sales Consultation"
  }
}
```

Rules:

- Chỉ nhận project đang ở nhóm pre-consultation.
- Nếu đã thuộc Sales khác: `409`.
- Backend set `assignedSalesId` bằng account đang đăng nhập; Sales không truyền `salesId`.
- Backend chuyển project sang `IN_CONSULTATION` và upsert Sales chat.

### 5.3 Yêu cầu bổ sung thông tin

#### `POST /projects/{projectId}/information-requests`

Request:

```json
{
  "message": "Please upload floor plan photos"
}
```

Response `data` chỉ gồm `projectId`, `status`, `requestedAt`. Project chuyển
sang `NEED_BASIC_INFORMATION`; Customer nhận notification. Response không trả
lại `message`, vì vậy mobile giữ message vừa gửi trong state cục bộ nếu cần
hiển thị ngay.

Chỉ assigned Sales hoặc Admin được gọi.

Sau khi Customer cập nhật thông tin, project hiện vẫn giữ
`NEED_BASIC_INFORMATION`. Để tiếp tục flow, assigned Sales gọi lại:

`PATCH /projects/{projectId}/sales-assignment`

với body note tùy chọn. Endpoint cho phép project ở
`NEED_BASIC_INFORMATION`, giữ Sales hiện tại, upsert chat và chuyển project về
`IN_CONSULTATION`. Không gọi thẳng status
`NEED_BASIC_INFORMATION -> WAITING_FOR_DESIGNER_ASSIGNMENT` vì transition đó
không được hỗ trợ.

### 5.4 Sales cập nhật basic information

#### `PATCH /projects/{projectId}/basic-information`

Request:

```json
{
  "projectName": "Cafe District 1",
  "businessType": "Cafe",
  "projectAddress": "123 Nguyen Hue, HCMC",
  "businessPurpose": "New cafe opening",
  "furnitureRequirement": "Tables, chairs, bar counter",
  "description": "Industrial style",
  "totalAreaSqm": 120.5,
  "numberOfFloors": 1,
  "budgetMin": 50000000,
  "budgetMax": 120000000,
  "targetCompletionDate": "2026-12-31"
}
```

Response là basic information đã lưu trong envelope.

Ba field bắt buộc trước khi chuyển bước:

- `projectName`
- `businessType`
- `furnitureRequirement`

Nếu rút ngắn `targetCompletionDate` làm xung đột schedule/production date hiện tại, backend trả `409 TARGET_DATE_CONFLICTS_WITH_OPERATIONAL_DATES`.

### 5.5 Transition sang chờ Designer

#### `PATCH /projects/{projectId}/status`

Request:

```json
{
  "status": "WAITING_FOR_DESIGNER_ASSIGNMENT",
  "note": "Basic information verified"
}
```

Response:

```json
{
  "projectId": "uuid",
  "status": "WAITING_FOR_DESIGNER_ASSIGNMENT",
  "oldStatus": "IN_CONSULTATION",
  "newStatus": "WAITING_FOR_DESIGNER_ASSIGNMENT",
  "note": "Basic information verified",
  "updatedAt": "2026-08-24T10:00:00Z"
}
```

Endpoint status chỉ hỗ trợ các transition:

```text
IN_CONSULTATION -> WAITING_FOR_DESIGNER_ASSIGNMENT
WAITING_FOR_DESIGNER_ASSIGNMENT -> MEASUREMENT_REQUIRED | SPACE_VERIFIED
SPACE_VERIFIED -> MEASUREMENT_REQUIRED
MEASUREMENT_REQUIRED -> SPACE_VERIFIED
MEASUREMENT_REQUIRED | SPACE_VERIFIED -> PROPOSAL_CONSULTING
PROPOSAL_CONSULTING -> PROPOSAL_SELECTED
```

Các transition giữa `SPACE_VERIFIED` và `MEASUREMENT_REQUIRED` yêu cầu `note`. Chuyển sang `PROPOSAL_CONSULTING` từ `MEASUREMENT_REQUIRED` yêu cầu measurement schedule đã complete; tùy config còn yêu cầu measurement file.

> Luồng mobile chuẩn không gọi transition
> `IN_CONSULTATION -> WAITING_FOR_DESIGNER_ASSIGNMENT` trước khi thu start fee.
> API tạo start fee không chấp nhận status
> `WAITING_FOR_DESIGNER_ASSIGNMENT`. Hãy tạo fee khi project còn
> `IN_CONSULTATION`; khi webhook xác nhận `PAID`, backend tự chuyển project sang
> `WAITING_FOR_DESIGNER_ASSIGNMENT`. Transition thủ công ở trên chỉ nên coi là
> contract kỹ thuật/recovery đã phối hợp với backend.

### 5.6 Reject project

#### `PATCH /projects/{projectId}/rejection`

Request:

```json
{
  "rejectionReason": "Out of service area"
}
```

Response là project rejection DTO với `status = REJECTED`, `rejectedAt`, `rejectionReason`.

Reason bắt buộc. Chỉ reject được trước các giai đoạn không còn cho phép hủy intake.

---

## 6. Project start fee và assign Designer

### 6.1 Kiểm tra start fee

#### `GET /api/projects/{projectId}/payments/project-start-fee/status`

Response `data`:

```json
{
  "projectId": "uuid",
  "requiresProjectStartFee": true,
  "projectStartFeeStatus": "PENDING",
  "isEligibleForDesignerAssignment": false,
  "paymentId": "uuid"
}
```

Mobile chỉ bật nút Assign Designer khi `isEligibleForDesignerAssignment = true`.

### 6.2 Tạo start fee

#### `POST /api/projects/{projectId}/payments/project-start-fee`

Request:

```json
{
  "amount": 2000000,
  "expiredAt": "2026-08-31T00:00:00Z",
  "note": "Project start fee"
}
```

Response `201 data`: payment detail. Mức mặc định hiện tại là `2,000,000 VND`, nhưng mobile nên lấy/hiển thị giá trị do nghiệp vụ cung cấp thay vì hardcode nếu backend có cấu hình khác.

Thứ tự bắt buộc:

1. Project đang `IN_CONSULTATION` (cũng có một số status recovery khác được backend hỗ trợ).
2. Tạo/reuse start fee.
3. Customer thanh toán.
4. Webhook đổi payment thành `PAID`.
5. Backend tự đổi project thành `WAITING_FOR_DESIGNER_ASSIGNMENT`.
6. Refetch project và start-fee status, sau đó mới cho assign Designer.

Nếu mobile chuyển project sang `WAITING_FOR_DESIGNER_ASSIGNMENT` trước bước 2,
API tạo fee trả lỗi `INVALID_PROJECT_STATUS`.

### 6.3 Sinh QR/link để gửi Customer

#### `POST /api/payments/{paymentId}/sepay/vietqr`

Không có body.

Response `data`:

```json
{
  "paymentId": "uuid",
  "paymentCode": "PAY-...",
  "provider": "SEPAY",
  "method": "QR_CODE",
  "amount": 2000000,
  "bankCode": "...",
  "accountNo": "...",
  "accountName": "...",
  "transferContent": "PAY-...",
  "vietQrUrl": "https://...",
  "status": "PENDING"
}
```

#### `POST /api/payments/{paymentId}/payos/payment-link`

Request:

```json
{
  "returnUrl": "https://app.example.com/payments/result",
  "cancelUrl": "https://app.example.com/payments/cancel"
}
```

Response `data` gồm `paymentId`, `paymentTransactionId`, `paymentCode`, `provider`, `method`, `orderCode`, `amount`, `status`, `checkoutUrl`, `qrCode`, `paymentStatus`.

Không coi deep-link return là xác nhận thanh toán. Mobile phải đọc lại payment hoặc chờ realtime; webhook mới là source of truth.

Nên dùng HTTPS universal/app link cho `returnUrl` và `cancelUrl`; luồng tạo
transaction của backend kiểm tra URL HTTPS và PayOS cũng cần URL web hợp lệ.

### 6.4 Danh sách Designer khả dụng

#### `GET /accounts/designers/available?page=1&pageSize=20&search=...`

Không có body. Response là paged list Designer với identity, số project/capacity và trạng thái khả dụng.

Danh sách là soft-capacity picker; backend vẫn kiểm tra capacity lúc assign và có thể trả `409`.

### 6.5 Assign Designer

#### `PATCH /projects/{projectId}/designer-assignment`

Request:

```json
{
  "designerId": "uuid",
  "spaceDataStatus": "INSUFFICIENT",
  "note": "Need on-site measurement"
}
```

`spaceDataStatus`:

- `INSUFFICIENT` -> `MEASUREMENT_REQUIRED`
- `SUFFICIENT` -> `SPACE_VERIFIED`

Response `data`:

```json
{
  "projectId": "uuid",
  "assignedDesigner": {
    "accountId": "uuid",
    "fullName": "Designer One",
    "email": "designer@example.com"
  },
  "status": "MEASUREMENT_REQUIRED",
  "designerAssignedAt": "2026-08-24T11:00:00Z"
}
```

Preconditions:

- Project đã có assigned Sales.
- Project start fee đã thỏa rule.
- Project đang `WAITING_FOR_DESIGNER_ASSIGNMENT`.
- Designer active, đúng role và chưa vượt capacity.

---

## 7. Deadline, area, schedule và file

### 7.1 Lập deadline nội bộ

#### `PUT /projects/{projectId}/phase-deadlines`

Request:

```json
{
  "proposalDueDate": "2026-09-10",
  "productionDueDate": "2026-09-25"
}
```

Chỉ assigned Sales/Admin được cập nhật và project phải đang `IN_CONSULTATION`.

Response `data`:

```json
{
  "projectId": "uuid",
  "targetCompletionDate": "2026-09-30",
  "deadlines": [
    {
      "phase": "PROPOSAL",
      "dueDate": "2026-09-10",
      "completedAt": null,
      "status": "ON_TRACK",
      "overdueDays": 0
    },
    {
      "phase": "PRODUCTION",
      "dueDate": "2026-09-25",
      "completedAt": null,
      "status": "PLANNED",
      "overdueDays": 0
    }
  ]
}
```

Rules:

- `proposalDueDate <= productionDueDate`.
- `productionDueDate <= targetCompletionDate` nếu project có target.

Đọc lại bằng `GET /projects/{projectId}/phase-deadlines`.

### 7.2 Area

API:

- `POST /projects/{projectId}/areas`
- `GET /projects/{projectId}/areas?includeCancelled=false`
- `GET /project-areas/{areaId}`
- `PATCH /project-areas/{areaId}`
- `PATCH /project-areas/{areaId}/cancel`

Create/update request:

```json
{
  "parentAreaId": null,
  "areaName": "Ground floor seating",
  "areaType": "ROOM",
  "floorNumber": 1,
  "description": "Main customer area",
  "areaSqm": 45,
  "width": 6,
  "length": 7.5,
  "height": 3.2,
  "currentCondition": "Empty shell",
  "requirementNote": "Need banquettes",
  "status": "DRAFT"
}
```

### 7.3 Schedule

#### `POST /projects/{projectId}/schedules`

Request:

```json
{
  "scheduleType": "MEASUREMENT",
  "title": "Site measurement",
  "description": "Measure ground floor",
  "assignedStaffId": "uuid",
  "scheduledStart": "2026-08-28T02:00:00Z",
  "scheduledEnd": "2026-08-28T04:00:00Z",
  "location": "123 Nguyen Hue",
  "customerNote": "Call before arrival",
  "internalNote": null
}
```

Response là schedule DTO đã tạo.

Riêng `MEASUREMENT` schedule:

- Project phải đang `MEASUREMENT_REQUIRED`.
- `assignedStaffId` là bắt buộc.
- Khi Sales tạo lịch, `assignedStaffId` phải là Designer đã assign cho project.
- Schedule mới là `PENDING_CONFIRMATION`; chỉ Customer sở hữu project xác nhận
  sang `CONFIRMED`.
- Sau khi xác nhận, assigned staff, assigned Sales hoặc Admin mới complete
  schedule theo quyền hiện tại.

List/detail:

- `GET /project-schedules?projectId={projectId}&scheduleType=MEASUREMENT&status=CONFIRMED&page=1&limit=20`
- `GET /project-schedules/my-assigned?...`
- `GET /project-schedules/{scheduleId}`

Update:

- `PATCH /project-schedules/{scheduleId}`: body cùng các field có thể sửa.
- `PATCH /project-schedules/{scheduleId}/status`

```json
{
  "status": "CONFIRMED",
  "note": "Customer confirmed"
}
```

- `DELETE /project-schedules/{scheduleId}`

Status: `PENDING_CONFIRMATION`, `CONFIRMED`, `COMPLETED`, `CANCELLED`.

Nếu trùng lịch active của cùng staff:

```json
{
  "status": 409,
  "message": "Assigned staff already has an overlapping active schedule.",
  "errorCode": "STAFF_SCHEDULE_OVERLAP"
}
```

### 7.4 Project files

#### `POST /projects/{projectId}/files`

`multipart/form-data`:

- `file`: binary.
- `fileType`: enum.
- `visibility`: `CUSTOMER_VISIBLE`, `STAFF_ONLY`, `PRIVATE`.
- `note`: optional.

Response `data`:

```json
{
  "fileId": "uuid",
  "fileLinkId": "uuid",
  "projectId": "uuid",
  "originalFileName": "measurement.pdf",
  "fileName": "...",
  "fileType": "MEASUREMENT_REPORT",
  "mimeType": "application/pdf",
  "fileSize": 123456,
  "storagePath": "...",
  "publicUrl": "https://...",
  "visibility": "STAFF_ONLY",
  "uploadedBy": "uuid",
  "uploadedAt": "2026-08-28T05:00:00Z"
}
```

List/search:

- `GET /projects/{projectId}/files?fileType=...&visibility=...&page=1&limit=20`
- `GET /projects/{projectId}/files/search?q=measurement&page=1&limit=20`

---

## 8. Proposal và quotation

Sales có quyền theo dõi và hỗ trợ tạo/sửa/publish proposal cùng Designer:

- `GET /projects/{projectId}/proposals`
- `GET /proposals/{proposalId}`
- `GET /proposals/{proposalId}/scenes`
- `GET /proposals/{proposalId}/items`
- `POST /projects/{projectId}/proposals`
- `PATCH /proposals/{proposalId}`
- `PATCH /proposals/{proposalId}/publish`
- `POST /proposals/{proposalId}/reopen-for-editing`
- `POST /proposals/{proposalId}/scenes`
- `PATCH /proposal-scenes/{sceneId}`
- `PATCH /proposal-items/{proposalItemId}`
- `DELETE /proposal-items/{proposalItemId}`

Create/update proposal request:

```json
{
  "proposalName": "Concept A — Industrial",
  "description": "Dark oak + black metal"
}
```

Publish request:

```json
{
  "note": "Ready for customer review"
}
```

Create scene request:

```json
{
  "sceneName": "Ground floor",
  "sceneType": "THREE_D",
  "projectAreaId": null,
  "mongoSceneId": null,
  "previewFileId": null
}
```

Update proposal item request:

```json
{
  "quantity": 6,
  "customizationNote": "Round corners"
}
```

Proposal response `data` gồm `proposalId`, `projectId`,
`parentProposalId`, `proposalName`, `description`, `versionNo`, `status`,
`publishedAt`, `selectedAt`, `rejectedAt`, `createdAt`, `updatedAt`.
Proposal detail bổ sung `scenes[]` và `items[]`.

Sales không được sync item từ Room Planner; endpoint
`POST /proposals/{proposalId}/items/sync-from-scene` chỉ dành cho
Designer/Admin.

Customer select-final bằng API riêng. Lần select thành công đầu tiên, backend tự tạo quotation `DRAFT`; mobile Sales không nên tạo thêm quotation thủ công.

### 8.1 Lấy quotation

- `GET /projects/{projectId}/quotations?status=DRAFT`
- `GET /quotations/{quotationId}`

`POST /projects/{projectId}/quotations` không có body, chỉ là fallback nếu quotation draft không được auto-create.

### 8.2 Cập nhật header

#### `PATCH /quotations/{quotationId}`

```json
{
  "validUntil": "2026-09-30",
  "depositAmount": 5832000,
  "customerNote": null,
  "salesNote": "VIP discount",
  "revisionReason": null
}
```

Chỉ các field trên được ghi. Tổng tiền do server tính.

### 8.3 Cập nhật giá item

#### Single item

`PATCH /quotations/{quotationId}/items/{quotationItemId}/financials`

```json
{
  "quantity": 4,
  "unitPrice": 4500000,
  "discountAmount": 0
}
```

#### Bulk

`PUT /quotations/{quotationId}/items/financials`

```json
{
  "items": [
    {
      "quotationItemId": "uuid",
      "quantity": 4,
      "unitPrice": 4500000,
      "discountAmount": 0
    }
  ]
}
```

Server tính:

```text
item.grossAmount = quantity * unitPrice
item.totalAmount = grossAmount - discountAmount
header.subtotalAmount = SUM(grossAmount)
header.totalDiscountAmount = SUM(discountAmount)
header.preVatAmount = SUM(item.totalAmount)
header.vatAmount = ROUND(preVatAmount * vatRate)
header.totalAmount = preVatAmount + vatAmount
```

Sau khi sửa item, mobile phải đọc `totalAmount` mới và bảo đảm `0 < depositAmount <= totalAmount`.

### 8.4 Gửi/revise/cancel

- `PATCH /quotations/{quotationId}/send`: không body.
- `PATCH /quotations/{quotationId}/revise`: không body; dùng khi status `REVISION_REQUESTED`.
- `PATCH /quotations/{quotationId}/cancel`: không body.

Cả ba endpoint trả quotation DTO sau khi đổi trạng thái trong response envelope.

Flow:

```text
DRAFT | REVISED -> send -> SENT
SENT -> Customer request-revision -> REVISION_REQUESTED
REVISION_REQUESTED -> Sales revise -> REVISED
REVISED -> Sales send -> SENT
SENT -> Customer accept -> ACCEPTED + Order CREATED
```

Quotation detail response:

```json
{
  "quotationId": "uuid",
  "projectId": "uuid",
  "proposalId": "uuid",
  "quotationCode": "QT-...",
  "versionNo": 1,
  "subtotalAmount": 18000000,
  "totalDiscountAmount": 0,
  "preVatAmount": 18000000,
  "vatRate": 0.08,
  "vatAmount": 1440000,
  "totalAmount": 19440000,
  "depositAmount": 5832000,
  "currency": "VND",
  "status": "SENT",
  "validUntil": "2026-09-30",
  "customerNote": null,
  "salesNote": "VIP discount",
  "revisionReason": null,
  "items": [
    {
      "quotationItemId": "uuid",
      "productNameSnapshot": "Oak Cafe Table",
      "quantity": 4,
      "unitPrice": 4500000,
      "grossAmount": 18000000,
      "discountAmount": 0,
      "totalAmount": 18000000,
      "isCustomized": false
    }
  ]
}
```

---

## 9. Order, deposit và production

### 9.1 Đọc Order

- `GET /projects/{projectId}/orders`
- `GET /orders/{orderId}`

Response detail:

```json
{
  "orderId": "uuid",
  "projectId": "uuid",
  "proposalId": "uuid",
  "quotationId": "uuid",
  "orderCode": "ORD-...",
  "customerId": "uuid",
  "salesId": "uuid",
  "vatRate": 0.08,
  "vatAmount": 1440000,
  "originalTotalAmount": 19440000,
  "itemAdjustmentAmount": 0,
  "additionalDiscountAmount": 0,
  "finalTotalAmount": 19440000,
  "depositAmount": 5832000,
  "paidAmount": 0,
  "remainingAmount": 19440000,
  "status": "CREATED",
  "items": [
    {
      "orderItemId": "uuid",
      "productNameSnapshot": "Oak Cafe Table",
      "quantity": 4,
      "unitPrice": 4500000,
      "discountAmount": 0,
      "subtotalAmount": 18000000,
      "status": "PENDING"
    }
  ]
}
```

### 9.2 Tạo/reuse deposit

#### `POST /orders/{orderId}/payments/deposit`

```json
{
  "expiredAt": "2026-09-05T00:00:00Z",
  "note": "Deposit invoice"
}
```

Response `201/200 data`: payment detail. Amount luôn lấy từ snapshot `order.depositAmount`, mobile không gửi amount.

Eligible:

- `CREATED`: tạo payment và chuyển `DEPOSIT_PENDING`.
- `DEPOSIT_PENDING`: reuse active pending deposit nếu có.

Sau đó dùng API PayOS/SePay ở mục 6.3 và theo dõi payment đến `PAID`.

### 9.3 Tạo production request

#### `POST /orders/{orderId}/production-request`

```json
{
  "assignedTo": "production-staff-uuid",
  "priority": "HIGH",
  "note": null
}
```

Response `201 data` gồm `productionRequestId`, `orderId`, `projectId`, status và assignment.

Rules:

- Deposit/order phải đạt điều kiện của production flow.
- Production deadline khong nam trong request nay. Backend doc deadline tu `project_phase_timelines` voi `phase = PRODUCTION`; Sales can lap deadline bang `PUT /projects/{projectId}/phase-deadlines` truoc khi tao production request.
- `assignedTo` là UUID bắt buộc và phải là account Production hợp lệ.
- `priority` là string; nếu bỏ trống backend dùng `"NORMAL"`.

Lỗ hổng ownership hiện tại: endpoint create mới kiểm tra role
`SALES/ADMIN`, chưa đối chiếu `assignedSalesId` của order/project. Mobile chỉ
gọi endpoint từ project đang thuộc Sales hiện tại, nhưng backend vẫn cần bổ
sung ownership check; không được coi `403` là lớp bảo vệ đầy đủ cho endpoint
này.

### 9.4 Production staff picker và assign

#### `GET /production-staff/available`

Query:

```text
projectId={projectId}
productionRequestId={productionRequestId}
search=keyword
```

Response item gồm `accountId`, `fullName`, `email`, `avatarUrl`, `accountStatus`, request counts và `isAvailable`.

#### `PATCH /production-requests/{productionRequestId}/assign`

```json
{
  "assignedTo": "production-staff-uuid",
  "assignmentNote": "Priority batch"
}
```

Response là assignment DTO hiện tại.

### 9.5 Theo dõi production

- `GET /production-requests?status=IN_PRODUCTION&assignedTo=...&priority=...`
- `GET /production-requests/{productionRequestId}`

Response detail chính:

```json
{
  "productionRequestId": "uuid",
  "productionCode": "PRD-...",
  "projectId": "uuid",
  "projectCode": "PRJ-...",
  "projectName": "Cafe District 1",
  "orderId": "uuid",
  "orderCode": "ORD-...",
  "assignedTo": "uuid",
  "assignedToName": "Production One",
  "status": "IN_PRODUCTION",
  "priority": "HIGH",
  "productionDeadline": "2026-09-25",
  "actualStartDate": "2026-09-06",
  "actualCompletionDate": null,
  "note": null,
  "items": []
}
```

Sales chỉ theo dõi và assign/reassign. `PATCH .../start`, `PATCH .../complete` và cập nhật production item thuộc Production/Admin.

---

## 10. Delivery, final payment và complete

### 10.1 Preconditions giao hàng

Trước `start-delivery`:

- Production request phải `COMPLETED`.
- Tất cả active product order item phải `READY`.
- Có đúng luồng `DELIVERY` schedule đã được Customer xác nhận.
- Có tối đa một active `DELIVERY` schedule cho project.

Nếu production chưa xong: `409 PRODUCTION_NOT_COMPLETED`.

Schedule mới tạo có status `PENDING_CONFIRMATION`. Customer sở hữu project
phải gọi update schedule status sang `CONFIRMED`; sau đó Sales/Production mới
gọi `start-delivery`. Nếu chưa xác nhận, backend trả
`DELIVERY_SCHEDULE_NOT_CONFIRMED`.

### 10.2 Giao hàng

- `PATCH /orders/{orderId}/start-delivery`: không body.
- `PATCH /orders/{orderId}/complete-delivery`: không body.

`complete-delivery` chuyển toàn bộ deliverable item `READY -> DELIVERED`. Hệ thống hiện hỗ trợ một lần giao đầy đủ, không hỗ trợ partial/incremental delivery.

Customer gọi `PATCH /orders/{orderId}/confirm-delivery`.

Sau Customer confirm:

- `remainingAmount > 0`: backend tạo/reuse `REMAINING_PAYMENT`, order -> `FINAL_PAYMENT_PENDING`.
- `remainingAmount = 0`: order -> `COMPLETED`.

### 10.3 Final payment fallback

#### `PATCH /orders/{orderId}/prepare-final-payment`

Không body. Đây là recovery endpoint; normal mobile flow không phụ thuộc endpoint này vì Customer confirm delivery đã chuẩn bị final payment.

#### `POST /orders/{orderId}/payments/remaining`

```json
{
  "expiredAt": "2026-10-05T00:00:00Z",
  "note": "Remaining payment"
}
```

Chỉ dùng khi cần tạo/reuse thủ công. Amount lấy từ `order.remainingAmount`.

### 10.4 Complete

1. Đọc lại `GET /orders/{orderId}`.
2. Chỉ cho complete khi remaining amount đã bằng `0` và các điều kiện delivery đã đạt.
3. Sau webhook remaining payment, refetch Order. Backend có thể đã tự chuyển
   Order sang `COMPLETED` nếu giao hàng đã được xác nhận và số dư bằng `0`.
4. Chỉ khi Order chưa complete, gọi `PATCH /orders/{orderId}/complete` như
   fallback idempotent.
5. `PATCH /projects/{projectId}/complete`.

Cả hai endpoint không có body và hỗ trợ gọi lại an toàn khi đã complete.

---

## 11. Payment APIs cho màn hình theo dõi

### 11.1 Danh sách

#### `GET /api/payments`

Query:

```text
projectId={projectId}
orderId={orderId}
status=PENDING
paymentType=DEPOSIT
page=1
pageSize=20
```

Response `data`:

```json
{
  "items": [
    {
      "paymentId": "uuid",
      "paymentCode": "PAY-...",
      "projectId": "uuid",
      "projectCode": "PRJ-...",
      "projectName": "Cafe District 1",
      "orderId": "uuid",
      "orderCode": "ORD-...",
      "paymentType": "DEPOSIT",
      "amount": 5832000,
      "currency": "VND",
      "status": "PENDING",
      "expiredAt": "2026-09-05T00:00:00Z",
      "paidAt": null,
      "createdAt": "2026-08-30T00:00:00Z",
      "isPayable": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

### 11.2 Các API đọc

- `GET /api/payments/summary`
- `GET /api/payments/{paymentId}`
- `GET /api/payments/{paymentId}/transactions`
- `GET /api/payments/code/{paymentCode}/status`

Summary `data`:

```json
{
  "pendingCount": 1,
  "processingCount": 0,
  "paidCount": 2,
  "expiredCount": 0,
  "cancelledCount": 0,
  "payableCount": 1,
  "pendingAmount": 5832000,
  "currency": "VND"
}
```

Polling fallback:

- Khi đang mở màn thanh toán: 5-10 giây/lần.
- Khi app background: ngừng polling, dựa vào notification.
- Dừng khi status terminal: `PAID`, `CANCELLED`, `EXPIRED`, `REFUNDED`.

---

## 12. Chat, notification và realtime

### 12.1 Chat

Khi Sales nhận project, Sales chat được tạo/upsert tự động.

API:

- `GET /projects/{projectId}/chats`
- `GET /project-chats/{chatId}/messages?page=1&limit=20&sort=ASC`
- `POST /project-chats/{chatId}/messages`
- `POST /project-chats/{chatId}/messages/files`
- `PATCH /project-chats/{chatId}/status`
- `GET /projects/{projectId}/chat-messages/search?q=chair&page=1&limit=20`

Text request:

```json
{
  "messageType": "TEXT",
  "content": "Hello, when can we schedule measurement?"
}
```

Message response:

```json
{
  "messageId": "uuid",
  "chatId": "uuid",
  "senderId": "uuid",
  "senderName": "Sales One",
  "senderRole": "SALES",
  "messageType": "TEXT",
  "content": "Hello, when can we schedule measurement?",
  "attachment": null,
  "createdAt": "2026-08-24T12:00:00Z",
  "editedAt": null,
  "deletedAt": null,
  "readAt": null
}
```

File message dùng multipart: `file`, `content?`, `fileType`, `visibility?`.

### 12.2 Notification REST

- `GET /notifications/me?isUnread=true&page=1&limit=20`
- `GET /notifications/me/unread-count`
- `PATCH /notifications/{notificationId}/read`
- `PATCH /notifications/me/read-all`

Notification item:

```json
{
  "notificationId": "uuid",
  "receiverId": "uuid",
  "projectId": "uuid",
  "title": "Quotation revision requested",
  "message": "Customer requested a revision",
  "notificationType": "...",
  "referenceType": "QUOTATION",
  "referenceId": "uuid",
  "isRead": false,
  "createdAt": "2026-08-24T12:00:00Z",
  "readAt": null
}
```

### 12.3 SignalR

Hubs:

- `/hubs/notifications`
- `/hubs/project-chat`
- `/hubs/payments`

Token:

- Cookie hoặc Bearer dùng được cho các hub.
- Query `?access_token=` chỉ được hỗ trợ cho notifications và project-chat; payment hub không nên dùng query token.

Project chat methods:

- `JoinProject(projectId)`
- `LeaveProject(projectId)`
- `JoinChat(chatId)`
- `LeaveChat(chatId)`

Mobile nên:

1. Lưu event ID/reference để chống render trùng.
2. Khi nhận event, update cache tối thiểu rồi refetch detail liên quan.
3. Sau reconnect, refetch unread count, action queue và màn hình đang mở.
4. Không dùng realtime payload làm nguồn dữ liệu duy nhất.

---

## 13. State và hành động trên UI

| Project status | Hành động chính của Sales |
| --- | --- |
| `SUBMITTED` | Xem detail, claim hoặc reject |
| `IN_CONSULTATION` | Chat, cập nhật info, request info, lập deadline, tạo/thu start fee |
| `NEED_BASIC_INFORMATION` | Chờ Customer/cập nhật info, sau đó tiếp tục consultation |
| `WAITING_FOR_DESIGNER_ASSIGNMENT` | Refetch fee status, chọn và assign Designer |
| `MEASUREMENT_REQUIRED` | Tạo/theo dõi measurement schedule, file và xác minh space |
| `SPACE_VERIFIED` | Chuyển sang proposal consulting |
| `PROPOSAL_CONSULTING` | Theo dõi proposal; Customer select final |
| `PROPOSAL_SELECTED` | Mở quotation draft, nhập giá và gửi |
| `QUOTATION_SENT` | Theo dõi Customer accept/revision |
| `QUOTATION_REVISION_REQUESTED` | Revise, cập nhật giá và gửi lại |
| `ORDER_CONFIRMED` | Tạo/theo dõi deposit |
| `IN_PRODUCTION` | Theo dõi production |
| `READY_FOR_DELIVERY` | Tạo lịch và start delivery |
| `DELIVERING` | Complete delivery, chờ Customer confirm |
| `DELIVERED` | Theo dõi remaining payment |
| `COMPLETED` | Read-only |
| `REJECTED` | Read-only |

Không chỉ dựa vào Project status để bật nút. Luôn đọc thêm:

- Quotation status.
- Order status, `paidAmount`, `remainingAmount`.
- Payment status.
- Production request/item status.
- Active delivery schedule.
- Ownership (`assignedSalesId`).

---

## 14. Error handling theo nghiệp vụ

Các code mobile cần map riêng:

| Error code/message | Cách xử lý |
| --- | --- |
| `PROJECT_START_FEE_REQUIRED` | Mở tab start fee, không cho assign Designer |
| `INVALID_PROJECT_STATUS` / `INVALID_PROJECT_STATUS_TRANSITION` | Refetch project, render action theo status mới |
| `DESIGNER_NOT_ASSIGNED` | Điều hướng assign Designer |
| `FINAL_PROPOSAL_REQUIRED` | Chờ Customer select final |
| `STAFF_SCHEDULE_OVERLAP` | Giữ form, yêu cầu chọn staff/time khác |
| `TARGET_DATE_CONFLICTS_WITH_OPERATIONAL_DATES` | Hiển thị xung đột timeline, không overwrite |
| `PRODUCTION_NOT_COMPLETED` | Điều hướng production detail |
| `DEPOSIT_ALREADY_PAID` | Refetch order/payment; không tạo lại |
| `PRODUCTION_REQUEST_ALREADY_EXISTS` | Refetch production request |
| `401` | Refresh một lần; thất bại thì logout |
| `403` | Refetch ownership; quay lại list nếu project thuộc Sales khác |
| `409` | Refetch resource trước khi cho retry |

Không retry tự động các request mutation khi chưa biết request đầu thành công hay thất bại. Với API create/reuse payment, refetch theo project/order trước khi gọi lại.

---

## 15. Checklist triển khai mobile

### P0

- Cookie jar/session + refresh.
- `/auth/me`.
- Sales dashboard và action queue.
- Project list/detail, claim, request info, status, reject.
- Project start fee, payment detail/status, QR/link.
- Designer picker và assignment.
- Schedule measurement/delivery.
- Proposal/quotation read + quotation financial editing/send/revise.
- Order read, deposit, production request/assignment.
- Delivery, remaining payment, complete order/project.
- Notifications và reconnect/refetch.

### P1

- Area management.
- Project file upload/search.
- Chat file attachment.
- Phase deadline dashboard.
- Payment transaction history.
- Customization request read-only/cancel theo quyền hiện tại.

### Kiểm thử E2E tối thiểu

1. Sales login và session tồn tại sau restart app.
2. Hai Sales cùng claim một project: chỉ một người thành công.
3. Request info -> Customer cập nhật -> Sales tiếp tục.
4. Không assign Designer khi start fee chưa đạt.
5. Webhook payment cập nhật UI qua realtime/polling.
6. Quotation sửa item làm header total thay đổi đúng.
7. Customer request revision -> Sales revise và gửi lại.
8. Deposit đã có thì create lại trả/reuse đúng, không tạo obligation trùng.
9. Production chưa complete thì delivery bị chặn.
10. Customer confirm delivery tạo remaining payment đúng.
11. Chỉ complete Order/Project khi đủ điều kiện.
12. Sales A không đọc/sửa project đã assign Sales B.

---

## 16. Nguồn chi tiết

- Full API contract: `docs/api-reference.md`
- Payment provider/webhook: `docs/payment-service-guide.md`
- Notification/SignalR: `docs/signalr-notification-guide.md`
- Upload/file: `docs/firebase-storage-service-guide.md`
- Backend architecture: `docs/backend-api-dev-guide.md`
- Swagger runtime: `GET /swagger/v1/swagger.json`

Khi DTO trong Swagger khác ví dụ ở tài liệu này, ưu tiên Swagger của đúng backend environment đang tích hợp và báo backend cập nhật docs nếu thay đổi contract là có chủ đích.
