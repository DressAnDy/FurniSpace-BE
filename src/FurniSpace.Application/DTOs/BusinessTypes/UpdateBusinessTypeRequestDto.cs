namespace FurniSpace.Application.DTOs.BusinessTypes;

public sealed class UpdateBusinessTypeRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
