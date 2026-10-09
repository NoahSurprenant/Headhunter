using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Headhunter.API.Telemetry;

public static class MetricsAndTracingExtensions
{
    /// <summary>Meters beyond the instrumentation packages' own.</summary>
    public static readonly string[] AdditionalMeters =
    [
        "Microsoft.EntityFrameworkCore",
        // dotnet.health_check.*, from AddTelemetryHealthCheckPublisher.
        "Microsoft.Extensions.Diagnostics.HealthChecks",
    ];

    /// <summary>
    /// Metrics and traces, pushed over OTLP to the collector when an endpoint is set; nothing is
    /// scraped from the app. Health probes are left out of the traces.
    /// </summary>
    public static IServiceCollection AddMetricsAndTracing(this IServiceCollection services, TelemetryOptions telemetry, IConfiguration configuration)
    {
        var healthPaths = HealthCheckPaths.FromConfiguration(configuration);
        services.AddOpenTelemetry()
            .ConfigureResource(resource => telemetry.ConfigureResource(resource))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddMeter(AdditionalMeters);

                if (telemetry.SignalEndpoint("metrics") is { } endpoint)
                {
                    metrics.AddOtlpExporter((exporter, reader) =>
                    {
                        ConfigureExporter(exporter, endpoint, telemetry.Protocol);
                        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = (int)telemetry.MetricExportInterval.TotalMilliseconds;
                    });
                }
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options => options.Filter = context => !healthPaths.IsHealthPath(context.Request.Path))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (telemetry.SignalEndpoint("traces") is { } endpoint)
                {
                    tracing.AddOtlpExporter(exporter => ConfigureExporter(exporter, endpoint, telemetry.Protocol));
                }

                if (configuration.GetValue<bool>("OTLP_CONSOLE_EXPORTER_ENABLED"))
                {
                    tracing.AddConsoleExporter();
                }
            });

        return services;
    }

    public static void ConfigureExporter(OtlpExporterOptions exporter, Uri endpoint, OtlpWireProtocol protocol)
    {
        exporter.Endpoint = endpoint;
        exporter.Protocol = protocol == OtlpWireProtocol.Grpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
    }
}
