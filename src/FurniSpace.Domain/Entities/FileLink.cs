using System;

namespace FurniSpace.Domain.Entities;

public class FileLink
{
    public Guid FileLinkId { get; set; }
    public Guid FileId { get; set; }
    public string OwnerType { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public string? FileType { get; set; }
    public string? Visibility { get; set; }
    public string? Description { get; set; }
    public DateTime? CreatedAt { get; set; }
}


