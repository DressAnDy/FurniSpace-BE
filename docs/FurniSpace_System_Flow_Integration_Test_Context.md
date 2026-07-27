# FurniSpace — System Flow & Integration Test Context

## 1. Purpose

Tài liệu này là context tổng hợp để xây dựng Integration Test cho toàn bộ hệ thống FurniSpace.

Tài liệu mô tả:

```text
- Actor và quyền hạn
- Module và trách nhiệm
- Table tham gia
- Status và transition
- Precondition
- Business action
- Side effect giữa các bảng
- Dữ liệu cần verify
- Notification và file
- Happy path
- Negative path
- Idempotency và concurrency
- Những phần ngoài scope hiện tại
```

FurniSpace là hệ thống nghiệp vụ của doanh nghiệp cung cấp giải pháp nội thất và thiết kế không gian bán lẻ.

FurniSpace không phải marketplace.

---

# 2. Architecture Baseline

## 2.1 PostgreSQL

PostgreSQL là source of truth cho business data.

PostgreSQL lưu:

```text
roles
accounts
business_types
categories
products
product_versions
projects
project_areas
project_schedules
project_chats
project_chat_messages
proposals
proposal_scenes
proposal_items
proposal_scene_variants
customization_requests
quotations
quotation_items
orders
order_items
order_adjustments
order_adjustment_items
payments
payment_transactions
production_requests
production_items
files
file_links
notifications
project_reviews
```

## 2.2 MongoDB

MongoDB chỉ lưu Room Planner visual/editor state.

MongoDB lưu:

```text
2D/3D scene document
walls
doors
windows
openings
floor regions
object transforms
camera
lighting
material override
flooring configuration
wallcovering configuration
scene variant data
```

MongoDB không phải source of truth cho:

```text
quotation
order
payment
production
delivery
business status
```

## 2.3 External File Storage

Binary file được lưu ở external storage.

SQL chỉ lưu metadata qua:

```text
files
file_links
```

---

# 3. Main Actors

## 3.1 CUSTOMER

Customer có thể:

```text
- Tạo Project request.
- Bổ sung thông tin Project.
- Upload file thuộc Project theo quyền.
- Xác nhận Project Schedule.
- Xem Proposal đã publish.
- Xem và chỉnh Customer Scene Variant.
- Tạo Customization Request.
- Xem Customization Request do Designer tạo cho Project.
- Accept hoặc reject kết quả customization.
- Chọn final Proposal.
- Xem, yêu cầu revision, accept hoặc reject Quotation.
- Xem Payment của mình.
- Tạo online Payment Transaction attempt.
- Thanh toán Project Start Fee, Deposit, Remaining Payment.
- Xác nhận Order Adjustment.
- Xem Delivery Schedule.
- Xác nhận từng Order Item đã giao đủ.
- Tạo Project Review sau khi Project hoàn tất.
```

Customer không thể:

```text
- Assign Sales, Designer hoặc Production.
- Publish Proposal.
- Tự đặt giá customization.
- Tự tạo Deposit hoặc Remaining Payment obligation.
- Tự set Payment thành PAID.
- Tự update Production status.
- Tự update delivered_quantity.
- Tự complete Order hoặc Project.
```

## 3.2 SALES

Sales chịu trách nhiệm:

```text
- Nhận Project request.
- Tư vấn Customer.
- Yêu cầu bổ sung thông tin.
- Reject hoặc approve Project.
- Tạo Project Start Fee.
- Kiểm tra Project Start Fee đã PAID.
- Assign Designer.
- Quyết định Measurement Required hoặc Space Verified.
- Tạo Project Schedule.
- Theo dõi Proposal consulting.
- Tạo và gửi Quotation.
- Xử lý Quotation revision.
- Tạo/confirm Order và cấu hình Deposit.
- Tạo Deposit Payment.
- Tạo Production Request.
- Xem available Production Staff.
- Assign hoặc reassign Production Request.
- Tạo và chỉnh Order Adjustment.
- Tạo Delivery Schedule.
- Start Delivery hoặc phối hợp Production.
- Cập nhật delivered_quantity khi được phép.
- Enter Final Payment phase.
- Tạo Remaining Payment.
- Explicitly complete Order và Project.
```

Sales không thể:

```text
- Tự set Payment online thành PAID.
- Tự accept Customization thay Customer.
- Tự confirm Delivery thay Customer.
```

## 3.3 DESIGNER

Designer chịu trách nhiệm:

```text
- Xử lý Project được assign.
- Thực hiện measurement.
- Verify space.
- Tạo Proposal và Proposal Scene.
- Sử dụng Room Planner.
- Publish Proposal.
- Tạo revision Proposal.
- Review Customer Scene Variant.
- Tạo Customization Request cho Customer của Project được assign.
- Review customization design/spec.
- Tạo approved project-specific Product Version theo flow Customer accept.
```

Designer không thể:

```text
- Set customization additional cost.
- Production review customization.
- Tạo hoặc sửa Payment.
- Cập nhật Production Item status.
- Complete Order hoặc Project.
```

## 3.4 PRODUCTION

Production chịu trách nhiệm:

```text
- Xem global Production Request queue.
- Xem Request được assign.
- Review Production Request.
- Start Production Request.
- Update Production Item status.
- Ghi production note, material note, cancellation reason.
- Review customization feasibility.
- Set material availability.
- Set estimated production days.
- Set customization additional cost.
- Complete Production Request.
- Start Delivery hoặc phối hợp Delivery.
- Cập nhật delivered_quantity.
```

Assignment là ownership chính, không giới hạn visibility global queue.

Production không thể:

```text
- Cấu hình Order financial.
- Tạo Deposit Payment.
- Customer-confirm Adjustment.
- Customer-confirm Delivery.
- Complete Project.
```

## 3.5 ADMIN

Admin có thể:

```text
- Xem toàn hệ thống.
- Quản lý account, role, catalog.
- Thao tác thay Sales, Designer, Production khi cần.
- Assign staff.
- Xử lý manual payment flow nếu được cấu hình.
- Override trong các flow hỗ trợ.
```

---

# 4. Main End-to-End Flow

