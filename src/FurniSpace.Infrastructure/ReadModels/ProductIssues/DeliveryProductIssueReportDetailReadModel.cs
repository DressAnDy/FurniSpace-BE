namespace FurniSpace.Infrastructure.ReadModels.ProductIssues;

public sealed class DeliveryProductIssueReportDetailReadModel : DeliveryProductIssueReportListItemReadModel
{
    public string? ProjectName { get; init; }
    public string? ProductNameSnapshot { get; init; }
    public List<DeliveryProductIssueEvidenceReadModel> EvidenceFiles { get; set; } = [];
}

public sealed class DeliveryProductIssueEvidenceReadModel
{
    public Guid FileId { get; init; }
    public Guid FileLinkId { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string? FileUrl { get; init; }
    public string? MimeType { get; init; }
    public long? FileSizeBytes { get; init; }
}
