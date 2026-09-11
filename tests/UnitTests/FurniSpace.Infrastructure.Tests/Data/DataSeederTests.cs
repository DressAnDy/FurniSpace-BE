#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Data;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Data;

public sealed class DataSeederTests
{
    [Fact]
    public async Task SeedAsync_ExecutesRoleAndAccountSeedCommandsInOrder()
    {
        var rawCommands = new List<string>();
        var interpolatedCommands = new List<FormattableString>();
        using var cancellationSource = new CancellationTokenSource();
        var expectedToken = cancellationSource.Token;

        await DataSeeder.SeedAsync(
            (sql, cancellationToken) =>
            {
                Assert.Equal(expectedToken, cancellationToken);
                rawCommands.Add(Normalize(sql));
                return Task.FromResult(1);
            },
            (sql, cancellationToken) =>
            {
                Assert.Equal(expectedToken, cancellationToken);
                interpolatedCommands.Add(sql);
                return Task.FromResult(1);
            },
            expectedToken);

        Assert.Single(rawCommands);
        Assert.Single(interpolatedCommands);
        Assert.Equal("roles", ExtractTableName(rawCommands[0]));

        var rolesSeed = rawCommands[0];
        Assert.Contains("ADMIN", rolesSeed);
        Assert.Contains("SALES", rolesSeed);
        Assert.Contains("DESIGNER", rolesSeed);
        Assert.Contains("CUSTOMER", rolesSeed);
        Assert.Contains("PRODUCTION", rolesSeed);
        Assert.Contains("ON CONFLICT (role_name) DO NOTHING", rolesSeed);

        var accountSeed = interpolatedCommands[0];
        Assert.Contains("INSERT INTO accounts", accountSeed.Format);
        Assert.Contains("admin@furnispace.local", accountSeed.Format);
        Assert.Contains("customer@furnispace.local", accountSeed.Format);
        Assert.Contains("production@furnispace.local", accountSeed.Format);
        Assert.Contains("ON CONFLICT (email) DO UPDATE", accountSeed.Format);
        Assert.Equal(6, accountSeed.ArgumentCount);
    }

    [Fact]
    public async Task SeedAsync_WhenCommandFails_StopsAtFailingCommand()
    {
        var executedTables = new List<string>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DataSeeder.SeedAsync(
                (sql, _) =>
                {
                    var table = ExtractTableName(sql);
                    executedTables.Add(table);
                    if (table == "roles")
                    {
                        throw new InvalidOperationException("seed failed");
                    }

                    return Task.FromResult(1);
                },
                (_, _) => Task.FromResult(1)));

        Assert.Equal("seed failed", exception.Message);
        Assert.Equal(["roles"], executedTables);
    }

    private static string ExtractTableName(string sql)
    {
        var normalized = Normalize(sql);
        var start = normalized.IndexOf("INSERT INTO ", StringComparison.Ordinal) + "INSERT INTO ".Length;
        var end = normalized.IndexOf(' ', start);
        return normalized[start..end];
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
