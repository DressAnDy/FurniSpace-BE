namespace FurniSpace.Application.DTOs.Accounts;

public sealed class AvailableDesignerQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
}