```text
Customer submits Project
→ Sales receives Project
→ Sales requests more information or approves
→ System/Sales creates Project Start Fee
→ Customer pays Project Start Fee
→ Sales assigns Designer
→ Sales chooses Measurement Required or Space Verified
→ Designer completes measurement if needed
→ Project enters Proposal Consulting
→ Designer creates and publishes Proposals
→ Customer reviews, requests revision, compares options
→ Customer/Designer creates Customization Request when needed
→ Designer reviews specification
→ Production reviews feasibility and additional cost
→ Customer accepts/rejects customization
→ Accepted customization creates project-specific Product Version
→ Customer selects final Proposal
→ Sales creates and sends Quotation
→ Customer accepts Quotation
→ System creates Order and Order Items
→ Sales configures Deposit
→ Customer pays Deposit
→ Sales creates Production Request
→ Sales assigns Production Staff
→ Production handles Production Items
→ Cancelled Item requires Customer-confirmed Order Adjustment
→ Production completes Request
→ Completed items become READY
→ Cancelled items become UNAVAILABLE
→ Sales creates one or more Delivery Schedules
→ Sales/Production starts Delivery
→ Sales/Production updates delivered_quantity
→ Customer confirms each fully delivered Order Item
→ Order and Project become DELIVERED
→ Sales enters final payment phase
→ Remaining Payment is created when remaining_amount > 0
→ Customer pays Remaining Payment
→ Sales/Admin explicitly completes Order and Project
→ Customer creates Project Review
```

---

# 5. Account & Role Module

## Tables

```text
roles
accounts
```

## Main Status

```text
account_status:
ACTIVE
INACTIVE
SUSPENDED
```

## Integration Test Rules

```text
- Account must have exactly one current role through role_id.
- Only ACTIVE account can be assigned to Project or Production Request.
- INACTIVE/SUSPENDED Production account is excluded from available staff.
- Authorization must be validated in backend, not only FE.
```

## Verify

```text
- Correct role access.
- Cross-role forbidden actions.
- Inactive account cannot be assigned.
- Deleted account behavior follows soft-delete policy.
```

---

# 6. Product Catalog Module

## Tables

```text
categories
business_types
products
product_versions
files
file_links
```

## Product Rules

```text
- Product is the generic catalog entity.
- Product Version is the concrete specification.
- Product business_type_ids is nullable.
- PostgreSQL array elements are validated by backend.
```

## Product Version Types

```text
STANDARD
CUSTOM
PROJECT_SPECIFIC
```

## Project-Specific Product Version Rules

```text
version_type = PROJECT_SPECIFIC
project_id is required
is_project_specific = true
is_public = false
is_default = false
status = ACTIVE
```

## Integration Test Scenarios

```text
- Create Standard Product Version.
- Reject project-specific version without project_id.
- Reject public project-specific version.
- Public catalog excludes project-specific version.
- Project context can load project-specific version.
- Original Product Version remains unchanged after customization.
```

---

# 7. File Management Module

## Tables

```text
files
file_links
```

## Main File Fields

```text
file_id
uploaded_by
original_file_name
stored_file_name
file_url
storage_path
mime_type
file_extension
file_size_bytes
checksum
status
uploaded_at
archived_at
```

## File Link Fields

```text
file_id
reference_type
reference_id
file_type
visibility
is_primary
display_order
description
created_by
created_at
```

`file_links.created_by` remains valid because it records who created the file association.

## File Status

```text
ACTIVE
ARCHIVED
```

## Visibility

```text
CUSTOMER_VISIBLE
STAFF_ONLY
PRIVATE
```

## Common Reference Types

```text
PROJECT
PROJECT_AREA
PROJECT_SCHEDULE
PROPOSAL
PROPOSAL_SCENE
PRODUCT_VERSION
CUSTOMIZATION_REQUEST
QUOTATION
ORDER
PRODUCTION_REQUEST
PROJECT_REVIEW
```

## Flooring & Wallcovering

Flooring and wallcovering are Room Planner assets, not Products.

Recommended file types:

```text
FLOORING_TEXTURE
WALLCOVERING_TEXTURE
```

Common asset:

```text
- Available for all allowed Projects.
- Managed by Admin or authorized catalog staff.
- Not linked to a specific Project.
```

Project-specific asset:

```text
- Uploaded through Project Room Planner UI.
- Linked by file_links to PROJECT or PROJECT_AREA.
- Only visible inside that Project.
```

SQL determines ownership and access.

MongoDB stores which asset is applied and its visual parameters:

```text
fileId
repeatX
repeatY
rotation
scale
offset
wallId/floorRegionId
```

## Integration Test Scenarios

```text
- Upload Project file.
- Link file to Project.
- Reject invalid polymorphic reference.
- Enforce visibility.
- Set and query primary file.
- Ensure display ordering.
- Common flooring asset visible in multiple Projects.
- Project flooring asset not visible in another Project.
- Mongo scene references existing allowed file.
```

---

# 8. Project Core Module

## Table

```text
projects
```

## Main Fields

```text
customer_id
assigned_sales_id
assigned_designer_id
project_code
project_name
business_type
project_address
business_purpose
furniture_requirement
description
total_area_sqm
number_of_floors
budget_min
budget_max
target_completion_date
status
submitted_at
sales_assigned_at
approved_at
designer_assigned_at
completed_at
rejected_at
rejection_reason
```

## Project Status

```text
SUBMITTED
IN_CONSULTATION
NEED_BASIC_INFORMATION
WAITING_FOR_DESIGNER_ASSIGNMENT
MEASUREMENT_REQUIRED
SPACE_VERIFIED
PROPOSAL_CONSULTING
PROPOSAL_SELECTED
QUOTATION_SENT
QUOTATION_REVISION_REQUESTED
ORDER_CONFIRMED
IN_PRODUCTION
PRODUCTION_BLOCKED
READY_FOR_DELIVERY
DELIVERING
DELIVERED
COMPLETED
REJECTED
```

## Main Transitions

### Submit Project

```text
Actor: CUSTOMER
From: none
To: SUBMITTED
Tables: projects, files, file_links, notifications
```

### Receive Project

```text
Actor: SALES/ADMIN
From: SUBMITTED
To: IN_CONSULTATION
Side effects:
assigned_sales_id set
sales_assigned_at set
```

### Request More Information

```text
Actor: SALES/ADMIN
From: IN_CONSULTATION
To: NEED_BASIC_INFORMATION
Notification: Customer
```

### Customer Updates Basic Information

