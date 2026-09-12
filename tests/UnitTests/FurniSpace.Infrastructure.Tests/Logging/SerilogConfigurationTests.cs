#nullable enable

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
        var configuration = new ConfigurationBuilder().Build();

        var logger = SerilogConfiguration.CreateLogger(configuration, useJsonFormatting);

        Assert.NotNull(logger);
        logger.Information("Serilog test message");
        Log.CloseAndFlush();
    }
}
