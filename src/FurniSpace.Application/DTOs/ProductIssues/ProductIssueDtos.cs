using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.ProductIssues;

public static class ProductIssueErrorCodes
{
    public const string OrderNotFound = "PRODUCT_ISSUE_ORDER_NOT_FOUND";
    public const string IssueNotFound = "PRODUCT_ISSUE_NOT_FOUND";
    public const string OrderItemNotFound = "PRODUCT_ISSUE_ORDER_ITEM_NOT_FOUND";
    public const string OrderItemOrderMismatch = "PRODUCT_ISSUE_ORDER_ITEM_ORDER_MISMATCH";
    public const string DeliveryItemNotFound = "PRODUCT_ISSUE_DELIVERY_ITEM_NOT_FOUND";
    public const string DeliveryItemOrderItemMismatch = "PRODUCT_ISSUE_DELIVERY_ITEM_ORDER_ITEM_MISMATCH";
    public const string NotDelivered = "PRODUCT_ISSUE_NOT_DELIVERED";
    public const string InvalidAffectedQuantity = "PRODUCT_ISSUE_INVALID_AFFECTED_QUANTITY";
    public const string Forbidden = "PRODUCT_ISSUE_FORBIDDEN";
    public const string InvalidRequest = "PRODUCT_ISSUE_INVALID_REQUEST";
    public const string FileUploadFailed = "PRODUCT_ISSUE_FILE_UPLOAD_FAILED";
}

public sealed class ProductIssueEvidenceUploadDto
{
    public Stream Content { get; set; } = Stream.Null;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
}

public sealed class CreateProductIssueRequestDto
{
    public Guid OrderItemId { get; set; }
    public Guid? DeliveryItemId { get; set; }
    public DeliveryProductIssueType IssueType { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? AffectedQuantity { get; set; }
    public IReadOnlyList<ProductIssueEvidenceUploadDto> EvidenceFiles { get; set; } = [];
}

public sealed class ProductIssueEvidenceFileDto
{
    public Guid FileId { get; set; }
    public Guid FileLinkId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
}

public sealed class ProductIssueReportDto
{
    public Guid DeliveryProductIssueReportId { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public string? ProductNameSnapshot { get; set; }
    public Guid? DeliveryItemId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? AffectedQuantity { get; set; }
    public Guid ReportedBy { get; set; }
    public string? ReporterName { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<ProductIssueEvidenceFileDto> EvidenceFiles { get; set; } = [];
}

public sealed class ProductIssueReportListResponseDto
{
    public IReadOnlyList<ProductIssueReportDto> Items { get; set; } = [];
}
