using System;

namespace FurniSpace.Domain.Entities;

public class ProjectReview
{
    public Guid ReviewId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public int? Rating { get; set; }
    public int? DesignQualityRating { get; set; }
    public int? ServiceQualityRating { get; set; }
    public int? DeliveryRating { get; set; }
    public string? Comment { get; set; }
    public bool AllowPublicDisplay { get; set; }
    public DateTime? PublicDisplayConsentAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


