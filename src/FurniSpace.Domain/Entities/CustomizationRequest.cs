using System;
using System.Collections.Generic;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Domain.Entities;

public class CustomizationRequest
{
    public Guid CustomizationRequestId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid SourceProductVersionId { get; set; }
    public Guid? RequestedByCustomerId { get; set; }
    public string RequestTitle { get; set; } = null!;
    public string? RequestDescription { get; set; }
    public decimal? RequestedWidth { get; set; }
    public decimal? RequestedHeight { get; set; }
    public decimal? RequestedDepth { get; set; }
    public string? RequestedMaterial { get; set; }
    public string? RequestedColor { get; set; }
    public string? RequestedChangeNote { get; set; }
    public Guid? AcceptedRequestVersionId { get; set; }
    public CustomizationStatus? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CustomizationRequestVersion> Versions { get; set; } = new List<CustomizationRequestVersion>();
}
