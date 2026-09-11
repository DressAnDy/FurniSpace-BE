#nullable enable

using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Financial;
using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Common.Reports;
using FurniSpace.Application.DTOs.Reports;
using FurniSpace.Application.Interfaces.Reports;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Financial;
using FurniSpace.Infrastructure.ReadModels.Reports;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Services.Reports;

public sealed class AdminProjectReportService : IAdminProjectReportService
{
    private const int MaxPageSize = 100;
    private const string SortSeverityDesc = "severitydesc";
    private const string SortAgeDaysDesc = "agedaysdesc";
    private const string SortSubmittedAtAsc = "submittedatasc";
    private const string SortSubmittedAtDesc = "submittedatdesc";

    private static readonly HashSet<string> ValidSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        AdminProjectReportAttention.SeverityWatch,
        AdminProjectReportAttention.SeverityAction,
        AdminProjectReportAttention.SeverityEscalate
    };

    private static readonly HashSet<string> ValidOwnerRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        AdminProjectReportAttention.RoleSales,
        AdminProjectReportAttention.RoleDesigner,
        AdminProjectReportAttention.RoleProduction,
        AdminProjectReportAttention.RoleAdmin
    };

    private static readonly HashSet<string> ValidAttentionReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        AdminProjectReportAttention.UnassignedIntake,
        AdminProjectReportAttention.WaitingCustomerInfo,
        AdminProjectReportAttention.StartFeeBlocking,
        AdminProjectReportAttention.WaitingDesigner,
        AdminProjectReportAttention.MeasurementOverdue,
        AdminProjectReportAttention.ProposalStalled,
        AdminProjectReportAttention.QuotationRevisionLoop,
        AdminProjectReportAttention.PaymentException,
        AdminProjectReportAttention.ProductionBlocked,
        AdminProjectReportAttention.DeliveryOverdue,
        AdminProjectReportAttention.FinalPaymentPending,
        AdminProjectReportAttention.ReadyToComplete
    };

    private readonly IAdminProjectReportRepository _reports;
    private readonly IFinancialReadRepository _financial;

    public AdminProjectReportService(
        IAdminProjectReportRepository reports,
        IFinancialReadRepository financial)
    {
        _reports = reports;
        _financial = financial;
    }

    public async Task<ServiceResult<PagedResult<AdminProjectReportListItemDto>>> GetListAsync(
        AdminProjectReportsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new AdminProjectReportsQueryDto();
        if (!TryNormalizeListQuery(query, out var page, out var pageSize, out var errorMessage))
        {
            return ServiceResult<PagedResult<AdminProjectReportListItemDto>>.Failure(
                Error.BadRequest(AdminProjectReportErrorCodes.FilterInvalid, errorMessage));
        }

        if (!TryResolveStageFilter(query.Stage, out var stageStatuses, out errorMessage))
        {
            return ServiceResult<PagedResult<AdminProjectReportListItemDto>>.Failure(
                Error.BadRequest(AdminProjectReportErrorCodes.FilterInvalid, errorMessage));
        }

        var utcNow = DateTime.UtcNow;
        var candidates = await _reports.GetCandidatesAsync(
            new AdminProjectReportListQueryReadModel
            {
                Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
                StageStatuses = stageStatuses,
                ProjectStatus = query.ProjectStatus,
                SalesId = query.SalesId,
                DesignerId = query.DesignerId,
                FromUtc = query.From,
                ToUtcExclusive = ToExclusiveEnd(query.To),
                ExcludeTerminal = query.AttentionOnly
            },
            utcNow,
            cancellationToken);

        var items = candidates
            .Select(candidate => TryMapListItem(candidate, query, utcNow))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        items = SortList(items, query.SortBy, query.SortDirection).ToList();
        var pageItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return ServiceResult<PagedResult<AdminProjectReportListItemDto>>.Success(
            PagedResult<AdminProjectReportListItemDto>.Create(pageItems, page, pageSize, items.Count),
            "Admin project reports retrieved successfully.");
    }

    public async Task<ServiceResult<AdminProjectReportDetailDto>> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var candidate = await _reports.GetCandidateAsync(projectId, utcNow, cancellationToken);
        if (candidate is null)
        {
            return ServiceResult<AdminProjectReportDetailDto>.Failure(
                Error.NotFound(AdminProjectReportErrorCodes.ProjectNotFound, "Project not found."));
        }

        var financial = await _financial.GetProjectFinancialRowAsync(
            projectId,
            utcNow,
            FinancialReportingConstants.CanonicalCollectedPaymentTypes,
            cancellationToken);

        var ageInStatusDays = AdminProjectReportAttention.AgeInStatusDays(candidate, utcNow);
        var hits = AdminProjectReportAttention.Evaluate(candidate, utcNow, ageInStatusDays);
        var primary = AdminProjectReportAttention.Primary(hits);
        var stageKey = AdminProjectReportAttention.ResolveStageKey(candidate.Status);
        var isRejected = candidate.Status == ProjectStatus.REJECTED;
        var isCompleted = candidate.Status == ProjectStatus.COMPLETED;

        var detail = new AdminProjectReportDetailDto
        {
            Header = new AdminProjectReportHeaderDto
            {
                ProjectId = candidate.ProjectId,
                ProjectCode = candidate.ProjectCode,
                ProjectName = candidate.ProjectName,
                ProjectStatus = candidate.Status,
                Stage = stageKey,
                IsRejected = isRejected,
                RejectionReason = candidate.RejectionReason,
                BusinessType = candidate.BusinessType,
                ProjectAddress = candidate.ProjectAddress,
                CustomerId = candidate.CustomerId,
                CustomerName = candidate.CustomerName,
                AssignedSalesId = candidate.AssignedSalesId,
                AssignedSalesName = candidate.AssignedSalesName,
                AssignedDesignerId = candidate.AssignedDesignerId,
                AssignedDesignerName = candidate.AssignedDesignerName,
                SubmittedAt = candidate.SubmittedAt,
                SalesAssignedAt = candidate.SalesAssignedAt,
                DesignerAssignedAt = candidate.DesignerAssignedAt,
                CompletedAt = candidate.CompletedAt,
                RejectedAt = candidate.RejectedAt,
                AgeDays = AdminProjectReportAttention.AgeDays(candidate.SubmittedAt, candidate.CreatedAt, utcNow),
                AgeInStatusDays = ageInStatusDays,
                PrimaryAttention = primary is null
                    ? null
                    : new AdminProjectReportAttentionDto
                    {
                        Reason = primary.Reason,
                        Severity = primary.Severity,
                        OwnerRole = primary.OwnerRole,
                        SuggestedAction = primary.SuggestedAction
                    },
                AllAttentionReasons = hits.Select(h => h.Reason).Distinct(StringComparer.Ordinal).ToList()
            },
            CurrentStageHealth = isRejected || isCompleted
                ? null
                : BuildStageHealth(candidate, stageKey, primary, hits, utcNow),
            FlowProgress = BuildFlowProgress(candidate),
            CommercialSnapshot = MapCommercial(financial),
            TerminalSummary = BuildTerminalSummary(candidate)
        };

        return ServiceResult<AdminProjectReportDetailDto>.Success(
            detail,
            "Admin project report retrieved successfully.");
    }

    private static AdminProjectReportStageHealthDto? BuildStageHealth(
        AdminProjectReportCandidateReadModel candidate,
        string? stageKey,
        AdminProjectReportAttention.AttentionHit? primary,
        IReadOnlyList<AdminProjectReportAttention.AttentionHit> hits,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(stageKey))
        {
            return null;
        }

        var isBlocked = hits.Any(h =>
            h.Reason is AdminProjectReportAttention.ProductionBlocked
                or AdminProjectReportAttention.DeliveryOverdue
                or AdminProjectReportAttention.WaitingCustomerInfo
                or AdminProjectReportAttention.MeasurementOverdue
                or AdminProjectReportAttention.QuotationRevisionLoop
                or AdminProjectReportAttention.PaymentException
                or AdminProjectReportAttention.StartFeeBlocking);

        var state = isBlocked
            ? ProjectWorkflowStageCatalog.StateBlocked
            : ProjectWorkflowStageCatalog.StateActive;

        var (title, summary) = ResolveStageCopy(stageKey, state, candidate);
        var blockers = BuildBlockers(hits);
        var next = primary is null
            ? new AdminProjectReportNextActionDto
            {
                OwnerRole = AdminProjectReportAttention.RoleSales,
                SuggestedAction = "Continue project workflow."
            }
            : new AdminProjectReportNextActionDto
            {
                OwnerRole = primary.OwnerRole,
                SuggestedAction = primary.SuggestedAction
            };

        return new AdminProjectReportStageHealthDto
        {
            Stage = stageKey,
            State = state,
            StatusInStage = candidate.Status,
            Title = title,
            Summary = summary,
            AgeInStageDays = AdminProjectReportAttention.AgeInStageDays(candidate, utcNow),
            Blockers = blockers,
            NextAction = next,
            Links = BuildLinks(candidate)
        };
    }

    private static AdminProjectReportListItemDto? TryMapListItem(
        AdminProjectReportCandidateReadModel candidate,
        AdminProjectReportsQueryDto query,
        DateTime utcNow)
    {
        var ageInStatusDays = AdminProjectReportAttention.AgeInStatusDays(candidate, utcNow);
        var hits = AdminProjectReportAttention.Evaluate(candidate, utcNow, ageInStatusDays);
        var primary = AdminProjectReportAttention.Primary(hits);

        if (!MatchesListFilters(query, primary))
        {
            return null;
        }

        var ageDays = AdminProjectReportAttention.AgeDays(candidate.SubmittedAt, candidate.CreatedAt, utcNow);
        if (query.MinAgeDays.HasValue && ageDays < query.MinAgeDays.Value)
        {
            return null;
        }

        return new AdminProjectReportListItemDto
        {
            ProjectId = candidate.ProjectId,
            ProjectCode = candidate.ProjectCode,
            ProjectName = candidate.ProjectName,
            ProjectStatus = candidate.Status,
            Stage = AdminProjectReportAttention.ResolveStageKey(candidate.Status),
            CustomerId = candidate.CustomerId,
            CustomerName = candidate.CustomerName,
            AssignedSalesId = candidate.AssignedSalesId,
            AssignedSalesName = candidate.AssignedSalesName,
            AssignedDesignerId = candidate.AssignedDesignerId,
            AssignedDesignerName = candidate.AssignedDesignerName,
            AgeDays = ageDays,
            AgeInStatusDays = ageInStatusDays,
            AttentionReason = primary?.Reason ?? string.Empty,
            SuggestedAction = primary?.SuggestedAction ?? string.Empty,
            OwnerRole = primary?.OwnerRole ?? string.Empty,
            Severity = primary?.Severity ?? string.Empty,
            SubmittedAt = candidate.SubmittedAt
        };
    }

    private static bool MatchesListFilters(
        AdminProjectReportsQueryDto query,
        AdminProjectReportAttention.AttentionHit? primary)
    {
        if (query.AttentionOnly && primary is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.AttentionReason)
            && (primary is null
                || !string.Equals(primary.Reason, query.AttentionReason, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Severity)
            && (primary is null
                || !string.Equals(primary.Severity, query.Severity, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerRole)
            && (primary is null
                || !string.Equals(primary.OwnerRole, query.OwnerRole, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static List<AdminProjectReportBlockerDto> BuildBlockers(
        IReadOnlyList<AdminProjectReportAttention.AttentionHit> hits)
    {
        return hits
            .Where(h => h.Severity is AdminProjectReportAttention.SeverityAction
                or AdminProjectReportAttention.SeverityEscalate)
            .Take(5)
            .Select(h => new AdminProjectReportBlockerDto
            {
                Code = h.Reason,
                Message = h.SuggestedAction
            })
            .ToList();
    }

    private static List<AdminProjectReportLinkDto> BuildLinks(
        AdminProjectReportCandidateReadModel candidate)
    {
        var links = new List<AdminProjectReportLinkDto>
        {
            new()
            {
                Type = "WORKFLOW",
                Id = candidate.ProjectId,
                Label = "Open workflow snapshot"
            }
        };

        if (candidate.LatestQuotationId.HasValue)
        {
            links.Add(new AdminProjectReportLinkDto
            {
                Type = "QUOTATION",
                Id = candidate.LatestQuotationId.Value,
                Label = "Open quotation"
            });
        }

        if (candidate.LatestOrderId.HasValue)
        {
            links.Add(new AdminProjectReportLinkDto
            {
                Type = "ORDER",
                Id = candidate.LatestOrderId.Value,
                Label = "Open order"
            });
        }

        if (candidate.LatestProductionRequestId.HasValue)
        {
            links.Add(new AdminProjectReportLinkDto
            {
                Type = "PRODUCTION_REQUEST",
                Id = candidate.LatestProductionRequestId.Value,
                Label = "Open production request"
            });
        }

        return links;
    }

    private static AdminProjectReportFlowProgressDto BuildFlowProgress(
        AdminProjectReportCandidateReadModel candidate)
    {
        var currentIndex = ProjectWorkflowStageCatalog.ResolveStageIndex(candidate.Status);
        var stages = new List<AdminProjectReportFlowStageDto>(ProjectWorkflowStageCatalog.Stages.Count);

        for (var i = 0; i < ProjectWorkflowStageCatalog.Stages.Count; i++)
        {
            var definition = ProjectWorkflowStageCatalog.Stages[i];
            var (state, completedAt) = ResolveFlowStage(candidate, currentIndex, i, definition.Key);
            stages.Add(new AdminProjectReportFlowStageDto
            {
                Key = definition.Key,
                Label = definition.Label,
                State = state,
                CompletedAt = completedAt
            });
        }

        return new AdminProjectReportFlowProgressDto { Stages = stages };
    }

    private static (string State, DateTime? CompletedAt) ResolveFlowStage(
        AdminProjectReportCandidateReadModel candidate,
        int? currentIndex,
        int stageIndex,
        string stageKey)
    {
        if (candidate.Status == ProjectStatus.REJECTED || currentIndex is null)
        {
            return (ProjectWorkflowStageCatalog.StateNotStarted, null);
        }

        if (stageIndex < currentIndex.Value)
        {
            return (
                ProjectWorkflowStageCatalog.StateCompleted,
                EstimateStageCompletedAt(candidate, stageKey));
        }

        if (stageIndex > currentIndex.Value)
        {
            return (ProjectWorkflowStageCatalog.StateNotStarted, null);
        }

        return ResolveCurrentFlowStage(candidate);
    }

    private static (string State, DateTime? CompletedAt) ResolveCurrentFlowStage(
        AdminProjectReportCandidateReadModel candidate)
    {
        var ageInStatus = AdminProjectReportAttention.AgeInStatusDays(candidate, DateTime.UtcNow);
        var hits = AdminProjectReportAttention.Evaluate(candidate, DateTime.UtcNow, ageInStatus);
        var blocked = hits.Any(IsFlowBlockingReason);

        if (blocked)
        {
            return (ProjectWorkflowStageCatalog.StateBlocked, null);
        }

        if (candidate.Status == ProjectStatus.COMPLETED)
        {
            return (ProjectWorkflowStageCatalog.StateCompleted, candidate.CompletedAt);
        }

        return (ProjectWorkflowStageCatalog.StateActive, null);
    }

    private static bool IsFlowBlockingReason(AdminProjectReportAttention.AttentionHit hit) =>
        hit.Reason is AdminProjectReportAttention.ProductionBlocked
            or AdminProjectReportAttention.DeliveryOverdue
            or AdminProjectReportAttention.WaitingCustomerInfo
            or AdminProjectReportAttention.MeasurementOverdue
            or AdminProjectReportAttention.QuotationRevisionLoop;

    private static DateTime? EstimateStageCompletedAt(
        AdminProjectReportCandidateReadModel candidate,
        string stageKey)
    {
        return stageKey switch
        {
            ProjectWorkflowStageCatalog.StageIntake => candidate.ApprovedAt ?? candidate.SalesAssignedAt,
            ProjectWorkflowStageCatalog.StageDesignerAssignment => candidate.DesignerAssignedAt,
            ProjectWorkflowStageCatalog.StageDesignReview => candidate.UpdatedAt,
            ProjectWorkflowStageCatalog.StageQuotationOrder => candidate.UpdatedAt,
            ProjectWorkflowStageCatalog.StageProduction => candidate.UpdatedAt,
            ProjectWorkflowStageCatalog.StageDelivery => candidate.CompletedAt,
            _ => null
        };
    }

    private static AdminProjectReportCommercialSnapshotDto MapCommercial(
        AdminFinancialProjectRowReadModel? financial)
    {
        if (financial is null)
        {
            return new AdminProjectReportCommercialSnapshotDto();
        }

        return new AdminProjectReportCommercialSnapshotDto
        {
            ProjectStartFeeAmount = financial.ProjectStartFeeAmount,
            ProjectStartFeeStatus = financial.ProjectStartFeeStatus,
            ProjectStartFeePaidAt = financial.ProjectStartFeePaidAt,
            OrderId = financial.OrderId,
            OrderCode = financial.OrderCode,
            OrderStatus = financial.OrderStatus,
            OrderFinalTotal = financial.OrderFinalTotal,
            OrderPaidAmount = financial.OrderPaidAmount,
            OrderRemainingAmount = financial.OrderRemainingAmount,
            ActivePaymentId = financial.ActivePaymentId,
            ActivePaymentType = financial.ActivePaymentType,
            ActivePaymentAmount = financial.ActivePaymentAmount,
            ActivePaymentStatus = financial.ActivePaymentStatus,
            TotalProjectCashCollected = financial.TotalProjectCashCollected,
            LastPaidAt = financial.LastPaidAt
        };
    }

    private static AdminProjectReportTerminalSummaryDto? BuildTerminalSummary(
        AdminProjectReportCandidateReadModel candidate)
    {
        if (candidate.Status == ProjectStatus.COMPLETED)
        {
            var duration = AdminProjectReportAttention.AgeDays(
                candidate.SubmittedAt,
                candidate.CreatedAt,
                candidate.CompletedAt ?? DateTime.UtcNow);
            return new AdminProjectReportTerminalSummaryDto
            {
                Outcome = "COMPLETED",
                CompletedAt = candidate.CompletedAt,
                DurationDays = duration,
                Note = "Project completed after final payment and explicit complete."
            };
        }

        if (candidate.Status == ProjectStatus.REJECTED)
        {
            return new AdminProjectReportTerminalSummaryDto
            {
                Outcome = "REJECTED",
                RejectedAt = candidate.RejectedAt,
                RejectionReason = candidate.RejectionReason,
                Note = "Project was rejected before completion."
            };
        }

        return null;
    }

    private static (string Title, string Summary) ResolveStageCopy(
        string stageKey,
        string state,
        AdminProjectReportCandidateReadModel candidate)
    {
        return (stageKey, state) switch
        {
            (ProjectWorkflowStageCatalog.StageIntake, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Waiting for basic information", "Sales requested more project information."),
            (ProjectWorkflowStageCatalog.StageIntake, _) =>
                ("Intake in progress", "Project is in consultation or pending sales assignment."),
            (ProjectWorkflowStageCatalog.StageDesignerAssignment, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Measurement required", "Designer assignment is blocked until measurement is completed."),
            (ProjectWorkflowStageCatalog.StageDesignerAssignment, _) =>
                ("Designer assignment in progress", "Waiting for designer assignment or space verification."),
            (ProjectWorkflowStageCatalog.StageDesignReview, _) =>
                ("Design review", "Customer is reviewing or selecting proposals."),
            (ProjectWorkflowStageCatalog.StageQuotationOrder, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Quotation revision requested", "Customer requested quotation revisions."),
            (ProjectWorkflowStageCatalog.StageQuotationOrder, _) =>
                ("Quotation & order", "Quotation or order confirmation is in progress."),
            (ProjectWorkflowStageCatalog.StageProduction, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Production blocked",
                    $"{candidate.CancelledProductionItemCount} production item(s) cancelled/blocked."),
            (ProjectWorkflowStageCatalog.StageProduction, _) =>
                ("Production in progress", "Production request is active."),
            (ProjectWorkflowStageCatalog.StageDelivery, ProjectWorkflowStageCatalog.StateBlocked) =>
                ("Delivery overdue", "A delivery or handover schedule is overdue."),
            (ProjectWorkflowStageCatalog.StageDelivery, _) =>
                ("Delivery in progress", "Delivery, handover, or final payment is in progress."),
            _ => ("Project in progress", "Continue the current workflow stage.")
        };
    }

    private static bool TryNormalizeListQuery(
        AdminProjectReportsQueryDto query,
        out int page,
        out int pageSize,
        out string errorMessage)
    {
        page = query.Page <= 0 ? 1 : query.Page;
        pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        errorMessage = string.Empty;

        if (page < 1)
        {
            errorMessage = "Page must be greater than zero.";
            return false;
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errorMessage = "Page size must be between 1 and 100.";
            return false;
        }

        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
        {
            errorMessage = "From date must be less than or equal to To date.";
            return false;
        }

        if (query.MinAgeDays is < 0)
        {
            errorMessage = "MinAgeDays cannot be negative.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Severity) && !ValidSeverities.Contains(query.Severity.Trim()))
        {
            errorMessage = "Severity filter is invalid.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerRole) && !ValidOwnerRoles.Contains(query.OwnerRole.Trim()))
        {
            errorMessage = "OwnerRole filter is invalid.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.AttentionReason)
            && !ValidAttentionReasons.Contains(query.AttentionReason.Trim()))
        {
            errorMessage = "AttentionReason filter is invalid.";
            return false;
        }

        query.Page = page;
        query.PageSize = pageSize;
        return true;
    }

    private static bool TryResolveStageFilter(
        string? stage,
        out IReadOnlyList<ProjectStatus>? stageStatuses,
        out string errorMessage)
    {
        stageStatuses = null;
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(stage))
        {
            return true;
        }

        stageStatuses = ResolveStageStatuses(stage);
        if (stageStatuses.Count > 0)
        {
            return true;
        }

        errorMessage = "Stage filter is invalid.";
        return false;
    }

    private static IReadOnlyList<ProjectStatus> ResolveStageStatuses(string stage)
    {
        var key = stage.Trim().ToUpperInvariant();
        var match = ProjectWorkflowStageCatalog.Stages
            .Where(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase))
            .Select(definition => definition.Statuses)
            .FirstOrDefault();
        return match ?? [];
    }

    private static DateTime? ToExclusiveEnd(DateTime? to)
    {
        if (to is null)
        {
            return null;
        }

        // Midnight local/UTC means include the full day.
        return to.Value.TimeOfDay == TimeSpan.Zero
            ? to.Value.Date.AddDays(1)
            : to.Value;
    }

    private static IEnumerable<AdminProjectReportListItemDto> SortList(
        IEnumerable<AdminProjectReportListItemDto> items,
        string? sortBy,
        string? sortDirection)
    {
        var key = (sortBy ?? SortSeverityDesc).Trim().ToLowerInvariant();
        var desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return key switch
        {
            SortAgeDaysDesc or "agedays" => desc
                ? items.OrderByDescending(i => i.AgeDays).ThenByDescending(i => i.SubmittedAt)
                : items.OrderBy(i => i.AgeDays).ThenBy(i => i.SubmittedAt),
            SortSubmittedAtAsc or "submittedat" when !desc =>
                items.OrderBy(i => i.SubmittedAt).ThenBy(i => i.ProjectCode),
            SortSubmittedAtDesc or "submittedat" =>
                items.OrderByDescending(i => i.SubmittedAt).ThenByDescending(i => i.ProjectCode),
            _ => items
                .OrderByDescending(i => AdminProjectReportAttention.SeverityRank(i.Severity))
                .ThenByDescending(i => i.AgeInStatusDays)
                .ThenByDescending(i => i.SubmittedAt)
        };
    }
}
