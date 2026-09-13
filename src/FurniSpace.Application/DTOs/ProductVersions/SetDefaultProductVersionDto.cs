namespace FurniSpace.Application.DTOs.ProductVersions;

public sealed class SetDefaultProductVersionDto
{
    public Guid ProductVersionId { get; set; }
    public Guid ProductId { get; set; }
    public bool IsDefault { get; set; }
}
