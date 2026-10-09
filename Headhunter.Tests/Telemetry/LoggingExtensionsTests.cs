using Headhunter.API.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Headhunter.Tests.Telemetry;

[TestFixture]
[NonParallelizable] // AddSerilogLogging replaces the static Log.Logger.
public class LoggingExtensionsTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        environment.ApplicationName.Returns("Headhunter.API");
        return environment;
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static TelemetryOptions Telemetry(IConfiguration configuration, string environment = "Production") =>
        TelemetryOptions.FromConfiguration(configuration, Environment(environment));

    /// <summary>
    /// A sink added the way an environment variable would add one: <c>Serilog:Using</c> names this
    /// assembly and <c>Serilog:WriteTo</c> the <see cref="ConfiguredSinkExtensions.Configured"/> method.
    /// </summary>
    private static Dictionary<string, string?> ConfiguredSinkSettings(string key) => new()
    {
        ["Serilog:MinimumLevel:Default"] = "Information",
        ["Serilog:Using:0"] = typeof(ConfiguredSinkExtensions).Assembly.GetName().Name,
        ["Serilog:WriteTo:0:Name"] = nameof(ConfiguredSinkExtensions.Configured),
        ["Serilog:WriteTo:0:Args:key"] = key,
    };

    [Test]
    public void CreateBootstrapLogger_WritesWithoutThrowing()
    {
        var logger = LoggingExtensions.CreateBootstrapLogger();
        Assert.DoesNotThrow(() => logger.Information("Starting up"));
        logger.Dispose();
    }

    [Test]
    public void ConfigureLogging_SinkFromConfiguration_ReceivesEvents_AlongsideTheCodeSinks()
    {
        var key = Guid.NewGuid().ToString("N");
        var configuration = Configuration(new(ConfiguredSinkSettings(key))
        {
            ["Serilog:Properties:Application"] = "headhunter",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:9",
        });
        var codeSink = new CollectingSink();

        using (var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), Telemetry(configuration))
            .WriteTo.Sink(codeSink)
            .CreateLogger())
        {
            logger.Information("hello {Name}", "headhunter");
            logger.Debug("below the configured minimum");
        }

        var configured = ConfiguredSinkExtensions.Sink(key).Events;
        Assert.Multiple(() =>
        {
            Assert.That(configured.Select(e => e.MessageTemplate.Text), Is.EqualTo(new[] { "hello {Name}" }));
            Assert.That(configured[0].Properties["Application"].ToString(), Is.EqualTo("\"headhunter\""));
            Assert.That(codeSink.Events, Has.Count.EqualTo(1), "configuration adds sinks, it does not replace them");
        });
    }

    [Test]
    public void ConfigureLogging_SinkFromConfiguration_HonoursMinimumLevelOverrides()
    {
        var key = Guid.NewGuid().ToString("N");
        var configuration = Configuration(new(ConfiguredSinkSettings(key))
        {
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:MinimumLevel:Override:Headhunter"] = "Debug",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Error",
        });

        using (var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), Telemetry(configuration))
            .CreateLogger())
        {
            logger.ForContext(Constants.SourceContextPropertyName, "Headhunter.API.Thing").Debug("app debug");
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing").Warning("framework warning");
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing").Error("framework error");
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing").Information("other info");
        }

        Assert.That(ConfiguredSinkExtensions.Sink(key).Events.Select(e => e.MessageTemplate.Text),
            Is.EqualTo(new[] { "app debug", "framework error" }));
    }

    [Test]
    public void ConfigureLogging_UsingAnAssemblyThatIsNotShipped_StopsStartup()
    {
        // A sink configured from the environment must ship in the image, or the app does not start.
        var configuration = Configuration(new()
        {
            ["Serilog:Using:1"] = "Serilog.Sinks.Grafana.Loki",
            ["Serilog:WriteTo:1:Name"] = "GrafanaLoki",
            ["Serilog:WriteTo:1:Args:uri"] = "http://loki-gateway.loki.svc.cluster.local",
        });

        Assert.That(() => new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), Telemetry(configuration))
            .CreateLogger(), Throws.Exception);
    }

    [TestCase("Development", null, null)]
    [TestCase("Development", "http://127.0.0.1:9", null)]
    [TestCase("Production", null, null)]
    [TestCase("Production", "http://127.0.0.1:9", null)]
    [TestCase("Production", "http://127.0.0.1:9", "http/protobuf")]
    public void ConfigureLogging_EachBranchBuildsAWorkingLogger(string environment, string? endpoint, string? protocol)
    {
        var configuration = Configuration(new()
        {
            ["Serilog:MinimumLevel:Default"] = "Information",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = protocol,
        });
        var sink = new CollectingSink();

        using var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment(environment), Telemetry(configuration, environment))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("hello {Name}", "headhunter");
        logger.Debug("filtered by the configured minimum level");

        Assert.That(sink.Events.Select(e => e.MessageTemplate.Text), Is.EqualTo(new[] { "hello {Name}" }));
    }

    [Test]
    public void ConfigureLogging_ReadsLevelsFromConfiguration_AndEnrichesFromLogContext()
    {
        var configuration = Configuration(new()
        {
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:MinimumLevel:Override:Headhunter"] = "Debug",
        });
        var sink = new CollectingSink();

        using var logger = new LoggerConfiguration()
            .ConfigureLogging(configuration, Environment("Production"), Telemetry(configuration), new ServiceCollection().BuildServiceProvider())
            .WriteTo.Sink(sink)
            .CreateLogger();

        using (Serilog.Context.LogContext.PushProperty("ClientIp", "203.0.113.7"))
        {
            logger.ForContext(Constants.SourceContextPropertyName, "Headhunter.API.Thing").Debug("app debug");
            logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing").Information("framework info");
        }

        Assert.That(sink.Events, Has.Count.EqualTo(1));
        Assert.That(sink.Events[0].Properties["ClientIp"].ToString(), Is.EqualTo("\"203.0.113.7\""));
    }

    [Test]
    public void AddSerilogLogging_ReplacesTheLoggerProvider()
    {
        var key = Guid.NewGuid().ToString("N");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(ConfiguredSinkSettings(key));

        builder.AddSerilogLogging(TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment));

        using (var app = builder.Build())
        {
            var factory = app.Services.GetRequiredService<ILoggerFactory>();
            Assert.That(factory.GetType().FullName, Does.Contain("Serilog"));
            factory.CreateLogger("test").LogInformation("hello");
        }

        Assert.That(ConfiguredSinkExtensions.Sink(key).Events.Select(e => e.MessageTemplate.Text), Does.Contain("hello"));
    }
}

/// <summary>A sink Serilog.Settings.Configuration can find by name; events land in <see cref="Sink"/>(key).</summary>
public static class ConfiguredSinkExtensions
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CollectingEventSink> Sinks = new();

    public static CollectingEventSink Sink(string key) => Sinks.GetOrAdd(key, _ => new CollectingEventSink());

    public static LoggerConfiguration Configured(this Serilog.Configuration.LoggerSinkConfiguration writeTo, string key) =>
        writeTo.Sink(Sink(key));
}

public sealed class CollectingEventSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];

    public IReadOnlyList<LogEvent> Events
    {
        get { lock (_events) return [.. _events]; }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_events) _events.Add(logEvent);
    }
}
