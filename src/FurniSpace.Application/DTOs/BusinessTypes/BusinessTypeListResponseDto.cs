namespace FurniSpace.Application.DTOs.BusinessTypes;

public sealed class BusinessTypeListResponseDto
{
    public List<BusinessTypeDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
