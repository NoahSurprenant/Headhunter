using Headhunter.API.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenTelemetry.Resources;

namespace Headhunter.Tests.Telemetry;

[TestFixture]
public class TelemetryOptionsTests
{
    private static IHostEnvironment Environment(string name = "Production")
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        environment.ApplicationName.Returns("Headhunter.API");
        return environment;
    }

    private static TelemetryOptions From(Dictionary<string, string?> values, string? version = null, string environment = "Production") =>
        TelemetryOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), Environment(environment), version);

    [Test]
    public void Defaults_WithNothingConfigured_ExportOff()
    {
        var options = From(new() { ["HOSTNAME"] = "headhunter-abc" });

        Assert.Multiple(() =>
        {
            Assert.That(options.ServiceName, Is.EqualTo("Headhunter.API"));
            Assert.That(options.Environment, Is.EqualTo("production"));
            Assert.That(options.ServiceInstanceId, Is.EqualTo("headhunter-abc"));
            Assert.That(options.ServiceVersion, Is.Null);
            Assert.That(options.OtlpEndpoint, Is.Null);
            Assert.That(options.ExportEnabled, Is.False);
            Assert.That(options.Protocol, Is.EqualTo(OtlpWireProtocol.Grpc));
            Assert.That(options.MetricExportInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.SignalEndpoint("traces"), Is.Null);
            Assert.That(options.ResourceAttributes.ContainsKey("service.version"), Is.False);
        });
    }

    [Test]
    public void InstanceId_FallsBackToMachineName()
    {
        Assert.That(From(new()).ServiceInstanceId, Is.EqualTo(System.Environment.MachineName));
    }

    [Test]
    public void StandardVariables_SetNameEnvironmentEndpointAndResource()
    {
        var options = From(new()
        {
            ["OTEL_SERVICE_NAME"] = "headhunter",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "deployment.environment.name=prod, team=head%20hunter,broken,=novalue,empty=",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://otel-collector.opentelemetry.svc.cluster.local:4317",
            ["HOSTNAME"] = "headhunter-1",
        }, version: "abc1234");

        Assert.Multiple(() =>
        {
            Assert.That(options.ServiceName, Is.EqualTo("headhunter"));
            Assert.That(options.Environment, Is.EqualTo("prod"));
            Assert.That(options.ServiceVersion, Is.EqualTo("abc1234"));
            Assert.That(options.OtlpEndpoint, Is.EqualTo(new Uri("http://otel-collector.opentelemetry.svc.cluster.local:4317")));
            Assert.That(options.ResourceAttributes, Is.EquivalentTo(new Dictionary<string, object>
            {
                ["service.name"] = "headhunter",
                ["service.instance.id"] = "headhunter-1",
                ["service.version"] = "abc1234",
                ["deployment.environment.name"] = "prod",
                ["team"] = "head hunter",
            }));
        });
    }

    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("not a uri", null)]
    [TestCase("ftp://collector:4317", null)]
    [TestCase("https://collector:4318", "https://collector:4318/")]
    public void ParseEndpoint(string? value, string? expected)
    {
        Assert.That(TelemetryOptions.ParseEndpoint(value)?.AbsoluteUri, Is.EqualTo(expected));
    }

    [TestCase(null, OtlpWireProtocol.Grpc)]
    [TestCase("grpc", OtlpWireProtocol.Grpc)]
    [TestCase(" HTTP/Protobuf ", OtlpWireProtocol.HttpProtobuf)]
    [TestCase("http/json", OtlpWireProtocol.Grpc)]
    public void ParseProtocol(string? value, OtlpWireProtocol expected)
    {
        Assert.That(TelemetryOptions.ParseProtocol(value), Is.EqualTo(expected));
    }

    [TestCase(null, 30_000)]
    [TestCase("not a number", 30_000)]
    [TestCase("NaN", 30_000)]
    [TestCase("1000", 5_000)]
    [TestCase("-5", 5_000)]
    [TestCase("15000", 15_000)]
    [TestCase("600000", 300_000)]
    [TestCase("Infinity", 300_000)]
    public void MetricExportInterval_IsClampedToFiveSecondsToFiveMinutes(string? value, double expectedMilliseconds)
    {
        Assert.That(TelemetryOptions.ParseMetricExportInterval(value).TotalMilliseconds, Is.EqualTo(expectedMilliseconds));
    }

    [Test]
    public void MetricExportInterval_FromConfiguration()
    {
        Assert.That(From(new() { ["OTEL_METRIC_EXPORT_INTERVAL"] = "10000" }).MetricExportInterval, Is.EqualTo(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void SignalEndpoint_Grpc_IsTheBaseEndpoint()
    {
        var options = From(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4317" });
        Assert.That(options.SignalEndpoint("logs"), Is.EqualTo(new Uri("http://collector:4317")));
    }

    [Test]
    public void SignalEndpoint_HttpProtobuf_AppendsTheSignalPath()
    {
        var options = From(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4318/",
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
        });

        Assert.Multiple(() =>
        {
            Assert.That(options.SignalEndpoint("traces"), Is.EqualTo(new Uri("http://collector:4318/v1/traces")));
            Assert.That(options.SignalEndpoint("metrics"), Is.EqualTo(new Uri("http://collector:4318/v1/metrics")));
            Assert.That(options.SignalEndpoint("logs"), Is.EqualTo(new Uri("http://collector:4318/v1/logs")));
        });
    }

    [Test]
    public void ConfigureResource_MatchesTheLogResource()
    {
        var options = From(new()
        {
            ["OTEL_SERVICE_NAME"] = "headhunter-staging",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "deployment.environment.name=staging",
            ["HOSTNAME"] = "pod-1",
        }, version: "deadbee");

        var resource = options.ConfigureResource(ResourceBuilder.CreateEmpty()).Build();
        var attributes = resource.Attributes.ToDictionary(a => a.Key, a => a.Value);

        Assert.That(attributes, Is.EquivalentTo(options.ResourceAttributes));
    }

    [Test]
    public void ParseResourceAttributes_Blank_IsEmpty()
    {
        Assert.That(TelemetryOptions.ParseResourceAttributes("  "), Is.Empty);
    }

    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("1.0.0", "1.0.0")]
    [TestCase("1.0.0+", "1.0.0")]
    [TestCase("1.0.0+abc", "abc")]
    [TestCase("1.0.0+0123456789abcdef0123456789abcdef01234567", "0123456")]
    public void ServiceVersionFrom_ShortCommitWhenStamped(string? informationalVersion, string? expected)
    {
        Assert.That(TelemetryOptions.ServiceVersionFrom(informationalVersion), Is.EqualTo(expected));
    }

    [Test]
    public void ServiceVersionOf_ReadsTheAssembly()
    {
        Assert.That(TelemetryOptions.ServiceVersionOf(typeof(TelemetryOptions).Assembly), Is.Not.Null.And.Not.Empty);
    }
}
