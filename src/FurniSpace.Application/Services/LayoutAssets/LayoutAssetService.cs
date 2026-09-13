using FurniSpace.Application.Common;
using FurniSpace.Application.Common.LayoutAssets;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.Constants.LayoutAssets;
using FurniSpace.Application.DTOs.LayoutAssets;
using FurniSpace.Application.DTOs.Products;
using FurniSpace.Application.Interfaces.LayoutAssets;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using System.Text.RegularExpressions;
using static FurniSpace.Application.Constants.LayoutAssets.LayoutAssetServiceConstants;

namespace FurniSpace.Application.Services.LayoutAssets;

public sealed partial class LayoutAssetService : ILayoutAssetService
{
    private const string AssetCodeRequiredMessage = "Asset code is required.";
    private const string AssetNameRequiredMessage = "Asset name is required.";
    private static readonly Regex AssetCodePattern = new(
        "^[A-Z0-9_-]+$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private readonly ILayoutAssetRepository _layoutAssets;
    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly FileUploadSettings _uploadSettings;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public LayoutAssetService(
        ILayoutAssetRepository layoutAssets,
        IProjectFileRepository files,
        IUnitOfWork unitOfWork,
        LayoutAssetServiceDependencies dependencies)
    {
        _layoutAssets = layoutAssets;
        _files = files;
        _unitOfWork = unitOfWork;
        _storage = dependencies.Storage;
        _uploadSettings = dependencies.UploadSettings;
        _firebaseSettings = dependencies.FirebaseSettings;
    }

    public async Task<ServiceResult<LayoutAssetDto>> CreateAsync(
        CreateLayoutAssetRequestDto request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeAssetCode(request.AssetCode);
        var errors = ValidateCreateRequest(request, normalizedCode);
        if (errors.Count > 0)
        {
            return ServiceResult<LayoutAssetDto>.BadRequest(errors);
        }

        if (await _layoutAssets.AssetCodeExistsAsync(normalizedCode, cancellationToken))
        {
            return ServiceResult<LayoutAssetDto>.Conflict(LayoutAssetErrorCodes.CodeDuplicate);
        }

        var now = DateTime.UtcNow;
        var layoutAsset = new LayoutAsset
        {
            LayoutAssetId = Guid.NewGuid(),
            AssetCode = normalizedCode,
            AssetName = request.AssetName.Trim(),
            AssetType = request.AssetType,
            Description = NormalizeOptional(request.Description),
            Status = LayoutAssetStatus.ACTIVE,
            CreatedBy = currentUserId == Guid.Empty ? null : currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _layoutAssets.AddAsync(layoutAsset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<LayoutAssetDto>.Created(
            layoutAsset.Adapt<LayoutAssetDto>(),
            CreatedMessage);
    }

    public async Task<ServiceResult<LayoutAssetDto>> UpdateAsync(
        Guid layoutAssetId,
        UpdateLayoutAssetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (layoutAssetId == Guid.Empty)
        {
            return ServiceResult<LayoutAssetDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<LayoutAssetDto>.BadRequest(errors);
        }

        var layoutAsset = await _layoutAssets.GetForUpdateAsync(layoutAssetId, cancellationToken);
        if (layoutAsset is null)
        {
            return ServiceResult<LayoutAssetDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        layoutAsset.AssetName = request.AssetName.Trim();
        layoutAsset.AssetType = request.AssetType;
        layoutAsset.Description = NormalizeOptional(request.Description);
        layoutAsset.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<LayoutAssetDto>.Success(
            await BuildDetailDtoAsync(layoutAsset, cancellationToken),
            UpdatedMessage);
    }

    public async Task<ServiceResult<LayoutAssetDto>> UpdateStatusAsync(
        Guid layoutAssetId,
        UpdateLayoutAssetStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (layoutAssetId == Guid.Empty)
        {
            return ServiceResult<LayoutAssetDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        var layoutAsset = await _layoutAssets.GetForUpdateAsync(layoutAssetId, cancellationToken);
        if (layoutAsset is null)
        {
            return ServiceResult<LayoutAssetDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        if (!IsValidStatusTransition(layoutAsset.Status, request.Status))
        {
            return ServiceResult<LayoutAssetDto>.Failure(
                Error.BadRequest(
                    LayoutAssetErrorCodes.InvalidStatusTransition,
                    "Layout asset status transition is not allowed."));
        }

        layoutAsset.Status = request.Status;
        layoutAsset.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<LayoutAssetDto>.Success(
            await BuildDetailDtoAsync(layoutAsset, cancellationToken),
            StatusUpdatedMessage);
    }

    public async Task<ServiceResult<LayoutAssetListResponseDto>> GetAllAsync(
        LayoutAssetQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidatePagination(query.Page, query.PageSize, query.Search);
        if (errors.Count > 0)
        {
            return ServiceResult<LayoutAssetListResponseDto>.BadRequest(errors);
        }

        var search = NormalizeOptional(query.Search);
        var items = await _layoutAssets.GetPagedAsync(
            query.AssetType,
            query.Status,
            search,
            query.Page,
            query.PageSize,
            cancellationToken);
        var total = await _layoutAssets.CountAsync(
            query.AssetType,
            query.Status,
            search,
            cancellationToken);

        var responseItems = await EnrichListItemsAsync(items, cancellationToken);
        return ServiceResult<LayoutAssetListResponseDto>.Success(
            new LayoutAssetListResponseDto
            {
                Items = responseItems,
                Page = query.Page,
                PageSize = query.PageSize,
                Total = total
            },
            RetrievedMessage);
    }

    public async Task<ServiceResult<LayoutAssetDto>> GetByIdAsync(
        Guid layoutAssetId,
        string? roleName,
        CancellationToken cancellationToken = default)
    {
        if (layoutAssetId == Guid.Empty)
        {
            return ServiceResult<LayoutAssetDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        var layoutAsset = await _layoutAssets.GetByIdAsync(layoutAssetId, cancellationToken);
        if (layoutAsset is null)
        {
            return ServiceResult<LayoutAssetDto>.NotFound(LayoutAssetErrorCodes.NotFound);
        }

        return ServiceResult<LayoutAssetDto>.Success(
            await BuildDetailDtoAsync(layoutAsset, cancellationToken),
            DetailRetrievedMessage);
    }

    public async Task<ServiceResult<LayoutAssetListResponseDto>> GetRoomPlannerCatalogAsync(
        RoomPlannerLayoutAssetCatalogQueryDto query,
        string? roleName,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidatePagination(query.Page, query.PageSize, query.Search);
        if (errors.Count > 0)
        {
            return ServiceResult<LayoutAssetListResponseDto>.BadRequest(errors);
        }

        var statusFilter = ResolveCatalogStatusFilter(query.Status, roleName);
        if (statusFilter.Forbidden)
        {
            return ServiceResult<LayoutAssetListResponseDto>.Failure(
                Error.Forbidden(LayoutAssetErrorCodes.Forbidden, "You do not have access to this catalog filter."));
        }

        var search = NormalizeOptional(query.Search);
        var items = await _layoutAssets.GetPagedAsync(
            query.AssetType,
            statusFilter.Status,
            search,
            query.Page,
            query.PageSize,
            cancellationToken);
        var total = await _layoutAssets.CountAsync(
            query.AssetType,
            statusFilter.Status,
            search,
            cancellationToken);

        var responseItems = await EnrichListItemsAsync(items, cancellationToken);
        return ServiceResult<LayoutAssetListResponseDto>.Success(
            new LayoutAssetListResponseDto
            {
                Items = responseItems,
                Page = query.Page,
                PageSize = query.PageSize,
                Total = total
            },
            CatalogRetrievedMessage);
    }

    private async Task<IReadOnlyList<LayoutAssetDto>> EnrichListItemsAsync(
        IReadOnlyList<LayoutAsset> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var assetIds = items.Select(item => item.LayoutAssetId).ToList();
        var files = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.LayoutAsset,
            assetIds,
            customerVisibleOnly: false,
            cancellationToken);
        var filesByAsset = files.GroupBy(file => file.ReferenceId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return items.Select(item =>
        {
            var dto = item.Adapt<LayoutAssetDto>();
            if (filesByAsset.TryGetValue(item.LayoutAssetId, out var assetFiles))
            {
                ApplyFileSummaries(dto, assetFiles);
            }

            return dto;
        }).ToList();
    }

    private async Task<LayoutAssetDto> BuildDetailDtoAsync(
        LayoutAsset layoutAsset,
        CancellationToken cancellationToken)
    {
        var dto = layoutAsset.Adapt<LayoutAssetDto>();
        var files = await _files.GetCatalogFilesByReferencesAsync(
            CatalogFileReferenceTypes.LayoutAsset,
            [layoutAsset.LayoutAssetId],
            customerVisibleOnly: false,
            cancellationToken);
        ApplyFileSummaries(dto, files);
        return dto;
    }

    private static void ApplyFileSummaries(LayoutAssetDto dto, IReadOnlyList<CatalogFileReadModel> files)
    {
        dto.Files = LayoutAssetFileSummaryHelper.ToFileDtos(files);
        dto.PrimaryModel = LayoutAssetFileSummaryHelper.PickPrimary(files, FileType.MODEL_3D);
        dto.PrimaryTexture = LayoutAssetFileSummaryHelper.PickPrimary(files, FileType.TEXTURE);
        dto.PrimaryPreview = LayoutAssetFileSummaryHelper.PickPrimaryPreview(files);
    }

    private static (LayoutAssetStatus? Status, bool Forbidden) ResolveCatalogStatusFilter(
        LayoutAssetStatus? requestedStatus,
        string? roleName)
    {
        if (IsDesigner(roleName))
        {
            if (requestedStatus.HasValue && requestedStatus.Value != LayoutAssetStatus.ACTIVE)
            {
                return (null, true);
            }

            return (LayoutAssetStatus.ACTIVE, false);
        }

        return (requestedStatus, false);
    }

    private static bool IsDesigner(string? roleName)
    {
        return string.Equals(roleName, ApplicationRoles.Designer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidStatusTransition(LayoutAssetStatus current, LayoutAssetStatus next)
    {
        if (current == next)
        {
            return true;
        }

        return (current, next) switch
        {
            (LayoutAssetStatus.ACTIVE, LayoutAssetStatus.INACTIVE) => true,
            (LayoutAssetStatus.INACTIVE, LayoutAssetStatus.ACTIVE) => true,
            (LayoutAssetStatus.ACTIVE, LayoutAssetStatus.ARCHIVED) => true,
            (LayoutAssetStatus.INACTIVE, LayoutAssetStatus.ARCHIVED) => true,
            (LayoutAssetStatus.ARCHIVED, LayoutAssetStatus.ACTIVE) => true,
            _ => false
        };
    }

    private static List<string> ValidateCreateRequest(CreateLayoutAssetRequestDto request, string normalizedCode)
    {
        var errors = ValidateAssetCode(normalizedCode);
        errors.AddRange(ValidateAssetName(request.AssetName));
        return errors;
    }

    private static List<string> ValidateUpdateRequest(UpdateLayoutAssetRequestDto request)
    {
        return ValidateAssetName(request.AssetName);
    }

    private static List<string> ValidateAssetCode(string normalizedCode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            errors.Add(AssetCodeRequiredMessage);
            return errors;
        }

        if (normalizedCode.Length > 50)
        {
            errors.Add("Asset code must not exceed 50 characters.");
        }

        if (!AssetCodePattern.IsMatch(normalizedCode))
        {
            errors.Add("Asset code allows letters, numbers, hyphen, and underscore only.");
        }

        return errors;
    }

    private static List<string> ValidateAssetName(string assetName)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(assetName))
        {
            errors.Add(AssetNameRequiredMessage);
            return errors;
        }

        if (assetName.Trim().Length > 150)
        {
            errors.Add("Asset name must not exceed 150 characters.");
        }

        return errors;
    }

    private static List<string> ValidatePagination(int page, int pageSize, string? search)
    {
        var errors = new List<string>();
        if (page < 1)
        {
            errors.Add("Page must be greater than zero.");
        }

        if (pageSize is < 1 or > 100)
        {
            errors.Add("Page size must be between 1 and 100.");
        }

        if (search?.Trim().Length > 100)
        {
            errors.Add("Search must not exceed 100 characters.");
        }

        return errors;
    }

    private static string NormalizeAssetCode(string assetCode)
    {
        return string.IsNullOrWhiteSpace(assetCode)
            ? string.Empty
            : assetCode.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
