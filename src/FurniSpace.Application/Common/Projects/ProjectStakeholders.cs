namespace FurniSpace.Application.Common.Projects;

public sealed class ProjectStakeholders
{
    public Guid CustomerId { get; init; }
    public Guid? AssignedSalesId { get; init; }
    public Guid? AssignedDesignerId { get; init; }

    public IReadOnlyList<Guid> AllParticipantIds()
    {
        var ids = new HashSet<Guid> { CustomerId };
        if (AssignedSalesId.HasValue)
        {
            ids.Add(AssignedSalesId.Value);
        }

        if (AssignedDesignerId.HasValue)
        {
            ids.Add(AssignedDesignerId.Value);
        }

        return [.. ids];
    }

    public IReadOnlyList<Guid> CustomerAndSalesIds()
    {
        var ids = new List<Guid> { CustomerId };
        if (AssignedSalesId.HasValue && AssignedSalesId.Value != CustomerId)
        {
            ids.Add(AssignedSalesId.Value);
        }

        return ids;
    }
}
