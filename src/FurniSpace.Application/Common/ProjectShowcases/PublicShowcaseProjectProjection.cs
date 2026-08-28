namespace FurniSpace.Application.Common.ProjectShowcases;

internal static class PublicShowcaseProjectProjection
{
    internal static DateOnly? ToCompletedDate(DateTime? completedAt)
    {
        if (!completedAt.HasValue)
        {
            return null;
        }

        var utcDate = completedAt.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(completedAt.Value, DateTimeKind.Utc).Date
            : completedAt.Value.ToUniversalTime().Date;

        return DateOnly.FromDateTime(utcDate);
    }

    internal static int? ToCompletionYear(DateTime? completedAt)
    {
        if (!completedAt.HasValue)
        {
            return null;
        }

        var utcDate = completedAt.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(completedAt.Value, DateTimeKind.Utc).Date
            : completedAt.Value.ToUniversalTime().Date;

        return utcDate.Year;
    }

    internal static int? ToImplementationDurationDays(DateTime? submittedAt, DateTime? completedAt)
    {
        if (!submittedAt.HasValue || !completedAt.HasValue)
        {
            return null;
        }

        var submittedDate = ToUtcDate(submittedAt.Value);
        var completedDate = ToUtcDate(completedAt.Value);
        return (completedDate - submittedDate).Days;
    }

    private static DateTime ToUtcDate(DateTime value)
    {
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc).Date
            : value.ToUniversalTime().Date;
    }
}
