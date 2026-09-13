namespace FurniSpace.Application.DTOs.Financial;

public sealed class AdminFinancialExceptionsDto
{
    public List<AdminFinancialExceptionRowDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
