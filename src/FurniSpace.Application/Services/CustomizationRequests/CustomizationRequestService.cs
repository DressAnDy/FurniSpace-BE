using FurniSpace.Application.Common;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.CustomizationRequests.CustomizationRequestServiceConstants;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.Interfaces.CustomizationRequests;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.CustomizationRequests;

public sealed class CustomizationRequestService : ICustomizationRequestService
{
    private readonly ICustomizationRequestRepository _customizationRequests;
    private readonly IProposalRepository _proposals;
    private readonly IProjectRepository _projects;
    private readonly IProductVersionRepository _productVersions;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public CustomizationRequestService(
        ICustomizationRequestRepository customizationRequests,
        IProposalRepository proposals,
        IProjectRepository projects,
        IProductVersionRepository productVersions,
        INotificationDispatcher dispatcher,
        IUnitOfWork unitOfWork)
    {
        _customizationRequests = customizationRequests;
        _proposals = proposals;
        _projects = projects;
        _productVersions = productVersions;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<CustomizationRequestListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CustomizationRequestQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestListResponseDto>.Unauthorized();
        }

        var project = await _proposals.GetProjectAccessAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFoundList(CustomizationRequestErrorCodes.ProjectNotFound, "Project not found.");
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        var accessError = await ValidateProjectAccessAsync(role, project, currentUserId, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var readQuery = query.Adapt<CustomizationRequestQueryReadModel>();
        readQuery.ProjectId = projectId;
        var items = await _customizationRequests.GetByProjectAsync(readQuery, cancellationToken);
        var filteredItems = FilterListByRole(items, role, currentUserId).ToList();
        var listDtos = await MapListItemsAsync(filteredItems, projectId, cancellationToken);

        return ServiceResult<CustomizationRequestListResponseDto>.Success(
            new CustomizationRequestListResponseDto
            {
                Items = listDtos
            },
            "Customization requests retrieved successfully.");
    }

    public async Task<ServiceResult<ProductionCustomizationRequestListResponseDto>> GetProductionQueueAsync(
        Guid currentUserId,
        ProductionCustomizationRequestQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProductionCustomizationRequestListResponseDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (ApplicationRoles.Production or ApplicationRoles.Admin))
        {
            return ServiceResult<ProductionCustomizationRequestListResponseDto>.Forbidden(
                "You do not have permission to view the production customization queue.");
        }

        var paginationError = ValidateProductionQueuePagination(query.Page, query.PageSize);
        if (paginationError is not null)
        {
            return ServiceResult<ProductionCustomizationRequestListResponseDto>.BadRequest(paginationError);
        }

        var statusFilterError = ResolveProductionQueueStatuses(role, query.Status, out var statuses);
        if (statusFilterError is not null)
        {
            return statusFilterError;
        }

        var readQuery = new ProductionCustomizationRequestQueueQueryReadModel
        {
            Statuses = statuses,
            ProjectId = query.ProjectId,
            ProposalId = query.ProposalId,
            MaterialAvailable = query.MaterialAvailable,
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            Page = query.Page,
            PageSize = query.PageSize
        };

        var items = await _customizationRequests.GetProductionQueueAsync(readQuery, cancellationToken);
        var total = await _customizationRequests.CountProductionQueueAsync(readQuery, cancellationToken);

