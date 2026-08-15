# FurniSpace REST API Reference

Complete HTTP + SignalR API reference for FurniSpace backend.

| Item | Value |
| --- | --- |
| Spec source | Controllers under `src/FurniSpace.API/Controllers` + DTOs under `src/FurniSpace.Application/DTOs` |
| Live OpenAPI | `GET /swagger/v1/swagger.json` (Swagger UI at `/`) |
| Related | `docs/backend-api-dev-guide.md`, `docs/payment-service-guide.md`, `docs/signalr-notification-guide.md` |

---

## Table of contents

1. [Conventions](#1-conventions)
2. [Authentication](#2-authentication)
3. [Auth](#3-auth--auth)
4. [Accounts](#4-accounts)
4b. [Admin Reports](#4b-admin-reports-scrum-428--scrum-436)
5. [Catalog — Business Types](#5-catalog--business-types)
6. [Catalog — Categories](#6-catalog--categories)
7. [Catalog — Products](#7-catalog--products)
8. [Catalog — Product Versions](#8-catalog--product-versions)
8a. [Catalog — Admin management list](#8a-catalog--admin-management-list)
8b. [Catalog — Designer project catalog](#8b-catalog--designer-project-catalog)
9. [Projects](#9-projects)
10. [Proposals & scenes](#10-proposals--scenes)
11. [Room planner](#11-room-planner)
12. [Quotations](#12-quotations)
13. [Orders](#13-orders)
14. [Customization requests](#14-customization-requests)
15. [Project areas](#15-project-areas)
16. [Project schedules](#16-project-schedules)
17. [Project files & shared files](#17-project-files--shared-files)
18. [Chat](#18-chat)
19. [Notifications](#19-notifications)
20. [Payments](#20-payments)
20a. [Admin Financial Dashboard](#20a-admin-financial-dashboard)
21. [Production](#21-production)
22. [SignalR hubs](#22-signalr-hubs)
23. [Enums](#23-enums)

---

## 1. Conventions

### 1.1 Base URL & routing

There is **no single global `/api` prefix**. Routes are defined per controller:

| Pattern | Examples |
| --- | --- |
| Root kebab-case | `/auth`, `/products`, `/projects`, `/business-types` |
| Explicit `/api/...` | `/api/payments`, `/api/projects/.../payments/...`, `/api/webhooks/...` |
| Base `[controller]` | `/api/Accounts`, `/api/ProductVersions/...` |

### 1.2 Response envelope

Every controller action returns `ServiceResult` / `ServiceResult<T>` via `BaseApiController.ToActionResult`. HTTP status code equals `status`.

```json
{
  "status": 200,
  "message": "Success",
  "data": {},
  "errors": null,
  "errorCode": null
}
```

| Field | Type | Notes |
| --- | --- | --- |
| `status` | `int` | Same as HTTP status (`200`, `201`, `400`, `401`, `403`, `404`, `409`, `413`, `415`, `429`, `500`) |
| `message` | `string?` | Human-readable summary |
| `data` | `T?` / `object?` | Payload; may be `null` on pure success messages |
| `errors` | `string[]?` | Omitted when null; validation / field errors |
| `errorCode` | `string?` | Omitted when null; machine-readable code (e.g. `INVALID_BUSINESS_TYPE_FILTER`) |

**Paged lists** often nest pagination inside `data`:

```json
{
  "status": 200,
  "message": "Success",
  "data": {
    "items": [],
    "page": 1,
    "limit": 20,
    "total": 100
  }
}
```

Some modules use `pageSize` / `totalItems` / `totalPages` / `hasPreviousPage` / `hasNextPage` (`PagedResult<T>`). Field names follow the DTO for that endpoint.

### 1.3 JSON conventions

| Topic | Rule |
| --- | --- |
| Property names | **camelCase** (ASP.NET default), except auth expiry fields which use **snake_case** (`token_type`, `expires_in`, `access_token_expires_at`) |
| Enums | String values in **SCREAMING_SNAKE_CASE** (`JsonStringEnumConverter`, no naming policy) |
| Dates | ISO-8601 (`DateTime` / `DateTimeOffset`); `DateOnly` as `YYYY-MM-DD` |
| IDs | UUID (`guid`) unless noted (`businessTypeId` is `int`) |
| Content-Type | `application/json` unless multipart upload |

### 1.4 Roles

| Role | Typical access |
| --- | --- |
| `CUSTOMER` | Own projects, proposals, quotations, orders, payments, chat |
| `SALES` | Project intake, quotations, orders, assignments, schedules |
| `DESIGNER` | Proposals, scenes, room planner, limited project status |
| `PRODUCTION` | Production requests/items, delivery ops, customization queue |
| `ADMIN` | Full admin + catalog + accounts |

Authorization: `[Authorize]` / `[Authorize(Roles = "...")]`. Multiple roles in one attribute are OR.

### 1.5 Common error examples

**Validation (400)**

```json
{
  "status": 400,
  "message": "Validation failed",
  "data": null,
  "errors": ["Email is required", "Password must be at least 8 characters"]
}
```

**Unauthorized (401)** / **Forbidden (403)** / **Not found (404)** / **Conflict (409)** / **Too many requests (429)** follow the same envelope with `data` usually null.

### 1.6 Auth header / cookies

```http
Authorization: Bearer {access_token}
```

Also accepted: HttpOnly cookies `access_token` and `refresh_token` (Secure, SameSite=None, Path=/).

Public auth routes are rate-limited: policy `auth-public` → **10 requests / minute / IP**.

---

## 2. Authentication

### Token delivery

| Endpoint | Sets cookies? | Tokens in JSON body? |
| --- | --- | --- |
| `POST /auth/login` | Yes | **No** (`access_token` / `refresh_token` are `[JsonIgnore]`) |
| `POST /auth/verify-email` | Yes | No |
| `POST /auth/refresh` | Yes | No |
| `POST /auth/logout` | Clears cookies | — |

**Auth success `data` shape** (`AuthResponseDto`):

```json
{
  "access_token_expires_at": "2026-07-27T12:00:00+00:00",
  "token_type": "Bearer",
  "expires_in": 900
}
```

Clients that cannot use cookies must read tokens from a custom FE bridge or extend the API; the current backend intentionally keeps tokens out of the JSON body and sets HttpOnly cookies.

Refresh may send `refreshToken` in the body **or** rely on the `refresh_token` cookie.

SignalR: for `/hubs/notifications` and `/hubs/project-chat`, `?access_token=` query is also accepted.

---

## 3. Auth — `/auth`

Controller: `AuthController` · route `auth`

| Method | Path | Auth | Rate limit | Description |
| --- | --- | --- | --- | --- |
| POST | `/auth/register` | Public | Yes | Register customer account + send email OTP |
| POST | `/auth/verify-email` | Public | Yes | Verify OTP → session cookies |
| POST | `/auth/resend-verification-otp` | Public | Yes | Resend OTP (enumeration-safe) |
| POST | `/auth/login` | Public | Yes | Login → session cookies |
| POST | `/auth/refresh` | Public | Yes | Rotate tokens |
| POST | `/auth/forgot-password` | Public | Yes | Request reset email (enumeration-safe) |
| POST | `/auth/reset-password` | Public | Yes | Reset with email token |
| GET | `/auth/me` | JWT | — | Current user profile |
| PATCH | `/auth/me` | JWT | — | Update profile |
| PATCH | `/auth/me/password` | JWT | — | Change password |
| POST | `/auth/logout` | JWT | — | Revoke refresh + blacklist access `jti` |

### `POST /auth/register`

**Request**

```json
{
  "email": "customer@example.com",
  "password": "Str0ngPass!",
  "fullName": "Nguyen Van A",
  "phone": "+84901234567"
}
```

| Field | Type | Required |
| --- | --- | --- |
| `email` | string | Yes |
| `password` | string | Yes |
| `fullName` | string | Yes |
| `phone` | string? | No |

**Response** `201` — account created; if email send fails, still `201` with delivery status in message/data (resend OTP allowed).

### `POST /auth/verify-email`

**Request**

```json
{
  "email": "customer@example.com",
  "otpCode": "123456"
}
```

**Response** `200` — `AuthResponseDto` + Set-Cookie.

### `POST /auth/login`

**Request**

```json
{
  "email": "customer@example.com",
  "password": "Str0ngPass!"
}
```

**Response** `200` — `AuthResponseDto` + Set-Cookie.

### `POST /auth/refresh`

**Request** (optional body; cookie fallback)

```json
{
  "refreshToken": "..."
}
```

**Response** `200` — `AuthResponseDto` + rotated cookies.

### `POST /auth/forgot-password` / `POST /auth/reset-password`

```json
{ "email": "customer@example.com" }
```

```json
{
  "email": "customer@example.com",
  "token": "reset-token-from-email",
  "newPassword": "NewStr0ngPass!"
}
```

### `GET /auth/me`

**Response `data`** (`CurrentUserDto`)

```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "customer@example.com",
  "fullName": "Nguyen Van A",
  "phone": "+84901234567",
  "avatarUrl": null,
  "status": "ACTIVE",
  "role": "CUSTOMER"
}
```

### `PATCH /auth/me`

```json
{
  "fullName": "Nguyen Van A",
  "phone": "+84901234567"
}
```

### `PATCH /auth/me/password`

```json
{
  "currentPassword": "OldPass!",
  "newPassword": "NewPass!"
}
```

### `POST /auth/logout`

```json
{
  "refreshToken": "optional-if-cookie-present"
}
```

**Response** `200` — `{ "status": 200, "message": "Logged out successfully", "data": null }`

---

## 4. Accounts

Controller: `AccountsController`

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/Accounts` | ⚠️ None on controller (known gap) | List accounts |
| GET | `/api/Accounts/{accountId}` | ⚠️ None | Get by id |
| POST | `/api/Accounts` | ⚠️ None | Create account |
| PUT | `/api/Accounts/{accountId}` | ⚠️ None | Update account |
| DELETE | `/api/Accounts/{accountId}` | ⚠️ None | Soft-delete style remove |
| GET | `/admin/accounts/suggest` | ADMIN | Suggest accounts |
| GET | `/admin/accounts/search-stats` | ADMIN | Facet stats |
| GET | `/admin/accounts/{accountId}` | ADMIN | Admin detail |
| GET | `/accounts/designers/available` | SALES, ADMIN | Designers with capacity counters |
| GET | `/admin/designers/workload` | ADMIN | Designer workload board (filter/sort) |
| GET | `/admin/designers/workload/summary` | ADMIN | Workload summary cards |
| GET | `/admin/designers/{designerId}/projects` | ADMIN | Designer assigned projects drill-down |
| GET | `/admin/sales/workload` | ADMIN | Sales workload + future pressure board |
| GET | `/admin/sales/workload/summary` | ADMIN | Sales workload summary cards |
| GET | `/admin/sales/{salesId}/projects` | ADMIN | Sales assigned projects drill-down |
| GET | `/admin/sales/unassigned-intake` | ADMIN | SUBMITTED projects with no sales |
| PATCH | `/accounts/me` | JWT | Update my profile |

### Query — `GET /api/Accounts`

| Param | Type | Default / notes |
| --- | --- | --- |
| `page` | int | pagination |
| `pageSize` | int | pagination |
| `search` | string? | |
| `status` | string? | |
| `includeDeleted` | bool | |

### `POST /api/Accounts` — body

```json
{
  "roleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "sales@example.com",
  "password": "Str0ngPass!",
  "fullName": "Sales User",
  "phone": null,
  "avatarUrl": null,
  "status": "ACTIVE"
}
```

### Response — `AccountDto` / `AccountDetailDto`

```json
{
  "accountId": "...",
  "roleId": "...",
  "email": "sales@example.com",
  "fullName": "Sales User",
  "phone": null,
  "avatarUrl": null,
  "status": "ACTIVE",
  "createdAt": "2026-07-01T00:00:00Z",
  "updatedAt": null,
  "deletedAt": null
}
```

`AccountDetailDto` nests `role: { roleId, roleName, description? }`.

### `GET /accounts/designers/available`

**Auth:** SALES, ADMIN  
**Query:** `page`, `pageSize`, `search?`

Used by Sales/Admin assign picker. Soft capacity only (does not hide FULL/OVER designers).

**Response item** (`AvailableDesignerDto`):

| Field | Notes |
| --- | --- |
| `accountId`, `email`, `fullName`, `phone?`, `avatarUrl?`, `status?` | Identity |
| `designActiveCount` | Projects in `MEASUREMENT_REQUIRED`, `SPACE_VERIFIED`, `PROPOSAL_CONSULTING` |
| `lifecycleAssignedCount` | Non-terminal projects still assigned |
| `currentActiveProjectCount` | Alias of `designActiveCount` (backward compatible) |
| `maxActiveProjects` | Soft limit (default **2**) |
| `availableSlot` | `maxActiveProjects - designActiveCount` (may be negative) |
| `capacityState` | `AVAILABLE` \| `FULL` \| `OVER` |
| `createdAt?`, `updatedAt?` | |

### `GET /admin/designers/workload`

**Auth:** ADMIN  
**Query:** `page`, `pageSize`, `search?`, `capacityState?` (`AVAILABLE`\|`FULL`\|`OVER`), `sortBy?` (`DesignActiveCountDesc` default \| `AvailableSlotDesc`)

Same item shape as available designers. Default sort: overload first (`DesignActiveCountDesc`).

### `GET /admin/designers/workload/summary`

**Auth:** ADMIN  

**Response** (`DesignerWorkloadSummaryDto`): `totalActiveDesigners`, `availableCount`, `fullCount`, `overCount`, `totalDesignActiveProjects`, `maxActiveProjects`

### `GET /admin/designers/{designerId}/projects`

**Auth:** ADMIN  
**Query:** `page`, `pageSize`, `bucket?` (`DESIGN_ACTIVE`\|`POST_DESIGN`\|`TERMINAL`\|`OTHER`)

Drill-down for Admin Designer Workload board.

**Response item** (`DesignerAssignedProjectDto`): `projectId`, `projectCode?`, `projectName`, `status?`, `designerAssignedAt?`, `customerId`, `customerName?`, `assignedSalesId?`, `salesName?`, `bucket`

### `GET /admin/sales/workload`

**Auth:** ADMIN  
**Query:** `page`, `pageSize`, `search?`, `capacityState?` (`AVAILABLE_NOW`\|`FULL_NOW`\|`OVER_NOW`), `futurePressureState?` (`LOW`\|`MEDIUM`\|`HIGH`), `sortBy?` (`FuturePressureScoreDesc` default \| `SalesActiveCountDesc` \| `AvailableSlotAsc`)

Soft capacity max = **5**. Current slot = `intakeCount + commercialCount` only.

**Response item** (`SalesWorkloadItemDto`): identity + `intakeCount`, `commercialCount`, `designMonitorCount`, `fulfillmentCount`, `salesActiveCount`, `lifecycleAssignedCount`, `maxActiveProjects`, `availableSlot`, `capacityState`, `futurePressureScore`, `futurePressureState`, `approachingCommercialCount`, `productionAttentionCount`, `deliveryAttentionCount`, `futurePressureBreakdown{...}`

### `GET /admin/sales/workload/summary`

**Auth:** ADMIN  

**Response** (`SalesWorkloadSummaryDto`): `totalActiveSales`, `availableNowCount`, `fullNowCount`, `overNowCount`, `highFuturePressureCount`, `totalSalesActiveProjects`, `unassignedIntakeCount`, `maxActiveProjects`

### `GET /admin/sales/{salesId}/projects`

**Auth:** ADMIN  
**Query:** `page`, `pageSize`, `bucket?` (`CURRENT_ACTIVE`\|`INTAKE`\|`COMMERCIAL`\|`DESIGN_MONITOR`\|`FULFILLMENT`\|`TERMINAL`\|`OTHER`\|`HIGH_PRESSURE_SOURCE`)

**Response item** (`SalesAssignedProjectDto`): `projectId`, `projectCode?`, `projectName`, `status?`, `salesAssignedAt?`, `customerId`, `customerName?`, `assignedDesignerId?`, `designerName?`, `bucket`, `pressureWeight`

### `GET /admin/sales/unassigned-intake`

**Auth:** ADMIN  
**Query:** `page`, `pageSize`

Only `status = SUBMITTED` AND `assigned_sales_id IS NULL`.

**Response item** (`UnassignedIntakeProjectDto`): `projectId`, `projectCode?`, `projectName`, `businessType?`, `submittedAt?`, `customerId`, `customerName?`

### `PATCH /accounts/me`

```json
{ "fullName": "Name", "phone": "+84..." }
```

**Response:** `MyProfileDto` — `accountId`, `email`, `fullName`, `phone?`, `avatarUrl?`, `role`, `status`, `updatedAt?`

---

## 5. Catalog — Business Types

Route: `business-types`

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/business-types` | Public | List (filter/page) |
| GET | `/business-types/{id}` | Public | Detail (`id` = int) |
| POST | `/business-types` | ADMIN | Create |
| PATCH | `/business-types/{id}` | ADMIN | Update |
| PATCH | `/business-types/{id}/status` | ADMIN | Activate / deactivate |

### Query — list

| Param | Type | Notes |
| --- | --- | --- |
| `status` | bool? | |
| `keyword` | string? | |
| `page` | int | |
| `limit` | int | |

### Create

```json
{
  "code": "CAFE",
  "name": "Cafe",
  "description": "Coffee shop furniture"
}
```

### Update

```json
{
  "name": "Cafe",
  "description": "..."
}
```

### Status

```json
{ "status": true }
```

### Response — `BusinessTypeDto`

```json
{
  "id": 1,
  "code": "CAFE",
  "name": "Cafe",
  "description": "...",
  "status": true,
  "createdAt": "2026-07-01T00:00:00Z",
  "updatedAt": null
}
```

List `data`: `{ items, page, limit, total }`

> Product `businessTypeIds` uses these IDs. Project `businessType` field remains free-text and is **not** FK-linked.

---

## 6. Catalog — Categories

Route: `categories`

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/categories` | Public · query `page`, `limit` |
| POST | `/categories` | ADMIN |
| PUT | `/categories/{categoryId}` | ADMIN |

### Create / update body

```json
{
  "categoryName": "Tables",
  "description": "Dining and cafe tables"
}
```

### Response — `CategoryDto`

```json
{
  "categoryId": "...",
  "categoryName": "Tables",
  "description": "...",
  "status": "ACTIVE"
}
```

List: `{ items, page, limit, total }`

---

## 7. Catalog — Products

Route: `products` (+ preview files controller)

| Method | Path | Auth | Description |
| --- | --- | --- | --- |
| GET | `/products/suggest` | Public | Autocomplete |
| GET | `/products/search` | Public | Elasticsearch search |
| GET | `/products` | Public | List |
| GET | `/products/{productId}` | Public | Detail |
| GET | `/products/{productId}/similar` | Public | Similar products |
| GET | `/products/by-category/{categoryId}` | Public | By category |
| GET | `/products/{productId}/preview-files` | Public | Preview images |
| POST | `/products` | ADMIN | Create |
| PATCH | `/products/{productId}` | ADMIN | Update |
| PATCH | `/products/{productId}/activate` | ADMIN | Lifecycle → ACTIVE (from INACTIVE) |
| PATCH | `/products/{productId}/deactivate` | ADMIN | Lifecycle → INACTIVE (from ACTIVE) |
| PATCH | `/products/{productId}/archive` | ADMIN | Lifecycle → ARCHIVED (from ACTIVE/INACTIVE) |
| PATCH | `/products/{productId}/restore` | ADMIN | Lifecycle → ACTIVE (from ARCHIVED) |
| POST | `/products/{productId}/files` | ADMIN | Multipart catalog file |
| POST | `/products/{productId}/preview-files` | ADMIN | Multipart preview image |
| PATCH | `/products/{productId}/preview-files/reorder` | ADMIN | Reorder |
| DELETE | `/products/{productId}/preview-files/{fileId}` | ADMIN | Delete preview |

### Query — list / by-category

| Param | Type | Notes |
| --- | --- | --- |
| `page` | int | |
| `limit` | int | |
| `businessTypeIds` | int[] | ANY overlap (`&&`); invalid ≤0 → `400 INVALID_BUSINESS_TYPE_FILTER` |
| `includeDefaultVersion` | bool | by-category only |

### Query — search

| Param | Type |
| --- | --- |
| `query` | string? |
| `categoryId` | guid? |
| `businessTypeIds` | int[]? |
| `material` | string? |
| `color` | string? |
| `minPrice` / `maxPrice` | decimal? |
| `sort` | string? |
| `page` / `limit` | int |

### Query — suggest / similar

| Endpoint | Params |
| --- | --- |
| suggest | `q`, `limit` |
| similar | `limit` |

### Create

```json
{
  "categoryId": "...",
  "productCode": "TBL-001",
  "productName": "Oak Cafe Table",
  "description": "...",
  "businessTypeIds": [1, 2]
}
```

| `businessTypeIds` | Meaning |
| --- | --- |
| `null` | no assignment stored |
| `[]` | explicitly none |
| `[1,2]` | assigned types (validated active IDs) |

### Update

```json
{
  "categoryId": "...",
  "productName": "Oak Cafe Table",
  "description": "...",
  "businessTypeIds": [1]
}
```

### Multipart — catalog file

`Content-Type: multipart/form-data`

| Field | Type |
| --- | --- |
| `file` | file |
| `fileType` | `FileType` enum |
| `visibility` | `FileVisibility?` |
| `description` | string? |
| `displayOrder` | int? |

### Multipart — preview image

| Field | Type |
| --- | --- |
| `file` | file |
| `description` | string? |
| `displayOrder` | int? |

### Reorder

```json
{ "fileIds": ["guid1", "guid2"] }
```

### Response shapes

**List item** (`ProductListItemDto`): `productId`, `categoryId?`, `businessTypeIds?`, `productCode?`, `productName`, `description?`, `status?`, `businessTypes[]`, `categoryName?`, `thumbnail?`, `defaultVersion?`

**Detail** (`ProductDetailDto`): list fields + `files[]`, `versions[]`, `defaultVersion?`

**Preview list**: `{ productId, items: ProductPreviewImageDto[] }` where item has `fileId`, `url`, `displayOrder`, `fileType`, `description?`, `mimeType`, `fileSizeBytes`, `isCover`, `createdAt`

**Upload catalog file response**: `fileId`, `fileLinkId`, `referenceType`, `referenceId`, `originalFileName`, `fileType`, `fileUrl`, `mimeType`, `fileSizeBytes`, `visibility`, `uploadedBy`, `uploadedAt`, …

### Product lifecycle (ADMIN)

Mutations are **independent** from Product Version lifecycle (no cascade).

| Transition | Endpoint |
| --- | --- |
| INACTIVE → ACTIVE | `PATCH .../activate` |
| ACTIVE → INACTIVE | `PATCH .../deactivate` |
| ACTIVE / INACTIVE → ARCHIVED | `PATCH .../archive` |
| ARCHIVED → ACTIVE | `PATCH .../restore` |

**Response** (`ProductLifecycleStatusResponseDto`): `productId`, `previousStatus`, `status`, `updatedAt`, `activeVersionCount?`

**Error codes**: `PRODUCT_NOT_FOUND`, `PRODUCT_INVALID_STATUS_TRANSITION`, `PRODUCT_ALREADY_ACTIVE`, `PRODUCT_ALREADY_INACTIVE`, `PRODUCT_ALREADY_ARCHIVED`, `PRODUCT_RESTORE_NOT_ALLOWED`

Public list/search endpoints are unchanged; use [§8a Admin catalog list](#8a-catalog--admin-management-list) for management views across all product statuses.

---

## 8. Catalog — Product Versions

`ProductVersionsController` uses base route → paths under `/api/ProductVersions/...`.  
Preview reorder/delete controller uses `[Route("ProductVersions")]` (no `/api` prefix) — document both as implemented.

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/api/ProductVersions/product-versions/{id}` | Public |
| POST | `/api/ProductVersions/products/{productId}/versions` | DESIGNER, ADMIN |
| GET | `/api/ProductVersions/products/{productId}/versions` | ADMIN |
| PATCH | `/api/ProductVersions/product-versions/{id}` | ADMIN |
| PATCH | `/api/ProductVersions/product-versions/{id}/set-default` | ADMIN |
| PATCH | `/api/ProductVersions/product-versions/{id}/activate` | ADMIN |
| PATCH | `/api/ProductVersions/product-versions/{id}/deactivate` | ADMIN |
| PATCH | `/api/ProductVersions/product-versions/{id}/archive` | ADMIN |
| PATCH | `/api/ProductVersions/product-versions/{id}/restore` | ADMIN |
| POST | `/api/ProductVersions/product-versions/{id}/files` | DESIGNER, ADMIN · multipart |
| PATCH | `/ProductVersions/product-versions/{id}/preview-files/reorder` | ADMIN |
| DELETE | `/ProductVersions/product-versions/{id}/preview-files/{fileId}` | ADMIN |

### Admin version list — query

| Param | Type | Notes |
| --- | --- | --- |
| `status` | `ProductStatus?` | ACTIVE / INACTIVE / ARCHIVED |
| `versionType` | `ProductVersionType?` | |
| `isDefault` | bool? | |
| `isPublic` | bool? | |
| `isProjectSpecific` | bool? | |
| `projectId` | guid? | PROJECT_SPECIFIC filter |
| `page` / `pageSize` | int | default 1 / 20; max pageSize 100 |

**List response**: `{ items: ProductVersionManagementDto[], page, pageSize, totalCount }`

`ProductVersionManagementDto` adds `projectId?`, `dimensionUnit?`, `createdAt?`, `updatedAt?` to version fields.

### Create body

```json
{
  "versionCode": "V1",
  "versionName": "Natural Oak",
  "versionType": "STANDARD",
  "material": "Oak",
  "color": "Natural",
  "width": 120,
  "height": 75,
  "depth": 60,
  "estimatedPrice": 4500000,
  "isDefault": true,
  "isPublic": true,
  "isProjectSpecific": false
}
```

### Update body

Same fields except `versionCode` is not updated; `versionName` required-by-type.

### Version lifecycle (ADMIN)

Does **not** change parent Product status. Deactivate/archive on a default version clears `isDefault` in the same transaction (no auto-replacement default). Restore sets ACTIVE with `isDefault: false`. `set-default` requires version status ACTIVE.

**Response** (`ProductVersionLifecycleStatusResponseDto`): `productVersionId`, `productId`, `previousStatus`, `status`, `isDefault?`, `updatedAt?`

### Response — `ProductVersionDto` / detail

Includes: `productVersionId`, `productId`, `versionCode`, `versionName`, `versionType?`, `material?`, `color?`, dimensions, `dimensionUnit?`, `estimatedPrice?`, flags, `status?`, `thumbnail?`, `files[]`. Detail may add `productName?`.

---

## 8a. Catalog — Admin management list

Dedicated admin catalog read API (not public `/products`). Shows products in **all** lifecycle states with version health metadata.

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/admin/catalog/products` | ADMIN |

### Query

| Param | Type | Notes |
| --- | --- | --- |
| `keyword` | string? | product name / code |
| `categoryId` | guid? | |
| `businessTypeId` | int? | ANY on product `businessTypeIds` |
| `productStatus` | `ProductStatus?` | |
| `versionStatus` | `ProductStatus?` | products having ≥1 version in status |
| `versionType` | `ProductVersionType?` | |
| `hasActiveVersion` | bool? | |
| `has3DModel` | bool? | product or version has `MODEL_3D` file link |
| `createdFrom` / `createdTo` | datetime? | |
| `page` / `pageSize` | int | default 1 / 20; max pageSize 100 |
| `sortBy` | string? | `createdAt`, `updatedAt`, `productName`, `productCode` |
| `sortDirection` | string? | `asc` / `desc` |

### Response row (`AdminCatalogProductItemDto`)

`productId`, `productCode`, `productName`, `categoryId?`, `categoryName?`, `businessTypeIds?`, `status?`, `totalVersionCount`, `activeVersionCount`, `inactiveVersionCount`, `archivedVersionCount`, `defaultVersionSummary?`, `createdAt?`, `updatedAt?`

**`defaultVersionSummary`**: `productVersionId`, `versionCode`, `versionName`, `status?`, `estimatedPrice?`

**Error codes**: `CATALOG_ADMIN_ACCESS_DENIED`, `CATALOG_FILTER_INVALID`, `CATALOG_SORT_INVALID`, `CATEGORY_NOT_FOUND`, `BUSINESS_TYPE_NOT_FOUND`

---

## 8b. Catalog — Designer project catalog

Project-scoped catalog for assigned Designer (ADMIN allowed). Eligibility: parent Product **ACTIVE**, version **ACTIVE**, and (public **or** PROJECT_SPECIFIC with matching `projectId`). **Does not expose** commercial totals beyond `estimatedPrice`.

Route prefix: `/projects/{projectId}/catalog`

| Method | Path | Roles |
| --- | --- | --- |
| GET | `/projects/{projectId}/catalog/products` | DESIGNER, ADMIN |
| GET | `/projects/{projectId}/catalog/products/{productId}` | DESIGNER, ADMIN |
| GET | `/projects/{projectId}/catalog/product-versions/{productVersionId}` | DESIGNER, ADMIN |

### List query

| Param | Type |
| --- | --- |
| `keyword` | string? |
| `categoryId` | guid? |
| `businessTypeId` | int? |
| `versionType` | `ProductVersionType?` |
| `page` / `pageSize` | int |

**List item**: `productId`, `productCode`, `productName`, `categoryId?`, `categoryName?`, `businessTypeIds?`, `thumbnail?`, `eligibleVersionCount`, `eligibleVersions[]`

**Version summary** (list + detail): `productVersionId`, `versionCode`, `versionName`, `versionType?`, `material?`, `color?`, dimensions, `dimensionUnit?`, `estimatedPrice?`, `isProjectSpecific?` — no tax fields.

**Product detail** adds `description?`, `files[]`, `eligibleVersions[]`. **Version detail** adds `projectId?`, `files[]` (preview / 3D per file visibility rules).

**Authorization**: Designer must be `assignedDesignerId` on project; otherwise `403 DESIGNER_NOT_ASSIGNED`.

**Error codes**: `PROJECT_NOT_FOUND`, `DESIGNER_NOT_ASSIGNED`, `CATALOG_PRODUCT_NOT_ELIGIBLE`, `CATALOG_VERSION_NOT_ELIGIBLE`

---

## 9. Projects

Route: `projects`

| Method | Path | Roles | Description |
| --- | --- | --- | --- |
| POST | `/projects` | CUSTOMER | Create project request |
| GET | `/projects` | SALES, ADMIN, CUSTOMER, DESIGNER | List / filter |
| GET | `/projects/by-user/{userId}` | ADMIN, SALES, DESIGNER, CUSTOMER | Projects for user |
| GET | `/projects/{projectId}` | SALES, ADMIN, CUSTOMER, DESIGNER | Detail |
| GET | `/projects/{projectId}/published-proposal` | CUSTOMER | Published proposal view |
| PATCH | `/projects/{projectId}/sales-assignment` | SALES, ADMIN | Claim / assign sales |
| POST | `/projects/{projectId}/information-requests` | SALES, ADMIN | Ask customer for info |
| PATCH | `/projects/{projectId}/basic-information` | CUSTOMER, SALES, ADMIN | Update basic info |
| PATCH | `/projects/{projectId}/status` | SALES, DESIGNER, ADMIN | Status transition |
| PATCH | `/projects/{projectId}/rejection` | SALES, ADMIN | Reject project |
| POST | `/projects/{projectId}/reopen-proposal` | CUSTOMER, SALES, ADMIN | Roll back to proposal consulting before deposit paid |
| PATCH | `/projects/{projectId}/designer-assignment` | SALES, ADMIN | Assign designer |
| GET | `/projects/{projectId}/catalog/products` | DESIGNER, ADMIN | Project-eligible catalog list — see [§8b](#8b-catalog--designer-project-catalog) |
| GET | `/projects/{projectId}/catalog/products/{productId}` | DESIGNER, ADMIN | Eligible product detail |
| GET | `/projects/{projectId}/catalog/product-versions/{productVersionId}` | DESIGNER, ADMIN | Eligible version detail |
| GET | `/projects/{projectId}/chat-messages/search` | SALES, ADMIN, CUSTOMER, DESIGNER | Search chat messages |

### Create / basic-information body

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

| Field | Type | Notes |
| --- | --- | --- |
| `projectName` | string | Required |
| `businessType` | string | Free-text (not catalog FK) |
| `furnitureRequirement` | string | Required on create |
| `projectAddress` / `businessPurpose` / `description` | string? | |
| `totalAreaSqm` | decimal? | |
| `numberOfFloors` | int? | |
| `budgetMin` / `budgetMax` | decimal? | |
| `targetCompletionDate` | date? | `YYYY-MM-DD` |

### List query

| Param | Type |
| --- | --- |
| `status` | `ProjectStatus?` |
| `assignedSalesId` | guid? |
| `assignedDesignerId` | guid? |
| `search` | string? |
| `page` / `limit` | int |

### By-user query

| Param | Type |
| --- | --- |
| `page` / `pageSize` | int |
| `status` | string? |
| `roleScope` | string? |
| `keyword` | string? |

### Sales assignment

```json
{ "note": "Taking this lead" }
```

**Response** (`ProjectSalesAssignmentDto`): `projectId`, `assignedSalesId?`, `status?`, `salesAssignedAt?`, `salesChat?`

### Information request

```json
{ "message": "Please upload floor plan photos" }
```

### Status update

```json
{
  "status": "IN_CONSULTATION",
  "note": "Customer called"
}
```

Customers cannot use this endpoint. Designer target statuses are restricted (`ProjectStatusTransitionEvaluator`).

### Rejection

```json
{ "rejectionReason": "Out of service area" }
```

### Reopen proposal

No request body.

Rolls back a project to `PROPOSAL_CONSULTING` **before deposit is paid** and **before production is created**. Supported source project statuses:

- `PROPOSAL_SELECTED` (active quotation typically `DRAFT`, no order required)
- `QUOTATION_SENT` (active quotation typically `SENT`, no order required)
- `ORDER_CONFIRMED` (order `CREATED` or `DEPOSIT_PENDING`, quotation `ACCEPTED`)

In one transaction the backend:

- Cancels or expires active `DEPOSIT` payments when an order exists
- Sets an eligible order (`CREATED` or `DEPOSIT_PENDING`) → `CANCELLED` when present
- Sets the active quotation (`DRAFT`, `SENT`, or `ACCEPTED`) → `CANCELLED`
- Demotes the selected proposal → `PUBLISHED` (clears `selectedAt`)
- Restores auto-rejected sibling proposals when applicable
- Moves project → `PROPOSAL_CONSULTING`

**Response** (`ReopenProposalResponseDto`): `projectId`, `oldStatus?`, `newStatus?`, `orderId?`, `orderStatus?`, `quotationId?`, `quotationStatus?`, `selectedProposalId?`, `selectedProposalStatus?`, `restoredProposalCount`, `updatedAt?`

**Common error codes:** `PROJECT_REOPEN_NOT_ALLOWED`, `PROJECT_NO_ACCEPTED_ORDER`, `PROJECT_SELECTED_PROPOSAL_NOT_FOUND`, `PROJECT_ACTIVE_QUOTATION_NOT_FOUND`, `PROJECT_DEPOSIT_ALREADY_PAID`, `PROJECT_PRODUCTION_ALREADY_CREATED`, `ACTIVE_DEPOSIT_CANNOT_BE_CANCELLED`

**Idempotency:** If project is already `PROPOSAL_CONSULTING`, returns `200` with stable state (no duplicate side effects).

**Access:** customer (own project), assigned sales, or admin.

### Designer assignment

```json
{
  "designerId": "...",
  "spaceDataStatus": "SUFFICIENT",
  "note": "Ready for design"
}
```

`spaceDataStatus`: `SUFFICIENT` | `INSUFFICIENT`

### Chat message search

**Query:** `q`, `page`, `limit`

### Response — `ProjectDto`

```json
{
  "projectId": "...",
  "customerId": "...",
  "assignedSalesId": null,
  "assignedDesignerId": null,
  "projectCode": "PRJ-2026-0001",
  "projectName": "Cafe District 1",
  "businessType": "Cafe",
  "projectAddress": "...",
  "businessPurpose": "...",
  "furnitureRequirement": "...",
  "description": "...",
  "totalAreaSqm": 120.5,
  "numberOfFloors": 1,
  "budgetMin": 50000000,
  "budgetMax": 120000000,
  "targetCompletionDate": "2026-12-31",
  "status": "SUBMITTED",
  "submittedAt": "2026-07-27T00:00:00Z"
}
```

### Project status lifecycle (summary)

```text
SUBMITTED
  → IN_CONSULTATION / NEED_BASIC_INFORMATION
  → WAITING_FOR_DESIGNER_ASSIGNMENT
  → MEASUREMENT_REQUIRED / SPACE_VERIFIED
  → PROPOSAL_CONSULTING → PROPOSAL_SELECTED
  → QUOTATION_SENT / QUOTATION_REVISION_REQUESTED
  → ORDER_CONFIRMED
  → (optional reopen-proposal back to PROPOSAL_CONSULTING before deposit paid)
  → IN_PRODUCTION
  → READY_FOR_DELIVERY → DELIVERING → DELIVERED → COMPLETED
  (or REJECTED)
```

---

## 10. Proposals & scenes

Controller uses `[Route("")]` — absolute paths.

### Proposals

| Method | Path | Roles |
| --- | --- | --- |
| POST | `/projects/{projectId}/proposals` | DESIGNER, SALES, ADMIN |
| GET | `/projects/{projectId}/proposals` | CUSTOMER, DESIGNER, SALES, ADMIN |
| GET | `/proposals/{proposalId}` | same |
| PATCH | `/proposals/{proposalId}` | DESIGNER, SALES, ADMIN |
| PATCH | `/proposals/{proposalId}/publish` | DESIGNER, SALES, ADMIN |
| PATCH | `/proposals/{proposalId}/select-final` | CUSTOMER |
| PATCH | `/proposals/{proposalId}/request-revision` | CUSTOMER |

### Scenes

| Method | Path | Roles |
| --- | --- | --- |
| POST | `/proposals/{proposalId}/scenes` | DESIGNER, SALES, ADMIN |
| GET | `/proposals/{proposalId}/scenes` | CUSTOMER, DESIGNER, SALES, ADMIN |
| GET | `/proposal-scenes/{sceneId}` | same |
| PATCH | `/proposal-scenes/{sceneId}` | DESIGNER, SALES, ADMIN |

### Items

| Method | Path | Roles |
| --- | --- | --- |
| POST | `/proposals/{proposalId}/items/sync-from-scene` | DESIGNER, ADMIN |
| GET | `/proposals/{proposalId}/items` | CUSTOMER, DESIGNER, SALES, ADMIN |
| PATCH | `/proposal-items/{proposalItemId}` | DESIGNER, SALES, ADMIN |
| DELETE | `/proposal-items/{proposalItemId}` | DESIGNER, SALES, ADMIN |

### Create proposal

```json
{
  "proposalName": "Concept A — Industrial",
  "description": "Dark oak + black metal"
}
```

### Update proposal

```json
{
  "proposalName": "Concept A",
  "description": "..."
}
```

### Publish / select-final / request-revision

```json
{ "note": "Ready for customer review" }
```

```json
{ "note": "Love this layout" }
```

```json
{ "revisionNote": "Need warmer lighting" }
```

**Select-final response** (`SelectFinalProposalResponseDto`): `proposalId`, `projectId`, `quotationId?`, `proposalStatus?`, `projectStatus?`, `selectedAt?`

On first successful select-final, the backend **auto-creates a draft quotation** from the selected proposal in the same transaction and returns its `quotationId`. Idempotent re-call when the proposal is already `SELECTED` returns `200` without creating another quotation (`quotationId` may be omitted).

Sales normally continue from this draft (`PATCH` quotation → `PATCH` send). `POST /projects/{projectId}/quotations` remains as a manual fallback for SALES/ADMIN.

### Create scene

```json
{
  "sceneName": "Ground floor",
  "sceneType": "THREE_D",
  "projectAreaId": null,
  "mongoSceneId": null,
  "previewFileId": null
}
```

`sceneType`: `TWO_D` | `THREE_D`

### Update scene

```json
{
  "sceneName": "Ground floor",
  "projectAreaId": null,
  "previewFileId": null,
  "isActive": true
}
```

### Sync items from scene

```json
{
  "sceneId": "...",
  "items": [
    {
      "sceneObjectId": "obj-1",
      "productVersionId": "...",
      "quantity": 4,
      "customizationNote": null
    }
  ]
}
```

**Response:** `proposalId`, `sceneId`, `items[]`, `createdCount`, `updatedCount`, `removedCount`

### Update item

```json
{
  "quantity": 6,
  "customizationNote": "Round corners"
}
```

### List queries

| Resource | Query |
| --- | --- |
| proposals | `status?`, `page`, `limit` |
| scenes | `sceneType?`, `isActive?`, `page`, `limit` |
| items | `sceneId?`, `page`, `limit` |

### Response — proposal / detail

`ProposalDto`: `proposalId`, `projectId`, `parentProposalId?`, `proposalName`, `description?`, `versionNo?`, `status?`, `publishedAt?`, `selectedAt?`, `rejectedAt?`, `createdAt?`, `updatedAt?`

`ProposalDetailDto` adds `scenes[]`, `items[]`.

`ProposalStatus`: `DRAFT`, `PUBLISHED`, `SELECTED`, `REVISION_REQUESTED`, `REJECTED`, `ARCHIVED`

---

## 11. Room planner

Route: `proposal-scenes`

| Method | Path | Roles | Description |
| --- | --- | --- | --- |
| GET | `/proposal-scenes/{sceneId}/room-planner` | CUSTOMER, DESIGNER, SALES, ADMIN | Load Mongo scene payload |
| POST | `/proposal-scenes/{sceneId}/room-planner/resolve-products` | CUSTOMER, DESIGNER, SALES, ADMIN | Resolve scene-referenced ProductVersions + files |
| PUT | `/proposal-scenes/{sceneId}/room-planner` | DESIGNER, ADMIN | Save scene payload |

### Request / response payload (`RoomPlannerScenePayloadDto`, schema v3)

```json
{
  "schemaVersion": 3,
  "editorVersion": "ROOM_PLANNER_BABYLON_BUILDING_V1",
  "unit": "meter",
  "blueprintLayout": {
    "id": "blueprint-{sceneId}",
    "unit": "meter",
    "floors": [
      {
        "id": "floor-...",
        "projectAreaId": "00000000-0000-0000-0000-000000000000",
        "elevation": 0,
        "floorHeight": 3,
        "points": [],
        "walls": [],
        "doors": [],
        "windows": [],
        "openings": []
      }
    ]
  },
  "objects": [],
  "layers": [],
  "stylePreset": null,
  "camera": { },
  "lighting": { },
  "validation": { },
  "editorState": { }
}
```

| Top-level field | Type |
| --- | --- |
| `schemaVersion` | int (required `3`) |
| `editorVersion` | string? |
| `unit` | string (must match `blueprintLayout.unit`) |
| `blueprintLayout` | multi-floor blueprint (`floors[]` is source of truth) |
| `objects` | furniture objects with `floorId`, transform, `productVersionId`, placement |
| `layers` | layer visibility/lock |
| `stylePreset` | string? |
| `camera` | mode, position, target, zoom |
| `lighting` | preset, intensities |
| `validation` | status, warnings, errors |
| `editorState` | active tool, selection, grid/snap |

Notes:

- Root legacy `layout` is not required for schema v3 and is cleared on save.
- `pointId` / `wallId` / `openingId` uniqueness is scoped **per floor**, not globally.
- Each floor `projectAreaId` must match SQL `proposal_scene_areas` for the scene.
- GET with no Mongo document returns an empty schema v3 template built from SQL scene areas (does not create Mongo).
- GET when SQL has `mongoSceneId` but Mongo doc is missing returns `ROOM_PLANNER_DOCUMENT_NOT_FOUND`.

**GET response** also includes: `sceneId`, `mongoSceneId?`, `proposalId?`, `projectId?`, `projectAreaIds[]`, `areas[]`, `lastSavedAt?`

**POST resolve-products** (`ResolveRoomPlannerProductsRequestDto` → `ResolveRoomPlannerProductsResponseDto`):

- Request: `{ "productVersionIds": ["..."] }` — IDs must already appear in the scene `objects[]`.
- Response: `{ "sceneId", "projectId", "items": [{ productVersionId, productId, productName, versionCode, versionName, dimensions, files[] }] }`.
- Customer receives only `CUSTOMER_VISIBLE` files. Does not expose full project catalog.

**PUT response** (`RoomPlannerSceneSaveResponseDto`): `sceneId`, `mongoSceneId`, `lastSavedAt`

See `docs/mongodb-room-planner-guide.md` for nested document details.

---

## 12. Quotations

Absolute routes on `QuotationsController`.

| Method | Path | Roles |
| --- | --- | --- |
| GET | `/projects/{projectId}/quotations` | CUSTOMER, SALES, DESIGNER, ADMIN |
| GET | `/quotations/{quotationId}` | same |
| POST | `/projects/{projectId}/quotations` | SALES, ADMIN · no body · **fallback** (normal flow: auto-created on select-final) |
| PATCH | `/quotations/{quotationId}` | SALES, ADMIN |
| PATCH | `/quotations/{quotationId}/items/{quotationItemId}/financials` | SALES, ADMIN |
| PUT | `/quotations/{quotationId}/items/financials` | SALES, ADMIN · bulk item financials |
| PATCH | `/quotations/{quotationId}/send` | SALES, ADMIN |
| PATCH | `/quotations/{quotationId}/revise` | SALES, ADMIN |
| PATCH | `/quotations/{quotationId}/cancel` | SALES, ADMIN |
| PATCH | `/quotations/{quotationId}/accept` | CUSTOMER |
| PATCH | `/quotations/{quotationId}/request-revision` | CUSTOMER |
| PATCH | `/quotations/{quotationId}/reject` | CUSTOMER |

Accepting a quotation creates an **Order** in status **`CREATED`**. The order snapshots `depositAmount`, `vatRate`, `vatAmount`, and monetary totals from the accepted quotation header. Deposit is **not** collected at accept time — the customer initiates deposit payment separately (§13).

Draft quotations are initialized with `depositAmount = 30%` of `totalAmount` (config: `OrderWorkflow:DepositPercent`). Sales may edit `depositAmount` on the quotation while it is still `DRAFT` / `REVISED`; send and accept require `0 < depositAmount ≤ totalAmount`. After item financials change, update `depositAmount` if the default no longer fits the new total.

### List query

`status?` (`QuotationStatus`)

### Update quotation

Only non-calculated header fields are writable. The backend recalculates `subtotalAmount`, `totalDiscountAmount`, `preVatAmount`, `vatAmount`, and `totalAmount` from quotation items whenever item financials change.

Draft quotations are created with header `vatRate = 0.08` (8%). VAT is applied once at the quotation header, not per item.

```json
{
  "validUntil": "2026-08-31",
  "depositAmount": 5832000,
  "customerNote": null,
  "salesNote": "VIP discount",
  "revisionReason": null
}
```

Writable: `validUntil`, `depositAmount`, `customerNote`, `salesNote`, `revisionReason`. Monetary header totals remain server-calculated from items.

### Update item financials

Single item (`PATCH .../items/{quotationItemId}/financials`) or bulk (`PUT .../items/financials`). Writable fields: `quantity`, `unitPrice`, `discountAmount`. All monetary totals on items and header are server-calculated.

```json
{
  "quantity": 4,
  "unitPrice": 4500000,
  "discountAmount": 0
}
```

Bulk body:

```json
{
  "items": [
    {
      "quotationItemId": "...",
      "quantity": 4,
      "unitPrice": 4500000,
      "discountAmount": 0
    }
  ]
}
```

### Customer revision / reject

```json
{ "revisionReason": "Price too high on chairs" }
```

```json
{ "rejectReason": "Changed requirements" }
```

### Response — `QuotationDetailDto`

```json
{
  "quotationId": "...",
  "projectId": "...",
  "proposalId": "...",
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
  "validUntil": "...",
  "customerNote": null,
  "salesNote": "...",
  "revisionReason": null,
  "rejectReason": null,
  "createdBy": "...",
  "sentAt": "...",
  "acceptedAt": null,
  "rejectedAt": null,
  "createdAt": "...",
  "updatedAt": "...",
  "items": [
    {
      "quotationItemId": "...",
      "quotationId": "...",
      "proposalItemId": "...",
      "productVersionId": "...",
      "productNameSnapshot": "Oak Cafe Table",
      "productVersionNameSnapshot": "Natural Oak",
      "productVersionCodeSnapshot": "TABLE-OAK-001-A",
      "quantity": 4,
      "unitPrice": 4500000,
      "grossAmount": 18000000,
      "discountAmount": 0,
      "totalAmount": 18000000,
      "isCustomized": false,
      "customizationNote": null,
      "note": null
    }
  ]
}
```

Quotation item formula (all amounts **pre-VAT**):

- `grossAmount = quantity * unitPrice`
- `totalAmount = grossAmount - discountAmount`

Quotation header formula:

- `subtotalAmount = SUM(item.grossAmount)`
- `totalDiscountAmount = SUM(item.discountAmount)`
- `preVatAmount = SUM(item.totalAmount)`
- `vatAmount = ROUND(preVatAmount * vatRate)` — `vatRate` is a decimal fraction (`0.08` = 8%)
- `totalAmount = preVatAmount + vatAmount`

`QuotationStatus`: `DRAFT`, `SENT`, `REVISION_REQUESTED`, `REVISED`, `ACCEPTED`, `REJECTED`, `EXPIRED`, `CANCELLED`

---

## 13. Orders

Absolute routes on `OrdersController`.

| Method | Path | Roles |
| --- | --- | --- |
| GET | `/projects/{projectId}/orders` | CUSTOMER, SALES, DESIGNER, PRODUCTION, ADMIN |
| GET | `/orders/{orderId}` | same |
| POST | `/orders/{orderId}/payments/deposit` | CUSTOMER, SALES, ADMIN |
| POST | `/orders/{orderId}/payments/remaining` | SALES, ADMIN |
| PATCH | `/orders/{orderId}/prepare-final-payment` | SALES, ADMIN |
| PATCH | `/orders/{orderId}/complete` | SALES, ADMIN |
| POST | `/orders/{orderId}/production-request` | SALES, ADMIN |
| PATCH | `/orders/{orderId}/start-delivery` | SALES, PRODUCTION, ADMIN |
| PATCH | `/orders/{orderId}/complete-delivery` | SALES, PRODUCTION, ADMIN |
| PATCH | `/orders/{orderId}/confirm-delivery` | CUSTOMER |

**Delivery flow (single full delivery per order):**

1. All deliverable order items must be `READY` before `start-delivery`.
2. Staff calls `complete-delivery` once while order is `DELIVERING` — every deliverable `READY` item becomes `DELIVERED` with backend-set `deliveredAt` / `deliveredBy`.
3. Customer calls `confirm-delivery` once at order level — sets `customerConfirmedDeliveryAt` and moves order/project to `DELIVERED`.

At most **one active** `DELIVERY` schedule (`PENDING_CONFIRMATION` or `CONFIRMED`) per project. Partial / incremental delivery is not supported.

**Target completion date:** operational schedule and production dates must be `<= project.targetCompletionDate`. Shortening target below existing schedule/production dates returns `409 TARGET_DATE_CONFLICTS_WITH_OPERATIONAL_DATES`.

`PaidAmount` / `RemainingAmount` live on **Order**, not on Payment.

New orders start as **`CREATED`** after quotation accept. Deposit payment is initiated explicitly; creating a deposit payment from `CREATED` moves the order to **`DEPOSIT_PENDING`**. Webhook/settlement then moves to `DEPOSIT_PAID`.

### Create deposit / remaining payment

```json
{
  "expiredAt": "2026-08-01T00:00:00Z",
  "note": "Deposit invoice"
}
```

Eligible order statuses: **`CREATED`** (creates payment and moves order → `DEPOSIT_PENDING`) or **`DEPOSIT_PENDING`** (reuses an active pending deposit payment when present). Amount is always taken from the order snapshot `depositAmount`.

### Create production request

```json
{
  "assignedTo": "...",
  "priority": 1,
  "estimatedStartDate": "2026-08-05T00:00:00Z",
  "estimatedCompletionDate": "2026-09-01T00:00:00Z",
  "note": null
}
```

Estimated production dates must satisfy `estimatedStartDate <= estimatedCompletionDate <= project.targetCompletionDate`.

### Complete delivery

No request body. Marks every deliverable order item `READY → DELIVERED`.

### Confirm delivery (customer)

No request body. Requires all deliverable items `DELIVERED` and order `DELIVERING`.

### Response — order detail

```json
{
  "orderId": "...",
  "projectId": "...",
  "proposalId": "...",
  "quotationId": "...",
  "orderCode": "ORD-...",
  "customerId": "...",
  "salesId": "...",
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
      "orderItemId": "...",
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

Order header `vatRate` and `vatAmount` are snapshotted from the accepted quotation at order creation and are not recalculated when quotation header values change later.

Order item formula:

- `subtotalAmount = quantity * unitPrice - discountAmount` (pre-VAT; copied from quotation item `totalAmount` at accept time)

`OrderStatus`: `CREATED`, `DEPOSIT_PENDING`, `DEPOSIT_PAID`, `IN_PRODUCTION`, `READY_FOR_DELIVERY`, `DELIVERING`, `DELIVERED`, `FINAL_PAYMENT_PENDING`, `COMPLETED`, `CANCELLED`

---

## 14. Customization requests

Multi-version model: one **request** snapshots a source product version; the designer creates multiple **versions** (each with its own PROJECT_SPECIFIC product version).

| Method | Path | Roles |
| --- | --- | --- |
| GET | `/projects/{projectId}/customization-requests` | CUSTOMER, SALES, DESIGNER, PRODUCTION, ADMIN |
| GET | `/customization-requests/{id}` | same |
| GET | `/customization-requests/{id}/versions` | same |
| GET | `/customization-requests/{id}/versions/{versionId}` | same |
| POST | `/proposal-items/{proposalItemId}/customization-requests` | CUSTOMER, DESIGNER, ADMIN |
| POST | `/customization-requests/{id}/versions` | DESIGNER, ADMIN |
| PATCH | `/customization-requests/{id}/versions/{versionId}` | DESIGNER, ADMIN |
| POST | `/customization-requests/{id}/versions/{versionId}/submit-for-review` | DESIGNER, ADMIN |
| POST | `/customization-requests/{id}/versions/{versionId}/withdraw` | DESIGNER, ADMIN |
| POST | `/customization-requests/{id}/accept` | CUSTOMER |
| PATCH | `/customization-requests/{id}/cancel` | CUSTOMER, SALES, DESIGNER, ADMIN |
| GET | `/api/production/customization-versions` | PRODUCTION, ADMIN · global queue |
| GET | `/api/production/customization-versions/{versionId}` | PRODUCTION, ADMIN |
| PATCH | `/api/production/customization-versions/{versionId}/review` | PRODUCTION, ADMIN |

### Project list query

`proposalId?`, `sourceProductVersionId?`, `status?`

### Production queue query

`status?`, `feasibilityStatus?`, `projectId?`, `proposalId?`, `materialAvailable?`, `fromDate?`, `toDate?`, `page`, `pageSize`

Default for PRODUCTION (no filters): `status=REVIEWING`, `feasibilityStatus=PENDING`.

### Submit customization request

```json
{
  "requestTitle": "Shorter table legs",
  "requestDescription": "Reduce height by 5cm",
  "requestedWidth": null,
  "requestedHeight": 70,
  "requestedDepth": null,
  "requestedMaterial": "Oak",
  "requestedColor": "Walnut",
  "requestedChangeNote": "Match bar stool height"
}
```

### Create version (Product Version + version row + file links in one transaction)

Upload files to the project first, then reference file IDs in the body. `previewFileIds` may be omitted, empty, or null.

```json
{
  "versionTitle": "Walnut option",
  "designerNote": "Reinforced frame",
  "versionName": "Chair Custom V1",
  "versionCode": "CHAIR-CUSTOM-V1",
  "material": "Walnut",
  "color": "Dark Brown",
  "width": 65,
  "height": 85,
  "depth": 60,
  "dimensionUnit": "cm",
  "estimatedPrice": 3200000,
  "modelFileId": "model-file-id",
  "previewFileIds": ["preview-file-id"]
}
```

### Update draft version

Same fields as create (partial update). Replacing `modelFileId` / `previewFileIds` syncs `file_links` in the same transaction.

### Production review (per version)

```json
{
  "result": "FEASIBLE",
  "materialAvailable": true,
  "estimatedProductionDays": 14,
  "estimatedAdditionalCost": 500000,
  "additionalCostReason": "Custom cut",
  "feasibilityNote": "OK",
  "productionRiskNote": null,
  "alternativeMaterialNote": null
}
```

`result`: `FEASIBLE` (version stays `REVIEWING`, feasibility → `FEASIBLE`) or `NOT_FEASIBLE` (version → `PRODUCTION_REJECTED`).

### Customer accept version

```json
{
  "customizationRequestVersionId": "version-id"
}
```

Requires request `REVIEWING`, version `REVIEWING` with `feasibilityStatus=FEASIBLE`, and production `estimatedAdditionalCost` set.

On accept, the accepted version's linked `ProductVersion.estimatedPrice` is set to **source version `estimatedPrice` + `estimatedAdditionalCost`** (final catalog price for the project-specific version).

### Cancel request

```json
{ "cancelReason": "No longer needed" }
```

### Version list / detail response (per version)

Includes: `versionNo`, `versionTitle`, `status`, `feasibilityStatus`, production review fields, `isAccepted`, embedded `productVersion` summary (name, code, material, dimensions, price), and `productVersion.files[]` (model/preview metadata per catalog file convention).

**Access:** DRAFT versions are visible to DESIGNER/ADMIN only on project-scoped endpoints. PRODUCTION uses the global queue endpoints (no project assignment required).

### Request status enum

`SUBMITTED`, `REVIEWING`, `ACCEPTED`, `CANCELLED`

### Version status enum

`DRAFT`, `REVIEWING`, `ACCEPTED`, `PRODUCTION_REJECTED`, `WITHDRAWN`

### Production feasibility enum

`PENDING`, `FEASIBLE`, `NOT_FEASIBLE`

---

## 15. Project areas

| Method | Path(s) | Roles |
| --- | --- | --- |
| POST | `/project-areas/{projectId}` **and** `/projects/{projectId}/areas` | SALES, DESIGNER, ADMIN |
| GET | `/projects/{projectId}/areas` | CUSTOMER, SALES, DESIGNER, ADMIN · `includeCancelled` |
| GET | `/project-areas/{projectAreaId}` | same |
| PATCH | `/project-areas/{id}` | SALES, DESIGNER, ADMIN |
| PATCH | `/project-areas/{id}/cancel` | SALES, DESIGNER, ADMIN |

### Create / update body

```json
{
  "parentAreaId": null,
  "areaName": "Ground floor seating",
  "areaType": "ROOM",
  "floorNumber": 1,
  "description": "...",
  "areaSqm": 45,
  "width": 6,
  "length": 7.5,
  "height": 3.2,
  "currentCondition": "Empty shell",
  "requirementNote": "Need banquettes",
  "status": "DRAFT"
}
```

`areaType`: `STORE`, `FLOOR`, `ROOM`, `ZONE`, `OUTDOOR_AREA`, `OTHER`  
`status`: `DRAFT`, `NEED_MEASUREMENT`, `MEASURED`, `VERIFIED`, `CANCELLED`

---

## 16. Project schedules

Route: `project-schedules` (+ absolute create alias)

| Method | Path | Roles |
| --- | --- | --- |
| POST | `/project-schedules/{projectId}` **and** `/projects/{projectId}/schedules` | SALES, PRODUCTION, ADMIN |
| GET | `/project-schedules?projectId=` | CUSTOMER, SALES, DESIGNER, PRODUCTION, ADMIN |
| GET | `/project-schedules/my-assigned` | SALES, DESIGNER, PRODUCTION, ADMIN |
| GET | `/project-schedules/{id}` | CUSTOMER, SALES, DESIGNER, PRODUCTION, ADMIN |
| PATCH | `/project-schedules/{id}` | SALES, PRODUCTION, ADMIN |
| PATCH | `/project-schedules/{id}/status` | CUSTOMER, SALES, DESIGNER, PRODUCTION, ADMIN |
| DELETE | `/project-schedules/{id}` | SALES, PRODUCTION, ADMIN |

### Create

```json
{
  "scheduleType": "MEASUREMENT",
  "title": "Site measurement",
  "description": "...",
  "assignedStaffId": "...",
  "scheduledStart": "2026-08-01T09:00:00Z",
  "scheduledEnd": "2026-08-01T11:00:00Z",
  "location": "123 Nguyen Hue",
  "customerNote": "Call before arrival",
  "internalNote": null
}
```

`scheduleType`: `MEASUREMENT`, `CONSULTATION`, `DESIGN_REVIEW`, `DELIVERY`, `HANDOVER`, `OTHER`

### Update status

```json
{
  "status": "CONFIRMED",
  "note": "Customer confirmed"
}
```

`status`: `PENDING_CONFIRMATION`, `CONFIRMED`, `COMPLETED`, `CANCELLED`

### List query

`scheduleType?`, `status?`, `from?`, `to?`, `page`, `limit` (+ `projectId` on list)

---

## 17. Project files & shared files

### Project files — `/projects/{projectId}/files`

| Method | Path | Auth |
| --- | --- | --- |
| POST | `/projects/{projectId}/files` | JWT · multipart |
| GET | `/projects/{projectId}/files` | JWT |
| GET | `/projects/{projectId}/files/search` | JWT · `q`, `page`, `limit` |

**Multipart fields:** `file`, `fileType`, `visibility?`, `note?`

**List query:** `fileType?`, `visibility?`, `page`, `limit`

**Upload response:** `fileId`, `fileLinkId`, `projectId`, `originalFileName`, `fileName`, `fileType`, `mimeType`, `fileSize`, `storagePath`, `publicUrl`, `visibility`, `uploadedBy`, `uploadedAt`

### Shared files — `/files`

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/files/{fileId}` | JWT |
| GET | `/files/by-reference` | **AllowAnonymous** |
| PATCH | `/files/{fileId}/archive` | JWT |
| DELETE | `/files/{fileId}` | JWT |

**By-reference query:** `referenceType`, `referenceId`, `fileType?`, `visibility?`, `page`, `limit`

**Archive body:** `{ "reason": "obsolete" }`

**Archive response:** `{ "fileId", "status": "ARCHIVED", "archivedAt" }`  
**Delete response:** `{ "fileId", "deletedAt" }`

`FileVisibility`: `CUSTOMER_VISIBLE`, `STAFF_ONLY`, `PRIVATE`  
`FileType`: see [Enums](#23-enums)

---

## 18. Chat

| Method | Path | Roles |
| --- | --- | --- |
| POST | `/projects/{projectId}/chats` | ADMIN |
| GET | `/projects/{projectId}/chats` | CUSTOMER, SALES, DESIGNER, ADMIN |
| PATCH | `/project-chats/{chatId}/status` | SALES, DESIGNER, ADMIN |
| POST | `/project-chats/{chatId}/messages` | CUSTOMER, SALES, DESIGNER, ADMIN |
| POST | `/project-chats/{chatId}/messages/files` | same · multipart |
| GET | `/project-chats/{chatId}/messages` | same |

### Create chat

```json
{
  "chatType": "SALES",
  "staffId": "...",
  "title": "Sales consultation"
}
```

`chatType`: `SALES`, `DESIGNER`, `PRODUCTION`, `DELIVERY`, `GENERAL`, `INTERNAL`

### Update status

```json
{ "status": "CLOSED" }
```

`status`: `OPEN`, `CLOSED`, `ARCHIVED`

### Send text message

```json
{
  "messageType": "TEXT",
  "content": "Hello, when can we schedule measurement?"
}
```

### Send file message (multipart)

| Field | Type |
| --- | --- |
| `file` | file |
| `content` | string? |
| `fileType` | `FileType` |
| `visibility` | `FileVisibility?` |

### List messages query

`page`, `limit`, `sort` (default `ASC`)

### Message response

```json
{
  "messageId": "...",
  "chatId": "...",
  "senderId": "...",
  "senderName": "Nguyen Van A",
  "senderRole": "CUSTOMER",
  "messageType": "TEXT",
  "content": "...",
  "attachment": null,
  "createdAt": "...",
  "editedAt": null,
  "deletedAt": null,
  "readAt": null
}
```

Realtime:

- Chat stream: join chat via SignalR `ProjectChatHub` (see §22). Server pushes `project_chat.message_sent` to `project:{projectId}` and `project_chat:{chatId}` groups after DB save.
- In-app notification: after a text/file message is saved, the backend also creates a notification for other chat participants and pushes `project_chat.message_sent` through `NotificationsHub` to each `user:{accountId}` receiver.
- Notification `referenceType`: `PROJECT_CHAT_MESSAGE`; `referenceId`: `messageId`.
- Notification metadata includes `chatId`, `chatType`, `messageId`, `messageType`, `senderId`, `senderName`, `projectName`, `contentPreview`.

---

## 19. Notifications

Route: `notifications`

| Method | Path | Auth |
| --- | --- | --- |
| GET | `/notifications/me` | JWT · `isUnread?`, `page`, `limit` |
| GET | `/notifications/me/unread-count` | JWT |
| PATCH | `/notifications/{notificationId}/read` | JWT |
| PATCH | `/notifications/me/read-all` | JWT |

### List item

```json
{
  "notificationId": "...",
  "receiverId": "...",
  "projectId": "...",
  "title": "Quotation sent",
  "message": "Your quotation is ready",
  "notificationType": "...",
  "referenceType": "QUOTATION",
  "referenceId": "...",
  "isRead": false,
  "createdAt": "...",
  "readAt": null
}
```

### Unread count

```json
{ "unreadCount": 3 }
```

Realtime push: `/hubs/notifications` (§22).

---

## 20. Payments

Primary guide for provider flows: `docs/payment-service-guide.md`.

Webhooks are the **source of truth** for payment confirmation. Return URLs are UI-only.

### 20.1 Customer / staff payment APIs — `/api/payments`

| Method | Path | Roles |
| --- | --- | --- |
| GET | `/api/payments` | CUSTOMER, SALES, DESIGNER, ADMIN |
| GET | `/api/payments/summary` | CUSTOMER, SALES, ADMIN |
| GET | `/api/payments/{paymentId}` | CUSTOMER, SALES, DESIGNER, ADMIN |
| GET | `/api/payments/{paymentId}/transactions` | same |
| GET | `/api/payments/{paymentId}/transactions/active` | CUSTOMER |
| GET | `/api/payments/code/{paymentCode}/status` | CUSTOMER, SALES, DESIGNER, ADMIN |
| POST | `/api/payments/{paymentId}/transactions` | CUSTOMER |
| PATCH | `/api/payments/{paymentId}/transactions/{txId}/cancel` | CUSTOMER |
| POST | `/api/payments/{paymentId}/sepay/vietqr` | CUSTOMER, SALES, DESIGNER, ADMIN |
| POST | `/api/payments/{paymentId}/payos/payment-link` | CUSTOMER, SALES, DESIGNER, ADMIN |

#### List query

| Param | Type |
| --- | --- |
| `projectId` | guid? |
| `orderId` | guid? |
| `status` | `PaymentStatus?` |
| `paymentType` | `PaymentType?` |
| `page` / `pageSize` | int |

#### List response

```json
{
  "items": [
    {
      "paymentId": "...",
      "paymentCode": "PAY-...",
      "projectId": "...",
      "projectCode": "PRJ-...",
      "projectName": "...",
      "orderId": "...",
      "orderCode": "ORD-...",
      "paymentType": "DEPOSIT",
      "amount": 29700000,
      "currency": "VND",
      "status": "PENDING",
      "expiredAt": "...",
      "paidAt": null,
      "createdAt": "...",
      "isPayable": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

#### Summary

```json
{
  "pendingCount": 1,
  "processingCount": 0,
  "paidCount": 2,
  "expiredCount": 0,
  "cancelledCount": 0,
  "payableCount": 1,
  "pendingAmount": 29700000,
  "currency": "VND"
}
```

#### Detail

Extends payment fields with `isPayable`, `reused?`, nested `project`, `order`, `latestTransaction`.

#### Create transaction attempt (CUSTOMER)

```json
{
  "paymentProvider": "PAYOS",
  "paymentMethod": "PAYMENT_LINK",
  "returnUrl": "https://app.example.com/payments/result",
  "cancelUrl": "https://app.example.com/payments/cancel"
}
```

For SePay VietQR typically:

```json
{
  "paymentProvider": "SEPAY",
  "paymentMethod": "QR_CODE"
}
```

**Response** (`PaymentTransactionAttemptResponseDto`): `paymentTransactionId`, `paymentId`, `transactionCode`, `amount`, `currency`, `status?`, `paymentProvider?`, `paymentMethod?`, `paymentUrl?`, `qrContent?`, `paymentStatus?`

#### Cancel transaction

```json
{ "cancelReason": "Changed mind" }
```

#### PayOS payment link

```json
{
  "returnUrl": "https://app.example.com/payments/result",
  "cancelUrl": "https://app.example.com/payments/cancel"
}
```

**Response:** `paymentId`, `paymentTransactionId`, `paymentCode`, `provider`, `method`, `orderCode` (long), `amount`, `status?`, `checkoutUrl`, `qrCode?`, `paymentStatus?`

#### SePay VietQR

No body. **Response:** `paymentId`, `paymentCode`, `provider`, `method`, `amount`, `bankCode`, `accountNo`, `accountName`, `transferContent`, `vietQrUrl`, `status?`

### 20.2 Project start fee — `/api/projects`

| Method | Path | Roles |
| --- | --- | --- |
| POST | `/api/projects/{projectId}/payments/project-start-fee` | SALES, ADMIN |
| GET | `/api/projects/{projectId}/payments/project-start-fee/status` | SALES, ADMIN |

**Create body**

```json
{
  "amount": 2000000,
  "expiredAt": "2026-08-01T00:00:00Z",
  "note": "Project start fee"
}
```

Default amount: `ProjectWorkflow:DefaultProjectStartFeeAmount` = **2_000_000** (override `PROJECT_START_FEE_AMOUNT`).

**Status response:** `projectId`, `requiresProjectStartFee`, `projectStartFeeStatus?`, `isEligibleForDesignerAssignment`, `paymentId?`

### 20.3 Order-linked payment creation

See §13: `POST /orders/{id}/payments/deposit` and `.../remaining`.

### 20.4 Webhooks & admin / test

| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| POST | `/api/webhooks/payos` | Anonymous | Raw PayOS webhook body |
| POST | `/api/webhooks/sepay` | Anonymous | Raw body + signature/timestamp headers |
| POST | `/api/admin/payments/payos/confirm-webhook` | ADMIN | `{ "webhookUrl": "https://..." }` |
| POST | `/api/test/payments` | ADMIN | Dev/test create payment |

**Test payment body**

```json
{
  "projectId": "...",
  "amount": 100000,
  "paymentType": "OTHER",
  "note": "test",
  "expiredAt": null
}
```

**Webhook success response:** `{ "success": true }` (provider-specific DTO)

### Payment enums (quick)

| Enum | Values |
| --- | --- |
| `PaymentType` | `PROJECT_START_FEE`, `DEPOSIT`, `REMAINING_PAYMENT`, `FULL_PAYMENT`, `REFUND`, `OTHER` |
| `PaymentStatus` | `PENDING`, `PROCESSING`, `PAID`, `CANCELLED`, `EXPIRED`, `REFUNDED` |
| `PaymentProvider` | `PAYOS`, `SEPAY`, `CASH`, `MANUAL_BANK_TRANSFER`, `OTHER` |
| `PaymentMethod` | `PAYMENT_LINK`, `QR_CODE`, `BANK_TRANSFER`, `CASH`, `OTHER` |
| `PaymentTransactionStatus` | `PENDING`, `SUCCESS`, `FAILED`, `CANCELLED` |

Realtime: `/hubs/payments` (§22).

---

## 20a. Admin Financial Dashboard

Admin financial APIs are read-only operational dashboard endpoints. They do not mutate Payment, Order, Project, Quotation, or PaymentTransaction state.

### `GET /admin/financial/summary`

**Roles:** ADMIN

Returns collected cash and core financial obligation metrics for the requested reporting period.

#### Query

| Param | Type | Default | Notes |
| --- | --- | --- | --- |
| `period` | `THIS_MONTH` / `THIS_YEAR` / `CUSTOM` | `THIS_MONTH` | Case-insensitive |
| `from` | DateTimeOffset? | null | Required when `period=CUSTOM` |
| `to` | DateTimeOffset? | null | Required when `period=CUSTOM`; date-only midnight is treated as the full local day |
| `currency` | string? | `VND` | P0 supports `VND`; unsupported values return `FINANCIAL_CURRENCY_INVALID` |

Reporting timezone is always `Asia/Ho_Chi_Minh`. Backend resolves local business boundaries and queries UTC timestamps using a half-open interval internally.

#### Response

```json
{
  "status": 200,
  "message": "Admin financial summary retrieved successfully.",
  "data": {
    "period": {
      "type": "CUSTOM",
      "from": "2026-07-01T00:00:00+07:00",
      "to": "2026-09-30T23:59:59.9999999+07:00",
      "timezone": "Asia/Ho_Chi_Minh"
    },
    "currency": "VND",
    "collectedAmount": 0,
    "outstandingPaymentAmount": 0,
    "contractedReceivableAmount": 0,
    "orderCommercialValue": 0,
    "failedTransactionCount": 0,
    "activePaymentCount": 0
  }
}
```

#### Metric Semantics

| Field | Meaning |
| --- | --- |
| `collectedAmount` | Sum of actual successful canonical Payments in the period |
| `outstandingPaymentAmount` | Sum of currently active collectible Payment obligations |
| `contractedReceivableAmount` | Sum of active Order `remainingAmount`; separate from outstanding payment |
| `orderCommercialValue` | Sum of confirmed Order `finalTotalAmount` in the period; not accounting revenue |
| `failedTransactionCount` | Count of failed PaymentTransaction rows in the period |
| `activePaymentCount` | Count of currently active collectible Payment obligations |

Collected cash includes only:

- `PROJECT_START_FEE`
- `DEPOSIT`
- `REMAINING_PAYMENT`

Collected cash excludes:

- `FULL_PAYMENT`
- `REFUND`
- `OTHER`
- standalone `PaymentTransaction.SUCCESS` amounts
- `Payment.status = PAID` rows without `paidAt`

Period fields:

| Metric | Date field |
| --- | --- |
| Collected cash | `payments.paid_at` |
| Order commercial value | `orders.confirmed_at` |
| Failed transactions | `payment_transactions.created_at` |
| Outstanding payment | current-state; not period-filtered |
| Contracted receivable | current-state; not period-filtered |

#### Error Codes

| HTTP | `errorCode` | Trigger |
| --- | --- | --- |
| 400 | `FINANCIAL_PERIOD_INVALID` | Unsupported `period` |
| 400 | `FINANCIAL_DATE_RANGE_INVALID` | Missing custom range or `from > to` |
| 400 | `FINANCIAL_CURRENCY_INVALID` | Unsupported currency |
| 401/403 | auth result | Non-admin or unauthenticated request |

### `GET /admin/financial/receivables`

**Roles:** ADMIN

Returns current outstanding Payment obligations and active Order receivables separately, plus paged drill-down rows for FE tables.

`GET /admin/financial/receivables/items` is also available for drill-down screens. It accepts the same query parameters and returns the same DTO shape, so FE can reuse the same table model while linking from the receivable card.

#### Query

| Param | Type | Default | Notes |
| --- | --- | --- | --- |
| `projectId` | guid? | null | Filter one project |
| `customerId` | guid? | null | Filter by order customer |
| `salesId` | guid? | null | Matches `orders.sales_id` or `projects.assigned_sales_id` |
| `paymentType` | `PaymentType?` | null | When supplied, returns only orders with a matching active collectible payment |
| `paymentStatus` | `PaymentStatus?` | null | Usually `PENDING` or `PROCESSING`; only active collectible payments are considered |
| `orderStatus` | `OrderStatus?` | null | Filter active receivable orders |
| `from` | DateTimeOffset? | null | Optional range start for `orders.confirmed_at` |
| `to` | DateTimeOffset? | null | Optional range end for `orders.confirmed_at`; midnight means full local day |
| `page` | int | `1` | Must be `> 0` |
| `pageSize` | int | `20` | `1..100` |
| `sortBy` | string? | `confirmedAt` | `confirmedAt`, `projectCode`, `projectName`, `orderCode`, `orderStatus`, `finalTotalAmount`, `remainingAmount` |
| `sortDirection` | string? | `desc` | `asc` or `desc` |

Date range is intentionally tied to `orders.confirmed_at` for this receivable view. Outstanding payments are resolved only for the filtered receivable orders, so the card totals and table rows remain consistent.

#### Response

```json
{
  "status": 200,
  "message": "Financial receivables retrieved successfully.",
  "data": {
    "outstandingPaymentAmount": 70000000,
    "outstandingPaymentCount": 1,
    "contractedReceivableAmount": 140000000,
    "ordersWithReceivableCount": 2,
    "items": [
      {
        "projectId": "...",
        "projectCode": "PRJ-2026-0001",
        "projectName": "Cafe Interior",
        "orderId": "...",
        "orderCode": "ORD-2026-0001",
        "orderStatus": "FINAL_PAYMENT_PENDING",
        "finalTotalAmount": 100000000,
        "paidAmount": 30000000,
        "remainingAmount": 70000000,
        "activePaymentId": "...",
        "activePaymentType": "REMAINING_PAYMENT",
        "activePaymentAmount": 70000000,
        "activePaymentStatus": "PENDING",
        "isPaymentCreated": true
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 2,
    "totalPages": 1
  }
}
```

#### Metric Semantics

| Field | Meaning |
| --- | --- |
| `outstandingPaymentAmount` / `outstandingPaymentCount` | Active collectible payment rows: `PENDING` / `PROCESSING`, not expired, no successful transaction |
| `contractedReceivableAmount` / `ordersWithReceivableCount` | Active orders with `remainingAmount > 0`; cancelled/completed orders are excluded by current active receivable policy |
| `isPaymentCreated` | `true` only when the order currently has an active collectible payment obligation |

Do not add `outstandingPaymentAmount` and `contractedReceivableAmount` together as a single "expected money" card. They are separate views of obligations and may refer to the same order after a remaining payment has been created.

#### Error Codes

| HTTP | `errorCode` | Trigger |
| --- | --- | --- |
| 400 | `FINANCIAL_RECEIVABLE_FILTER_INVALID` | Invalid paging, sort, or date range |
| 401/403 | auth result | Non-admin or unauthenticated request |

### `GET /admin/financial/payment-breakdown`

**Roles:** ADMIN

Returns collected cash, active outstanding obligations, and expired count grouped by canonical payment type.

#### Query

| Param | Type | Default | Notes |
| --- | --- | --- | --- |
| `from` | DateTimeOffset | required | Reporting range start |
| `to` | DateTimeOffset | required | Reporting range end; midnight means full local day |
| `currency` | string? | `VND` | P0 supports `VND`; unsupported values return `FINANCIAL_CURRENCY_INVALID` |

#### Response

```json
{
  "status": 200,
  "message": "Payment breakdown retrieved successfully.",
  "data": {
    "currency": "VND",
    "items": [
      {
        "paymentType": "PROJECT_START_FEE",
        "collectedAmount": 0,
        "paidCount": 0,
        "outstandingAmount": 0,
        "outstandingCount": 0,
        "expiredCount": 0
      },
      {
        "paymentType": "DEPOSIT",
        "collectedAmount": 0,
        "paidCount": 0,
        "outstandingAmount": 0,
        "outstandingCount": 0,
        "expiredCount": 0
      },
      {
        "paymentType": "REMAINING_PAYMENT",
        "collectedAmount": 0,
        "paidCount": 0,
        "outstandingAmount": 0,
        "outstandingCount": 0,
        "expiredCount": 0
      }
    ]
  }
}
```

Collected fields use `payments.paid_at` inside `[from, to]` after backend conversion to UTC. Outstanding fields are current active collectible payment rows. `expiredCount` counts `EXPIRED` payment rows whose `expiredAt` is inside the range.

Only canonical payment types appear:

- `PROJECT_START_FEE`
- `DEPOSIT`
- `REMAINING_PAYMENT`

`FULL_PAYMENT`, `REFUND`, `OTHER`, and standalone `PaymentTransaction.SUCCESS` amounts are excluded.

### `GET /admin/financial/collection-trend`

**Roles:** ADMIN

Returns chart-ready collected cash trend buckets. P0 supports monthly buckets only.

#### Query

| Param | Type | Default | Notes |
| --- | --- | --- | --- |
| `from` | DateTimeOffset | required | Reporting range start |
| `to` | DateTimeOffset | required | Reporting range end; midnight means full local day |
| `granularity` | string? | `MONTH` | Only `MONTH` is supported |
| `currency` | string? | `VND` | P0 supports `VND` |

#### Response

```json
{
  "status": 200,
  "message": "Collection trend retrieved successfully.",
  "data": {
    "granularity": "MONTH",
    "timezone": "Asia/Ho_Chi_Minh",
    "currency": "VND",
    "series": [
      {
        "period": "2026-07",
        "projectStartFee": 2000000,
        "deposit": 30000000,
        "remainingPayment": 70000000,
        "total": 102000000
      }
    ]
  }
}
```

Buckets are Vietnam calendar months. Backend clips the first/last month to the requested range and returns zero buckets for months without collected cash so FE can render stable charts.

#### Story 3 Error Codes

| HTTP | `errorCode` | Trigger |
| --- | --- | --- |
| 400 | `FINANCIAL_DATE_RANGE_INVALID` | Missing range or `from > to` |
| 400 | `FINANCIAL_GRANULARITY_INVALID` | Unsupported granularity |
| 400 | `FINANCIAL_CURRENCY_INVALID` | Unsupported currency |
| 401/403 | auth result | Non-admin or unauthenticated request |

### `GET /admin/financial/projects`

**Roles:** ADMIN

Returns a paged project financial overview. This is a read-only dashboard/drill-down endpoint and does not update Project, Order, Payment, Quotation, or PaymentTransaction data.

#### Query

| Param | Type | Default | Notes |
| --- | --- | --- | --- |
| `keyword` | string? | null | Searches project code, project name, or customer name |
| `projectStatus` | `ProjectStatus?` | null | Filter by current project status |
| `customerId` | guid? | null | Filter one customer |
| `salesId` | guid? | null | Filter assigned sales |
| `paymentStatus` | `PaymentStatus?` | null | Filters projects that have a matching active collectible payment |
| `paymentType` | `PaymentType?` | null | Filters projects that have a matching active collectible payment |
| `hasOrder` | bool? | null | `true` = only projects with order; `false` = only projects without order |
| `hasOutstandingPayment` | bool? | null | Uses current active collectible payment rules |
| `hasReceivable` | bool? | null | Uses active order receivable rules: active order status and `remainingAmount > 0` |
| `from` | DateTimeOffset? | null | Optional range start for `projects.created_at` |
| `to` | DateTimeOffset? | null | Optional range end for `projects.created_at`; midnight means full local day |
| `page` | int | `1` | Must be `> 0` |
| `pageSize` | int | `20` | `1..100` |
| `sortBy` | string? | `createdAt` | `createdAt`, `projectCode`, `projectName`, `projectStatus`, `orderFinalTotal`, `orderRemainingAmount`, `totalProjectCashCollected`, `lastPaidAt` |
| `sortDirection` | string? | `desc` | `asc` or `desc` |

Date filtering intentionally uses `projects.created_at` for this overview because the current schema does not have a dedicated project confirmed/financial started timestamp.

#### Response

```json
{
  "status": 200,
  "message": "Project financial overview retrieved successfully.",
  "data": {
    "items": [
      {
        "projectId": "...",
        "projectCode": "PRJ-2026-0001",
        "projectName": "Cafe Interior",
        "projectStatus": "QUOTATION_SENT",
        "customerId": "...",
        "customerName": "Customer Alpha",
        "assignedSalesId": "...",
        "assignedSalesName": "Sales Alpha",
        "projectStartFeeAmount": 2000000,
        "projectStartFeeStatus": "PAID",
        "projectStartFeePaidAt": "2026-07-01T03:00:00Z",
        "orderId": "...",
        "orderCode": "ORD-2026-0001",
        "orderStatus": "FINAL_PAYMENT_PENDING",
        "orderOriginalTotal": 100000000,
        "orderAdjustmentAmount": 0,
        "orderAdditionalDiscount": 0,
        "orderFinalTotal": 100000000,
        "orderPaidAmount": 30000000,
        "orderRemainingAmount": 70000000,
        "activePaymentId": "...",
        "activePaymentType": "REMAINING_PAYMENT",
        "activePaymentAmount": 70000000,
        "activePaymentStatus": "PENDING",
        "totalProjectCashCollected": 32000000,
        "lastPaidAt": "2026-07-10T03:00:00Z"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
}
```

#### Project Financial Semantics

| Field | Meaning |
| --- | --- |
| `projectStartFee*` | Latest project-level `PROJECT_START_FEE` payment for the project |
| `order*` | Latest order for the project by `confirmedAt`, `createdAt`, then `orderId`; nullable when no order exists |
| `activePayment*` | Latest active collectible payment: `PENDING` / `PROCESSING`, not expired, and no successful transaction |
| `totalProjectCashCollected` | Sum of canonical `PAID` payments directly on the project; excludes `FULL_PAYMENT`, `REFUND`, `OTHER`, and standalone transactions |
| `lastPaidAt` | Latest `paidAt` among canonical paid payments |

Do not compute collected cash as `projectStartFeeAmount + orderPaidAmount`. The API already returns `totalProjectCashCollected` using canonical paid Payment rows.

### `GET /admin/financial/projects/{projectId}`

**Roles:** ADMIN

Returns the same financial overview shape for one project. Nullable order/payment fields are expected when the project has not reached those workflow steps.

#### Error Codes

| HTTP | `errorCode` | Trigger |
| --- | --- | --- |
| 400 | `FINANCIAL_PROJECT_FILTER_INVALID` | Invalid paging, sort, or date range on list endpoint |
| 404 | `PROJECT_NOT_FOUND` | Project detail does not exist |
| 401/403 | auth result | Non-admin or unauthenticated request |

### `GET /admin/financial/payments`

**Roles:** ADMIN

Returns a paged payment operations list with provider attempt diagnostics. This endpoint is read-only and never exposes raw provider payloads, signatures, webhook bodies, checkout secrets, or QR payload internals.

#### Query

| Param | Type | Default | Notes |
| --- | --- | --- | --- |
| `projectId` | guid? | null | Filter one project |
| `orderId` | guid? | null | Filter one order-linked payment |
| `customerId` | guid? | null | Filter by project customer |
| `paymentType` | `PaymentType?` | null | Example: `DEPOSIT`, `REMAINING_PAYMENT` |
| `paymentStatus` | `PaymentStatus?` | null | Payment has no fake `FAILED` status; failures are on attempts |
| `provider` | `PaymentProvider?` | null | Filters attempt provider, example `PAYOS` |
| `currency` | string? | null | Optional drill-down filter; P0 accepts `VND` when supplied |
| `createdFrom` / `createdTo` | DateTimeOffset? | null | Optional range for `payments.created_at`; midnight `to` means full local day |
| `paidFrom` / `paidTo` | DateTimeOffset? | null | Optional range for `payments.paid_at` |
| `expiredFrom` / `expiredTo` | DateTimeOffset? | null | Optional range for `payments.expired_at` |
| `hasFailedAttempt` | bool? | null | `true` = at least one failed transaction attempt; `false` = none |
| `minFailedAttemptCount` | int? | null | Must be `>= 0`; repeated failure screens usually use `2` |
| `page` | int | `1` | Must be `> 0` |
| `pageSize` | int | `20` | `1..100` |
| `sortBy` | string? | `createdAt` | `createdAt`, `paidAt`, `expiredAt`, `amount`, `paymentCode`, `status` |
| `sortDirection` | string? | `desc` | `asc` or `desc` |

#### Response

```json
{
  "status": 200,
  "message": "Financial payments retrieved successfully.",
  "data": {
    "items": [
      {
        "paymentId": "...",
        "paymentCode": "PAY-2026-0001",
        "projectId": "...",
        "projectCode": "PRJ-2026-0001",
        "orderId": "...",
        "orderCode": "ORD-2026-0001",
        "customerId": "...",
        "customerName": "Customer Alpha",
        "paymentType": "DEPOSIT",
        "amount": 30000000,
        "currency": "VND",
        "status": "PENDING",
        "createdAt": "2026-07-25T03:00:00Z",
        "expiredAt": "2026-07-30T03:00:00Z",
        "paidAt": null,
        "lastProvider": "PAYOS",
        "attemptCount": 2,
        "failedAttemptCount": 2,
        "lastTransactionStatus": "FAILED",
        "lastFailureReason": "Insufficient funds",
        "lastAttemptAt": "2026-07-26T03:00:00Z"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
}
```

`lastFailureReason` is the latest failed attempt reason, not necessarily the latest transaction reason. A paid payment can still show historical failed attempts, but it is not treated as an active failure exception.

### `GET /admin/financial/exceptions`

**Roles:** ADMIN

Returns read-only operational financial exceptions for Admin attention. The endpoint does not create notification records, does not mutate Payment/Order state, and does not introduce a Payment `FAILED` lifecycle status.

#### Query

| Param | Type | Default | Notes |
| --- | --- | --- | --- |
| `exceptionType` | string? | null | One of the exception types below; case-insensitive input |
| `severity` | string? | null | Example: `HIGH`, `MEDIUM` |
| `projectId` | guid? | null | Filter one project |
| `paymentType` | `PaymentType?` | null | Applies to payment-backed exceptions |
| `from` / `to` | DateTimeOffset? | null | Optional range for exception occurrence time |
| `page` | int | `1` | Must be `> 0` |
| `pageSize` | int | `20` | `1..100` |

#### Exception Types

| Type | Meaning |
| --- | --- |
| `PAYMENT_EXPIRED` | Payment status is `EXPIRED` |
| `PAYMENT_REPEATED_FAILURE` | Non-paid payment has at least 2 failed transaction attempts |
| `FINAL_PAYMENT_NOT_CREATED` | Order is `FINAL_PAYMENT_PENDING`, has receivable, but no active `REMAINING_PAYMENT` |
| `DELIVERED_WITH_RECEIVABLE` | Delivered order still has `remainingAmount > 0` |
| `PAYMENT_PENDING_TOO_LONG` | Active collectible payment has stayed pending/processing beyond the operational threshold |

#### Response

```json
{
  "status": 200,
  "message": "Financial exceptions retrieved successfully.",
  "data": {
    "items": [
      {
        "exceptionType": "PAYMENT_REPEATED_FAILURE",
        "severity": "HIGH",
        "projectId": "...",
        "orderId": "...",
        "paymentId": "...",
        "title": "Payment has repeated failed attempts",
        "reason": "Payment has two or more failed transaction attempts.",
        "amount": 30000000,
        "age": 1,
        "occurredAt": "2026-07-26T03:00:00Z",
        "recommendedAction": "Open payment attempts and support the customer with a new checkout if needed.",
        "targetResourceType": "PAYMENT",
        "targetResourceId": "..."
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
}
```

#### Error Codes

| HTTP | `errorCode` | Trigger |
| --- | --- | --- |
| 400 | `FINANCIAL_PAYMENT_FILTER_INVALID` | Invalid paging, sort, failed-attempt, or date filter |
| 400 | `FINANCIAL_EXCEPTION_TYPE_INVALID` | Unsupported `exceptionType` |
| 401/403 | auth result | Non-admin or unauthenticated request |

### FIN-ADM-06 Reporting Hardening Notes

No new business endpoint was added for FIN-ADM-06. It hardens the existing Admin Financial endpoints:

- Financial reporting periods use explicit `Asia/Ho_Chi_Minh` boundaries.
- Backend resolves local business periods to UTC and queries half-open ranges: `>= fromUtc` and `< toUtc`.
- Canonical collected payment types are centralized as:
  - `PROJECT_START_FEE`
  - `DEPOSIT`
  - `REMAINING_PAYMENT`
- `FULL_PAYMENT`, `REFUND`, and `OTHER` remain excluded from collected cash metrics.
- `GET /admin/financial/payments` supports `currency=VND` so FE can reconcile card totals with paid payment drill-down rows.
- Financial indexes were added by migration `20260810143000_AddAdminFinancialDashboardIndexes`.

The added indexes target implemented query paths only:

| Index | Purpose |
| --- | --- |
| `idx_fin_payments_paid_reporting` | Summary, breakdown, trend, and paid payment drill-down |
| `idx_fin_payments_active_obligations` | Outstanding payment and stale pending payment checks |
| `idx_fin_payment_transactions_failed_reporting` | Failed transaction count over reporting periods |
| `idx_fin_payment_transactions_payment_failed_time` | Per-payment failed attempt diagnostics |
| `idx_fin_orders_project_confirmed` | Project financial overview latest-order lookup |
| `idx_fin_orders_receivable_status_confirmed` | Receivable and order exception scans |

---

## 21. Production

| Method | Path | Roles |
| --- | --- | --- |
| GET | `/production-requests` | PRODUCTION, SALES, ADMIN |
| GET | `/production-requests/{id}` | same |
| PATCH | `/production-requests/{id}/assign` | SALES, ADMIN |
| PATCH | `/production-requests/{id}/mark-feasible` | PRODUCTION, ADMIN |
| PATCH | `/production-requests/{id}/start` | PRODUCTION, ADMIN |
| PATCH | `/production-requests/{id}/complete` | PRODUCTION, ADMIN |
| PATCH | `/production-items/{id}/status` | PRODUCTION, ADMIN |
| GET | `/production-staff/available` | SALES, ADMIN |

Create production request: `POST /orders/{orderId}/production-request` (§13).

### List query

`status?`, `assignedTo?`, `priority?`

### Assign

```json
{
  "assignedTo": "...",
  "assignmentNote": "Priority batch"
}
```

### Mark feasible

```json
{ "note": "Materials in stock" }
```

### Start

Optional body (ignored for date assignment — server sets `actualStartDate` to UTC today on start):

```json
{ "actualStartDate": "2026-08-05" }
```

### Update production item status

```json
{
  "status": "IN_PRODUCTION",
  "productionNote": "Cutting",
  "cancellationReason": null
}
```

`ProductionItemStatus`: `PENDING`, `IN_PRODUCTION`, `COMPLETED`, `CANCELLED`  
`ProductionRequestStatus`: `PENDING_REVIEW`, `FEASIBLE`, `IN_PRODUCTION`, `COMPLETED`, `CANCELLED`

When completing production, each item must be `COMPLETED` or `CANCELLED`. Cancelled production items map the linked order item to **`UNAVAILABLE`** (with `unavailableReason` from production cancellation) — no order financial adjustment is required. Server sets `actualCompletionDate` on complete.

### Available staff query

`projectId?`, `productionRequestId?`, `search?`

**Response item:** `accountId`, `fullName`, `email`, `avatarUrl?`, `accountStatus`, request counts, `isAvailable`

### Completion response

`ProductionCompletionDto`: `productionRequestId`, `productionStatus`, `orderStatus`, `projectStatus`, `actualStartDate?`, `actualCompletionDate?`, `readyOrderItemCount`, `unavailableOrderItemCount`, `finalTotalAmount`, `paidAmount?`, `remainingAmount?`

---

## 22. SignalR hubs

| Hub | Path | Auth | Client methods |
| --- | --- | --- | --- |
| NotificationsHub | `/hubs/notifications` | JWT | Auto-join `user:{accountId}`, `role:{ROLE}` |
| ProjectChatHub | `/hubs/project-chat` | JWT | `JoinProject`, `LeaveProject`, `JoinChat`, `LeaveChat` |
| PaymentHub | `/hubs/payments` | CUSTOMER, SALES, DESIGNER, ADMIN | `JoinPayment`, `LeavePayment` |

### Token sources

| Source | REST | notifications / project-chat | payments hub |
| --- | --- | --- | --- |
| `Authorization: Bearer` | ✓ | ✓ | ✓ |
| Cookie `access_token` | ✓ | ✓ | ✓ |
| `?access_token=` | — | ✓ | Not wired in `IsRealtimeHubPath` |

Negotiate example:

```text
GET /hubs/notifications/negotiate?negotiateVersion=1
```

Details: `docs/signalr-notification-guide.md`.

### Chat notification event

`project_chat.message_sent` is emitted on both hubs with different purposes:

| Hub | Receiver | Purpose |
| --- | --- | --- |
| `/hubs/project-chat` | joined `project:{projectId}` / `project_chat:{chatId}` groups | live chat thread refresh |
| `/hubs/notifications` | direct `user:{accountId}` groups | notification bell / unread notification UI |

Notification title: `New chat message`  
Notification message: `{SenderName} sent a new message in "{ChatTitle}".`

### Payment realtime payload (typical)

`PaymentUpdatedRealtimeDto`: `paymentId`, `projectId`, `paymentCode`, `status?`, `amount`, `paidAmount`, `remainingAmount`, `paymentTransactionId`, `transactionAmount`, `appliedAmount`, `paidAt?`, `occurredAt`

---

## 23. Enums

All values are JSON strings matching C# member names.

| Enum | Values |
| --- | --- |
| `AccountStatus` | `ACTIVE`, `INACTIVE`, `SUSPENDED` |
| `ProjectStatus` | `SUBMITTED`, `IN_CONSULTATION`, `NEED_BASIC_INFORMATION`, `WAITING_FOR_DESIGNER_ASSIGNMENT`, `MEASUREMENT_REQUIRED`, `SPACE_VERIFIED`, `PROPOSAL_CONSULTING`, `PROPOSAL_SELECTED`, `QUOTATION_SENT`, `QUOTATION_REVISION_REQUESTED`, `ORDER_CONFIRMED`, `IN_PRODUCTION`, `READY_FOR_DELIVERY`, `DELIVERING`, `DELIVERED`, `COMPLETED`, `REJECTED` |
| `ProjectSpaceDataStatus` | `SUFFICIENT`, `INSUFFICIENT` |
| `ProposalStatus` | `DRAFT`, `PUBLISHED`, `SELECTED`, `REVISION_REQUESTED`, `REJECTED`, `ARCHIVED` |
| `ProposalSceneType` | `TWO_D`, `THREE_D` |
| `QuotationStatus` | `DRAFT`, `SENT`, `REVISION_REQUESTED`, `REVISED`, `ACCEPTED`, `REJECTED`, `EXPIRED`, `CANCELLED` |
| `OrderStatus` | `CREATED`, `DEPOSIT_PENDING`, `DEPOSIT_PAID`, `IN_PRODUCTION`, `READY_FOR_DELIVERY`, `DELIVERING`, `DELIVERED`, `FINAL_PAYMENT_PENDING`, `COMPLETED`, `CANCELLED` |
| `OrderItemStatus` | `PENDING`, `IN_PRODUCTION`, `READY`, `UNAVAILABLE`, `DELIVERED`, `CANCELLED` |
| `PaymentType` | `PROJECT_START_FEE`, `DEPOSIT`, `REMAINING_PAYMENT`, `FULL_PAYMENT`, `REFUND`, `OTHER` |
| `PaymentStatus` | `PENDING`, `PROCESSING`, `PAID`, `CANCELLED`, `EXPIRED`, `REFUNDED` |
| `PaymentProvider` | `PAYOS`, `SEPAY`, `CASH`, `MANUAL_BANK_TRANSFER`, `OTHER` |
| `PaymentMethod` | `PAYMENT_LINK`, `QR_CODE`, `BANK_TRANSFER`, `CASH`, `OTHER` |
| `PaymentTransactionStatus` | `PENDING`, `SUCCESS`, `FAILED`, `CANCELLED` |
| `PaymentTransactionType` | `CHARGE`, `REFUND`, `ADJUSTMENT` |
| `CustomizationStatus` | `SUBMITTED`, `REVIEWING`, `ACCEPTED`, `CANCELLED` |
| `CustomizationVersionStatus` | `DRAFT`, `REVIEWING`, `ACCEPTED`, `PRODUCTION_REJECTED`, `WITHDRAWN` |
| `ProductionFeasibilityStatus` | `PENDING`, `FEASIBLE`, `NOT_FEASIBLE` |
| `ProductionRequestStatus` | `PENDING_REVIEW`, `FEASIBLE`, `IN_PRODUCTION`, `COMPLETED`, `CANCELLED` |
| `ProductionItemStatus` | `PENDING`, `IN_PRODUCTION`, `COMPLETED`, `CANCELLED` |
| `ProductStatus` | `ACTIVE`, `INACTIVE`, `ARCHIVED` |
| `ProductVersionType` | `STANDARD`, `CUSTOM`, `PROJECT_SPECIFIC` |
| `ProjectAreaType` | `STORE`, `FLOOR`, `ROOM`, `ZONE`, `OUTDOOR_AREA`, `OTHER` |
| `ProjectAreaStatus` | `DRAFT`, `NEED_MEASUREMENT`, `MEASURED`, `VERIFIED`, `CANCELLED` |
| `ProjectScheduleType` | `MEASUREMENT`, `CONSULTATION`, `DESIGN_REVIEW`, `DELIVERY`, `HANDOVER`, `OTHER` |
| `ProjectScheduleStatus` | `PENDING_CONFIRMATION`, `CONFIRMED`, `COMPLETED`, `CANCELLED` |
| `ProjectChatType` | `SALES`, `DESIGNER`, `PRODUCTION`, `DELIVERY`, `GENERAL`, `INTERNAL` |
| `ProjectChatStatus` | `OPEN`, `CLOSED`, `ARCHIVED` |
| `ProjectChatMessageType` | `TEXT`, `FILE`, `SYSTEM` |
| `FileStatus` | `ACTIVE`, `ARCHIVED` |
| `FileVisibility` | `CUSTOMER_VISIBLE`, `STAFF_ONLY`, `PRIVATE` |
| `FileType` | `SPACE_IMAGE`, `FLOOR_PLAN`, `REFERENCE_IMAGE`, `BRAND_ASSET`, `CAD_FILE`, `PDF_DRAWING`, `MEASUREMENT_REPORT`, `LIDAR_SCAN`, `MODEL_3D`, `TEXTURE`, `PRODUCT_PREVIEW`, `PROPOSAL_PREVIEW`, `PROPOSAL_FILE`, `QUOTATION_FILE`, `ORDER_DOCUMENT`, `PRODUCTION_FILE`, `DELIVERY_PHOTO`, `DELIVERY_NOTE`, `REVIEW_IMAGE`, `OTHER` |
| `NotificationStatus` | `UNREAD`, `READ` |

---

## Appendix A — Misc endpoints

| Method | Path | Auth | Notes |
| --- | --- | --- | --- |
| GET | `/` | Public | Returns `"FurniSpace API"`; Swagger UI also served at `/` |
| GET | `/health/redis` | Public | Only if `REDIS_DEBUG_HEALTH` / `Redis:DebugHealth` enabled |
| GET | `/swagger/v1/swagger.json` | Public | OpenAPI document |

CLI (not HTTP): `dotnet run --project src/FurniSpace.API -- reindex {accounts|products|projects|chat-messages|project-files}`

---

## Appendix B — Typical end-to-end flow (customer project)

```text
1. POST /auth/register → verify-email → cookies
2. POST /projects
3. Sales: PATCH .../sales-assignment → consultation / info requests
4. Sales: PATCH .../designer-assignment (after start fee if required)
5. Designer: POST proposals → scenes → PUT room-planner → sync items → publish
6. Customer: PATCH proposals/{id}/select-final → draft quotation auto-created (`quotationId` in response)
7. Sales: PATCH quotations/{id} (validUntil, depositAmount) → PATCH send
8. Customer: PATCH quotations/{id}/accept → Order **CREATED** (deposit snapshotted, not collected yet)
9. POST orders/{id}/payments/deposit → order **DEPOSIT_PENDING** → POST /api/payments/{id}/transactions (or SePay/PayOS helpers)
10. Provider webhook → PAID → order/project side effects
11. (Optional before deposit paid) POST projects/{id}/reopen-proposal → back to PROPOSAL_CONSULTING
12. Sales: POST production-request → production lifecycle → delivery → complete
```

---

## 4b. Admin Reports (SCRUM-428 → SCRUM-436)

Controllers: `AdminReportsController`, `AdminProductionWorkloadController`  
**Auth:** ADMIN only on all endpoints  
Envelope: `ServiceResult` / `PagedResult` (except export which returns raw CSV on success)

| Method | Path | Ticket | Description |
| --- | --- | --- | --- |
| GET | `/admin/reports/overview` | SCRUM-428 | Cross-domain dashboard snapshot |
| GET | `/admin/reports/business` | SCRUM-429 | Accounts + designer/sales capacity |
| GET | `/admin/reports/projects` | SCRUM-430 | Funnel, aging snapshot |
| GET | `/admin/reports/commercial` | SCRUM-431 | Quotations / orders / payments KPIs |
| GET | `/admin/reports/production` | SCRUM-432 | Production request/item KPIs |
| GET | `/admin/reports/delivery` | SCRUM-433 | Delivery projects/orders/schedules |
| GET | `/admin/reports/catalog` | SCRUM-435 | Catalog health + facets |
| GET | `/admin/reports/projects/aging` | SCRUM-436 | Aging drill-down (paged) |
| GET | `/admin/reports/commercial/trend` | SCRUM-436 | Day/week commercial trend (max 90d) |
| GET | `/admin/reports/export` | SCRUM-436 | CSV export (`domain` required) |
| GET | `/admin/reports/delivery/reviews` | SCRUM-436 | CSAT / project reviews |
| GET | `/admin/reports/catalog/bestsellers` | SCRUM-436 | Top products by qty/revenue |
| GET | `/admin/production/workload` | SCRUM-436 | Production staff workload board |
| GET | `/admin/production/workload/summary` | SCRUM-436 | Production workload summary cards |

### Common query

| Param | Type | Notes |
| --- | --- | --- |
| `from`, `to` | datetime? | optional unless noted; `from <= to` |
| `page`, `pageSize` | int | paging endpoints; pageSize 1–100 |

### Common errors

| HTTP | Message |
| --- | --- |
| 400 | `From date must be less than or equal to To date.` |
| 400 | `Page must be greater than zero.` |
| 400 | `Page size must be between 1 and 100.` |
| 401/403 | auth |

### `GET /admin/reports/overview`

**Query:** `from?`, `to?`  
**Success message:** `Report overview retrieved successfully.`

Key `data` groups: `business`, `projects`, `commercial`, `production`, `delivery`, `catalog`.

### `GET /admin/reports/business`

Snapshot; reuses designer (max 2) + sales (max 5) workload semantics from SCRUM-412/414.

### `GET /admin/reports/projects`

Includes `byStatus`, `byBucket`, `unassignedIntakeCount`, `waitingForDesignerCount`, `aging.over7/14/30Days`.

### `GET /admin/reports/projects/aging`

| Param | Default | Notes |
| --- | --- | --- |
| `thresholdDays` | 7 | must be > 0 |
| `bucket` | — | `INTAKE` \| `COMMERCIAL` \| `DESIGN_MONITOR` \| `FULFILLMENT` |
| `reason` | — | `UNASSIGNED_INTAKE` \| `WAITING_DESIGNER` \| `STUCK` |
| `sortBy` | `AgeDaysDesc` | or `SubmittedAtAsc` |

### `GET /admin/reports/commercial/trend`

**Required:** `from`, `to` (≤ 90 days). `granularity`: `day` (default) \| `week`.

### `GET /admin/reports/export`

| Param | Notes |
| --- | --- |
| `domain` | `overview` \| `business` \| `projects` \| `commercial` \| `production` \| `delivery` \| `catalog` |
| `format` | `csv` (default) |

Success: raw `text/csv` file (`Content-Disposition` attachment). Errors still use JSON `ServiceResult`.

### `GET /admin/production/workload`

Soft cap `maxActiveRequests = 5`. `capacityState`: `AVAILABLE` \| `FULL` \| `OVER`.

---

## Appendix C — Maintenance notes

- Prefer this doc + live `/swagger/v1/swagger.json` when fields drift; DTO source of truth is `src/FurniSpace.Application/DTOs` (report models also in `src/FurniSpace.Shared/DTOs/Reports`).
  2190|- Routing is intentionally inconsistent in a few places (`/api/Accounts` vs `/accounts/...`, `/api/ProductVersions` vs `/ProductVersions`); paths above match controllers as coded.
- `AccountsController` CRUD currently lacks `[Authorize]` — treat as a security gap until locked down.
- Auth tokens are cookie-first; JSON body does not include raw access/refresh tokens.
- Deeper payment / SignalR / room-planner / Firebase behavior: see related guides listed at the top of `docs/backend-api-dev-guide.md`.
