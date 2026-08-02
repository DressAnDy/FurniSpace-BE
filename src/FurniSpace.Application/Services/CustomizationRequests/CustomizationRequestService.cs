using FurniSpace.Application.Common;
using FurniSpace.Application.Common.CustomizationRequests;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Storage;
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
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.CustomizationRequests;

public sealed class CustomizationRequestService : ICustomizationRequestService
{
    private readonly ICustomizationRequestRepository _customizationRequests;
    private readonly ICustomizationRequestVersionRepository _customizationRequestVersions;
    private readonly IProposalRepository _proposals;
    private readonly IProjectRepository _projects;
    private readonly IProductVersionRepository _productVersions;
    private readonly IProjectFileRepository _projectFiles;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public CustomizationRequestService(CustomizationRequestServiceDependencies dependencies)
    {
        _customizationRequests = dependencies.CustomizationRequests;
        _customizationRequestVersions = dependencies.CustomizationRequestVersions;
        _proposals = dependencies.Proposals;
        _projects = dependencies.Projects;
        _productVersions = dependencies.ProductVersions;
        _projectFiles = dependencies.ProjectFiles;
        _dispatcher = dependencies.Dispatcher;
        _unitOfWork = dependencies.UnitOfWork;
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
        var filteredItems = await FilterListByRoleAsync(items, role, currentUserId, cancellationToken);
        var listDtos = await MapListItemsAsync(filteredItems, role, cancellationToken);

        return ServiceResult<CustomizationRequestListResponseDto>.Success(
            new CustomizationRequestListResponseDto { Items = listDtos },
            "Customization requests retrieved successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestDto>> GetDetailAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDto>.Unauthorized();
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                CustomizationRequestNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanAccessRequestAsync(role, detail, currentUserId, cancellationToken))
        {
            return ServiceResult<CustomizationRequestDto>.Forbidden(
                "You do not have access to this customization request.");
        }

        return ServiceResult<CustomizationRequestDto>.Success(
            await ToDetailDtoAsync(detail, role, cancellationToken),
            "Customization request detail retrieved successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestVersionListResponseDto>> GetVersionsAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestVersionListResponseDto>.Unauthorized();
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return NotFoundVersionList(
                CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                CustomizationRequestNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanAccessRequestAsync(role, detail, currentUserId, cancellationToken))
        {
            return ServiceResult<CustomizationRequestVersionListResponseDto>.Forbidden(
                "You do not have access to this customization request.");
        }

        var versions = await MapVersionsAsync(detail, role, cancellationToken);
        return ServiceResult<CustomizationRequestVersionListResponseDto>.Success(
            new CustomizationRequestVersionListResponseDto { Items = versions },
            "Customization request versions retrieved successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestVersionDto>> GetVersionDetailAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestVersionDto>.Unauthorized();
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return NotFoundVersion(
                CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                CustomizationRequestNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanAccessRequestAsync(role, detail, currentUserId, cancellationToken))
        {
            return ServiceResult<CustomizationRequestVersionDto>.Forbidden(
                "You do not have access to this customization request.");
        }

        var version = await _customizationRequestVersions.GetByIdWithRequestAsync(
            customizationRequestVersionId,
            cancellationToken);
        if (version is null ||
            version.CustomizationRequestId != customizationRequestId ||
            version.ProductVersion is null ||
            !ShouldIncludeVersionForRole(role, version.Status))
        {
            return NotFoundVersion(
                CustomizationRequestErrorCodes.CustomizationVersionNotFound,
                CustomizationVersionNotFoundMessage);
        }

        var dto = await MapVersionDtoAsync(version, detail, role, cancellationToken);
        return ServiceResult<CustomizationRequestVersionDto>.Success(
            dto,
            "Customization request version detail retrieved successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestDto>> SubmitAsync(
        Guid proposalItemId,
        Guid currentUserId,
        SubmitCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (ApplicationRoles.Customer or ApplicationRoles.Designer or ApplicationRoles.Admin))
        {
            return ServiceResult<CustomizationRequestDto>.Forbidden(
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

        var businessError = await ValidateSubmitBusinessRulesAsync(context, role, currentUserId, cancellationToken);
        if (businessError is not null)
        {
            return businessError;
        }

        var entity = CreateCustomizationRequest(context, request);
        await _customizationRequests.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchSubmittedNotificationAsync(entity, context, cancellationToken);

        var detail = await _customizationRequests.GetDetailAsync(entity.CustomizationRequestId, cancellationToken);
        return ServiceResult<CustomizationRequestDto>.Created(
            detail is null
                ? entity.Adapt<CustomizationRequestDto>()
                : await ToDetailDtoAsync(detail, role, cancellationToken),
            "Customization request submitted successfully.");
    }

    public async Task<ServiceResult<CreateCustomizationRequestVersionResponseDto>> CreateVersionAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CreateCustomizationRequestVersionDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CreateCustomizationRequestVersionResponseDto>.Unauthorized();
        }

        var contextResult = await ResolveVersionMutationContextAsync(
            customizationRequestId,
            currentUserId,
            cancellationToken);
        if (contextResult.Error is not null)
        {
            return contextResult.Error;
        }

        var entity = contextResult.Entity!;
        var validationError = ValidateCreateVersionRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var fileValidationError = await ValidateVersionFilesAsync(
            request.ModelFileId,
            request.PreviewFileIds,
            cancellationToken);
        if (fileValidationError is not null)
        {
            return fileValidationError;
        }

        var sourceContextResult = await ResolveSourceProductContextAsync(entity, cancellationToken);
        if (sourceContextResult.Error is not null)
        {
            return sourceContextResult.Error;
        }

        var versionCodeError = await ValidateVersionCodeAvailabilityAsync(request.VersionCode, cancellationToken);
        if (versionCodeError is not null)
        {
            return versionCodeError;
        }

        var sourceContext = sourceContextResult.Context!;
        var versionName = request.VersionName!.Trim();
        var versionCode = string.IsNullOrWhiteSpace(request.VersionCode) ? null : request.VersionCode.Trim();
        var designerId = contextResult.DesignerId!.Value;

        ProductVersion productVersion;
        CustomizationRequestVersion requestVersion;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var sequence = await _productVersions.CountProjectSpecificByProjectAsync(
                entity.ProjectId,
                cancellationToken) + 1;
            productVersion = CustomizationAcceptedProductVersionFactory.CreateFromDesignerRequest(
                request,
                entity,
                sourceContext.SourceVersion,
                sourceContext.ProjectCode,
                sequence,
                versionName,
                versionCode);
            var versionNo = await _customizationRequestVersions.GetNextVersionNoAsync(
                entity.CustomizationRequestId,
                cancellationToken);
            requestVersion = CustomizationAcceptedProductVersionFactory.CreateRequestVersion(
                entity,
                productVersion,
                versionNo,
                designerId,
                request);

            await _productVersions.AddAsync(productVersion, cancellationToken);
            await _customizationRequestVersions.AddAsync(requestVersion, cancellationToken);
            entity.UpdatedAt = DateTime.UtcNow;
            _customizationRequests.Update(entity);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return VersionFailure(
                Error.InternalServerError(
                    CustomizationRequestErrorCodes.CustomProductVersionCreationFailed,
                    "Failed to create customization request version."));
        }

        await AddProductVersionFileLinksAsync(
            productVersion.ProductVersionId,
            currentUserId,
            request.ModelFileId,
            request.PreviewFileIds,
            cancellationToken);

        return ServiceResult<CreateCustomizationRequestVersionResponseDto>.Created(
            CustomizationAcceptedProductVersionFactory.ToCreateVersionResponse(entity, requestVersion, productVersion),
            "Customization request version created successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestVersionDto>> UpdateDraftVersionAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        UpdateCustomizationRequestVersionDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestVersionDto>.Unauthorized();
        }

        var contextResult = await ResolveVersionUpdateContextAsync(
            customizationRequestId,
            customizationRequestVersionId,
            currentUserId,
            cancellationToken);
        if (contextResult.Error is not null)
        {
            return contextResult.Error;
        }

        var version = contextResult.Version!;
        var entity = contextResult.Entity!;
        if (version.Status != CustomizationVersionStatus.DRAFT)
        {
            return VersionDtoFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.CustomizationVersionNotDraft,
                    "Only draft versions can be updated."));
        }

