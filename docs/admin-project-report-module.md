# Admin Project Report Module

Spec + API cho tab Report Admin (chi tiết dự án / attention queue).

Related:

- [`docs/backend-api-dev-guide.md`](backend-api-dev-guide.md) §5.1 — project lifecycle
- [`docs/api-reference.md`](api-reference.md) §4b / §20a — reports KPI & financial (không thay)
- [`docs/FurniSpace_System_Flow_Integration_Test_Context_Updated.md`](FurniSpace_System_Flow_Integration_Test_Context_Updated.md) — flow / ProjectStatus

## Endpoints (P1 — đã implement)

| Method | Path | Dùng để |
| --- | --- | --- |
| `GET` | `/admin/project-reports` | Danh sách dự án cần chú ý (filter, sort, phân trang) |
| `GET` | `/admin/project-reports/{projectId}` | Chi tiết 1 dự án khi click dòng |

Auth: **ADMIN** only. Envelope: `ServiceResult` / `PagedResult`.

### Query list thường dùng

`keyword`, `severity`, `stage`, `attentionReason`, `ownerRole`, `attentionOnly` (mặc định `true`), `from`/`to`, `page`, `pageSize`, `sortBy=severityDesc`

Thêm: `projectStatus`, `salesId`, `designerId`, `minAgeDays`, `sortDirection`

### Detail trả về 4 khối

`header` · `currentStageHealth` · `flowProgress` · `commercialSnapshot` (+ `terminalSummary` nếu COMPLETED/REJECTED)

## Không bắt buộc cho tab này

| API | Ghi chú |
| --- | --- |
| `/admin/reports/*` | KPI tổng — tab khác |
| `/admin/financial/*` | tab Tài chính riêng |
| `GET /admin/project-reports/{id}/export` | CSV — Phase 2 (chưa implement) |

## Deep-link khi đào sâu (không phải API của module report)

| Mục đích | API |
| --- | --- |
| Workflow | `GET /admin/projects/{projectId}/workflow` |
| Tiền chi tiết | `GET /admin/financial/projects/{projectId}` |

## Attention reasons (primary)

`UNASSIGNED_INTAKE`, `WAITING_CUSTOMER_INFO`, `START_FEE_BLOCKING`, `WAITING_DESIGNER`, `MEASUREMENT_OVERDUE`, `PROPOSAL_STALLED`, `QUOTATION_REVISION_LOOP`, `PAYMENT_EXCEPTION`, `PRODUCTION_BLOCKED` (IN_PRODUCTION + cancelled production items), `DELIVERY_OVERDUE`, `FINAL_PAYMENT_PENDING`, `READY_TO_COMPLETE`

Severity: `WATCH` | `ACTION` | `ESCALATE`

## Code map

```text
API/Controllers/Admin/AdminProjectReportsController.cs
Application/Interfaces/Reports/IAdminProjectReportService.cs
Application/Services/Reports/AdminProjectReportService.cs
Application/DTOs/Reports/AdminProjectReportDtos.cs
Application/Common/Reports/AdminProjectReportAttention.cs
Infrastructure/Repositories/.../AdminProjectReportRepository.cs
```