```text
Actor: CUSTOMER
From: NEED_BASIC_INFORMATION
To: IN_CONSULTATION or remains NEED_BASIC_INFORMATION until Sales accepts
```

### Reject Project

```text
Actor: SALES/ADMIN
Allowed before Order phase
To: REJECTED
rejected_at set
rejection_reason required
```

### Approve For Designer Assignment

```text
Actor: SALES/ADMIN
Precondition: Project Start Fee rule satisfied
To: WAITING_FOR_DESIGNER_ASSIGNMENT
approved_at set
```

### Assign Designer

```text
Actor: SALES/ADMIN
From: WAITING_FOR_DESIGNER_ASSIGNMENT
assigned_designer_id set
designer_assigned_at set
Next:
MEASUREMENT_REQUIRED or SPACE_VERIFIED
```

### Proposal Phase

```text
SPACE_VERIFIED
→ PROPOSAL_CONSULTING
```

### Select Proposal

```text
PROPOSAL_CONSULTING
→ PROPOSAL_SELECTED
```

### Quotation

```text
PROPOSAL_SELECTED
→ QUOTATION_SENT
→ QUOTATION_REVISION_REQUESTED when revision requested
```

### Order

```text
Accepted Quotation
→ ORDER_CONFIRMED
```

### Production

```text
Create Production Request
→ IN_PRODUCTION

Request-level blocker
→ PRODUCTION_BLOCKED

Resolve blocker/start again
→ IN_PRODUCTION
```

### Delivery

```text
Production complete
→ READY_FOR_DELIVERY

Start Delivery
→ DELIVERING

Final available item confirmed
→ DELIVERED
```

### Complete

```text
Explicit Sales/Admin complete
→ COMPLETED
completed_at set
```

## Integration Test Scenarios

```text
- Every valid transition.
- Every invalid transition.
- Wrong actor.
- Customer ownership.
- Status side effect on related entity.
- Duplicate action idempotency.
- Project status cannot skip mandatory phase.
```

---

# 9. Project Area & Measurement Module

## Tables

```text
project_areas
project_schedules
files
file_links
```

## Project Area Status

```text
DRAFT
NEED_MEASUREMENT
MEASURED
VERIFIED
CANCELLED
```

## Schedule Types

```text
MEASUREMENT
CONSULTATION
DESIGN_REVIEW
DELIVERY
HANDOVER
OTHER
```

## Schedule Status

```text
PENDING_CONFIRMATION
CONFIRMED
COMPLETED
CANCELLED
```

## Measurement Flow

```text
Sales marks Project MEASUREMENT_REQUIRED
→ Sales creates MEASUREMENT Schedule
→ Customer confirms Schedule
→ Designer performs measurement
→ Designer uploads Measurement Report/LiDAR/File
→ Schedule becomes COMPLETED
→ Project Area becomes MEASURED/VERIFIED
→ Project becomes SPACE_VERIFIED
→ Project enters PROPOSAL_CONSULTING
```

## Integration Test Scenarios

```text
- Create nested Area hierarchy.
- Reject parent area from different Project.
- Create Measurement Schedule.
- Customer confirms own Schedule.
- Customer cannot confirm another Project's Schedule.
- Complete Measurement only after required file/data.
- Multiple Delivery Schedules allowed.
- Completed Schedule does not itself mark Order DELIVERED.
```

---

# 10. Project Chat Module

## Tables

```text
project_chats
project_chat_messages
```

## Chat Types

```text
SALES
DESIGNER
PRODUCTION
DELIVERY
GENERAL
INTERNAL
```

## Chat Status

```text
OPEN
CLOSED
ARCHIVED
```

## Message Types

```text
TEXT
FILE
SYSTEM
```

## Rules

```text
- Chat belongs to one Project.
- Staff chat access depends on assignment/role.
- CUSTOMER cannot access INTERNAL chat.
- FILE message may reference files.
- Soft-deleted message is excluded from normal query.
- Completed Project may close business chats and keep history read-only.
```

## Integration Test Scenarios

```text
- Create role-specific chat.
- Send text/file message.
- Enforce Customer visibility.
- Internal chat forbidden to Customer.
- Deleted message excluded.
- Completed Project chat read-only rule.
```

---

# 11. Notification Module

## Table

```text
notifications
```

## Common Events

```text
PROJECT_REQUEST_SUBMITTED
PROJECT_MORE_INFORMATION_REQUESTED
PROJECT_REQUEST_REJECTED
PROJECT_DESIGNER_ASSIGNED
PROJECT_SCHEDULE_CREATED
PROJECT_SCHEDULE_CONFIRMED
PROPOSAL_PUBLISHED
CUSTOMIZATION_REQUEST_CREATED
CUSTOMIZATION_PRODUCTION_REVIEWED
CUSTOMIZATION_WAITING_CUSTOMER_APPROVAL
QUOTATION_SENT
PAYMENT_CREATED
PAYMENT_PAID
PAYMENT_EXPIRED
PRODUCTION_REQUEST_ASSIGNED
PRODUCTION_ITEM_CANCELLED
ORDER_ADJUSTMENT_CONFIRMED
DELIVERY_SCHEDULE_CREATED
ORDER_DELIVERED
PROJECT_COMPLETED
```

## Rules

```text
- Notification side effects occur after successful business transaction.
- Duplicate webhook/command must not create duplicate important notification.
- receiver_id is required.
- deleted_at is soft delete.
- is_read/read_at controlled by notification API.
```

---

# 12. Proposal Module

## Tables

```text
proposals
proposal_scenes
proposal_items
proposal_scene_variants
files
file_links
```

## Proposal Status

```text
DRAFT
PUBLISHED
REVISION_REQUESTED
SELECTED
REJECTED
ARCHIVED
```

## Proposal Flow

```text
Designer creates Proposal
→ DRAFT

Designer creates Scene and Items
→ DRAFT

Designer publishes Proposal
→ PUBLISHED

Customer reviews
→ may request revision
→ Proposal REVISION_REQUESTED

Designer may create revised/new Proposal
→ DRAFT → PUBLISHED

Customer selects one final Proposal
→ selected Proposal SELECTED
→ other active proposals REJECTED/ARCHIVED according to rule
→ Project PROPOSAL_SELECTED
```

## Proposal Item Rules