        return ServiceResult<ProductionCustomizationRequestListResponseDto>.Success(
            new ProductionCustomizationRequestListResponseDto
            {
                Items = items.Select(ProductionCustomizationRequestQueueMapper.ToDto).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                Total = total
            },
            "Production customization requests retrieved successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestDetailDto>> GetDetailAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDetailDto>.Unauthorized();
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                "Customization request not found.");
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanAccessRequest(role, detail, currentUserId))
        {
            return ServiceResult<CustomizationRequestDetailDto>.Forbidden(
                "You do not have access to this customization request.");
        }

        return ServiceResult<CustomizationRequestDetailDto>.Success(
            await ToDetailDtoAsync(detail, cancellationToken),
            "Customization request detail retrieved successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestDetailDto>> SubmitAsync(
        Guid proposalItemId,
        Guid currentUserId,
        SubmitCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDetailDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (ApplicationRoles.Customer or ApplicationRoles.Designer or ApplicationRoles.Admin))
        {
            return ServiceResult<CustomizationRequestDetailDto>.Forbidden(
                "You do not have permission to submit customization requests.");
        }

        var validationError = ValidateSubmitRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var context = await _customizationRequests.GetSubmitContextAsync(proposalItemId, cancellationToken);
        if (context is null)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.ProposalItemNotFound,
                "Proposal item not found.");
        }

        var businessError = await ValidateSubmitBusinessRulesAsync(
            context,
            role,
            currentUserId,
            cancellationToken);
        if (businessError is not null)
        {
            return businessError;
        }

        var entity = CreateCustomizationRequest(context, request);
        await _customizationRequests.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await DispatchSubmittedNotificationAsync(entity, context, cancellationToken);

        var detail = await _customizationRequests.GetDetailAsync(
            entity.CustomizationRequestId,
            cancellationToken);

        return ServiceResult<CustomizationRequestDetailDto>.Created(
            detail is null
                ? entity.Adapt<CustomizationRequestDetailDto>()
                : await ToDetailDtoAsync(detail, cancellationToken),
            "Customization request submitted successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestDetailDto>> DesignerReviewAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        DesignerReviewCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDetailDto>.Unauthorized();
        }

        var context = await GetUpdateContextAsync(customizationRequestId, cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanDesignerReview(role, context.Detail!, currentUserId))
        {
            return ServiceResult<CustomizationRequestDetailDto>.Forbidden(
                "You do not have permission to review this customization request.");
        }

        if (!CanMoveToProductionReview(context.Entity!.Status))
        {
            return InvalidTransition("Customization request is not ready for designer review.");
        }

        if (string.IsNullOrWhiteSpace(request.DesignerSpecNote))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                "Designer spec note is required.");
        }

        context.Entity.DesignerId = role == ApplicationRoles.Admin
            ? context.Detail!.AssignedDesignerId ?? currentUserId
            : currentUserId;
        context.Entity.DesignerSpecNote = request.DesignerSpecNote.Trim();
        context.Entity.Status = CustomizationStatus.PRODUCTION_REVIEWING;
        context.Entity.UpdatedAt = DateTime.UtcNow;

        _customizationRequests.Update(context.Entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchDesignerReviewedNotificationAsync(context.Entity, context.Detail!, cancellationToken);

        return await ReloadUpdatedDetailAsync(
            customizationRequestId,
            "Customization request designer review submitted successfully.",
            cancellationToken);
    }

    public async Task<ServiceResult<CustomizationRequestDetailDto>> ProductionReviewAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        ProductionReviewCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDetailDto>.Unauthorized();
        }

        var context = await GetUpdateContextAsync(customizationRequestId, cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (ApplicationRoles.Production or ApplicationRoles.Admin))
        {
            return ServiceResult<CustomizationRequestDetailDto>.Forbidden(
                "You do not have permission to review production feasibility.");
        }

        if (context.Entity!.Status != CustomizationStatus.PRODUCTION_REVIEWING)
        {
            return InvalidTransition("Customization request is not ready for production review.");
        }

        var validationError = ValidateProductionReview(request);
        if (validationError is not null)
        {
            return validationError;
        }

        ApplyProductionReview(context.Entity, currentUserId, request);
        _customizationRequests.Update(context.Entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadUpdatedDetailAsync(
            customizationRequestId,
            "Customization request production review submitted successfully.",
            cancellationToken);
    }

    public async Task<ServiceResult<CustomizationRequestDetailDto>> CustomerDecisionAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CustomerDecisionCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDetailDto>.Unauthorized();
        }

        var context = await GetUpdateContextAsync(customizationRequestId, cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role != ApplicationRoles.Customer || context.Detail!.CustomerId != currentUserId)
        {
            return ServiceResult<CustomizationRequestDetailDto>.Forbidden(
                "You can only decide customization requests for your own project.");
        }

        var validationError = ValidateCustomerDecision(context.Entity!, request);
        if (validationError is not null)
        {
            return validationError;
        }

        if (IsDecision(request.Decision, AcceptDecision))
        {
            if (context.Entity!.Status == CustomizationStatus.ACCEPTED)
            {
                return await ReloadUpdatedDetailAsync(
                    customizationRequestId,
                    "Customization request customer decision submitted successfully.",
                    cancellationToken);
            }

            var proposalItem = await _proposals.GetItemEntityAsync(
                context.Entity.ProposalItemId,
                cancellationToken);
            if (proposalItem is null)
            {
                return NotFoundDetail(
                    CustomizationRequestErrorCodes.ProposalItemNotFound,
                    "Proposal item not found.");
            }

            var acceptError = await AcceptCustomizationAsync(
                context.Entity,
                proposalItem,
                cancellationToken);
            if (acceptError is not null)
            {
                return acceptError;
            }
        }
        else
        {
            ApplyRejectedCustomization(context.Entity!, request);
            _customizationRequests.Update(context.Entity!);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await ReloadUpdatedDetailAsync(
            customizationRequestId,
            "Customization request customer decision submitted successfully.",
            cancellationToken);
    }

    public async Task<ServiceResult<CustomizationRequestDetailDto>> CancelAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancelCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDetailDto>.Unauthorized();
        }

        var context = await GetUpdateContextAsync(customizationRequestId, cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanCancelRequest(role, context.Detail!, currentUserId))
        {
            return ServiceResult<CustomizationRequestDetailDto>.Forbidden(
                "You do not have permission to cancel this customization request.");
        }

        var validationError = ValidateCancellation(context.Entity!);
        if (validationError is not null)
        {
            return validationError;
        }

        ApplyCancellation(context.Entity!, request);
        _customizationRequests.Update(context.Entity!);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadUpdatedDetailAsync(
            customizationRequestId,
            "Customization request cancelled successfully.",
            cancellationToken);
    }

    private async Task<ServiceResult<CustomizationRequestListResponseDto>?> ValidateProjectAccessAsync(
        string? role,
        ProposalProjectAccessReadModel project,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (role == ApplicationRoles.Production)
        {
            var hasProductionRequest = await _customizationRequests.HasProductionVisibleRequestAsync(
                project.ProjectId,
                currentUserId,
                cancellationToken);
            return hasProductionRequest
                ? null
                : ServiceResult<CustomizationRequestListResponseDto>.Forbidden(
                    "You do not have access to this project's customization requests.");
        }

        return ProjectAssignmentAccessEvaluator.CanAccessProjectAssignment(role, project, currentUserId)
            ? null
            : ServiceResult<CustomizationRequestListResponseDto>.Forbidden(
                "You do not have access to this project's customization requests.");
    }

    private async Task<ServiceResult<CustomizationRequestDetailDto>?> ValidateSubmitBusinessRulesAsync(
        CustomizationSubmitContextReadModel context,
        string? role,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var roleError = ValidateSubmitRoleAccess(context, role, currentUserId);
        if (roleError is not null)
        {
            return roleError;
        }

        if (IsProposalAlreadySelected(context))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.ProposalAlreadySelected,
                "Proposal has already been selected.");
        }

        if (context.ProjectStatus != ProjectStatus.PROPOSAL_CONSULTING)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                "Customization request can only be submitted while the project is in proposal consulting.");
        }

        if (context.ProposalStatus != ProposalStatus.PUBLISHED)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                "Customization request can only be submitted for a published proposal.");
        }

        if (await _customizationRequests.HasActiveRequestForProposalItemAsync(
                context.ProposalItemId,
                cancellationToken))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationRequestAlreadyActive,
                "An active customization request already exists for this proposal item.");
        }

        var hasQuotation = await _customizationRequests.HasQuotationForProposalAsync(
            context.ProposalId,
            cancellationToken);
        return hasQuotation
            ? BadRequestDetail(
                CustomizationRequestErrorCodes.QuotationAlreadyCreated,
                "Quotation has already been created for this proposal.")
            : null;
    }

    private static ServiceResult<CustomizationRequestDetailDto>? ValidateSubmitRoleAccess(
        CustomizationSubmitContextReadModel context,
        string? role,
        Guid currentUserId)
    {
        if (role == ApplicationRoles.Customer)
        {
            return context.CustomerId != currentUserId
                ? ServiceResult<CustomizationRequestDetailDto>.Forbidden(
                    "You can only submit customization requests for your own project.")
                : null;
        }

        if (role == ApplicationRoles.Designer)
        {
            return context.AssignedDesignerId != currentUserId
                ? ServiceResult<CustomizationRequestDetailDto>.Failure(
                    Error.Forbidden(
                        CustomizationRequestErrorCodes.DesignerNotAssignedToProject,
                        "Designer is not assigned to this project."))
                : null;
        }

        if (role == ApplicationRoles.Admin)
        {
            return null;
        }

        return ServiceResult<CustomizationRequestDetailDto>.Forbidden(
            "You do not have permission to submit customization requests.");
    }

    private static ServiceResult<CustomizationRequestDetailDto>? ValidateSubmitRequest(
        SubmitCustomizationRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestTitle))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                "Request title is required.");
        }

        var hasRequestedChange = !string.IsNullOrWhiteSpace(request.RequestDescription)
            || !string.IsNullOrWhiteSpace(request.RequestedMaterial)
            || !string.IsNullOrWhiteSpace(request.RequestedColor)
            || !string.IsNullOrWhiteSpace(request.RequestedChangeNote)
            || request.RequestedWidth.HasValue
            || request.RequestedHeight.HasValue
            || request.RequestedDepth.HasValue;

        return hasRequestedChange
            ? null
            : BadRequestDetail(
                CustomizationRequestErrorCodes.InvalidCustomizationRequest,
            "At least one requested customization field is required.");
    }

    private async Task<CustomizationUpdateContext> GetUpdateContextAsync(
        Guid customizationRequestId,
        CancellationToken cancellationToken)
    {
        var entity = await _customizationRequests.GetByIdAsync(customizationRequestId, cancellationToken);
        if (entity is null)
        {
            return CustomizationUpdateContext.NotFound();
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        return detail is null
            ? CustomizationUpdateContext.NotFound()
            : new CustomizationUpdateContext(entity, detail, null);
    }

    private static bool CanDesignerReview(
        string? role,
        CustomizationRequestReadModel request,
        Guid currentUserId)
    {
        return role switch
        {
            ApplicationRoles.Admin => true,
            ApplicationRoles.Designer => request.AssignedDesignerId == currentUserId,
            _ => false
        };
    }

    private static bool CanMoveToProductionReview(CustomizationStatus? status)
    {
        return status is CustomizationStatus.SUBMITTED or CustomizationStatus.DESIGN_REVIEWING;
    }

    private static ServiceResult<CustomizationRequestDetailDto>? ValidateProductionReview(
        ProductionReviewCustomizationRequestDto request)
    {
        var result = request.Result?.Trim() ?? string.Empty;
        if (IsResult(result, NotFeasibleResult))
        {
            return request.MaterialAvailable == true
                ? BadRequestDetail(
                    CustomizationRequestErrorCodes.MaterialNotAvailable,
                    "Material must not be available when customization is not feasible.")
                : null;
        }

        if (!IsResult(result, FeasibleResult))
        {
            return InvalidTransition("Unsupported production review result.");
        }

        if (request.MaterialAvailable != true)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.MaterialNotAvailable,
                "Material must be available for feasible customization.");
        }

        if (!request.EstimatedProductionDays.HasValue || !request.EstimatedAdditionalCost.HasValue)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationCostRequired,
                "Estimated production days and additional cost are required.");
        }

        return request.EstimatedAdditionalCost > 0 &&
            string.IsNullOrWhiteSpace(request.AdditionalCostReason)
                ? BadRequestDetail(
                    CustomizationRequestErrorCodes.AdditionalCostReasonRequired,
                    "Additional cost reason is required when additional cost is greater than zero.")
                : null;
    }

    private static void ApplyProductionReview(
        CustomizationRequest entity,
        Guid currentUserId,
        ProductionReviewCustomizationRequestDto request)
    {
        entity.ProductionReviewBy = currentUserId;
        entity.MaterialAvailable = request.MaterialAvailable;
        entity.EstimatedProductionDays = request.EstimatedProductionDays;
        entity.EstimatedAdditionalCost = request.EstimatedAdditionalCost;
        entity.AdditionalCostReason = request.AdditionalCostReason;
        entity.FeasibilityNote = request.FeasibilityNote;
        entity.ProductionRiskNote = request.ProductionRiskNote;
        entity.Status = IsResult(request.Result, FeasibleResult)
            ? CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL
            : CustomizationStatus.NOT_FEASIBLE;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    private static ServiceResult<CustomizationRequestDetailDto>? ValidateCustomerDecision(
        CustomizationRequest entity,
        CustomerDecisionCustomizationRequestDto request)
    {
        if (entity.Status == CustomizationStatus.NOT_FEASIBLE && IsDecision(request.Decision, AcceptDecision))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationNotFeasible,
                "Not feasible customization requests cannot be accepted.");
        }

        if (entity.Status != CustomizationStatus.WAITING_FOR_CUSTOMER_FINAL_APPROVAL)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationNotReadyForFinalApproval,
                "Customization request is not ready for final approval.");
        }

        if (IsDecision(request.Decision, AcceptDecision))
        {
            return entity.EstimatedAdditionalCost.HasValue
                ? null
                : BadRequestDetail(
                    CustomizationRequestErrorCodes.CustomizationCostNotApproved,
                    "Customization cost has not been approved by Production.");
        }

        if (IsDecision(request.Decision, RejectDecision))
        {
            return string.IsNullOrWhiteSpace(request.RejectReason)
                ? BadRequestDetail(
                    CustomizationRequestErrorCodes.InvalidCustomizationDecision,
                    "Reject reason is required.")
                : null;
        }

        return BadRequestDetail(
            CustomizationRequestErrorCodes.InvalidCustomizationDecision,
            "Decision must be ACCEPT or REJECT.");
    }

    private static ServiceResult<CustomizationRequestDetailDto>? ValidateCancellation(
        CustomizationRequest entity)
    {
        if (entity.Status == CustomizationStatus.ACCEPTED)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationAlreadyAccepted,
                "Accepted customization requests cannot be cancelled.");
        }

        return entity.Status == CustomizationStatus.CANCELLED
            ? InvalidTransition("Customization request has already been cancelled.")
            : null;
    }

    private static void ApplyCancellation(
        CustomizationRequest request,
        CancelCustomizationRequestDto cancellation)
    {
        request.Status = CustomizationStatus.CANCELLED;
        request.ProductionRiskNote = string.IsNullOrWhiteSpace(cancellation.CancelReason)
            ? request.ProductionRiskNote
            : cancellation.CancelReason.Trim();
        request.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyRejectedCustomization(
        CustomizationRequest request,
        CustomerDecisionCustomizationRequestDto decision)
    {
        request.Status = CustomizationStatus.REJECTED_BY_CUSTOMER;
        request.CustomerRejectedAt = DateTime.UtcNow;
        request.ProductionRiskNote = string.IsNullOrWhiteSpace(decision.RejectReason)
            ? request.ProductionRiskNote
            : decision.RejectReason.Trim();
        request.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<ServiceResult<CustomizationRequestDetailDto>?> AcceptCustomizationAsync(
        CustomizationRequest request,
        ProposalItem proposalItem,
        CancellationToken cancellationToken)
    {
        if (!proposalItem.ProductVersionId.HasValue)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.OriginalProductVersionNotFound,
                "Original product version not found for proposal item.");
        }

        var originalVersion = await _productVersions.GetByIdAsync(
            proposalItem.ProductVersionId.Value,
            cancellationToken);
        if (originalVersion is null)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.OriginalProductVersionNotFound,
                "Original product version not found.");
        }

        var project = await _projects.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null || string.IsNullOrWhiteSpace(project.ProjectCode))
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.ProjectNotFound,
                "Project not found.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var sequence = await _productVersions.CountProjectSpecificByProjectAsync(
                request.ProjectId,
                cancellationToken) + 1;
            var approvedVersion = CustomizationAcceptedProductVersionFactory.Create(
                request,
                originalVersion,
                proposalItem,
                project.ProjectCode,
                sequence);

            await _productVersions.AddAsync(approvedVersion, cancellationToken);
            CustomizationAcceptedProductVersionFactory.ApplyAcceptedChanges(
                request,
                proposalItem,
                approvedVersion);
            _customizationRequests.Update(request);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return ServiceResult<CustomizationRequestDetailDto>.Failure(
                Error.InternalServerError(
                    CustomizationRequestErrorCodes.ProductVersionCreationFailed,
                    "Failed to create approved product version."));
        }

        return null;
    }

    private async Task<ServiceResult<CustomizationRequestDetailDto>> ReloadUpdatedDetailAsync(
        Guid customizationRequestId,
        string message,
        CancellationToken cancellationToken)
    {
        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        return detail is null
            ? NotFoundDetail(
                CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                "Customization request not found.")
            : ServiceResult<CustomizationRequestDetailDto>.Success(
                await ToDetailDtoAsync(detail, cancellationToken),
                message);
    }

    private static CustomizationRequest CreateCustomizationRequest(
        CustomizationSubmitContextReadModel context,
        SubmitCustomizationRequestDto request)
    {
        var now = DateTime.UtcNow;
        return new CustomizationRequest
        {
            CustomizationRequestId = Guid.NewGuid(),
            ProjectId = context.ProjectId,
            ProposalId = context.ProposalId,
            ProposalItemId = context.ProposalItemId,
            RequestedByCustomerId = context.CustomerId,
            RequestTitle = request.RequestTitle.Trim(),
            RequestDescription = request.RequestDescription,
            RequestedWidth = request.RequestedWidth,
            RequestedHeight = request.RequestedHeight,
            RequestedDepth = request.RequestedDepth,
            RequestedMaterial = request.RequestedMaterial,
            RequestedColor = request.RequestedColor,
            RequestedChangeNote = request.RequestedChangeNote,
            Status = CustomizationStatus.SUBMITTED,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private async Task DispatchSubmittedNotificationAsync(
        CustomizationRequest request,
        CustomizationSubmitContextReadModel context,
        CancellationToken cancellationToken)
    {
        var receivers = BuildSubmitReceivers(
            context.CustomerId,
            context.AssignedSalesId,
            context.AssignedDesignerId);
        if (receivers.Count == 0)
        {
            return;
        }

        await _dispatcher.DispatchAsync(
            NotificationType.CustomizationRequestSubmitted,
            new Dictionary<string, string>
            {
                ["RequestTitle"] = request.RequestTitle,
                ["ProjectName"] = context.ProjectName,
                ["ProposalName"] = context.ProposalName
            },
            receivers,
            context.ProjectId,
            CustomizationReferenceType,
            request.CustomizationRequestId,
            cancellationToken);
    }

    private async Task DispatchDesignerReviewedNotificationAsync(
        CustomizationRequest request,
        CustomizationRequestReadModel context,
        CancellationToken cancellationToken)
    {
        var receivers = await _projects.GetActiveAccountIdsByRoleNamesAsync(
            [ApplicationRoles.Production],
            cancellationToken);
        if (receivers.Count == 0)
        {
            return;
        }

        await _dispatcher.DispatchAsync(
            NotificationType.CustomizationDesignerReviewed,
            new Dictionary<string, string>
            {
                ["RequestTitle"] = request.RequestTitle,
                ["ProjectName"] = context.ProjectName
            },
            receivers,
            context.ProjectId,
            CustomizationReferenceType,
            request.CustomizationRequestId,
            cancellationToken);
    }

    private static bool CanAccessRequest(
        string? role,
        CustomizationRequestReadModel request,
        Guid currentUserId)
    {
        if (role == ApplicationRoles.Production)
        {
            return IsProductionVisible(request, currentUserId);
        }

        return role switch
        {
            ApplicationRoles.Admin => true,
            ApplicationRoles.Customer => request.CustomerId == currentUserId,
            ApplicationRoles.Sales => request.AssignedSalesId == currentUserId,
            ApplicationRoles.Designer => request.AssignedDesignerId == currentUserId,
            _ => false
        };
    }

    private static bool CanCancelRequest(
        string? role,
        CustomizationRequestReadModel request,
        Guid currentUserId)
    {
        return role is ApplicationRoles.Customer or ApplicationRoles.Sales or ApplicationRoles.Designer or ApplicationRoles.Admin &&
            CanAccessRequest(role, request, currentUserId);
    }

    private static IEnumerable<CustomizationRequestReadModel> FilterListByRole(
        IEnumerable<CustomizationRequestReadModel> items,
        string? role,
        Guid currentUserId)
    {
        return role == ApplicationRoles.Production
            ? items.Where(item => IsProductionVisible(item, currentUserId))
            : items;
    }

    private static bool IsProductionVisible(
        CustomizationRequestReadModel request,
        Guid currentUserId)
    {
        return request.ProductionReviewBy == currentUserId ||
            request.Status.HasValue && ProductionVisibleStatuses.Contains(request.Status.Value);
    }

    private static bool IsProposalAlreadySelected(CustomizationSubmitContextReadModel context)
    {
        return context.ProposalStatus == ProposalStatus.SELECTED ||
            context.ProjectStatus.HasValue &&
            ProjectStatusesAfterProposalSelection.Contains(context.ProjectStatus.Value);
    }

    private static bool IsResult(string? value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDecision(string? value, string expected)
    {
        return string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static List<Guid> BuildSubmitReceivers(
        Guid customerId,
        Guid? salesId,
        Guid? designerId)
    {
        var receivers = new List<Guid> { customerId };
        if (salesId.HasValue && salesId.Value != customerId)
        {
            receivers.Add(salesId.Value);
        }

        if (designerId.HasValue &&
            designerId.Value != customerId &&
            designerId.Value != salesId)
        {
            receivers.Add(designerId.Value);
        }

        return receivers;
    }

    private static ServiceResult<ProductionCustomizationRequestListResponseDto>? ResolveProductionQueueStatuses(
        string? role,
        string? status,
        out IReadOnlyList<CustomizationStatus>? statuses)
    {
        statuses = null;
        var normalizedStatus = status?.Trim();

        if (role == ApplicationRoles.Production)
        {
            if (string.IsNullOrWhiteSpace(normalizedStatus))
            {
                statuses = [CustomizationStatus.PRODUCTION_REVIEWING];
                return null;
            }

            if (!TryParseCustomizationStatus(normalizedStatus, out var parsedStatus) ||
                !ProductionVisibleStatuses.Contains(parsedStatus))
            {
                return ServiceResult<ProductionCustomizationRequestListResponseDto>.BadRequest(
                    "Status filter is not allowed for production queue.");
            }

            statuses = [parsedStatus];
            return null;
        }

        if (string.IsNullOrWhiteSpace(normalizedStatus) ||
            string.Equals(normalizedStatus, AllStatusesFilter, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!TryParseCustomizationStatus(normalizedStatus, out var adminStatus))
        {
            return ServiceResult<ProductionCustomizationRequestListResponseDto>.BadRequest(
                "Status filter is invalid.");
        }

        statuses = [adminStatus];
        return null;
    }

    private static bool TryParseCustomizationStatus(string value, out CustomizationStatus status)
    {
        return Enum.TryParse(value, true, out status);
    }

    private static string? ValidateProductionQueuePagination(int page, int pageSize)
    {
        if (page <= 0)
        {
            return "Page must be greater than zero.";
        }

        return pageSize is < 1 or > MaxProductionQueuePageSize
            ? $"Page size must be between 1 and {MaxProductionQueuePageSize}."
            : null;
    }

    private async Task<IReadOnlyList<CustomizationRequestDto>> MapListItemsAsync(
        List<CustomizationRequestReadModel> items,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var approvedVersionIds = items
            .Where(item => item.ApprovedProductVersionId.HasValue)
            .Select(item => item.ApprovedProductVersionId!.Value)
            .Distinct()
            .ToList();

        var approvedVersions = approvedVersionIds.Count == 0
            ? []
            : await _productVersions.GetValidDetailsAsync(approvedVersionIds, projectId, cancellationToken);

        var approvedVersionLookup = approvedVersions.ToDictionary(version => version.ProductVersionId);

        return items
            .Select(item =>
            {
                var dto = item.Adapt<CustomizationRequestDto>();
                if (item.ApprovedProductVersionId.HasValue &&
                    approvedVersionLookup.TryGetValue(item.ApprovedProductVersionId.Value, out var version))
                {
                    dto.ApprovedProductVersion = ApprovedProductVersionSummaryMapper.ToDto(version, projectId);
                }

                return dto;
            })
            .ToList();
    }

    private async Task<CustomizationRequestDetailDto> ToDetailDtoAsync(
        CustomizationRequestDetailReadModel detail,
        CancellationToken cancellationToken)
    {
        var dto = detail.Adapt<CustomizationRequestDetailDto>();
        dto.ProposalItem = CustomizationRequestItemSnapshotMapper.ToDto(detail.ProposalItem);

        if (!detail.ApprovedProductVersionId.HasValue)
        {
            return dto;
        }

        var approvedVersion = await _productVersions.GetByIdAsync(
            detail.ApprovedProductVersionId.Value,
            cancellationToken);
        if (approvedVersion is not null)
        {
            dto.ApprovedProductVersion = ApprovedProductVersionSummaryMapper.ToDto(approvedVersion);
        }

        return dto;
    }

    private static ServiceResult<CustomizationRequestListResponseDto> NotFoundList(
        string code,
        string message)
    {
        return ServiceResult<CustomizationRequestListResponseDto>.Failure(
            Error.NotFound(code, message));
    }

    private static ServiceResult<CustomizationRequestDetailDto> NotFoundDetail(
        string code,
        string message)
    {
        return ServiceResult<CustomizationRequestDetailDto>.Failure(
            Error.NotFound(code, message));
    }

    private static ServiceResult<CustomizationRequestDetailDto> BadRequestDetail(
        string code,
        string message)
    {
        return ServiceResult<CustomizationRequestDetailDto>.Failure(
            Error.BadRequest(code, message));
    }

    private static ServiceResult<CustomizationRequestDetailDto> InvalidTransition(string message)
    {
        return BadRequestDetail(CustomizationRequestErrorCodes.InvalidCustomizationTransition, message);
    }

    private sealed record CustomizationUpdateContext(
        CustomizationRequest? Entity,
        CustomizationRequestDetailReadModel? Detail,
        ServiceResult<CustomizationRequestDetailDto>? Error)
    {
        public static CustomizationUpdateContext NotFound()
        {
            return new CustomizationUpdateContext(
                null,
                null,
                NotFoundDetail(
                    CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                    "Customization request not found."));
        }
    }
}
