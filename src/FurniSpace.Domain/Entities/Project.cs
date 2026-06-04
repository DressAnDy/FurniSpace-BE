using System;

namespace FurniSpace.Domain.Entities;

public class Project
{
    public Guid ProjectId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedSalesId { get; set; }
    public Guid? AssignedDesignerId { get; set; }
    public string? ProjectCode { get; set; }
    public string ProjectName { get; set; } = null!;
    public string? BusinessType { get; set; }
    public string? ProjectAddress { get; set; }
    public string? BusinessPurpose { get; set; }
    public string? PreferredStyle { get; set; }
    public string? FurnitureRequirement { get; set; }
    public string? Description { get; set; }
    public decimal? TotalAreaSqm { get; set; }
    public int? NumberOfFloors { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public DateOnly? ExpectedStartDate { get; set; }
    public DateOnly? ExpectedCompletionDate { get; set; }
    public string? Status { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? SalesAssignedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DesignerAssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}