```text
- Proposal Item contains Product only.
- product_version_id references original selected version.
- approved_product_version_id references accepted customization version.
- Snapshot dimensions/material/color/price do not auto-change from catalog.
- quantity must be positive.
- total_price_snapshot = quantity × unit_price_snapshot.
```

## Final Selection Gate

Customer cannot select final Proposal while Customization Request is unresolved.

Pending statuses:

```text
SUBMITTED
DESIGN_REVIEWING
PRODUCTION_REVIEWING
WAITING_FOR_CUSTOMER_FINAL_APPROVAL
```

Resolved statuses:

```text
ACCEPTED
REJECTED_BY_CUSTOMER
NOT_FEASIBLE
CANCELLED
```

## Integration Test Scenarios

```text
- Create multiple Proposals.
- Publish Proposal.
- Customer sees only published Proposal.
- Request revision.
- Select exactly one final Proposal.
- Block selection when customization pending.
- Prevent edit after SELECTED.
- Verify Proposal Item snapshots.
```

---

# 13. Room Planner & Scene Variant Module

## SQL Tables

```text
proposal_scenes
proposal_items
proposal_scene_variants
files
file_links
```

## MongoDB Documents

```text
official proposal scene
customer/designer variant scene
```

## Scene Types

```text
TWO_D
THREE_D
```

## Variant Types

```text
CUSTOMER_SUGGESTION
DESIGNER_REVISION
```

## Variant Status

```text
DRAFT
SUBMITTED
ACCEPTED
REJECTED
APPLIED
```

## Variant Flow

```text
Customer creates variant
→ DRAFT

Customer edits Mongo scene
→ DRAFT

Customer submits
→ SUBMITTED

Designer reviews
→ ACCEPTED or REJECTED

Designer applies accepted variant
→ APPLIED
→ official scene updated
```

## Rules

```text
- Customer cannot edit official Designer scene directly.
- Variant references official scene.
- MongoDB stores detailed transforms.
- SQL stores ownership, status, review and apply metadata.
- Applying variant must not silently alter commercial Proposal Items without sync validation.
```

## Integration Test Scenarios

```text
- Create Customer variant from published scene.
- Unauthorized Project access forbidden.
- Submit only DRAFT.
- Review only SUBMITTED.
- Apply only ACCEPTED.
- Reject duplicate apply.
- Mongo and SQL consistency when apply succeeds/fails.
```

---

# 14. Customization Module

## Table

```text
customization_requests
```

## Status

```text
SUBMITTED
DESIGN_REVIEWING
PRODUCTION_REVIEWING
WAITING_FOR_CUSTOMER_FINAL_APPROVAL
NOT_FEASIBLE
ACCEPTED
REJECTED_BY_CUSTOMER
CANCELLED
```

## Actors

```text
Create request:
CUSTOMER
assigned DESIGNER
ADMIN

Designer review:
assigned DESIGNER
ADMIN

Production review:
PRODUCTION
ADMIN

Final decision:
CUSTOMER
```

## Creator Rule

No additional creator field is added to `customization_requests`.

```text
requested_by_customer_id
= Customer who owns the requirement and makes final decision
```

Designer can create request for the Customer of an assigned Project, but the database does not persist a separate dedicated request-creator column.

## Flow

```text
Customer/Designer creates request
→ SUBMITTED

Designer reviews specification
→ PRODUCTION_REVIEWING

Production marks FEASIBLE
→ WAITING_FOR_CUSTOMER_FINAL_APPROVAL

Production marks NOT_FEASIBLE
→ NOT_FEASIBLE

Customer accepts
→ ACCEPTED
→ create PROJECT_SPECIFIC Product Version
→ set customization_requests.approved_product_version_id
→ set proposal_items.approved_product_version_id
→ update Proposal Item snapshots

Customer rejects
→ REJECTED_BY_CUSTOMER
→ Proposal Item unchanged

Authorized cancel before accepted
→ CANCELLED
```

## Production Review Rules

Feasible:

```text
material_available = true
estimated_production_days required
estimated_additional_cost required
additional_cost_reason required when cost > 0
```

Not feasible:

```text
status = NOT_FEASIBLE
Customer cannot accept
Proposal Item unchanged
```

## Price Rule

```text
estimated_additional_cost is additional unit cost

approved unit price
= original proposal item unit price
+ estimated additional cost

approved total
= approved unit price × quantity
```

## Accepted Product Version Rules

```text
- Same product_id as original Product Version.
- project_id = current Project.
- version_type = PROJECT_SPECIFIC.
- is_project_specific = true.
- is_public = false.
- is_default = false.
- Original Product Version remains unchanged.
- Null requested value falls back to original version value.
- Customer accept must be idempotent.
```

## Integration Test Scenarios

```text
- Customer creates request.
- Assigned Designer creates request for Customer.
- Unassigned Designer forbidden.
- Designer cannot set price.
- Production review mandatory.
- Sales cannot production-review.
- Customer cannot accept before review.
- Not feasible cannot be accepted.
- Accept creates one project-specific version.
- Retry accept does not duplicate version/cost.
- Reject leaves Proposal Item unchanged.
- Pending request blocks final Proposal selection.
```

---

# 15. Quotation Module

## Tables

```text
quotations
quotation_items
```

## Quotation Status

```text
DRAFT
SENT
REVISION_REQUESTED
REVISED
ACCEPTED
REJECTED
EXPIRED
CANCELLED
```

## Item Types

```text
PRODUCT_ITEM
MANUAL_ITEM
```

## Flow

```text
Project PROPOSAL_SELECTED
→ Sales creates Quotation DRAFT

Sales sends Quotation
→ SENT
→ Project QUOTATION_SENT

Customer requests revision
→ REVISION_REQUESTED
→ Project QUOTATION_REVISION_REQUESTED

Sales revises
→ REVISED

Sales sends revised quotation
→ SENT

Customer accepts
→ ACCEPTED
→ System creates Order and Order Items
→ Project ORDER_CONFIRMED

Customer rejects
→ REJECTED
```

## Quotation Item Rules

```text
PRODUCT_ITEM:
- based on Proposal Item/Product Version
- approved_product_version_id is preferred when customization accepted
- snapshot name/version/code/price/spec

MANUAL_ITEM:
- manual commercial line
- does not create Production Item
- does not participate in physical Delivery gate
```

## Integration Test Scenarios

