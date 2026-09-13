namespace FurniSpace.Application.DTOs.BusinessTypes;

public sealed class BusinessTypeQueryDto
{
    public bool? Status { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
