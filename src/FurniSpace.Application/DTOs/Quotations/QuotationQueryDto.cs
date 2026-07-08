using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Quotations;

public sealed class QuotationQueryDto
{
    public QuotationStatus? Status { get; set; }
}