```text
- Create only after final Proposal selected.
- Snapshot approved customized version.
- Send valid Quotation.
- Revision cycle.
- Expiration.
- Accept once creates exactly one Order.
- Duplicate accept idempotent.
- Cannot modify accepted Quotation.
```

---

# 16. Order Module

## Tables

```text
orders
order_items
```

## Order Status

```text
CREATED
DEPOSIT_PENDING
DEPOSIT_PAID
IN_PRODUCTION
READY_FOR_DELIVERY
DELIVERING
DELIVERED
FINAL_PAYMENT_PENDING
COMPLETED
CANCELLED
```

## Order Item Status

```text
PENDING
IN_PRODUCTION
READY
UNAVAILABLE
DELIVERED
CANCELLED
```

## Financial Fields

```text
original_total_amount
item_adjustment_amount
additional_discount_amount
final_total_amount
deposit_amount
paid_amount
remaining_amount
```

## Financial Formulas

```text
final_total_amount
= original_total_amount
- item_adjustment_amount
- additional_discount_amount

paid_amount
= total Order CHARGE Payments with status PAID
- excludes PROJECT_START_FEE

remaining_amount
= final_total_amount - paid_amount

remaining_amount must not be negative in current MVP
```

## Order Creation

```text
Accepted Quotation
→ Order CREATED
→ copy Quotation Items to Order Items
→ Order Items PENDING
→ delivered_quantity = 0
```

## Deposit Configuration

```text
Sales/Admin configures deposit_amount
→ create Deposit Payment
→ Order DEPOSIT_PENDING
```

## Deposit Success

```text
Deposit Payment PAID
→ recalculate paid_amount/remaining_amount
→ Order DEPOSIT_PAID
→ Project remains ORDER_CONFIRMED
→ no automatic Production Request
```

## Production Start

```text
Create Production Request
→ Product Order Items IN_PRODUCTION
→ Order IN_PRODUCTION
→ Project IN_PRODUCTION
```

## Production Completion Mapping

```text
Production Item COMPLETED
→ Order Item READY

Production Item CANCELLED
→ confirmed Adjustment applied
→ Order Item UNAVAILABLE

Order → READY_FOR_DELIVERY
Project → READY_FOR_DELIVERY
```

## Delivery

```text
Start Delivery
→ Order DELIVERING
→ Project DELIVERING

Update delivered_quantity
→ Order Item remains READY

Customer confirms full item
→ Order Item DELIVERED

Final available Product Item confirmed
→ Order DELIVERED
→ Project DELIVERED
→ customer_confirmed_delivery_at set
```

## Final Payment

```text
Order DELIVERED
→ recompute financial
→ if remaining_amount > 0:
   Order FINAL_PAYMENT_PENDING
→ if remaining_amount = 0:
   no Remaining Payment required
```

## Complete

```text
Sales/Admin explicit action
Preconditions:
- Delivery confirmed
- All adjustments APPLIED/CANCELLED
- remaining_amount = 0
  or Remaining Payment is PAID

Side effects:
Order COMPLETED
Project COMPLETED
Project completed_at set
```

## Integration Test Scenarios

```text
- Order snapshot creation.
- Manual Item behavior.
- Deposit configuration.
- Status synchronization with Payment.
- Status synchronization with Production.
- READY required for Delivery.
- Customer confirmation gate.
- Zero remaining amount completion.
- Positive remaining amount requires Payment.
- Payment webhook does not auto-complete.
```

---

# 17. Payment Module

## Tables

```text
payments
payment_transactions
```

## Payment Status

```text
PENDING
PROCESSING
PAID
CANCELLED
EXPIRED
REFUNDED
```

## Transaction Status

```text
PENDING
SUCCESS
FAILED
CANCELLED
```

## Payment Types

```text
PROJECT_START_FEE
DEPOSIT
REMAINING_PAYMENT
FULL_PAYMENT
REFUND
OTHER
```

## Providers

```text
PAYOS
SEPAY
CASH
MANUAL_BANK_TRANSFER
OTHER
```

## Core Rules

```text
- No partial payment.
- payments does not store paid_amount or remaining_amount.
- One Payment may have multiple Transaction attempts.
- One Payment may have at most one SUCCESS Transaction.
- SUCCESS Transaction amount must equal Payment amount.
- SUCCESS Transaction currency must equal Payment currency.
- FAILED/CANCELLED attempt does not make Payment FAILED.
- Active Payment status: PENDING or PROCESSING.
- Expired Payment cannot be retried.
- A new Payment is created after old Payment becomes EXPIRED.
- No replaced_payment_id.
```

## Required Partial Unique Indexes

```sql
CREATE UNIQUE INDEX uq_payment_transactions_one_success
ON payment_transactions(payment_id)
WHERE status = 'SUCCESS';
```

```sql
CREATE UNIQUE INDEX uq_payments_active_order_type
ON payments(order_id, payment_type)
WHERE order_id IS NOT NULL
  AND status IN ('PENDING', 'PROCESSING');
```

## Customer API Baseline

```http
GET  /api/payments
GET  /api/payments/summary
GET  /api/payments/{paymentId}
GET  /api/payments/{paymentId}/transactions
GET  /api/payments/{paymentId}/transactions/active
POST /api/payments/{paymentId}/transactions
PATCH /api/payments/{paymentId}/transactions/{transactionId}/cancel
```

## Transaction Attempt Flow

```text
Payment PENDING
→ Customer creates attempt
→ Transaction PENDING
→ Payment PROCESSING

Provider success
→ Transaction SUCCESS
→ Payment PAID
→ business effect once

Provider failed/cancelled
→ Transaction FAILED/CANCELLED
→ Payment PENDING if still valid
→ retry creates new Transaction attempt

Valid existing PENDING attempt:
→ backend may return existing transaction
→ response does not contain reused flag
```

## Project Start Fee Effect

```text
Payment PAID
→ unlock Designer assignment/Project approval flow
→ not included in Order paid_amount
```

## Deposit Effect

```text
Payment PAID
→ Order DEPOSIT_PAID
→ recompute Order paid_amount and remaining_amount
→ no automatic Production Request
```

## Remaining Payment Effect

```text
Payment PAID
→ recompute Order financial
→ does not auto-complete Order or Project
```

## Integration Test Scenarios

