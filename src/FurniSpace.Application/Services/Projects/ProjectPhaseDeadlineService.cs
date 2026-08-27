using FurniSpace.Application.Common;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.Constants.Projects;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using System.Linq;

namespace FurniSpace.Application.Services.Projects;

public sealed class ProjectPhaseDeadlineService : IProjectPhaseDeadlineService
{
    private const string PhaseDeadlineNotPlannedMessage = "Project phase deadlines have not been planned.";
    private const string ManageForbiddenMessage = "You do not have access to manage project phase deadlines.";
    private const string ViewForbiddenMessage = "You do not have access to view project phase deadlines.";
    private const string PlannedStatus = "PLANNED";
    private const string OnTrackStatus = "ON_TRACK";
    private const string OverdueStatus = "OVERDUE";
    private const string CompletedOnTimeStatus = "COMPLETED_ON_TIME";
    private const string CompletedLateStatus = "COMPLETED_LATE";
    private const string NotStartedStatus = "NOT_STARTED";

    private static readonly ProjectPhaseType[] SupportedPhases =
    [
        ProjectPhaseType.PROPOSAL,
        ProjectPhaseType.PRODUCTION
    ];

    private static readonly ProjectStatus[] ProductionDeadlineWritableProjectStatuses =
    [
        ProjectStatus.ORDER_CONFIRMED,
        ProjectStatus.IN_PRODUCTION,
        ProjectStatus.READY_FOR_DELIVERY,
        ProjectStatus.DELIVERING,
        ProjectStatus.DELIVERED
    ];

    private static readonly OrderStatus[] ActiveOrderStatuses =
    [
        OrderStatus.CREATED,
        OrderStatus.DEPOSIT_PENDING,
        OrderStatus.DEPOSIT_PAID,
        OrderStatus.IN_PRODUCTION,
        OrderStatus.READY_FOR_DELIVERY,
        OrderStatus.DELIVERING,
        OrderStatus.AWAITING_CUSTOMER_CONFIRMATION,
        OrderStatus.DELIVERED,
        OrderStatus.FINAL_PAYMENT_PENDING,
        OrderStatus.COMPLETED
    ];

    private readonly IProjectRepository _projects;
    private readonly IProjectPhaseTimelineRepository _timelines;
    private readonly IProductionRequestRepository _productionRequests;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectPhaseDeadlineService(
        IProjectRepository projects,
        IProjectPhaseTimelineRepository timelines,
        IProductionRequestRepository productionRequests,
        IOrderRepository orders,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _timelines = timelines;
        _productionRequests = productionRequests;
        _orders = orders;
        _unitOfWork = unitOfWork;
    }

    public Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> UpsertAsync(
        Guid projectId,
        Guid currentUserId,
        UpsertProjectPhaseDeadlinesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = projectId;
        _ = currentUserId;
        _ = cancellationToken;

        return Task.FromResult(ServiceResult<ProjectPhaseDeadlinePlanDto>.Failure(Error.BadRequest(
            ProjectPhaseDeadlineErrorCodes.PhaseDeadlineUpsertDeprecated,
            "Use designer assignment for proposal deadline and PUT /projects/{projectId}/phase-deadlines/production for production deadline.")));
    }

