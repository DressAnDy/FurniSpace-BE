namespace FurniSpace.Application.DTOs.BusinessTypes;

public sealed class CreateBusinessTypeRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