```text
- Create Payment obligation.
- Reuse active obligation for same Order and type.
- Expire then create replacement Payment.
- Multiple FAILED/CANCELLED attempts allowed.
- Underpayment rejected.
- Overpayment rejected.
- One SUCCESS enforced.
- Duplicate webhook idempotent.
- Order effect applied once.
- Transaction cancel behavior.
- Customer cannot access another Customer Payment.
```

---

# 18. Production Assignment Module

## Tables

```text
accounts
production_requests
```

## Available Staff Query

```text
Only ACTIVE PRODUCTION accounts.
```

Workload count statuses:

```text
PENDING_REVIEW
FEASIBLE
IN_PRODUCTION
BLOCKED
```

Workload is advisory for MVP.

Sales chooses the assignee.

## Assignment Rules

```text
- Production Request assigned_to is nullable.
- Request may be created unassigned.
- All Production Staff can view global queue.
- Sales/Admin can assign or reassign.
- Assignee must be ACTIVE PRODUCTION account.
- Reassign allowed while:
  PENDING_REVIEW
  FEASIBLE
  IN_PRODUCTION
  BLOCKED
- Reassign forbidden after COMPLETED/CANCELLED.
- Assign same staff is idempotent.
```

## Integration Test Scenarios

```text
- List only active Production accounts.
- Workload count correct.
- Create unassigned Request.
- Assign Request.
- Reassign Request.
- Non-Production assignee rejected.
- Inactive assignee rejected.
- Completed Request cannot reassign.
- Global queue visible regardless of assignment.
```

---

# 19. Production Module

## Tables

```text
production_requests
production_items
orders
order_items
order_adjustments
order_adjustment_items
```

## Production Request Status

```text
PENDING_REVIEW
FEASIBLE
IN_PRODUCTION
COMPLETED
BLOCKED
CANCELLED
```

## Production Item Status

```text
PENDING
IN_PRODUCTION
COMPLETED
BLOCKED
CANCELLED
```

## Create Production Request

Preconditions:

```text
Order DEPOSIT_PAID
Deposit Payment PAID
No duplicate active Production Request
```

Side effects:

```text
Create Production Request
Create Production Items only for PRODUCT_ITEM
Order Items → IN_PRODUCTION
Order → IN_PRODUCTION
Project → IN_PRODUCTION
```

## Review & Start

```text
PENDING_REVIEW → FEASIBLE
FEASIBLE → IN_PRODUCTION
BLOCKED → IN_PRODUCTION after resolve
```

## Production Item Transition

```text
PENDING → IN_PRODUCTION
IN_PRODUCTION → BLOCKED
BLOCKED → IN_PRODUCTION
IN_PRODUCTION → COMPLETED
PENDING/IN_PRODUCTION/BLOCKED → CANCELLED
```

Rules:

```text
- COMPLETED means full quantity.
- No partial production completion.
- CANCELLED requires cancellation_reason.
- Individual COMPLETED does not immediately set Order Item READY.
- Individual CANCELLED does not immediately set Order Item UNAVAILABLE.
```

## Request-Level Blocker

```text
Production Request BLOCKED
→ Project PRODUCTION_BLOCKED

Resolve blocker
→ Request IN_PRODUCTION
→ Project IN_PRODUCTION
```

Item-level BLOCKED does not automatically set Project PRODUCTION_BLOCKED unless business action blocks whole Request.

## Complete Production Request

Preconditions:

```text
All Production Items COMPLETED or CANCELLED
Every CANCELLED item covered by Customer-confirmed Adjustment
```

Atomic side effects:

```text
Apply all CONFIRMED Adjustments
COMPLETED Production Item → Order Item READY
CANCELLED Production Item → Order Item UNAVAILABLE
Production Request → COMPLETED
Order → READY_FOR_DELIVERY
Project → READY_FOR_DELIVERY
```

## Integration Test Scenarios

```text
- Create only after Deposit PAID.
- Manual Item excluded.
- Unique production_request_id + order_item_id.
- Valid/invalid item transition.
- Cancellation reason required.
- Complete blocked by unresolved Item.
- Complete blocked by missing Adjustment.
- Completed mapping to READY.
- Cancelled mapping to UNAVAILABLE.
- Complete idempotent.
```

---

# 20. Production Issue & Order Adjustment Module

## Tables

```text
order_adjustments
order_adjustment_items
production_items
order_items
orders
```

## Adjustment Status

```text
DRAFT
CONFIRMED
APPLIED
CANCELLED
```

## Adjustment Item Type

```text
UNAVAILABLE_ITEM
ADDITIONAL_DISCOUNT
```

## Current Supported Issue Flow

```text
Production Item cannot be completed
→ Production Item CANCELLED with reason
→ Sales discusses solution externally with Customer
→ Sales creates Adjustment DRAFT
→ Sales adds/edits/removes Adjustment Items
→ Customer confirms final content
→ Adjustment CONFIRMED
→ Production complete applies Adjustment
→ Adjustment APPLIED
```

## DRAFT Rules

```text
- Sales/Admin may edit.
- Header totals are calculated from items.
- Frontend totals are not source of truth.
```

## UNAVAILABLE_ITEM Rules

```text
order_item_id required
Order Item must belong to same Order
Related Production Item must be CANCELLED
adjustment_amount = full order_item.subtotal_amount
reason required
```

## ADDITIONAL_DISCOUNT Rules

```text
order_item_id may be null
adjustment_amount > 0
reason required
```

## Confirm Rules

```text
- Customer owns Project.
- Adjustment DRAFT.
- At least one item.
- CONFIRMED becomes immutable.
- There is no rejected status.
- If Customer disagrees, Sales keeps editing DRAFT.
```

## Apply Rules

```text
- Apply only during Production Request completion.
- Recalculate Order summary from all APPLIED Adjustments.
- Do not increment totals blindly.
- If final_total_amount < paid_amount:
  rollback
  return ADJUSTMENT_REQUIRES_REFUND_FLOW
```

## Deferred Issue Extensions

Not currently in main MVP:

```text
- Partial quantity reduction.
- Deadline extension agreement entity.
- Product replacement.
- Delivery-damaged issue.
- Refund execution.
```

## Integration Test Scenarios

