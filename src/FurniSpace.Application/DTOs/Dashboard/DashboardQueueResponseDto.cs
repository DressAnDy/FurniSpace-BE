namespace FurniSpace.Application.DTOs.Dashboard;

public sealed class DashboardQueueResponseDto
{
    public IReadOnlyList<DashboardQueueItemDto> Items { get; set; } = [];

    public IReadOnlyDictionary<string, int> CountsByGroup { get; set; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public int Page { get; set; }

    public int Limit { get; set; }

    public int Total { get; set; }
}
