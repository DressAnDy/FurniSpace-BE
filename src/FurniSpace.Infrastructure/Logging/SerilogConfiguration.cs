using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Json;

namespace FurniSpace.Infrastructure.Logging;

public static class SerilogConfiguration
{
    public const string ApplicationName = "FurniSpace.API";

    private const string ConsoleOutputTemplate =
        "{Timestamp:HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] [CID:{CorrelationId}] {Message:lj}{NewLine}{Exception}";

    private const string FileOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Application}] [{SourceContext}] [CID:{CorrelationId}] [TraceId:{TraceId}] {Message:lj}{NewLine}{Exception}";

    public static Serilog.ILogger CreateLogger(
        IConfiguration configuration,
        bool useJsonFormatting)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", ApplicationName);

        if (useJsonFormatting)
        {
            var jsonFormatter = new JsonFormatter(renderMessage: true);

            loggerConfiguration
                .WriteTo.Console(jsonFormatter)
                .WriteTo.File(
                    jsonFormatter,
                    "logs/furnispace-.json",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30);
        }
        else
        {
            loggerConfiguration
                .WriteTo.Console(outputTemplate: ConsoleOutputTemplate)
                .WriteTo.File(
                    "logs/furnispace-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: FileOutputTemplate);
        }

        return loggerConfiguration.CreateLogger();
    }
}
