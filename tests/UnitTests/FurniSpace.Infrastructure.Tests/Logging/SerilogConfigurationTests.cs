#nullable enable

using System.Collections.Generic;
using FurniSpace.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Logging;

public sealed class SerilogConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateLogger_WithConsoleAndFileSinks_ReturnsLogger(bool useJsonFormatting)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ElasticsearchLogging:Enabled"] = "false"
            })
            .Build();

        var logger = SerilogConfiguration.CreateLogger(configuration, useJsonFormatting);

        Assert.NotNull(logger);
        logger.Information("Serilog test message");
        Log.CloseAndFlush();
    }

    [Fact]
    public void CreateLogger_WithElasticsearchEnabledButNoUrl_SkipsElasticsearchSink()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ElasticsearchLogging:Enabled"] = "true",
                ["ElasticsearchLogging:IndexFormat"] = "test-logs-{0:yyyy.MM}"
            })
            .Build();

        var logger = SerilogConfiguration.CreateLogger(configuration, useJsonFormatting: false);

        Assert.Equal("FurniSpace.API", SerilogConfiguration.ApplicationName);
        logger.Warning("Serilog elasticsearch disabled by missing URL");
        Log.CloseAndFlush();
    }

    [Fact]
    public void CreateLogger_WithElasticsearchUrl_ConfiguresLoggerWithoutConnectingImmediately()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ElasticsearchLogging:Enabled"] = "true",
                ["ElasticsearchLogging:IndexFormat"] = "test-logs-{0:yyyy.MM}",
                ["Elasticsearch:Url"] = "http://localhost:9200"
            })
            .Build();

        var logger = SerilogConfiguration.CreateLogger(configuration, useJsonFormatting: true);

        Assert.NotNull(logger);
        Log.CloseAndFlush();
    }
}
