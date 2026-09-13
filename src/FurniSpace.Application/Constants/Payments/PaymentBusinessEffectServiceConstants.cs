using FurniSpace.Application.Common.Payments;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Constants.Payments;

internal static class PaymentBusinessEffectServiceConstants
{
    internal const string OrderReferenceType = "ORDER";
    internal const string OrderCodeParameter = "OrderCode";

    internal static readonly ProjectStatus[] ProjectStartFeeEligibleStatuses =
        ProjectStartFeeRules.PaymentCreationEligibleStatuses;
}
