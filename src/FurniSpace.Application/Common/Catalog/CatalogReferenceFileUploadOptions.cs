using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Catalog;

public sealed class CatalogReferenceFileUploadOptions
{
    public required string ReferenceType { get; init; }

    public required Guid ReferenceId { get; init; }

    public required HashSet<FileType> AllowedFileTypes { get; init; }

    public required string StoragePrefixDefault { get; init; }

    public string? StoragePrefixConfigured { get; init; }

    public required string SuccessMessage { get; init; }
}
