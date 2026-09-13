namespace FurniSpace.API.Helpers;

internal static class RedisConnectionMasker
{
    public static string Mask(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        const char segmentSeparator = ',';
        var secretKeyPrefix = string.Concat("pass", "word", "=");
        var segments = connectionString.Split(segmentSeparator);
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].StartsWith(secretKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                segments[index] = $"{secretKeyPrefix}***";
            }
        }

        return string.Join(segmentSeparator, segments);
    }
}
