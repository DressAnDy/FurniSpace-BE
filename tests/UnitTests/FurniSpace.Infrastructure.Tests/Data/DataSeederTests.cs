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
    public async Task SeedAsync_ExecutesAllSeedCommandsInWorkflowOrder()
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

        Assert.Equal(25, rawCommands.Count);
        Assert.Single(interpolatedCommands);
        AssertTablesAreSeededInOrder(rawCommands);

        var accountSeed = interpolatedCommands[0];
        Assert.Contains("INSERT INTO accounts", accountSeed.Format);
        Assert.Contains("customer@furnispace.local", accountSeed.Format);
        Assert.Contains("production@furnispace.local", accountSeed.Format);
        Assert.Contains("ON CONFLICT (email) DO UPDATE", accountSeed.Format);
        Assert.Equal(6, accountSeed.ArgumentCount);

        var businessTypeSeed = rawCommands.Single(command => ExtractTableName(command) == "business_types");
        Assert.Contains("CAFE", businessTypeSeed);
        Assert.Contains("FASHION_STORE", businessTypeSeed);
        Assert.Contains("ON CONFLICT (code) DO NOTHING", businessTypeSeed);
        Assert.Contains("pg_get_serial_sequence('business_types', 'id')", businessTypeSeed);

        var productSeed = rawCommands.Single(command => ExtractTableName(command) == "products");
        Assert.Contains("business_type_ids", productSeed);
        Assert.Contains("ARRAY[1, 2, 6]::integer[]", productSeed);
        Assert.Contains("ARRAY[5, 6, 9]::integer[]", productSeed);
        Assert.Contains("ARRAY[1, 2, 7]::integer[]", productSeed);
        Assert.Contains("ARRAY[3, 4, 5, 6]::integer[]", productSeed);
        Assert.Contains("ON CONFLICT (product_code) DO UPDATE", productSeed);
        Assert.Contains("COALESCE(products.business_type_ids, EXCLUDED.business_type_ids)", productSeed);
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
                    if (table == "project_schedules")
                    {
                        throw new InvalidOperationException("seed failed");
                    }

                    return Task.FromResult(1);
                },
                (_, _) => Task.FromResult(1)));

        Assert.Equal("seed failed", exception.Message);
        Assert.Equal(
            ["roles", "business_types", "categories", "products", "product_versions", "projects", "project_areas", "project_schedules"],
            executedTables);
    }

    private static void AssertTablesAreSeededInOrder(IReadOnlyList<string> rawCommands)
    {
        var tables = rawCommands.Select(ExtractTableName).ToArray();

        Assert.Equal(
            [
                "roles",
                "business_types",
                "categories",
                "products",
                "product_versions",
                "projects",
                "project_areas",
                "project_schedules",
                "files",
                "file_links",
                "project_chats",
                "project_chat_messages",
                "proposals",
                "proposal_scenes",
                "proposal_items",
                "proposal_scene_variants",
                "customization_requests",
                "quotations",
                "quotation_items",
                "orders",
                "order_items",
                "production_requests",
                "production_items",
                "notifications",
                "project_reviews"
            ],
            tables);

        foreach (var command in rawCommands)
        {
            Assert.Contains("ON CONFLICT", command);
        }
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
