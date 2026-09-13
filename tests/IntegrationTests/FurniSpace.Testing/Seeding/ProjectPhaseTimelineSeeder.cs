using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;

namespace FurniSpace.Testing.Seeding;

public static class ProjectPhaseTimelineSeeder
{
    public static void AddProductionDeadline(
        AppDbContext context,
        Guid projectId,
        Guid createdBy,
        int dueDaysFromNow = 30)
    {
        context.ProjectPhaseTimelineSet.Add(new ProjectPhaseTimeline
        {
            ProjectPhaseTimelineId = Guid.NewGuid(),
            ProjectId = projectId,
            Phase = ProjectPhaseType.PRODUCTION,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dueDaysFromNow)),
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = CoreAccountSeeder.FixedTimestamp,
            UpdatedAt = CoreAccountSeeder.FixedTimestamp
        });
    }
}
