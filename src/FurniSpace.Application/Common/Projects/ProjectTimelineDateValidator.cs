using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.DTOs.ProjectSchedules;
using FurniSpace.Application.Services.Production;
using FurniSpace.Infrastructure.Repositories.IRepository;

namespace FurniSpace.Application.Common.Projects;

internal static class ProjectTimelineDateValidator
{
    internal static Error? ValidateTargetNotInPast(DateOnly? targetCompletionDate, DateOnly todayUtc)
    {
        if (targetCompletionDate.HasValue && targetCompletionDate.Value < todayUtc)
        {
            return Error.Validation(
                ProjectErrorCodes.InvalidTargetCompletionDate,
                "Target completion date must not be in the past.");
        }

        return null;
    }

    internal static Error? ValidateScheduleDateWithinTarget(
        DateTime scheduleDate,
        DateOnly? targetCompletionDate)
    {
        if (!targetCompletionDate.HasValue)
        {
            return null;
        }

        var scheduleDateOnly = DateOnly.FromDateTime(scheduleDate.ToUniversalTime());
        if (scheduleDateOnly > targetCompletionDate.Value)
        {
            return Error.Validation(
                ProjectScheduleErrorCodes.ScheduleDateExceedsTarget,
                "Schedule date must not exceed project target completion date.");
        }

        return null;
    }

    internal static Error? ValidateDateOnlyWithinTarget(
        DateOnly date,
        DateOnly? targetCompletionDate,
        string errorCode,
        string message)
    {
        if (!targetCompletionDate.HasValue)
        {
            return null;
        }

        if (date > targetCompletionDate.Value)
        {
            return Error.Validation(errorCode, message);
        }

        return null;
    }

    internal static async Task<Error?> ValidateTargetNotBeforeCommittedDatesAsync(
        Guid projectId,
        DateOnly? newTargetCompletionDate,
        IProjectScheduleRepository scheduleRepository,
        IProductionRequestRepository productionRequestRepository,
        CancellationToken cancellationToken)
    {
        if (!newTargetCompletionDate.HasValue)
        {
            return null;
        }

        var maxScheduleDate = await scheduleRepository.GetMaxOperationalScheduleDateAsync(
            projectId,
            cancellationToken);
        if (maxScheduleDate.HasValue && maxScheduleDate.Value > newTargetCompletionDate.Value)
        {
            return Error.Conflict(
                ProjectErrorCodes.TargetDateConflictsWithOperationalDates,
                "Target completion date cannot be earlier than existing project schedule dates.");
        }

        var maxProductionDate = await productionRequestRepository.GetMaxOperationalProductionDateAsync(
            projectId,
            cancellationToken);
        if (maxProductionDate.HasValue && maxProductionDate.Value > newTargetCompletionDate.Value)
        {
            return Error.Conflict(
                ProjectErrorCodes.TargetDateConflictsWithOperationalDates,
                "Target completion date cannot be earlier than existing production dates.");
        }

        return null;
    }

    internal static Error? ValidateProductionEstimatedDatesWithinTarget(
        DateOnly? estimatedStartDate,
        DateOnly? estimatedCompletionDate,
        DateOnly? targetCompletionDate)
    {
        if (estimatedStartDate.HasValue)
        {
            var startError = ValidateDateOnlyWithinTarget(
                estimatedStartDate.Value,
                targetCompletionDate,
                ProductionErrorCodes.ProductionDateExceedsTarget,
                "Estimated start date must not exceed project target completion date.");
            if (startError is not null)
            {
                return startError;
            }
        }

        if (estimatedCompletionDate.HasValue)
        {
            return ValidateDateOnlyWithinTarget(
                estimatedCompletionDate.Value,
                targetCompletionDate,
                ProductionErrorCodes.ProductionDateExceedsTarget,
                "Estimated completion date must not exceed project target completion date.");
        }

        return null;
    }
}
