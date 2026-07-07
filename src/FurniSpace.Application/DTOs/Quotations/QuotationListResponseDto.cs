namespace FurniSpace.Application.DTOs.Quotations;

public sealed class QuotationListResponseDto
{
    public IReadOnlyList<QuotationDto> Items { get; set; } = [];
}
