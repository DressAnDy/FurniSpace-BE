using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.BusinessTypes;
using FurniSpace.Application.Interfaces.BusinessTypes;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using System.Text.RegularExpressions;

namespace FurniSpace.Application.Services.BusinessTypes;

public sealed class BusinessTypeService : IBusinessTypeService
{
    private const string CodeAlreadyExistsCode = "BUSINESS_TYPE_CODE_ALREADY_EXISTS";
    private const string CodeRequiredMessage = "Business type code is required.";
    private const string CreatedMessage = "Business Type created successfully.";
    private const string RetrievedMessage = "Business Types retrieved successfully.";
    private const string DetailRetrievedMessage = "Business Type retrieved successfully.";
    private const string NotFoundCode = "BUSINESS_TYPE_NOT_FOUND";
    private const string StatusUpdatedMessage = "Business Type status updated successfully.";
    private const string UpdatedMessage = "Business Type updated successfully.";
    private const string NameRequiredMessage = "Business type name is required.";
    private static readonly Regex CodePattern = new("^[A-Z0-9_]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private readonly IBusinessTypeRepository _businessTypes;
    private readonly IUnitOfWork _unitOfWork;

    public BusinessTypeService(IBusinessTypeRepository businessTypes, IUnitOfWork unitOfWork)
    {
        _businessTypes = businessTypes;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<BusinessTypeDto>> CreateAsync(
        CreateBusinessTypeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(request.Code);
        var errors = ValidateCreateRequest(request, normalizedCode);
        if (errors.Count > 0)
        {
            return ServiceResult<BusinessTypeDto>.BadRequest(errors);
        }

        if (await _businessTypes.CodeExistsAsync(normalizedCode, cancellationToken))
        {
            return ServiceResult<BusinessTypeDto>.Conflict(CodeAlreadyExistsCode);
        }

        var now = DateTime.UtcNow;
        var businessType = new BusinessType
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Status = true,
            CreatedAt = now,
            UpdatedAt = null
        };

        await _businessTypes.AddAsync(businessType, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<BusinessTypeDto>.Created(businessType.Adapt<BusinessTypeDto>(), CreatedMessage);
    }

    public async Task<ServiceResult<BusinessTypeDto>> UpdateAsync(
        int businessTypeId,
        UpdateBusinessTypeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (businessTypeId <= 0)
        {
            return ServiceResult<BusinessTypeDto>.NotFound(NotFoundCode);
        }

        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
        {
            return ServiceResult<BusinessTypeDto>.BadRequest(errors);
        }

        var businessType = await _businessTypes.GetForUpdateAsync(businessTypeId, cancellationToken);
        if (businessType is null)
        {
            return ServiceResult<BusinessTypeDto>.NotFound(NotFoundCode);
        }

        businessType.Name = request.Name.Trim();
        businessType.Description = NormalizeOptional(request.Description);
        businessType.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<BusinessTypeDto>.Success(businessType.Adapt<BusinessTypeDto>(), UpdatedMessage);
    }

    public async Task<ServiceResult<BusinessTypeDto>> UpdateStatusAsync(
        int businessTypeId,
        UpdateBusinessTypeStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (businessTypeId <= 0)
        {
            return ServiceResult<BusinessTypeDto>.NotFound(NotFoundCode);
        }

        var businessType = await _businessTypes.GetForUpdateAsync(businessTypeId, cancellationToken);
        if (businessType is null)
        {
            return ServiceResult<BusinessTypeDto>.NotFound(NotFoundCode);
        }

        businessType.Status = request.Status;
        businessType.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<BusinessTypeDto>.Success(
            businessType.Adapt<BusinessTypeDto>(),
            StatusUpdatedMessage);
    }

    public async Task<ServiceResult<BusinessTypeListResponseDto>> GetAllAsync(
        BusinessTypeQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateQuery(query);
        if (errors.Count > 0)
        {
            return ServiceResult<BusinessTypeListResponseDto>.BadRequest(errors);
        }

        var status = query.Status ?? true;
        var keyword = NormalizeOptional(query.Keyword);
        var items = await _businessTypes.GetPagedAsync(
            status,
            keyword,
            query.Page,
            query.Limit,
            cancellationToken);
        var total = await _businessTypes.CountAsync(status, keyword, cancellationToken);
        var response = new BusinessTypeListResponseDto
        {
            Items = items.Adapt<List<BusinessTypeDto>>(),
            Page = query.Page,
            Limit = query.Limit,
            Total = total
        };

        return ServiceResult<BusinessTypeListResponseDto>.Success(response, RetrievedMessage);
    }

    public async Task<ServiceResult<BusinessTypeDto>> GetByIdAsync(
        int businessTypeId,
        CancellationToken cancellationToken = default)
    {
        if (businessTypeId <= 0)
        {
            return ServiceResult<BusinessTypeDto>.NotFound(NotFoundCode);
        }

        var businessType = await _businessTypes.GetByIdAsync(businessTypeId, cancellationToken);
        return businessType is null
            ? ServiceResult<BusinessTypeDto>.NotFound(NotFoundCode)
            : ServiceResult<BusinessTypeDto>.Success(businessType.Adapt<BusinessTypeDto>(), DetailRetrievedMessage);
    }

    private static List<string> ValidateQuery(BusinessTypeQueryDto query)
    {
        var errors = new List<string>();
        if (query.Page < 1)
        {
            errors.Add("Page must be greater than zero.");
        }

        if (query.Limit is < 1 or > 100)
        {
            errors.Add("Limit must be between 1 and 100.");
        }

        if (query.Keyword?.Trim().Length > 100)
        {
            errors.Add("Keyword must not exceed 100 characters.");
        }

        return errors;
    }

    private static List<string> ValidateCreateRequest(CreateBusinessTypeRequestDto request, string normalizedCode)
    {
        var errors = ValidateCode(normalizedCode);
        errors.AddRange(ValidateName(request.Name));

        return errors;
    }

    private static List<string> ValidateUpdateRequest(UpdateBusinessTypeRequestDto request)
    {
        return ValidateName(request.Name);
    }

    private static List<string> ValidateCode(string normalizedCode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            errors.Add(CodeRequiredMessage);
            return errors;
        }

        if (normalizedCode.Length > 50)
        {
            errors.Add("Business type code must not exceed 50 characters.");
        }

        if (!CodePattern.IsMatch(normalizedCode))
        {
            errors.Add("Business type code allows letters, numbers, and underscore only.");
        }

        return errors;
    }

    private static List<string> ValidateName(string name)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(NameRequiredMessage);
            return errors;
        }

        if (name.Trim().Length > 150)
        {
            errors.Add("Business type name must not exceed 150 characters.");
        }

        return errors;
    }

    private static string NormalizeCode(string code)
    {
        return string.IsNullOrWhiteSpace(code)
            ? string.Empty
            : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
