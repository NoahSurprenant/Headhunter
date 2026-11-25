using Headhunter.API.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Headhunter.Tests.Telemetry;

public class DatabaseInstrumentationTests
{
    // Npgsql creates its activities only while something listens to its source; nothing else in the app does, so a listener on it
    // after the app's tracing is built shows Postgres queries are traced.
    [Test]
    public void Tracing_ListensToNpgsql()
    {
        var builder = Host.CreateApplicationBuilder();
        var telemetry = TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMetricsAndTracing(telemetry, builder.Configuration);

        using var host = builder.Build();
        using var source = new ActivitySource("Npgsql");

        _ = host.Services.GetRequiredService<TracerProvider>();

        Assert.That(source.HasListeners(), Is.True);
    }
}
