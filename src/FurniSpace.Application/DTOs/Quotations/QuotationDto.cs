using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Quotations;

public class QuotationDto
{
    public Guid QuotationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProposalId { get; set; }
    public string QuotationCode { get; set; } = string.Empty;
    public int? VersionNo { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public QuotationStatus? Status { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? CustomerNote { get; set; }
    public string? SalesNote { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
