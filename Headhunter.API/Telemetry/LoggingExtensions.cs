using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Settings.Configuration;
using Serilog.Sinks.OpenTelemetry;

namespace Headhunter.API.Telemetry;

public static class LoggingExtensions
{
    /// <summary>
    /// The logger used until the host is built, and for a crash before that: console only, compact JSON.
    /// </summary>
    public static Serilog.Extensions.Hosting.ReloadableLogger CreateBootstrapLogger() => new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new RenderedCompactJsonFormatter())
        .CreateBootstrapLogger();

    /// <summary>Replaces the bootstrap logger with the configured one once the host is built.</summary>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder, TelemetryOptions telemetry)
    {
        builder.Services.AddSerilog((services, configuration) => configuration
            .ConfigureLogging(builder.Configuration, builder.Environment, telemetry, services));
        return builder;
    }

    /// <summary>
    /// Compact JSON on the console in every environment (what kubectl logs and Alloy see), plus the debug
    /// output in Development; when an endpoint is set, OTLP to the collector with the same resource as
    /// the metrics and traces (in Development too, e.g. a local Aspire dashboard). Each event carries its
    /// trace and span id natively, so local output shows them as @tr and @sp.
    /// </summary>
    /// <remarks>
    /// The whole <c>Serilog</c> section is read, so configuration can add sinks on top of these, e.g.
    /// <c>Serilog__Using__0=Serilog.Sinks.File</c>, <c>Serilog__WriteTo__0__Name=File</c> and
    /// <c>Serilog__WriteTo__0__Args__path=...</c> from the environment. Do not configure a Console sink
    /// there: it would print every line twice. A <c>Using</c> naming an assembly the image does not ship
    /// stops the app at startup, so drop such settings before deploying an image without that sink.
    /// </remarks>
    public static LoggerConfiguration ConfigureLogging(this LoggerConfiguration logger, IConfiguration configuration,
        IHostEnvironment environment, TelemetryOptions telemetry, IServiceProvider? services = null)
    {
        logger.ReadFrom.Configuration(configuration, new ConfigurationReaderOptions { SectionName = "Serilog" });
        if (services is not null)
            logger.ReadFrom.Services(services);

        logger.Enrich.FromLogContext();

        // JSON everywhere, Development included, so every line carries its trace and span ids (@tr, @sp).
        var json = new RenderedCompactJsonFormatter();
        logger.WriteTo.Console(json);
        if (environment.IsDevelopment())
            logger.WriteTo.Debug(json);

        var endpoint = telemetry.SignalEndpoint("logs");
        if (endpoint is not null)
        {
            logger.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = endpoint.AbsoluteUri;
                options.Protocol = telemetry.Protocol == OtlpWireProtocol.Grpc ? OtlpProtocol.Grpc : OtlpProtocol.HttpProtobuf;
                options.ResourceAttributes = new Dictionary<string, object>(telemetry.ResourceAttributes);
                // The sink's own export requests would otherwise show up as HttpClient spans.
                options.OnBeginSuppressInstrumentation = OpenTelemetry.SuppressInstrumentationScope.Begin;
            }, ignoreEnvironment: true);
        }

        return logger;
    }
}