```text
- Create DRAFT.
- Add unavailable item.
- Reject non-cancelled Production Item.
- Enforce full subtotal deduction.
- Add compensation discount.
- Recalculate totals.
- Confirm by wrong Customer forbidden.
- Confirm locks editing.
- Apply once.
- Refund guard rolls back transaction.
```

---

# 21. Delivery Module

## Tables

```text
project_schedules
orders
order_items
files
file_links
```

No tables:

```text
deliveries
delivery_items
```

## Delivery Schedule

```text
schedule_type = DELIVERY
```

Multiple schedules are allowed.

## Create Schedule

Precondition:

```text
Order/Project READY_FOR_DELIVERY or DELIVERING
```

Initial status:

```text
PENDING_CONFIRMATION
```

Customer may confirm:

```text
CONFIRMED
```

Schedule completion does not automatically set Order DELIVERED.

## Start Delivery

Preconditions:

```text
Order READY_FOR_DELIVERY
At least one confirmed Delivery Schedule
```

Side effects:

```text
Order DELIVERING
Project DELIVERING
```

## Update Delivered Quantity

Preconditions:

```text
Order DELIVERING
Order Item PRODUCT_ITEM
Order Item READY
Order Item not UNAVAILABLE/CANCELLED
increment > 0
new delivered_quantity <= quantity
```

Side effects:

```text
delivered_quantity += increment
last_delivered_at set
last_delivered_by set
Order Item remains READY
```

## Customer Confirm Item

Preconditions:

```text
Customer owns Order
Order DELIVERING
Order Item PRODUCT_ITEM
Order Item READY
delivered_quantity = quantity
customer_confirmed_at is null
```

Side effects:

```text
Order Item DELIVERED
customer_confirmed_at set
```

## Delivery Completion Gate

Included:

```text
available PRODUCT_ITEM
```

Excluded:

```text
MANUAL_ITEM
UNAVAILABLE
CANCELLED
```

When final available Product Item is DELIVERED:

```text
orders.customer_confirmed_delivery_at set
Order DELIVERED
Project DELIVERED
```

## Integration Test Scenarios

```text
- Multiple Delivery Schedules.
- Start without confirmed schedule rejected.
- Deliver only READY Item.
- Atomic increment.
- Exceed quantity rejected.
- Full quantity still READY until Customer confirm.
- Customer confirm wrong owner forbidden.
- Final item completes Order/Project.
- Manual/unavailable item excluded from gate.
```

---

# 22. Final Payment & Completion Module

## Tables

```text
orders
payments
payment_transactions
projects
notifications
```

## Enter Final Payment Phase

Preconditions:

```text
Order DELIVERED
customer_confirmed_delivery_at is set
All Adjustments APPLIED or CANCELLED
Financial summary recalculated
remaining_amount >= 0
```

Behavior:

```text
remaining_amount > 0
→ Order FINAL_PAYMENT_PENDING

remaining_amount = 0
→ no Remaining Payment
→ Order can be explicitly completed
```

## Create Remaining Payment

```text
Actor: SALES/ADMIN
Order must be FINAL_PAYMENT_PENDING
Payment type REMAINING_PAYMENT
Payment amount = Order remaining_amount
Reuse active Payment obligation if valid
Create new Payment after old one EXPIRED/CANCELLED
```

## Explicit Complete

Preconditions:

```text
Delivery fully confirmed
All Adjustments APPLIED/CANCELLED
If remaining_amount > 0:
Remaining Payment PAID
If remaining_amount = 0:
No Payment required
```

Side effects:

```text
Order COMPLETED
Project COMPLETED
projects.completed_at set
Notification created
```

Payment webhook must not perform this completion.

## Integration Test Scenarios

```text
- Enter final payment only after Delivery.
- Block if Adjustment not applied.
- Zero remaining amount.
- Positive remaining amount.
- Remaining Payment amount exact.
- Remaining Payment PAID does not auto-complete.
- Explicit complete by Sales/Admin.
- Duplicate complete idempotent.
```

---

# 23. Project Review Module

## Table

```text
project_reviews
```

## Rules

```text
- Project must be COMPLETED.
- Customer must own Project.
- One review per Project.
- rating fields must follow configured range.
- Customer may update own review according to current policy.
```

## Main Fields

```text
rating
design_quality_rating
service_quality_rating
delivery_rating
comment
```

## Integration Test Scenarios

```text
- Create review after completion.
- Reject before completion.
- Reject another Customer.
- Reject duplicate review.
- Update own review.
```

---

# 24. Cross-Module Status Synchronization Matrix

| Business Action | Project | Order | Order Item | Production Request | Production Item | Payment | Adjustment |
|---|---|---|---|---|---|---|---|
| Submit Project | SUBMITTED | — | — | — | — | — | — |
| Receive Project | IN_CONSULTATION | — | — | — | — | — | — |
| Assign Designer | MEASUREMENT_REQUIRED or SPACE_VERIFIED | — | — | — | — | Start fee PAID | — |
| Enter Proposal | PROPOSAL_CONSULTING | — | — | — | — | — | — |
| Select Proposal | PROPOSAL_SELECTED | — | — | — | — | — | — |
| Send Quotation | QUOTATION_SENT | — | — | — | — | — | — |
| Accept Quotation | ORDER_CONFIRMED | CREATED | PENDING | — | — | — | — |
| Create Deposit | ORDER_CONFIRMED | DEPOSIT_PENDING | PENDING | — | — | PENDING | — |
| Deposit Paid | ORDER_CONFIRMED | DEPOSIT_PAID | PENDING | — | — | PAID | — |
| Create Production Request | IN_PRODUCTION | IN_PRODUCTION | IN_PRODUCTION | PENDING_REVIEW | PENDING | — | — |
| Start Production | IN_PRODUCTION | IN_PRODUCTION | IN_PRODUCTION | IN_PRODUCTION | PENDING/IN_PRODUCTION | — | — |
| Cancel Production Item | IN_PRODUCTION | IN_PRODUCTION | IN_PRODUCTION | IN_PRODUCTION | CANCELLED | — | DRAFT required |
| Customer Confirms Adjustment | IN_PRODUCTION | IN_PRODUCTION | IN_PRODUCTION | IN_PRODUCTION | CANCELLED | — | CONFIRMED |
| Complete Production | READY_FOR_DELIVERY | READY_FOR_DELIVERY | READY/UNAVAILABLE | COMPLETED | COMPLETED/CANCELLED | — | APPLIED |
| Start Delivery | DELIVERING | DELIVERING | READY/UNAVAILABLE | COMPLETED | terminal | — | APPLIED |
| Update Delivered Qty | DELIVERING | DELIVERING | READY | COMPLETED | terminal | — | APPLIED |
| Confirm Final Item | DELIVERED | DELIVERED | DELIVERED/UNAVAILABLE | COMPLETED | terminal | — | APPLIED |
| Create Remaining Payment | DELIVERED | FINAL_PAYMENT_PENDING | terminal delivery state | COMPLETED | terminal | PENDING | APPLIED |
| Remaining Paid | DELIVERED | FINAL_PAYMENT_PENDING | terminal delivery state | COMPLETED | terminal | PAID | APPLIED |
| Explicit Complete | COMPLETED | COMPLETED | terminal | COMPLETED | terminal | PAID or none required | APPLIED/CANCELLED |

