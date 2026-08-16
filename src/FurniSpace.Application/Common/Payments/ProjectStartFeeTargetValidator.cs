using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Payments;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Domain.Entities;

namespace FurniSpace.Application.Common.Payments;

internal static class ProjectStartFeeTargetValidator
{
    internal static Error? ValidateCreateExpiry(
        DateTime? expiredAt,
        DateOnly? targetCompletionDate,
        DateTime utcNow)
    {
        if (!expiredAt.HasValue)
        {
            return null;
        }

        if (expiredAt.Value <= utcNow)
        {
            return Error.Validation(
                PaymentErrorCodes.PaymentExpired,
                "Project start fee expiry must be in the future.");
        }

        if (!targetCompletionDate.HasValue)
        {
            return null;
        }

        var expiryDate = DateOnly.FromDateTime(expiredAt.Value.ToUniversalTime());
        if (expiryDate > targetCompletionDate.Value)
        {
            return Error.Validation(
                PaymentErrorCodes.ProjectStartFeeExpiryExceedsTarget,
                "Project start fee expiry must not exceed project target completion date.");
        }

        return null;
    }

    internal static Error? ValidateTargetUpdateAgainstActiveStartFee(
        DateOnly? newTargetCompletionDate,
        Payment? projectStartFeePayment,
        DateTime utcNow)
    {
        if (!newTargetCompletionDate.HasValue ||
            projectStartFeePayment is null ||
            !ActivePaymentResolver.IsActive(projectStartFeePayment, utcNow) ||
            !projectStartFeePayment.ExpiredAt.HasValue)
        {
            return null;
        }

        var expiryDate = DateOnly.FromDateTime(projectStartFeePayment.ExpiredAt.Value.ToUniversalTime());
        if (expiryDate > newTargetCompletionDate.Value)
        {
            return Error.Conflict(
                ProjectErrorCodes.TargetDateConflictsWithActiveStartFee,
                "Target completion date cannot be earlier than the active project start fee expiry date.");
        }

        return null;
    }
}
