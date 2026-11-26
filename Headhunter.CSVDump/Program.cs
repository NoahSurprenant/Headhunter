using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Headhunter.CSVDump;

public class Program
{
    public static void Main(string[] args)
    {
        // The image runs this from /app, so appsettings.json (log level Warning) sits next to the dll, not in the working directory.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddDbContextFactory<HeadhunterContext>(options =>
        {
            options.UseHeadhunterDatabase(builder.Configuration);
        });

        // The import's steps (ImportTelemetry) and its Npgsql commands, to the collector the standard OTEL_* variables name.
        if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("headhunter-import"))
                .WithTracing(tracing => tracing.AddSource(ImportTelemetry.Name).AddNpgsql())
                .WithMetrics(metrics => metrics.AddMeter(ImportTelemetry.Name))
                .UseOtlpExporter();
        }

        var host = builder.Build();
        host.Run();
    }
}
