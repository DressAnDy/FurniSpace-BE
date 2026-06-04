using System;

namespace FurniSpace.Domain.Entities;

public class Quotation
{
    public Guid QuotationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProposalId { get; set; }
    public string QuotationCode { get; set; } = null!;
    public int? VersionNo { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? ServiceFee { get; set; }
    public decimal? CustomizationFee { get; set; }
    public decimal? DeliveryFee { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Status { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? CustomerNote { get; set; }
    public string? SalesNote { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