        var validationError = await ValidateDraftVersionUpdateAsync(
            request,
            version,
            cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var sourceContextResult = await ResolveSourceProductContextAsync(entity, cancellationToken);
        if (sourceContextResult.Error is not null)
        {
            return VersionDtoFailure(ToError(sourceContextResult.Error));
        }

        var productVersion = await _productVersions.GetByIdAsync(version.ProductVersionId, cancellationToken);
        if (productVersion is null)
        {
            return VersionDtoFailure(
                Error.NotFound(
                    CustomizationRequestErrorCodes.ApprovedProductVersionNotFound,
                    "Linked product version was not found."));
        }

        var versionName = request.VersionName?.Trim() ?? productVersion.VersionName;
        var versionCode = request.VersionCode?.Trim() ?? productVersion.VersionCode;
        CustomizationAcceptedProductVersionFactory.CreateFromDesignerRequest(
            request,
            entity,
            sourceContextResult.Context!.SourceVersion,
            productVersion,
            versionName,
            versionCode);
        CustomizationAcceptedProductVersionFactory.ApplyDraftMetadata(version, request);

        _productVersions.Update(productVersion);
        _customizationRequestVersions.Update(version);
        entity.UpdatedAt = DateTime.UtcNow;
        _customizationRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.ModelFileId.HasValue || request.PreviewFileIds is not null)
        {
            await AddProductVersionFileLinksAsync(
                productVersion.ProductVersionId,
                currentUserId,
                request.ModelFileId,
                request.PreviewFileIds ?? [],
                cancellationToken);
        }

        return ServiceResult<CustomizationRequestVersionDto>.Success(
            CustomizationRequestVersionMapper.ToDto(version, productVersion),
            "Customization request version updated successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestVersionDto>> SubmitVersionForReviewAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestVersionDto>.Unauthorized();
        }

        var contextResult = await ResolveVersionUpdateContextAsync(
            customizationRequestId,
            customizationRequestVersionId,
            currentUserId,
            cancellationToken);
        if (contextResult.Error is not null)
        {
            return contextResult.Error;
        }

        var version = contextResult.Version!;
        var entity = contextResult.Entity!;
        var detail = contextResult.Detail!;

