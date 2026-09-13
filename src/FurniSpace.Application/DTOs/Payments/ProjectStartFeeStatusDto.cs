using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Payments;

public sealed class ProjectStartFeeStatusDto
{
    public Guid ProjectId { get; set; }
    public bool RequiresProjectStartFee { get; set; }
    public PaymentStatus? ProjectStartFeeStatus { get; set; }
    public bool IsEligibleForDesignerAssignment { get; set; }
    public Guid? PaymentId { get; set; }
}
