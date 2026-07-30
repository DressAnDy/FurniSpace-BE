using FurniSpace.Application.Common;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Constants.Common;
using static FurniSpace.Application.Constants.CustomizationRequests.CustomizationRequestServiceConstants;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.CustomizationRequests;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.CustomizationRequests;

public sealed class CustomizationRequestService : ICustomizationRequestService
{
    private readonly ICustomizationRequestRepository _customizationRequests;
    private readonly IProposalRepository _proposals;
    private readonly IProjectRepository _projects;
    private readonly IProductVersionRepository _productVersions;
    private readonly IProjectFileRepository _projectFiles;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public CustomizationRequestService(
        ICustomizationRequestRepository customizationRequests,
        IProposalRepository proposals,
        IProjectRepository projects,
        IProductVersionRepository productVersions,
        IProjectFileRepository projectFiles,
        INotificationDispatcher dispatcher,
        IUnitOfWork unitOfWork)
    {
        _customizationRequests = customizationRequests;
        _proposals = proposals;
        _projects = projects;
        _productVersions = productVersions;
        _projectFiles = projectFiles;
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
                CustomizationRequestNotFoundMessage);
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

        if (string.IsNullOrWhiteSpace(request.DesignerSpecNote))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                "Designer spec note is required.");
        }

        context.Entity!.DesignerId = role == ApplicationRoles.Admin
            ? context.Detail!.AssignedDesignerId ?? currentUserId
            : currentUserId;
        context.Entity.DesignerSpecNote = request.DesignerSpecNote.Trim();
        context.Entity.UpdatedAt = DateTime.UtcNow;

        if (context.Entity.Status == CustomizationStatus.SUBMITTED)
        {
            context.Entity.Status = CustomizationStatus.DESIGN_REVIEWING;
            _customizationRequests.Update(context.Entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await ReloadUpdatedDetailAsync(
                customizationRequestId,
                "Customization request moved to design reviewing.",
                cancellationToken);
        }

        if (context.Entity.Status != CustomizationStatus.DESIGN_REVIEWING)
        {
            return InvalidTransition("Customization request is not ready for designer review.");
        }

        var linkedVersionError = await ValidateLinkedProductVersionForProductionAsync(
            context.Entity,
            cancellationToken);
        if (linkedVersionError is not null)
        {
            return linkedVersionError;
        }

        context.Entity.Status = CustomizationStatus.PRODUCTION_REVIEWING;
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

        if (IsDecision(request.Decision, AcceptDecision) &&
            context.Entity!.Status == CustomizationStatus.ACCEPTED)
        {
            return await ReloadUpdatedDetailAsync(
                customizationRequestId,
                "Customization request customer decision submitted successfully.",
                cancellationToken);
        }

        var validationError = ValidateCustomerDecision(context.Entity!, request);
        if (validationError is not null)
        {
            return validationError;
        }

        if (IsDecision(request.Decision, AcceptDecision))
        {
            var acceptError = await AcceptCustomizationAsync(context.Entity!, cancellationToken);
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

    public async Task<ServiceResult<CreateCustomizationProductVersionResponseDto>> CreateCustomizationProductVersionAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CreateCustomizationProductVersionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CreateCustomizationProductVersionResponseDto>.Unauthorized();
        }

        var contextResult = await ResolveCreateProductVersionContextAsync(
            customizationRequestId,
            currentUserId,
            cancellationToken);
        if (contextResult.Error is not null)
        {
            return contextResult.Error;
        }

        var entity = contextResult.Entity!;
        var existingResponse = await TryGetExistingLinkedVersionAsync(
            entity,
            cancellationToken);
        if (existingResponse is not null)
        {
            return existingResponse;
        }

        var validationError = ValidateCreateProductVersionRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var sourceContextResult = await ResolveSourceProductContextAsync(entity, cancellationToken);
        if (sourceContextResult.Error is not null)
        {
            return sourceContextResult.Error;
        }

        var sourceContext = sourceContextResult.Context!;
        var versionCodeError = await ValidateVersionCodeAvailabilityAsync(
            request.VersionCode,
            cancellationToken);
        if (versionCodeError is not null)
        {
            return versionCodeError;
        }

        var fileValidationError = await ValidateProductVersionFilesAsync(request, cancellationToken);
        if (fileValidationError is not null)
        {
            return fileValidationError;
        }

        var versionName = request.VersionName!.Trim();
        var versionCode = string.IsNullOrWhiteSpace(request.VersionCode)
            ? null
            : request.VersionCode.Trim();
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var sequence = await _productVersions.CountProjectSpecificByProjectAsync(
                entity.ProjectId,
                cancellationToken) + 1;
            var productVersion = CustomizationAcceptedProductVersionFactory.CreateFromDesignerRequest(
                request,
                entity,
                sourceContext.SourceVersion,
                sourceContext.ProjectCode,
                sequence,
                versionName,
                versionCode);

            await _productVersions.AddAsync(productVersion, cancellationToken);
            await AddProductVersionFileLinksAsync(
                productVersion.ProductVersionId,
                currentUserId,
                request,
                cancellationToken);
            CustomizationAcceptedProductVersionFactory.LinkToCustomizationRequest(entity, productVersion);
            _customizationRequests.Update(entity);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ServiceResult<CreateCustomizationProductVersionResponseDto>.Created(
                CustomizationAcceptedProductVersionFactory.ToCreateResponse(entity, productVersion),
                "Customization product version created successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return ProductVersionFailure(
                Error.InternalServerError(
                    CustomizationRequestErrorCodes.CustomProductVersionCreationFailed,
                    "Failed to create customization product version."));
        }
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

        if (!context.ProductVersionId.HasValue || context.ProductVersionId == Guid.Empty)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.ProposalItemProductVersionRequired,
                "Proposal item must reference a product version before submitting a customization request.");
        }

        if (await _customizationRequests.HasActiveRequestForProductVersionAsync(
                context.ProjectId,
                context.ProposalId,
                context.ProductVersionId.Value,
                cancellationToken))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.ActiveCustomizationRequestAlreadyExists,
                "An active customization request already exists for this product version.");
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
        CancellationToken cancellationToken)
    {
        if (!request.ApprovedProductVersionId.HasValue)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationProductVersionRequired,
                "Customization request must have a linked product version before acceptance.");
        }

        var approvedVersion = await _productVersions.GetByIdAsync(
            request.ApprovedProductVersionId.Value,
            cancellationToken);
        if (approvedVersion is null)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.InvalidApprovedProductVersionData,
                "Linked product version was not found.");
        }

        CustomizationAcceptedProductVersionFactory.MarkAccepted(request);
        _customizationRequests.Update(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<ServiceResult<CustomizationRequestDetailDto>?> ValidateLinkedProductVersionForProductionAsync(
        CustomizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.ApprovedProductVersionId.HasValue)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationProductVersionRequired,
                "A project-specific product version must be linked before production review.");
        }

        var linkedVersion = await _productVersions.GetByIdAsync(
            request.ApprovedProductVersionId.Value,
            cancellationToken);
        if (linkedVersion is null)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.ApprovedProductVersionNotFound,
                "Approved product version was not found.");
        }

        var sourceVersion = await _productVersions.GetByIdAsync(
            request.ProductVersionId,
            cancellationToken);
        if (sourceVersion is null)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.SourceProductVersionNotFound,
                "Source product version not found.");
        }

        if (linkedVersion.VersionType != ProductVersionType.PROJECT_SPECIFIC)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.ApprovedProductVersionInvalidType,
                "Approved product version must be PROJECT_SPECIFIC.");
        }

        if (linkedVersion.Status != ProductStatus.ACTIVE)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.ApprovedProductVersionNotActive,
                "Approved product version must be ACTIVE.");
        }

        if (linkedVersion.ProjectId != request.ProjectId)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.ApprovedProductVersionProjectMismatch,
                "Approved product version must belong to the same project.");
        }

        if (linkedVersion.ProductId != sourceVersion.ProductId)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.ApprovedProductVersionProductMismatch,
                "Approved product version must belong to the same product as the source version.");
        }

        return null;
    }

    private async Task<CreateProductVersionContextResult> ResolveCreateProductVersionContextAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var entity = await _customizationRequests.GetByIdAsync(customizationRequestId, cancellationToken);
        if (entity is null)
        {
            return CreateProductVersionContextResult.Failure(
                ProductVersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                        CustomizationRequestNotFoundMessage)));
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return CreateProductVersionContextResult.Failure(
                ProductVersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                        CustomizationRequestNotFoundMessage)));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanDesignerReview(role, detail, currentUserId))
        {
            return CreateProductVersionContextResult.Failure(
                ProductVersionFailure(
                    Error.Forbidden(
                        CustomizationRequestErrorCodes.ProjectAccessDenied,
                        "You do not have permission to create a product version for this project.")));
        }

        if (entity.Status != CustomizationStatus.DESIGN_REVIEWING)
        {
            return CreateProductVersionContextResult.Failure(
                ProductVersionFailure(
                    Error.Conflict(
                        CustomizationRequestErrorCodes.CustomizationNotInDesignReview,
                        "Product version can only be created while the request is DESIGN_REVIEWING.")));
        }

        if (entity.ProductVersionId == Guid.Empty)
        {
            return CreateProductVersionContextResult.Failure(
                ProductVersionFailure(
                    Error.BadRequest(
                        CustomizationRequestErrorCodes.SourceProductVersionRequired,
                        "Customization request must reference a source product version.")));
        }

        return CreateProductVersionContextResult.Success(entity);
    }

    private async Task<SourceProductContextResult> ResolveSourceProductContextAsync(
        CustomizationRequest entity,
        CancellationToken cancellationToken)
    {
        var sourceVersion = await _productVersions.GetByIdAsync(
            entity.ProductVersionId,
            cancellationToken);
        if (sourceVersion is null)
        {
            return SourceProductContextResult.Failure(
                ProductVersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.SourceProductVersionNotFound,
                        "Source product version not found.")));
        }

        if (!await _productVersions.ProductExistsAsync(sourceVersion.ProductId, cancellationToken))
        {
            return SourceProductContextResult.Failure(
                ProductVersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.SourceProductNotFound,
                        "Source product not found.")));
        }

        var project = await _projects.GetByIdAsync(entity.ProjectId, cancellationToken);
        if (project is null || string.IsNullOrWhiteSpace(project.ProjectCode))
        {
            return SourceProductContextResult.Failure(
                ProductVersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.ProjectNotFound,
                        "Project not found.")));
        }

        return SourceProductContextResult.Success(
            new SourceProductContext(sourceVersion, project.ProjectCode));
    }

    private async Task<ServiceResult<CreateCustomizationProductVersionResponseDto>?> ValidateVersionCodeAvailabilityAsync(
        string? requestedVersionCode,
        CancellationToken cancellationToken)
    {
        var versionCode = string.IsNullOrWhiteSpace(requestedVersionCode)
            ? null
            : requestedVersionCode.Trim();
        if (string.IsNullOrWhiteSpace(versionCode))
        {
            return null;
        }

        return await _productVersions.VersionCodeExistsAsync(versionCode, cancellationToken)
            ? ProductVersionFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.VersionCodeAlreadyExists,
                    "Version code already exists."))
            : null;
    }

    private async Task<ServiceResult<CreateCustomizationProductVersionResponseDto>?> TryGetExistingLinkedVersionAsync(
        CustomizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.ApprovedProductVersionId.HasValue)
        {
            return null;
        }

        var existingVersion = await _productVersions.GetByIdAsync(
            request.ApprovedProductVersionId.Value,
            cancellationToken);
        if (existingVersion is null)
        {
            return ProductVersionFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.CustomizationProductVersionLinkConflict,
                    "Linked product version reference is missing."));
        }

        var sourceVersion = await _productVersions.GetByIdAsync(
            request.ProductVersionId,
            cancellationToken);
        if (sourceVersion is null ||
            existingVersion.ProjectId != request.ProjectId ||
            existingVersion.ProductId != sourceVersion.ProductId ||
            existingVersion.VersionType != ProductVersionType.PROJECT_SPECIFIC)
        {
            return ProductVersionFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.CustomizationProductVersionLinkConflict,
                    "Linked product version is inconsistent with the customization request."));
        }

        return ServiceResult<CreateCustomizationProductVersionResponseDto>.Success(
            CustomizationAcceptedProductVersionFactory.ToCreateResponse(request, existingVersion),
            "Customization product version already exists.");
    }

    private static ServiceResult<CreateCustomizationProductVersionResponseDto>? ValidateCreateProductVersionRequest(
        CreateCustomizationProductVersionRequestDto request)
    {
        var versionNameError = CustomizationAcceptedProductVersionFactory.ValidateVersionName(request.VersionName);
        if (versionNameError is not null)
        {
            return ProductVersionFailure(
                Error.BadRequest(CustomizationRequestErrorCodes.VersionNameRequired, versionNameError));
        }

        var versionCodeError = CustomizationAcceptedProductVersionFactory.ValidateVersionCode(request.VersionCode);
        if (versionCodeError is not null)
        {
            return ProductVersionFailure(
                Error.BadRequest(CustomizationRequestErrorCodes.InvalidCustomizationRequest, versionCodeError));
        }

        if (!CustomizationAcceptedProductVersionFactory.IsValidDimensionUnit(request.DimensionUnit))
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidDimensionUnit,
                    "Dimension unit must be cm, m, or mm."));
        }

        if (HasInvalidDimension(request.Width) ||
            HasInvalidDimension(request.Height) ||
            HasInvalidDimension(request.Depth))
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidProductDimensions,
                    "Product dimensions must be greater than zero."));
        }

        if (request.EstimatedPrice.HasValue && request.EstimatedPrice.Value < 0m)
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidEstimatedPrice,
                    "Estimated price must be greater than or equal to zero."));
        }

        var previewFileIds = request.PreviewFileIds ?? [];
        if (previewFileIds.Count > MaxProductVersionPreviewFileCount)
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                    $"At most {MaxProductVersionPreviewFileCount} preview files are allowed."));
        }

        if (previewFileIds.Count != previewFileIds.Distinct().Count())
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                    "Preview file ids must be unique."));
        }

        return null;
    }

    private static bool HasInvalidDimension(decimal? value)
    {
        return value.HasValue && value.Value <= 0m;
    }

    private async Task<ServiceResult<CreateCustomizationProductVersionResponseDto>?> ValidateProductVersionFilesAsync(
        CreateCustomizationProductVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ModelFileId.HasValue)
        {
            var modelError = await ValidateModelFileAsync(request.ModelFileId.Value, cancellationToken);
            if (modelError is not null)
            {
                return modelError;
            }
        }

        var previewFileIds = request.PreviewFileIds ?? [];
        foreach (var previewFileId in previewFileIds)
        {
            var previewError = await ValidatePreviewFileAsync(previewFileId, cancellationToken);
            if (previewError is not null)
            {
                return previewError;
            }
        }

        return null;
    }

    private async Task<ServiceResult<CreateCustomizationProductVersionResponseDto>?> ValidateModelFileAsync(
        Guid modelFileId,
        CancellationToken cancellationToken)
    {
        var metadata = await _projectFiles.GetFileMetadataAsync(modelFileId, cancellationToken);
        if (metadata is null)
        {
            return ProductVersionFailure(
                Error.NotFound(
                    CustomizationRequestErrorCodes.ModelFileNotFound,
                    "Model file not found."));
        }

        if (metadata.Status != FileStatus.ACTIVE)
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.ModelFileNotActive,
                    "Model file is not active."));
        }

        if (!IsSupportedModelFile(metadata))
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidModelFileType,
                    "Model file must be MODEL_3D in GLB or GLTF format."));
        }

        return null;
    }

    private async Task<ServiceResult<CreateCustomizationProductVersionResponseDto>?> ValidatePreviewFileAsync(
        Guid previewFileId,
        CancellationToken cancellationToken)
    {
        var metadata = await _projectFiles.GetFileMetadataAsync(previewFileId, cancellationToken);
        if (metadata is null)
        {
            return ProductVersionFailure(
                Error.NotFound(
                    CustomizationRequestErrorCodes.PreviewFileNotFound,
                    "Preview file not found."));
        }

        if (metadata.Status != FileStatus.ACTIVE)
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.PreviewFileNotActive,
                    "Preview file is not active."));
        }

        if (metadata.FileType.HasValue && metadata.FileType != FileType.PRODUCT_PREVIEW)
        {
            return ProductVersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                    "Preview file must be PRODUCT_PREVIEW."));
        }

        return null;
    }

    private static bool IsSupportedModelFile(FileMetadataReadModel metadata)
    {
        if (metadata.FileType.HasValue && metadata.FileType != FileType.MODEL_3D)
        {
            return false;
        }

        var fileName = metadata.OriginalFileName ?? string.Empty;
        return fileName.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase) ||
               metadata.FileType == FileType.MODEL_3D;
    }

    private async Task AddProductVersionFileLinksAsync(
        Guid productVersionId,
        Guid currentUserId,
        CreateCustomizationProductVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (request.ModelFileId.HasValue)
        {
            var modelMetadata = await _projectFiles.GetFileMetadataAsync(
                request.ModelFileId.Value,
                cancellationToken);
            await _projectFiles.AddFileLinkAsync(
                new FileLink
                {
                    FileLinkId = Guid.NewGuid(),
                    FileId = request.ModelFileId.Value,
                    ReferenceType = CatalogFileReferenceTypes.ProductVersion,
                    ReferenceId = productVersionId,
                    FileType = FileType.MODEL_3D,
                    Visibility = modelMetadata?.Visibility ?? FileVisibility.CUSTOMER_VISIBLE,
                    IsPrimary = false,
                    DisplayOrder = 0,
                    CreatedBy = currentUserId,
                    CreatedAt = now
                },
                cancellationToken);
        }

        var previewFileIds = request.PreviewFileIds ?? [];
        var displayOrder = 0;
        foreach (var previewFileId in previewFileIds)
        {
            var previewMetadata = await _projectFiles.GetFileMetadataAsync(previewFileId, cancellationToken);
            await _projectFiles.AddFileLinkAsync(
                new FileLink
                {
                    FileLinkId = Guid.NewGuid(),
                    FileId = previewFileId,
                    ReferenceType = CatalogFileReferenceTypes.ProductVersion,
                    ReferenceId = productVersionId,
                    FileType = FileType.PRODUCT_PREVIEW,
                    Visibility = previewMetadata?.Visibility ?? FileVisibility.CUSTOMER_VISIBLE,
                    IsPrimary = displayOrder == 0,
                    DisplayOrder = displayOrder,
                    CreatedBy = currentUserId,
                    CreatedAt = now
                },
                cancellationToken);
            displayOrder++;
        }
    }

    private static ServiceResult<CreateCustomizationProductVersionResponseDto> ProductVersionFailure(Error error)
    {
        return ServiceResult<CreateCustomizationProductVersionResponseDto>.Failure(error);
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
                CustomizationRequestNotFoundMessage)
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
            ProductVersionId = context.ProductVersionId!.Value,
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
        if (detail.SourceProductVersion.ProductVersionId != Guid.Empty)
        {
            dto.SourceProductVersion = ApprovedProductVersionSummaryMapper.ToDto(detail.SourceProductVersion);
        }

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
                    CustomizationRequestNotFoundMessage));
        }
    }

    private sealed record CreateProductVersionContextResult(
        CustomizationRequest? Entity,
        ServiceResult<CreateCustomizationProductVersionResponseDto>? Error)
    {
        internal static CreateProductVersionContextResult Success(CustomizationRequest entity)
        {
            return new CreateProductVersionContextResult(entity, null);
        }

        internal static CreateProductVersionContextResult Failure(
            ServiceResult<CreateCustomizationProductVersionResponseDto> error)
        {
            return new CreateProductVersionContextResult(null, error);
        }
    }

    private sealed record SourceProductContext(
        ProductVersion SourceVersion,
        string ProjectCode);

    private sealed record SourceProductContextResult(
        SourceProductContext? Context,
        ServiceResult<CreateCustomizationProductVersionResponseDto>? Error)
    {
        internal static SourceProductContextResult Success(SourceProductContext context)
        {
            return new SourceProductContextResult(context, null);
        }

        internal static SourceProductContextResult Failure(
            ServiceResult<CreateCustomizationProductVersionResponseDto> error)
        {
            return new SourceProductContextResult(null, error);
        }
    }
}
