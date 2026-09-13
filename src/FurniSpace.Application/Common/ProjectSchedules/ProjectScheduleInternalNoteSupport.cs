namespace FurniSpace.Application.Common.ProjectSchedules;

internal static class ProjectScheduleInternalNoteSupport
{
    internal static string AppendSystemNote(string? existingNote, string systemNote)
    {
        if (string.IsNullOrWhiteSpace(systemNote))
        {
            return existingNote?.Trim() ?? string.Empty;
        }

        var trimmedExisting = existingNote?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedExisting))
        {
            return systemNote;
        }

        if (trimmedExisting.Contains(systemNote, StringComparison.Ordinal))
        {
            return trimmedExisting;
        }

        return $"{trimmedExisting}{Environment.NewLine}{systemNote}";
    }
}
