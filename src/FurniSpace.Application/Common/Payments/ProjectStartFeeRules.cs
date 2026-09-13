using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Common.Payments;

public static class ProjectStartFeeRules
{
    public static readonly ProjectStatus[] PaymentCreationEligibleStatuses =
    [
        ProjectStatus.SUBMITTED,
        ProjectStatus.IN_CONSULTATION,
        ProjectStatus.NEED_BASIC_INFORMATION,
        ProjectStatus.QUOTATION_SENT,
        ProjectStatus.QUOTATION_REVISION_REQUESTED,
        ProjectStatus.ORDER_CONFIRMED
    ];

    public static readonly PaymentStatus[] CollectablePaymentStatuses =
    [
        PaymentStatus.PENDING,
        PaymentStatus.PROCESSING
    ];

    public static bool RequiresProjectStartFee => true;

    public static bool IsProjectStatusEligibleForPaymentCreation(ProjectStatus? status)
    {
        return status.HasValue && PaymentCreationEligibleStatuses.Contains(status.Value);
    }

    public static bool IsPaid(Payment? payment)
    {
        return payment?.Status == PaymentStatus.PAID;
    }

    public static bool IsEligibleForDesignerAssignment(Payment? projectStartFeePayment)
    {
        return !RequiresProjectStartFee || IsPaid(projectStartFeePayment);
    }

    public static ProjectStartFeeStatusDto BuildStatus(Guid projectId, Payment? payment)
    {
        return new ProjectStartFeeStatusDto
        {
            ProjectId = projectId,
            RequiresProjectStartFee = RequiresProjectStartFee,
            ProjectStartFeeStatus = payment?.Status,
            IsEligibleForDesignerAssignment = IsEligibleForDesignerAssignment(payment),
            PaymentId = payment?.PaymentId
        };
    }
}