        if (version.Status != CustomizationVersionStatus.DRAFT)
        {
            return VersionDtoFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.CustomizationVersionNotDraft,
                    "Only draft versions can be submitted for review."));
        }

        if (entity.Status is not (CustomizationStatus.SUBMITTED or CustomizationStatus.REVIEWING))
        {
            return VersionDtoFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.InvalidCustomizationTransition,
                    "Customization request is not open for version review."));
        }

        var now = DateTime.UtcNow;
        version.Status = CustomizationVersionStatus.REVIEWING;
        version.SubmittedForReviewAt = now;
        version.UpdatedAt = now;
        if (entity.Status == CustomizationStatus.SUBMITTED)
        {
            entity.Status = CustomizationStatus.REVIEWING;
        }

        entity.UpdatedAt = now;
        _customizationRequestVersions.Update(version);
        _customizationRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchVersionSubmittedNotificationAsync(entity, detail, cancellationToken);

        var productVersion = await _productVersions.GetByIdAsync(version.ProductVersionId, cancellationToken);
        return ServiceResult<CustomizationRequestVersionDto>.Success(
            CustomizationRequestVersionMapper.ToDto(version, productVersion!),
            "Customization request version submitted for production review.");
    }

    public async Task<ServiceResult<CustomizationRequestVersionDto>> WithdrawVersionAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestVersionDto>.Unauthorized();
        }

        var contextResult = await ResolveVersionUpdateContextAsync(
            customizationRequestId,
            customizationRequestVersionId,
            currentUserId,
            cancellationToken);
        if (contextResult.Error is not null)
        {
            return contextResult.Error;
        }

        var version = contextResult.Version!;
        var entity = contextResult.Entity!;
        if (version.Status is not (CustomizationVersionStatus.DRAFT or CustomizationVersionStatus.REVIEWING))
        {
            return VersionDtoFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.InvalidCustomizationTransition,
                    "Only draft or reviewing versions can be withdrawn."));
        }

        if (version.Status == CustomizationVersionStatus.REVIEWING &&
            version.FeasibilityStatus != ProductionFeasibilityStatus.PENDING)
        {
            return VersionDtoFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.CustomizationVersionAlreadyReviewed,
                    "Reviewed versions cannot be withdrawn."));
        }

        var now = DateTime.UtcNow;
        version.Status = CustomizationVersionStatus.WITHDRAWN;
        version.WithdrawnAt = now;
        version.UpdatedAt = now;
        entity.UpdatedAt = now;

        _customizationRequestVersions.Update(version);
        _customizationRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var productVersion = await _productVersions.GetByIdAsync(version.ProductVersionId, cancellationToken);
        return ServiceResult<CustomizationRequestVersionDto>.Success(
            CustomizationRequestVersionMapper.ToDto(version, productVersion!),
            "Customization request version withdrawn successfully.");
    }

    public async Task<ServiceResult<CustomizationRequestDto>> AcceptVersionAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        AcceptCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDto>.Unauthorized();
        }

        if (request.CustomizationRequestVersionId == Guid.Empty)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                "Customization request version id is required.");
        }

        var entity = await _customizationRequests.GetByIdAsync(customizationRequestId, cancellationToken);
        if (entity is null)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                CustomizationRequestNotFoundMessage);
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                CustomizationRequestNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role != ApplicationRoles.Customer || detail.CustomerId != currentUserId)
        {
            return ServiceResult<CustomizationRequestDto>.Forbidden(
                "You can only accept customization requests for your own project.");
        }

        if (entity.Status == CustomizationStatus.ACCEPTED &&
            entity.AcceptedRequestVersionId == request.CustomizationRequestVersionId)
        {
            return await ReloadUpdatedDetailAsync(
                customizationRequestId,
                currentUserId,
                "Customization request accepted successfully.",
                cancellationToken);
        }

        if (entity.Status != CustomizationStatus.REVIEWING)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationNotInReviewing,
                "Customization request is not in reviewing status.");
        }

        var version = await _customizationRequestVersions.GetByIdForUpdateAsync(
            request.CustomizationRequestVersionId,
            cancellationToken);
        if (version is null || version.CustomizationRequestId != customizationRequestId)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationVersionNotFound,
                CustomizationVersionNotFoundMessage);
        }

        if (version.Status != CustomizationVersionStatus.REVIEWING ||
            version.FeasibilityStatus != ProductionFeasibilityStatus.FEASIBLE)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationVersionNotFeasible,
                "Only production-feasible reviewing versions can be accepted.");
        }

        var now = DateTime.UtcNow;
        CustomizationAcceptedProductVersionFactory.MarkRequestAccepted(entity, version, now);
        await WithdrawOtherVersionsAsync(entity.CustomizationRequestId, version.CustomizationRequestVersionId, now, cancellationToken);

        _customizationRequests.Update(entity);
        _customizationRequestVersions.Update(version);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadUpdatedDetailAsync(
            customizationRequestId,
            currentUserId,
            "Customization request accepted successfully.",
            cancellationToken);
    }

    public async Task<ServiceResult<CustomizationRequestDto>> CancelAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancelCustomizationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<CustomizationRequestDto>.Unauthorized();
        }

        var context = await GetUpdateContextAsync(customizationRequestId, cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanCancelRequest(role, context.Detail!, currentUserId))
        {
            return ServiceResult<CustomizationRequestDto>.Forbidden(
                "You do not have permission to cancel this customization request.");
        }

        if (context.Entity!.Status == CustomizationStatus.ACCEPTED)
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationAlreadyAccepted,
                "Accepted customization requests cannot be cancelled.");
        }

        if (context.Entity.Status == CustomizationStatus.CANCELLED)
        {
            return InvalidTransition("Customization request has already been cancelled.");
        }

        var now = DateTime.UtcNow;
        context.Entity.Status = CustomizationStatus.CANCELLED;
        context.Entity.UpdatedAt = now;
        await WithdrawAllNonTerminalVersionsAsync(context.Entity.CustomizationRequestId, now, cancellationToken);
        _customizationRequests.Update(context.Entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadUpdatedDetailAsync(
            customizationRequestId,
            currentUserId,
            "Customization request cancelled successfully.",
            cancellationToken);
    }

    public async Task<ServiceResult<ProductionCustomizationVersionListResponseDto>> GetProductionVersionQueueAsync(
        Guid currentUserId,
        ProductionCustomizationVersionQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProductionCustomizationVersionListResponseDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (ApplicationRoles.Production or ApplicationRoles.Admin))
        {
            return ServiceResult<ProductionCustomizationVersionListResponseDto>.Forbidden(
                "You do not have permission to view the production customization queue.");
        }

        var paginationError = ValidateProductionQueuePagination(query.Page, query.PageSize);
        if (paginationError is not null)
        {
            return ServiceResult<ProductionCustomizationVersionListResponseDto>.BadRequest(paginationError);
        }

        var filterError = ResolveProductionVersionQueueFilters(role, query, out var readQuery);
        if (filterError is not null)
        {
            return filterError;
        }

        var items = await _customizationRequestVersions.GetProductionQueueAsync(readQuery, cancellationToken);
        var total = await _customizationRequestVersions.CountProductionQueueAsync(readQuery, cancellationToken);

        return ServiceResult<ProductionCustomizationVersionListResponseDto>.Success(
            new ProductionCustomizationVersionListResponseDto
            {
                Items = items.Select(ProductionCustomizationVersionQueueMapper.ToDto).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                Total = total
            },
            "Production customization versions retrieved successfully.");
    }

    public async Task<ServiceResult<ProductionCustomizationVersionDetailDto>> GetProductionVersionDetailAsync(
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (ApplicationRoles.Production or ApplicationRoles.Admin))
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Forbidden(
                "You do not have permission to view production customization versions.");
        }

        var detail = await _customizationRequestVersions.GetProductionDetailAsync(
            customizationRequestVersionId,
            cancellationToken);
        if (detail is null)
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Failure(
                Error.NotFound(
                    CustomizationRequestErrorCodes.CustomizationVersionNotFound,
                    CustomizationVersionNotFoundMessage));
        }

        var requestDetail = await ToDetailDtoAsync(
            BuildDetailFromReadModel(detail.Request, detail.SourceProductVersion),
            ApplicationRoles.Production,
            cancellationToken);

        return ServiceResult<ProductionCustomizationVersionDetailDto>.Success(
            ProductionCustomizationVersionQueueMapper.ToDetailDto(detail, requestDetail),
            "Production customization version detail retrieved successfully.");
    }

    public async Task<ServiceResult<ProductionCustomizationVersionDetailDto>> ReviewVersionAsync(
        Guid customizationRequestVersionId,
        Guid currentUserId,
        ReviewCustomizationVersionDto request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Unauthorized();
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role is not (ApplicationRoles.Production or ApplicationRoles.Admin))
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Forbidden(
                "You do not have permission to review production feasibility.");
        }

        var validationError = ValidateProductionVersionReview(request);
        if (validationError is not null)
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Failure(validationError);
        }

        var existing = await _customizationRequestVersions.GetProductionDetailAsync(
            customizationRequestVersionId,
            cancellationToken);
        if (existing is null)
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Failure(
                Error.NotFound(
                    CustomizationRequestErrorCodes.CustomizationVersionNotFound,
                    CustomizationVersionNotFoundMessage));
        }

        if (existing.Version.FeasibilityStatus != ProductionFeasibilityStatus.PENDING)
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Failure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.CustomizationVersionAlreadyReviewed,
                    "Customization version has already been reviewed."));
        }

        if (existing.Version.Status != CustomizationVersionStatus.REVIEWING)
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Failure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.CustomizationVersionNotReviewing,
                    "Customization version is not awaiting production review."));
        }

        var isFeasible = IsResult(request.Result, FeasibleResult);
        var reviewedAt = DateTime.UtcNow;
        var versionStatus = isFeasible
            ? CustomizationVersionStatus.REVIEWING
            : CustomizationVersionStatus.PRODUCTION_REJECTED;
        var feasibilityStatus = isFeasible
            ? ProductionFeasibilityStatus.FEASIBLE
            : ProductionFeasibilityStatus.NOT_FEASIBLE;

        var updated = await _customizationRequestVersions.TryMarkProductionReviewedAsync(
            new ProductionVersionReviewUpdate(
                customizationRequestVersionId,
                feasibilityStatus,
                versionStatus,
                currentUserId,
                request.FeasibilityNote,
                request.EstimatedProductionDays,
                request.EstimatedAdditionalCost,
                request.AdditionalCostReason,
                request.MaterialAvailable,
                request.ProductionRiskNote,
                request.AlternativeMaterialNote,
                reviewedAt),
            cancellationToken);

        if (!updated)
        {
            return ServiceResult<ProductionCustomizationVersionDetailDto>.Failure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.CustomizationVersionAlreadyReviewed,
                    "Customization version has already been reviewed."));
        }

        return await GetProductionVersionDetailAsync(customizationRequestVersionId, currentUserId, cancellationToken);
    }

    private async Task WithdrawOtherVersionsAsync(
        Guid customizationRequestId,
        Guid acceptedVersionId,
        DateTime withdrawnAt,
        CancellationToken cancellationToken)
    {
        var versions = await _customizationRequestVersions.GetByRequestIdAsync(customizationRequestId, cancellationToken);
        foreach (var versionReadModel in versions)
        {
            if (versionReadModel.CustomizationRequestVersionId == acceptedVersionId ||
                !NonTerminalVersionStatuses.Contains(versionReadModel.Status))
            {
                continue;
            }

            var version = await _customizationRequestVersions.GetByIdForUpdateAsync(
                versionReadModel.CustomizationRequestVersionId,
                cancellationToken);
            if (version is null)
            {
                continue;
            }

            version.Status = CustomizationVersionStatus.WITHDRAWN;
            version.WithdrawnAt = withdrawnAt;
            version.UpdatedAt = withdrawnAt;
            _customizationRequestVersions.Update(version);
        }
    }

    private async Task WithdrawAllNonTerminalVersionsAsync(
        Guid customizationRequestId,
        DateTime withdrawnAt,
        CancellationToken cancellationToken)
    {
        var versions = await _customizationRequestVersions.GetByRequestIdAsync(customizationRequestId, cancellationToken);
        foreach (var versionReadModel in versions.Where(v => NonTerminalVersionStatuses.Contains(v.Status)))
        {
            var version = await _customizationRequestVersions.GetByIdForUpdateAsync(
                versionReadModel.CustomizationRequestVersionId,
                cancellationToken);
            if (version is null)
            {
                continue;
            }

            version.Status = CustomizationVersionStatus.WITHDRAWN;
            version.WithdrawnAt = withdrawnAt;
            version.UpdatedAt = withdrawnAt;
            _customizationRequestVersions.Update(version);
        }
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

    private async Task<ServiceResult<CustomizationRequestDto>?> ValidateSubmitBusinessRulesAsync(
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

    private static ServiceResult<CustomizationRequestDto>? ValidateSubmitRoleAccess(
        CustomizationSubmitContextReadModel context,
        string? role,
        Guid currentUserId)
    {
        if (role == ApplicationRoles.Customer)
        {
            return context.CustomerId != currentUserId
                ? ServiceResult<CustomizationRequestDto>.Forbidden(
                    "You can only submit customization requests for your own project.")
                : null;
        }

        if (role == ApplicationRoles.Designer)
        {
            return context.AssignedDesignerId != currentUserId
                ? ServiceResult<CustomizationRequestDto>.Failure(
                    Error.Forbidden(
                        CustomizationRequestErrorCodes.DesignerNotAssignedToProject,
                        "Designer is not assigned to this project."))
                : null;
        }

        return role == ApplicationRoles.Admin
            ? null
            : ServiceResult<CustomizationRequestDto>.Forbidden(
                "You do not have permission to submit customization requests.");
    }

    private static ServiceResult<CustomizationRequestDto>? ValidateSubmitRequest(
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

    private async Task<VersionMutationContextResult> ResolveVersionMutationContextAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var entity = await _customizationRequests.GetByIdAsync(customizationRequestId, cancellationToken);
        if (entity is null)
        {
            return VersionMutationContextResult.Failure(
                VersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                        CustomizationRequestNotFoundMessage)));
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return VersionMutationContextResult.Failure(
                VersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                        CustomizationRequestNotFoundMessage)));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanDesignerMutateVersion(role, detail, currentUserId))
        {
            return VersionMutationContextResult.Failure(
                VersionFailure(
                    Error.Forbidden(
                        CustomizationRequestErrorCodes.ProjectAccessDenied,
                        "You do not have permission to manage versions for this customization request.")));
        }

        if (entity.Status is not (CustomizationStatus.SUBMITTED or CustomizationStatus.REVIEWING))
        {
            return VersionMutationContextResult.Failure(
                VersionFailure(
                    Error.Conflict(
                        CustomizationRequestErrorCodes.InvalidCustomizationTransition,
                        "Customization request is not open for version creation.")));
        }

        var designerId = role == ApplicationRoles.Admin
            ? detail.AssignedDesignerId ?? currentUserId
            : currentUserId;

        return VersionMutationContextResult.Success(entity, detail, designerId);
    }

    private async Task<VersionDtoContextResult> ResolveVersionUpdateContextAsync(
        Guid customizationRequestId,
        Guid customizationRequestVersionId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var entity = await _customizationRequests.GetByIdAsync(customizationRequestId, cancellationToken);
        if (entity is null)
        {
            return VersionDtoContextResult.Failure(
                VersionDtoFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                        CustomizationRequestNotFoundMessage)));
        }

        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return VersionDtoContextResult.Failure(
                VersionDtoFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                        CustomizationRequestNotFoundMessage)));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!CanDesignerMutateVersion(role, detail, currentUserId))
        {
            return VersionDtoContextResult.Failure(
                VersionDtoFailure(
                    Error.Forbidden(
                        CustomizationRequestErrorCodes.ProjectAccessDenied,
                        "You do not have permission to manage versions for this customization request.")));
        }

        var version = await _customizationRequestVersions.GetByIdForUpdateAsync(
            customizationRequestVersionId,
            cancellationToken);
        if (version is null || version.CustomizationRequestId != customizationRequestId)
        {
            return VersionDtoContextResult.Failure(
                VersionDtoFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.CustomizationVersionNotFound,
                        CustomizationVersionNotFoundMessage)));
        }

        return VersionDtoContextResult.Success(entity, detail, version);
    }

    private async Task<SourceProductContextResult> ResolveSourceProductContextAsync(
        CustomizationRequest entity,
        CancellationToken cancellationToken)
    {
        var sourceVersion = await _productVersions.GetByIdAsync(entity.SourceProductVersionId, cancellationToken);
        if (sourceVersion is null)
        {
            return SourceProductContextResult.Failure(
                VersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.SourceProductVersionNotFound,
                        "Source product version not found.")));
        }

        if (!await _productVersions.ProductExistsAsync(sourceVersion.ProductId, cancellationToken))
        {
            return SourceProductContextResult.Failure(
                VersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.SourceProductNotFound,
                        "Source product not found.")));
        }

        var project = await _projects.GetByIdAsync(entity.ProjectId, cancellationToken);
        if (project is null || string.IsNullOrWhiteSpace(project.ProjectCode))
        {
            return SourceProductContextResult.Failure(
                VersionFailure(
                    Error.NotFound(
                        CustomizationRequestErrorCodes.ProjectNotFound,
                        "Project not found.")));
        }

        return SourceProductContextResult.Success(new SourceProductContext(sourceVersion, project.ProjectCode));
    }

    private async Task<ServiceResult<CreateCustomizationRequestVersionResponseDto>?> ValidateVersionCodeAvailabilityAsync(
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
            ? VersionFailure(
                Error.Conflict(
                    CustomizationRequestErrorCodes.VersionCodeAlreadyExists,
                    "Version code already exists."))
            : null;
    }

    private static ServiceResult<CreateCustomizationRequestVersionResponseDto>? ValidateCreateVersionRequest(
        CreateCustomizationRequestVersionDto request)
    {
        var versionNameError = CustomizationAcceptedProductVersionFactory.ValidateVersionName(request.VersionName);
        if (versionNameError is not null)
        {
            return VersionFailure(
                Error.BadRequest(CustomizationRequestErrorCodes.VersionNameRequired, versionNameError));
        }

        var versionCodeError = CustomizationAcceptedProductVersionFactory.ValidateVersionCode(request.VersionCode);
        if (versionCodeError is not null)
        {
            return VersionFailure(
                Error.BadRequest(CustomizationRequestErrorCodes.InvalidCustomizationRequest, versionCodeError));
        }

        if (!CustomizationAcceptedProductVersionFactory.IsValidDimensionUnit(request.DimensionUnit))
        {
            return VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidDimensionUnit,
                    "Dimension unit must be cm, m, or mm."));
        }

        if (HasInvalidDimension(request.Width) ||
            HasInvalidDimension(request.Height) ||
            HasInvalidDimension(request.Depth))
        {
            return VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidProductDimensions,
                    "Product dimensions must be greater than zero."));
        }

        if (request.EstimatedPrice.HasValue && request.EstimatedPrice.Value < 0m)
        {
            return VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidEstimatedPrice,
                    "Estimated price must be greater than or equal to zero."));
        }

        return ValidatePreviewFileCount(request.PreviewFileIds);
    }

    private static ServiceResult<CreateCustomizationRequestVersionResponseDto>? ValidatePreviewFileCount(
        IReadOnlyList<Guid> previewFileIds)
    {
        if (previewFileIds.Count > MaxProductVersionPreviewFileCount)
        {
            return VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                    $"At most {MaxProductVersionPreviewFileCount} preview files are allowed."));
        }

        return previewFileIds.Count != previewFileIds.Distinct().Count()
            ? VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                    "Preview file ids must be unique."))
            : null;
    }

    private async Task<ServiceResult<CustomizationRequestVersionDto>?> ValidateDraftVersionUpdateAsync(
        UpdateCustomizationRequestVersionDto request,
        CustomizationRequestVersion version,
        CancellationToken cancellationToken)
    {
        if (request.VersionName is not null)
        {
            var versionNameError = CustomizationAcceptedProductVersionFactory.ValidateVersionName(request.VersionName);
            if (versionNameError is not null)
            {
                return VersionDtoFailure(
                    Error.BadRequest(CustomizationRequestErrorCodes.VersionNameRequired, versionNameError));
            }
        }

        if (request.VersionCode is not null)
        {
            var versionCodeError = CustomizationAcceptedProductVersionFactory.ValidateVersionCode(request.VersionCode);
            if (versionCodeError is not null)
            {
                return VersionDtoFailure(
                    Error.BadRequest(CustomizationRequestErrorCodes.InvalidCustomizationRequest, versionCodeError));
            }

            var trimmedCode = request.VersionCode.Trim();
            var existingVersion = await _productVersions.GetByIdAsync(version.ProductVersionId, cancellationToken);
            if (existingVersion is not null &&
                !string.Equals(existingVersion.VersionCode, trimmedCode, StringComparison.Ordinal) &&
                await _productVersions.VersionCodeExistsAsync(trimmedCode, cancellationToken))
            {
                return VersionDtoFailure(
                    Error.Conflict(
                        CustomizationRequestErrorCodes.VersionCodeAlreadyExists,
                        "Version code already exists."));
            }
        }

        if (request.DimensionUnit is not null &&
            !CustomizationAcceptedProductVersionFactory.IsValidDimensionUnit(request.DimensionUnit))
        {
            return VersionDtoFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidDimensionUnit,
                    "Dimension unit must be cm, m, or mm."));
        }

        var fileValidationError = await ValidateVersionFilesAsync(
            request.ModelFileId,
            request.PreviewFileIds,
            cancellationToken);
        return fileValidationError is not null
            ? VersionDtoFailure(ToError(fileValidationError))
            : null;
    }

    private async Task<ServiceResult<CreateCustomizationRequestVersionResponseDto>?> ValidateVersionFilesAsync(
        Guid? modelFileId,
        IReadOnlyList<Guid>? previewFileIds,
        CancellationToken cancellationToken)
    {
        if (modelFileId.HasValue)
        {
            var modelError = await ValidateModelFileAsync(modelFileId.Value, cancellationToken);
            if (modelError is not null)
            {
                return modelError;
            }
        }

        foreach (var previewFileId in previewFileIds ?? [])
        {
            var previewError = await ValidatePreviewFileAsync(previewFileId, cancellationToken);
            if (previewError is not null)
            {
                return previewError;
            }
        }

        return null;
    }

    private async Task<ServiceResult<CreateCustomizationRequestVersionResponseDto>?> ValidateModelFileAsync(
        Guid modelFileId,
        CancellationToken cancellationToken)
    {
        var metadata = await _projectFiles.GetFileMetadataAsync(modelFileId, cancellationToken);
        if (metadata is null)
        {
            return VersionFailure(
                Error.NotFound(
                    CustomizationRequestErrorCodes.ModelFileNotFound,
                    "Model file not found."));
        }

        if (metadata.Status != FileStatus.ACTIVE)
        {
            return VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.ModelFileNotActive,
                    "Model file is not active."));
        }

        if (!IsSupportedModelFile(metadata))
        {
            return VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidModelFileType,
                    "Model file must be MODEL_3D in GLB or GLTF format."));
        }

        return null;
    }

    private async Task<ServiceResult<CreateCustomizationRequestVersionResponseDto>?> ValidatePreviewFileAsync(
        Guid previewFileId,
        CancellationToken cancellationToken)
    {
        var metadata = await _projectFiles.GetFileMetadataAsync(previewFileId, cancellationToken);
        if (metadata is null)
        {
            return VersionFailure(
                Error.NotFound(
                    CustomizationRequestErrorCodes.PreviewFileNotFound,
                    "Preview file not found."));
        }

        if (metadata.Status != FileStatus.ACTIVE)
        {
            return VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.PreviewFileNotActive,
                    "Preview file is not active."));
        }

        if (metadata.FileType.HasValue && metadata.FileType != FileType.PRODUCT_PREVIEW)
        {
            return VersionFailure(
                Error.BadRequest(
                    CustomizationRequestErrorCodes.InvalidCustomizationRequest,
                    "Preview file must be PRODUCT_PREVIEW."));
        }

        return null;
    }

    private static Error? ValidateProductionVersionReview(ReviewCustomizationVersionDto request)
    {
        var result = request.Result?.Trim() ?? string.Empty;
        if (IsResult(result, NotFeasibleResult))
        {
            return request.MaterialAvailable == true
                ? Error.BadRequest(
                    CustomizationRequestErrorCodes.MaterialNotAvailable,
                    "Material must not be available when customization is not feasible.")
                : null;
        }

        if (!IsResult(result, FeasibleResult))
        {
            return Error.BadRequest(
                CustomizationRequestErrorCodes.InvalidCustomizationTransition,
                "Unsupported production review result.");
        }

        if (request.MaterialAvailable != true)
        {
            return Error.BadRequest(
                CustomizationRequestErrorCodes.MaterialNotAvailable,
                "Material must be available for feasible customization.");
        }

        if (!request.EstimatedProductionDays.HasValue || !request.EstimatedAdditionalCost.HasValue)
        {
            return Error.BadRequest(
                CustomizationRequestErrorCodes.CustomizationCostRequired,
                "Estimated production days and additional cost are required.");
        }

        return request.EstimatedAdditionalCost > 0 &&
            string.IsNullOrWhiteSpace(request.AdditionalCostReason)
                ? Error.BadRequest(
                    CustomizationRequestErrorCodes.AdditionalCostReasonRequired,
                    "Additional cost reason is required when additional cost is greater than zero.")
                : null;
    }

    private async Task AddProductVersionFileLinksAsync(
        Guid productVersionId,
        Guid currentUserId,
        Guid? modelFileId,
        IReadOnlyList<Guid> previewFileIds,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (modelFileId.HasValue)
        {
            var modelMetadata = await _projectFiles.GetFileMetadataAsync(modelFileId.Value, cancellationToken);
            await _projectFiles.AddFileLinkAsync(
                new FileLink
                {
                    FileLinkId = Guid.NewGuid(),
                    FileId = modelFileId.Value,
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

    private async Task<ServiceResult<CustomizationRequestDto>> ReloadUpdatedDetailAsync(
        Guid customizationRequestId,
        Guid currentUserId,
        string message,
        CancellationToken cancellationToken)
    {
        var detail = await _customizationRequests.GetDetailAsync(customizationRequestId, cancellationToken);
        if (detail is null)
        {
            return NotFoundDetail(
                CustomizationRequestErrorCodes.CustomizationRequestNotFound,
                CustomizationRequestNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        return ServiceResult<CustomizationRequestDto>.Success(
            await ToDetailDtoAsync(detail, role, cancellationToken),
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
            SourceProductVersionId = context.ProductVersionId!.Value,
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

    private async Task DispatchVersionSubmittedNotificationAsync(
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

    private static bool CanDesignerMutateVersion(
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

    private async Task<bool> CanAccessRequestAsync(
        string? role,
        CustomizationRequestReadModel request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (role == ApplicationRoles.Production)
        {
            return await _customizationRequests.HasProductionVisibleRequestAsync(
                request.ProjectId,
                currentUserId,
                cancellationToken);
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
        if (role is not (ApplicationRoles.Customer or ApplicationRoles.Sales or ApplicationRoles.Designer or ApplicationRoles.Admin))
        {
            return false;
        }

        return role == ApplicationRoles.Admin ||
            (role == ApplicationRoles.Customer && request.CustomerId == currentUserId) ||
            (role == ApplicationRoles.Sales && request.AssignedSalesId == currentUserId) ||
            (role == ApplicationRoles.Designer && request.AssignedDesignerId == currentUserId);
    }

    private async Task<List<CustomizationRequestReadModel>> FilterListByRoleAsync(
        IEnumerable<CustomizationRequestReadModel> items,
        string? role,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (role != ApplicationRoles.Production)
        {
            return items.ToList();
        }

        var result = new List<CustomizationRequestReadModel>();
        foreach (var item in items)
        {
            if (await _customizationRequests.HasProductionVisibleRequestAsync(
                    item.ProjectId,
                    currentUserId,
                    cancellationToken))
            {
                result.Add(item);
            }
        }

        return result;
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

    private static ServiceResult<ProductionCustomizationVersionListResponseDto>? ResolveProductionVersionQueueFilters(
        string? role,
        ProductionCustomizationVersionQueryDto query,
        out ProductionCustomizationVersionQueueQueryReadModel readQuery)
    {
        readQuery = new ProductionCustomizationVersionQueueQueryReadModel
        {
            ProjectId = query.ProjectId,
            ProposalId = query.ProposalId,
            MaterialAvailable = query.MaterialAvailable,
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            Page = query.Page,
            PageSize = query.PageSize
        };

        if (role == ApplicationRoles.Production &&
            string.IsNullOrWhiteSpace(query.Status) &&
            string.IsNullOrWhiteSpace(query.FeasibilityStatus))
        {
            readQuery.Statuses = [CustomizationVersionStatus.REVIEWING];
            readQuery.FeasibilityStatuses = [ProductionFeasibilityStatus.PENDING];
            return null;
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<CustomizationVersionStatus>(query.Status, true, out var parsedStatus))
            {
                return ServiceResult<ProductionCustomizationVersionListResponseDto>.BadRequest(
                    "Status filter is invalid.");
            }

            readQuery.Statuses = [parsedStatus];
        }

        if (!string.IsNullOrWhiteSpace(query.FeasibilityStatus))
        {
            if (!Enum.TryParse<ProductionFeasibilityStatus>(query.FeasibilityStatus, true, out var parsedFeasibility))
            {
                return ServiceResult<ProductionCustomizationVersionListResponseDto>.BadRequest(
                    "Feasibility status filter is invalid.");
            }

            readQuery.FeasibilityStatuses = [parsedFeasibility];
        }

        return null;
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
        string? role,
        CancellationToken cancellationToken)
    {
        var result = new List<CustomizationRequestDto>();
        foreach (var item in items)
        {
            result.Add(await MapRequestDtoAsync(item, role, cancellationToken));
        }

        return result;
    }

    private async Task<CustomizationRequestDto> ToDetailDtoAsync(
        CustomizationRequestDetailReadModel detail,
        string? role,
        CancellationToken cancellationToken)
    {
        var dto = await MapRequestDtoAsync(detail, role, cancellationToken);
        if (detail.SourceProductVersion.ProductVersionId != Guid.Empty)
        {
            dto.SourceProductVersion = ApprovedProductVersionSummaryMapper.ToDto(detail.SourceProductVersion);
        }

        return dto;
    }

    private async Task<CustomizationRequestDto> MapRequestDtoAsync(
        CustomizationRequestReadModel detail,
        string? role,
        CancellationToken cancellationToken)
    {
        var dto = detail.Adapt<CustomizationRequestDto>();
        dto.Versions = await MapVersionsAsync(detail, role, cancellationToken);

        if (detail.AcceptedRequestVersionId.HasValue)
        {
            dto.AcceptedVersion = dto.Versions.FirstOrDefault(
                version => version.CustomizationRequestVersionId == detail.AcceptedRequestVersionId);
        }

        var sourceVersion = await _productVersions.GetByIdAsync(detail.SourceProductVersionId, cancellationToken);
        if (sourceVersion is not null)
        {
            dto.SourceProductVersion = ApprovedProductVersionSummaryMapper.ToDto(sourceVersion);
        }

        return dto;
    }

    private async Task<IReadOnlyList<CustomizationRequestVersionDto>> MapVersionsAsync(
        CustomizationRequestReadModel request,
        string? role,
        CancellationToken cancellationToken)
    {
        var versions = await _customizationRequestVersions.GetByRequestIdAsync(
            request.CustomizationRequestId,
            cancellationToken);

        var visibleVersions = versions
            .Where(version => ShouldIncludeVersionForRole(role, version.Status))
            .ToList();

        var filesByProductVersionId = await LoadVersionFilesByProductVersionIdsAsync(
            visibleVersions.Select(version => version.ProductVersionId),
            role,
            cancellationToken);

        return visibleVersions
            .Select(version => EnrichVersionDto(
                CustomizationRequestVersionMapper.ToDto(version),
                request.AcceptedRequestVersionId,
                filesByProductVersionId))
            .ToList();
    }

    private async Task<CustomizationRequestVersionDto> MapVersionDtoAsync(
        CustomizationRequestVersion version,
        CustomizationRequestReadModel request,
        string? role,
        CancellationToken cancellationToken)
    {
        var filesByProductVersionId = await LoadVersionFilesByProductVersionIdsAsync(
            [version.ProductVersionId],
            role,
            cancellationToken);

        return EnrichVersionDto(
            CustomizationRequestVersionMapper.ToDto(version, version.ProductVersion!),
            request.AcceptedRequestVersionId,
            filesByProductVersionId);
    }

    private static CustomizationRequestVersionDto EnrichVersionDto(
        CustomizationRequestVersionDto dto,
        Guid? acceptedRequestVersionId,
        IReadOnlyDictionary<Guid, List<CatalogFileDto>> filesByProductVersionId)
    {
        dto.IsAccepted = acceptedRequestVersionId == dto.CustomizationRequestVersionId;
        if (filesByProductVersionId.TryGetValue(dto.ProductVersion.ProductVersionId, out var files))
        {
            dto.ProductVersion.Files = files;
        }

        return dto;
    }

    private async Task<IReadOnlyDictionary<Guid, List<CatalogFileDto>>> LoadVersionFilesByProductVersionIdsAsync(
        IEnumerable<Guid> productVersionIds,
        string? role,
        CancellationToken cancellationToken)
    {
        var ids = productVersionIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, List<CatalogFileDto>>();
        }

        var customerVisibleOnly = IsCustomerVisibleFilesOnly(role);
        var files = await _projectFiles.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.ProductVersion,
            ids,
            customerVisibleOnly,
            cancellationToken);

        return files
            .GroupBy(file => file.ReferenceId)
            .ToDictionary(
                group => group.Key,
                group => ToCatalogFileList(group, customerVisibleOnly));
    }

    private static bool ShouldIncludeVersionForRole(string? role, CustomizationVersionStatus status)
    {
        if (role is ApplicationRoles.Admin or ApplicationRoles.Designer)
        {
            return true;
        }

        return status != CustomizationVersionStatus.DRAFT;
    }

    private static bool IsCustomerVisibleFilesOnly(string? role)
        => role == ApplicationRoles.Customer;

    private static List<CatalogFileDto> ToCatalogFileList(
        IEnumerable<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        return CatalogFileOrdering
            .SortCatalogFiles(CatalogFileOrdering.FilterVisible(files, customerVisibleOnly))
            .Adapt<List<CatalogFileDto>>();
    }

    private static CustomizationRequestDetailReadModel BuildDetailFromReadModel(
        CustomizationRequestReadModel request,
        ProductVersion sourceProductVersion)
    {
        var detail = request.Adapt<CustomizationRequestDetailReadModel>();
        detail.SourceProductVersion = sourceProductVersion;
        return detail;
    }

    private static bool HasInvalidDimension(decimal? value)
    {
        return value.HasValue && value.Value <= 0m;
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

    private static ServiceResult<CreateCustomizationRequestVersionResponseDto> VersionFailure(Error error)
    {
        return ServiceResult<CreateCustomizationRequestVersionResponseDto>.Failure(error);
    }

    private static Error ToError<T>(ServiceResult<T> result)
    {
        var code = result.ErrorCode ?? CustomizationRequestErrorCodes.InvalidCustomizationRequest;
        var message = result.Message ?? "Request failed.";
        return result.Status switch
        {
            401 => Error.Unauthorized(code, message),
            403 => Error.Forbidden(code, message),
            404 => Error.NotFound(code, message),
            409 => Error.Conflict(code, message),
            500 => Error.InternalServerError(code, message),
            _ => Error.BadRequest(code, message)
        };
    }

    private static ServiceResult<CustomizationRequestVersionDto> VersionDtoFailure(Error error)
    {
        return ServiceResult<CustomizationRequestVersionDto>.Failure(error);
    }

    private static ServiceResult<CustomizationRequestVersionListResponseDto> NotFoundVersionList(string code, string message)
    {
        return ServiceResult<CustomizationRequestVersionListResponseDto>.Failure(Error.NotFound(code, message));
    }

    private static ServiceResult<CustomizationRequestVersionDto> NotFoundVersion(string code, string message)
    {
        return ServiceResult<CustomizationRequestVersionDto>.Failure(Error.NotFound(code, message));
    }

    private static ServiceResult<CustomizationRequestListResponseDto> NotFoundList(string code, string message)
    {
        return ServiceResult<CustomizationRequestListResponseDto>.Failure(Error.NotFound(code, message));
    }

    private static ServiceResult<CustomizationRequestDto> NotFoundDetail(string code, string message)
    {
        return ServiceResult<CustomizationRequestDto>.Failure(Error.NotFound(code, message));
    }

    private static ServiceResult<CustomizationRequestDto> BadRequestDetail(string code, string message)
    {
        return ServiceResult<CustomizationRequestDto>.Failure(Error.BadRequest(code, message));
    }

    private static ServiceResult<CustomizationRequestDto> InvalidTransition(string message)
    {
        return BadRequestDetail(CustomizationRequestErrorCodes.InvalidCustomizationTransition, message);
    }

    private sealed record CustomizationUpdateContext(
        CustomizationRequest? Entity,
        CustomizationRequestDetailReadModel? Detail,
        ServiceResult<CustomizationRequestDto>? Error)
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

    private sealed record VersionMutationContextResult(
        CustomizationRequest? Entity,
        CustomizationRequestDetailReadModel? Detail,
        Guid? DesignerId,
        ServiceResult<CreateCustomizationRequestVersionResponseDto>? Error)
    {
        internal static VersionMutationContextResult Success(
            CustomizationRequest entity,
            CustomizationRequestDetailReadModel detail,
            Guid designerId)
        {
            return new VersionMutationContextResult(entity, detail, designerId, null);
        }

        internal static VersionMutationContextResult Failure(
            ServiceResult<CreateCustomizationRequestVersionResponseDto> error)
        {
            return new VersionMutationContextResult(null, null, null, error);
        }
    }

    private sealed record VersionDtoContextResult(
        CustomizationRequest? Entity,
        CustomizationRequestDetailReadModel? Detail,
        CustomizationRequestVersion? Version,
        ServiceResult<CustomizationRequestVersionDto>? Error)
    {
        internal static VersionDtoContextResult Success(
            CustomizationRequest entity,
            CustomizationRequestDetailReadModel detail,
            CustomizationRequestVersion version)
        {
            return new VersionDtoContextResult(entity, detail, version, null);
        }

        internal static VersionDtoContextResult Failure(ServiceResult<CustomizationRequestVersionDto> error)
        {
            return new VersionDtoContextResult(null, null, null, error);
        }
    }

    private sealed record SourceProductContext(ProductVersion SourceVersion, string ProjectCode);

    private sealed record SourceProductContextResult(
        SourceProductContext? Context,
        ServiceResult<CreateCustomizationRequestVersionResponseDto>? Error)
    {
        internal static SourceProductContextResult Success(SourceProductContext context)
        {
            return new SourceProductContextResult(context, null);
        }

        internal static SourceProductContextResult Failure(
            ServiceResult<CreateCustomizationRequestVersionResponseDto> error)
        {
            return new SourceProductContextResult(null, error);
        }
    }
}
