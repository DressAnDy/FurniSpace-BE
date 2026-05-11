using System;
using System.IO;
using System.Linq;

namespace FurniSpace.Shared.Helpers;

public static class EnvLoader
{
    public static void LoadEnv(string fileName = ".env")
    {
        var filePath = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Directory.GetCurrentDirectory(), fileName);

        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"Required environment file not found: {filePath}");
        }

        var lines = File.ReadAllLines(filePath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .ToList();

        if (!lines.Any())
        {
            throw new InvalidOperationException($"Environment file '{filePath}' is empty.");
        }

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
