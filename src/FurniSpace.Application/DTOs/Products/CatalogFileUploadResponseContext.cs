using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;

namespace FurniSpace.Application.DTOs.Products;

public sealed record CatalogFileUploadResponseContext
{
    public required Guid FileId { get; init; }

    public required Guid FileLinkId { get; init; }

    public required string ReferenceType { get; init; }

    public required Guid ReferenceId { get; init; }

    public required string OriginalFileName { get; init; }

    public required UploadCatalogFileRequestDto Request { get; init; }

    public required StorageUploadResult UploadResult { get; init; }

    public required StoredFile StoredFile { get; init; }

    public required FileLink FileLink { get; init; }

    public required FileVisibility Visibility { get; init; }

    public required Guid CurrentUserId { get; init; }

    public required DateTime UploadedAt { get; init; }
}
