using System;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class FileLink
{
    public Guid FileLinkId { get; set; }
    public Guid FileId { get; set; }
    public string ReferenceType { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public FileType? FileType { get; set; }
    public FileVisibility? Visibility { get; set; }
    public bool? IsPrimary { get; set; }
    public int? DisplayOrder { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
}
