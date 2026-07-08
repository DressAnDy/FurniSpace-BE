using System;

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class ProductionCustomizationRequestQueryDto
{
    public string? Status { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProposalId { get; set; }
    public bool? MaterialAvailable { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
