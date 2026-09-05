#nullable enable

using FurniSpace.Application.DTOs.ProductIssues;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FurniSpace.API.DTOs.ProductIssues;

public sealed class CreateProductIssueFormRequest
{
    public Guid OrderItemId { get; set; }
    public Guid? DeliveryItemId { get; set; }
    public DeliveryProductIssueType IssueType { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? AffectedQuantity { get; set; }
    public List<IFormFile>? Files { get; set; }

    public CreateProductIssueRequestDto ToRequestDto()
    {
        return new CreateProductIssueRequestDto
        {
            OrderItemId = OrderItemId,
            DeliveryItemId = DeliveryItemId,
            IssueType = IssueType,
            Description = Description,
            AffectedQuantity = AffectedQuantity,
            EvidenceFiles = (Files ?? [])
                .Where(file => file.Length > 0)
                .Select(file => new ProductIssueEvidenceUploadDto
                {
                    Content = file.OpenReadStream(),
                    OriginalFileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length
                })
                .ToList()
        };
    }
}