    public async Task<ServiceResult<ProjectProductionPhaseDeadlineResponseDto>> UpsertProductionDeadlineAsync(
        Guid projectId,
        Guid currentUserId,
        UpsertProductionPhaseDeadlineRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var requestError = ValidateIdentity<ProjectProductionPhaseDeadlineResponseDto>(projectId, currentUserId);
        if (requestError is not null)
        {
            return requestError;
        }

        if (!request.ProductionDeadline.HasValue)
        {
            return ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Failure(Error.BadRequest(
                ProjectPhaseDeadlineErrorCodes.ProductionDeadlineRequired,
                "Production deadline is required."));
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.NotFound(
                ProjectServiceConstants.ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanManage(project, currentUserId, roleName))
        {
            return ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Forbidden(ManageForbiddenMessage);
        }

        if (project.Status is null ||
            !ProductionDeadlineWritableProjectStatuses.Contains(project.Status.Value))
        {
            return ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Failure(Error.BadRequest(
                ProjectPhaseDeadlineErrorCodes.InvalidProjectStatus,
                "Production deadline can only be set after the order is confirmed."));
        }

        var order = await _orders.GetLatestByProjectInStatusesAsync(
            projectId,
            ActiveOrderStatuses,
            cancellationToken);
        if (order is null)
        {
            return ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Failure(Error.BadRequest(
                ProjectPhaseDeadlineErrorCodes.OrderRequired,
                "An active order is required before setting production deadline."));
        }

        var productionDeadline = request.ProductionDeadline.Value;
        var validationError = await ValidateProductionDeadlineAsync(
            projectId,
            productionDeadline,
            project.TargetCompletionDate,
            cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var now = DateTime.UtcNow;
        var existing = await _timelines.GetByProjectAsync(projectId, cancellationToken);
        await UpsertTimelineAsync(
            existing,
            projectId,
            ProjectPhaseType.PRODUCTION,
            productionDeadline,
            currentUserId,
            now,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var timeline = await _timelines.GetByProjectAndPhaseAsync(
            projectId,
            ProjectPhaseType.PRODUCTION,
            cancellationToken);

        return ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Success(
            ToProductionResponseDto(projectId, order.OrderId, timeline!),
            "Production deadline saved successfully.");
    }

    public async Task<ServiceResult<DateOnly>> StageProposalDeadlineForDesignerAssignmentAsync(
        Guid projectId,
        Guid currentUserId,
        DateOnly proposalDeadline,
        DateOnly? targetCompletionDate,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateProposalDeadline(proposalDeadline, targetCompletionDate);
        if (validationError is not null)
        {
            return validationError;
        }

        var now = DateTime.UtcNow;
        var existing = await _timelines.GetByProjectAsync(projectId, cancellationToken);
        await UpsertTimelineAsync(
            existing,
            projectId,
            ProjectPhaseType.PROPOSAL,
            proposalDeadline,
            currentUserId,
            now,
            cancellationToken);

        return ServiceResult<DateOnly>.Success(proposalDeadline);
    }

    public async Task<bool> HasProductionDeadlineAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var timeline = await _timelines.GetByProjectAndPhaseAsync(
            projectId,
            ProjectPhaseType.PRODUCTION,
            cancellationToken);

        return timeline is not null;
    }

    public async Task<DateOnly?> GetProductionDeadlineAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var timeline = await _timelines.GetByProjectAndPhaseAsync(
            projectId,
            ProjectPhaseType.PRODUCTION,
            cancellationToken);

        return timeline?.DueDate;
    }

    public async Task<ServiceResult<ProjectPhaseDeadlinePlanDto>> GetAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var requestError = ValidateIdentity<ProjectPhaseDeadlinePlanDto>(projectId, currentUserId);
        if (requestError is not null)
        {
            return requestError;
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProjectPhaseDeadlinePlanDto>.NotFound(ProjectServiceConstants.ProjectNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanViewAsync(project, currentUserId, roleName, cancellationToken))
        {
            return ServiceResult<ProjectPhaseDeadlinePlanDto>.Forbidden(ViewForbiddenMessage);
        }

        var timelines = await _timelines.GetByProjectAsync(projectId, cancellationToken);
        return ServiceResult<ProjectPhaseDeadlinePlanDto>.Success(
            ToPlanDto(project, timelines, DateOnly.FromDateTime(DateTime.UtcNow)),
            timelines.Count == 0 ? PhaseDeadlineNotPlannedMessage : "Project phase deadlines retrieved successfully.");
    }

    public async Task MarkStartedOnceAsync(
        Guid projectId,
        ProjectPhaseType phase,
        DateTime startedAt,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedPhases.Contains(phase))
        {
            return;
        }

        var timeline = await _timelines.GetByProjectAndPhaseAsync(projectId, phase, cancellationToken);
        if (timeline is null || timeline.StartedAt.HasValue)
        {
            return;
        }

        timeline.StartedAt = startedAt;
        timeline.UpdatedAt = startedAt;
        _timelines.Update(timeline);
    }

    public async Task MarkCompletedOnceAsync(
        Guid projectId,
        ProjectPhaseType phase,
        DateTime completedAt,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedPhases.Contains(phase))
        {
            return;
        }

