using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Json;

namespace FurniSpace.Infrastructure.Logging;

public static class SerilogConfiguration
{
    public const string ApplicationName = "FurniSpace.API";

    private const string ConsoleOutputTemplate =
        "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

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

        loggerConfiguration.WriteTo.Console(outputTemplate: ConsoleOutputTemplate);

        if (useJsonFormatting)
        {
            // Console stays one line. The file keeps structured fields without a rendered duplicate.
            loggerConfiguration.WriteTo.File(
                new JsonFormatter(renderMessage: false),
                "logs/furnispace-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30);
        }
        else
        {
            loggerConfiguration.WriteTo.File(
                "logs/furnispace-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: FileOutputTemplate);
        }

        return loggerConfiguration.CreateLogger();
    }
}
