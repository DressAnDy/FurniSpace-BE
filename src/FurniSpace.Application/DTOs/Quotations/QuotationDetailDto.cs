namespace FurniSpace.Application.DTOs.Quotations;

public sealed class QuotationDetailDto : QuotationDto
{
    public IReadOnlyList<QuotationItemDto> Items { get; set; } = [];
}