        var timeline = await _timelines.GetByProjectAndPhaseAsync(projectId, phase, cancellationToken);
        if (timeline is null || timeline.CompletedAt.HasValue)
        {
            return;
        }

        if (!timeline.StartedAt.HasValue)
        {
            timeline.StartedAt = completedAt;
        }

        timeline.CompletedAt = completedAt;
        timeline.UpdatedAt = completedAt;
        _timelines.Update(timeline);
    }

    private static ServiceResult<T>? ValidateIdentity<T>(Guid projectId, Guid currentUserId)
    {
        if (projectId == Guid.Empty)
        {
            return ServiceResult<T>.BadRequest(ProjectServiceConstants.ProjectIdRequiredMessage);
        }

        return currentUserId == Guid.Empty
            ? ServiceResult<T>.Unauthorized(ProjectServiceConstants.AuthenticatedAccountIdRequiredMessage)
            : null;
    }

    private static ServiceResult<DateOnly>? ValidateProposalDeadline(
        DateOnly proposalDeadline,
        DateOnly? targetCompletionDate)
    {
        if (targetCompletionDate.HasValue && proposalDeadline > targetCompletionDate.Value)
        {
            return ServiceResult<DateOnly>.Failure(Error.BadRequest(
                ProjectPhaseDeadlineErrorCodes.ProposalDeadlineInvalid,
                "Proposal deadline must be on or before project target completion date."));
        }

        return null;
    }

    private async Task<ServiceResult<ProjectProductionPhaseDeadlineResponseDto>?> ValidateProductionDeadlineAsync(
        Guid projectId,
        DateOnly productionDeadline,
        DateOnly? targetCompletionDate,
        CancellationToken cancellationToken)
    {
        if (targetCompletionDate.HasValue && productionDeadline > targetCompletionDate.Value)
        {
            return ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Failure(Error.BadRequest(
                ProjectPhaseDeadlineErrorCodes.ProductionDeadlineInvalid,
                "Production deadline must be on or before project target completion date."));
        }

        var proposalTimeline = await _timelines.GetByProjectAndPhaseAsync(
            projectId,
            ProjectPhaseType.PROPOSAL,
            cancellationToken);
        if (proposalTimeline is not null && productionDeadline < proposalTimeline.DueDate)
        {
            return ServiceResult<ProjectProductionPhaseDeadlineResponseDto>.Failure(Error.BadRequest(
                ProjectPhaseDeadlineErrorCodes.ProductionDeadlineInvalid,
                "Production deadline must be on or after proposal deadline."));
        }

        return null;
    }

    private async Task UpsertTimelineAsync(
        List<ProjectPhaseTimeline> existing,
        Guid projectId,
        ProjectPhaseType phase,
        DateOnly dueDate,
        Guid currentUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var timeline = existing.FirstOrDefault(item => item.Phase == phase);
        if (timeline is null)
        {
            await _timelines.AddAsync(
                new ProjectPhaseTimeline
                {
                    ProjectPhaseTimelineId = Guid.NewGuid(),
                    ProjectId = projectId,
                    Phase = phase,
                    DueDate = dueDate,
                    CreatedBy = currentUserId,
                    UpdatedBy = currentUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                cancellationToken);
            return;
        }

        timeline.DueDate = dueDate;
        timeline.UpdatedBy = currentUserId;
        timeline.UpdatedAt = now;
        _timelines.Update(timeline);
    }

    private async Task<bool> CanViewAsync(
        ProjectDetailReadModel project,
        Guid currentUserId,
        string? roleName,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return project.CustomerId == currentUserId;
        }

        if (IsSales(roleName))
        {
            return project.AssignedSalesId == currentUserId;
        }

        if (IsDesigner(roleName))
        {
            return project.AssignedDesignerId == currentUserId;
        }

        return IsProduction(roleName) &&
            await _productionRequests.HasViewableAssignedRequestAsync(
                project.ProjectId,
                currentUserId,
                cancellationToken);
    }

    private static bool CanManage(Project project, Guid currentUserId, string? roleName)
    {
        return IsAdmin(roleName) ||
            (IsSales(roleName) && project.AssignedSalesId == currentUserId);
    }

    private static ProjectProductionPhaseDeadlineResponseDto ToProductionResponseDto(
        Guid projectId,
        Guid orderId,
        ProjectPhaseTimeline timeline)
    {
        return new ProjectProductionPhaseDeadlineResponseDto
        {
            ProjectId = projectId,
            OrderId = orderId,
            Phase = ProjectPhaseType.PRODUCTION,
            DueDate = timeline.DueDate,
            StartedAt = timeline.StartedAt,
            CompletedAt = timeline.CompletedAt,
            Status = ResolveProductionUpsertStatus(timeline)
        };
    }

    private static string ResolveProductionUpsertStatus(ProjectPhaseTimeline timeline)
    {
        if (timeline.CompletedAt.HasValue)
        {
            var completionDate = DateOnly.FromDateTime(timeline.CompletedAt.Value);
            return completionDate > timeline.DueDate ? CompletedLateStatus : CompletedOnTimeStatus;
        }

        if (timeline.StartedAt.HasValue)
        {
            return OnTrackStatus;
        }

        return NotStartedStatus;
    }

    private static ProjectPhaseDeadlinePlanDto ToPlanDto(
        Project project,
        List<ProjectPhaseTimeline> timelines,
        DateOnly today)
    {
        return ToPlanDto(
            new ProjectDetailReadModel
            {
                ProjectId = project.ProjectId,
                TargetCompletionDate = project.TargetCompletionDate
            },
            timelines,
            today);
    }

    private static ProjectPhaseDeadlinePlanDto ToPlanDto(
        ProjectDetailReadModel project,
        List<ProjectPhaseTimeline> timelines,
        DateOnly today)
    {
        var orderedTimelines = timelines
            .Where(timeline => SupportedPhases.Contains(timeline.Phase))
            .OrderBy(timeline => timeline.DueDate)
            .ThenBy(timeline => timeline.Phase)
            .ToList();
        var firstOpenTimelineId = orderedTimelines
            .FirstOrDefault(timeline => timeline.CompletedAt is null)
            ?.ProjectPhaseTimelineId;

        return new ProjectPhaseDeadlinePlanDto
        {
            ProjectId = project.ProjectId,
            TargetCompletionDate = project.TargetCompletionDate,
            Deadlines = orderedTimelines
                .Select(timeline => ToDeadlineDto(timeline, today, firstOpenTimelineId))
                .ToList()
        };
    }

    private static ProjectPhaseDeadlineItemDto ToDeadlineDto(
        ProjectPhaseTimeline timeline,
        DateOnly today,
        Guid? firstOpenTimelineId)
    {
        var dueDate = timeline.DueDate;
        var completionDate = timeline.CompletedAt.HasValue
            ? DateOnly.FromDateTime(timeline.CompletedAt.Value)
            : (DateOnly?)null;
        var overdueDays = CalculateOverdueDays(dueDate, completionDate ?? today);

        return new ProjectPhaseDeadlineItemDto
        {
            Phase = timeline.Phase,
            DueDate = dueDate,
            StartedAt = timeline.StartedAt,
            CompletedAt = timeline.CompletedAt,
            Status = ResolveStatus(timeline, today, completionDate, firstOpenTimelineId),
            OverdueDays = overdueDays
        };
    }

    private static int CalculateOverdueDays(DateOnly dueDate, DateOnly comparisonDate)
    {
        return Math.Max(0, comparisonDate.DayNumber - dueDate.DayNumber);
    }

    private static string ResolveStatus(
        ProjectPhaseTimeline timeline,
        DateOnly today,
        DateOnly? completionDate,
        Guid? firstOpenTimelineId)
    {
        if (completionDate.HasValue)
        {
            return completionDate.Value > timeline.DueDate ? CompletedLateStatus : CompletedOnTimeStatus;
        }

        if (today > timeline.DueDate)
        {
            return OverdueStatus;
        }

        return timeline.ProjectPhaseTimelineId == firstOpenTimelineId ? OnTrackStatus : PlannedStatus;
    }

    private static bool IsAdmin(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCustomer(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDesigner(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Designer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProduction(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Production, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSales(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Sales, StringComparison.OrdinalIgnoreCase);
    }
}
