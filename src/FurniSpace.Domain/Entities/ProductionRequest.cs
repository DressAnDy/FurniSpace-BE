using System;

namespace FurniSpace.Domain.Entities;

public class ProductionRequest
{
    public Guid ProductionRequestId { get; set; }
    public string? ProductionCode { get; set; }
    public Guid ProjectId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateOnly? EstimatedStartDate { get; set; }
    public DateOnly? EstimatedCompletionDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}