---

# 25. Transaction Boundaries

Các action sau phải chạy trong một database transaction:

```text
- Select final Proposal.
- Accept Quotation and create Order.
- Create Production Request and Production Items.
- Payment SUCCESS and Order business effect.
- Customer accepts customization and creates project-specific Product Version.
- Complete Production Request and apply Adjustments.
- Customer confirms final Delivery Item and updates Order/Project.
- Explicit complete Order and Project.
```

Rollback toàn bộ khi một side effect fail.

---

# 26. Idempotency Requirements

Integration Test cần gọi lặp các action quan trọng:

```text
- Receive Project.
- Assign same staff.
- Select final Proposal.
- Accept Quotation.
- Customer accepts customization.
- Process Payment webhook.
- Complete Production Request.
- Confirm Order Adjustment.
- Confirm Delivery Item.
- Enter Final Payment.
- Create Remaining Payment.
- Complete Order/Project.
```

Expected:

```text
- Không duplicate entity.
- Không cộng tiền hai lần.
- Không apply Adjustment hai lần.
- Không tạo Product Version hai lần.
- Không tạo notification quan trọng hai lần.
- Response trả trạng thái hiện tại hoặc success idempotent.
```

---

# 27. Concurrency Requirements

## Payment

```text
- Concurrent active Payment creation:
  one active Payment only.
- Concurrent SUCCESS webhook:
  one SUCCESS Transaction only.
```

## Production

```text
- Concurrent Production complete:
  Adjustment and status apply once.
```

## Delivery

```text
- Concurrent delivered quantity update:
  no lost update.
  never exceed quantity.
```

## Customization

```text
- Concurrent Customer acceptance:
  one approved Product Version only.
```

## Quotation

```text
- Concurrent Quotation accept:
  one Order only.
```

---

# 28. Suggested Integration Test Suites

## Suite A — Project Intake

```text
Project submit
Sales receive
Request more info
Customer update
Approve/reject
Project Start Fee
Designer assignment
```

## Suite B — Measurement

```text
Area creation
Measurement Schedule
Customer confirmation
Designer completion
Space verification
```

## Suite C — Proposal & Room Planner

```text
Proposal create
Scene create
Mongo save/load
Proposal Item sync
Publish
Variant
Revision
Final selection
```

## Suite D — Customization

```text
Customer/Designer create
Designer review
Production review
Customer decision
Project-specific Product Version
Selection blocker
```

## Suite E — Quotation & Order

```text
Create Quotation
Revision
Accept
Order snapshot
Deposit configuration
```

## Suite F — Payment

```text
Obligation
Attempt
Webhook
Retry
Expire
Order business effect
```

## Suite G — Production

```text
Available staff
Assign
Request review
Item lifecycle
Issue
Adjustment
Complete
Status synchronization
```

## Suite H — Delivery

```text
Schedules
Start
Delivered quantity
Customer confirmation
Completion gate
```

## Suite I — Final Payment & Completion

```text
Final payment phase
Remaining Payment
Payment success
Explicit complete
Review
```

## Suite J — Cross-Cutting

```text
Authorization
Files
Notifications
Idempotency
Concurrency
Rollback
```

---

# 29. Recommended Test Data Graph

```text
Customer A
Sales A
Designer A
Production A
Production B
Admin A

Project A owned by Customer A
Project B owned by Customer B

Category
Product
Standard Product Version

Project A:
Area A
Proposal A1
Proposal A2
Scene A1
Proposal Item A1
Customization Request A1
Project-Specific Product Version A1
Quotation A1
Order A1
Product Order Item A1
Manual Order Item A2
Deposit Payment
Production Request A1
Production Item A1
Order Adjustment A1
Delivery Schedules A1/A2
Remaining Payment
Project Review
```

Dùng Project B để test cross-project authorization.

---

# 30. Current Deferred / Out-of-Scope Flows

Không coi các flow sau là main-flow requirement hiện tại:

```text
- Partial payment.
- Partial Production completion quantity.
- Quantity reduction agreement.
- Contract amendment.
- Full Contract module.
- Refund execution.
- Delivery damaged/wrong/refused issue.
- Warranty.
- Repair.
- Maintenance.
- After-sales ticket.
- Delivery Staff role.
- deliveries/delivery_items tables.
```

Current guard:

```text
final_total_amount < paid_amount
→ ADJUSTMENT_REQUIRES_REFUND_FLOW
```

---

# 31. Final Main Flow Assertion

Một Integration Test end-to-end thành công phải chứng minh:

```text
1. Customer tạo Project.
2. Sales tiếp nhận và hoàn tất intake.
3. Project Start Fee được thanh toán đủ.
4. Designer được assign.
5. Space được verified.
6. Proposal được tạo, publish và chọn final.
7. Customization được xử lý đầy đủ nếu có.
8. Quotation được accept.
9. Order được tạo đúng snapshot.
10. Deposit được thanh toán đủ.
11. Production Request được tạo và assign.
12. Production Item được hoàn tất hoặc xử lý Adjustment.
13. Production completion set Order Items READY/UNAVAILABLE.
14. Delivery chỉ xử lý READY Items.
15. Customer xác nhận từng Item đã giao đủ.
16. Remaining Payment được tạo đúng số tiền nếu cần.
17. Payment success không tự complete.
18. Sales/Admin complete Order và Project.
19. Customer review Project.
20. Tất cả status, financial, file, notification và ownership đều nhất quán.
```
