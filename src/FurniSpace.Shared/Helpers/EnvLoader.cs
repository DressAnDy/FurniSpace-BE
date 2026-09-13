using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FurniSpace.Shared.Helpers;

public static class EnvLoader
{
    public static void LoadEnv(string fileName = ".env", bool required = true)
    {
        var filePath = ResolveEnvPath(fileName);

        if (filePath is null)
        {
            if (!required)
            {
                return;
            }

            throw new InvalidOperationException($"Required environment file not found: {fileName}");
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
            var value = ExpandVariables(line[(separatorIndex + 1)..].Trim());

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string ResolveEnvPath(string fileName)
    {
        if (Path.IsPathRooted(fileName))
        {
            return File.Exists(fileName) ? fileName : null;
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var filePath = Path.Combine(directory.FullName, fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ExpandVariables(string value)
    {
        return Regex.Replace(value, @"\$\{(?<key>[A-Za-z_][A-Za-z0-9_]*)\}", match =>
        {
            var key = match.Groups["key"].Value;
            return Environment.GetEnvironmentVariable(key) ?? match.Value;
        });
    }
}
