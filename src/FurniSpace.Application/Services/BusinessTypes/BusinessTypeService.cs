using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.BusinessTypes;
using FurniSpace.Application.Interfaces.BusinessTypes;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;

namespace FurniSpace.Application.Services.BusinessTypes;

public sealed class BusinessTypeService : IBusinessTypeService
{
    private const string RetrievedMessage = "Business Types retrieved successfully.";
    private const string DetailRetrievedMessage = "Business Type retrieved successfully.";
    private const string NotFoundCode = "BUSINESS_TYPE_NOT_FOUND";
    private readonly IBusinessTypeRepository _businessTypes;

    public BusinessTypeService(IBusinessTypeRepository businessTypes)
    {
        _businessTypes = businessTypes;
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
