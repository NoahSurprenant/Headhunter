using OpenTelemetry.Resources;

namespace Headhunter.API.Telemetry;

/// <summary>OTLP wire protocol, per OTEL_EXPORTER_OTLP_PROTOCOL.</summary>
public enum OtlpWireProtocol
{
    /// <summary><c>grpc</c>, normally port 4317. The default.</summary>
    Grpc,
    /// <summary><c>http/protobuf</c>, normally port 4318.</summary>
    HttpProtobuf,
}

/// <summary>
/// One resolved view of the telemetry settings, shared by logs (Serilog), metrics and traces so all
/// three carry the same resource and go to the same collector.
/// </summary>
/// <remarks>
/// Read from the standard OpenTelemetry environment variables (through <see cref="IConfiguration"/>):
/// <list type="bullet">
/// <item><c>OTEL_SERVICE_NAME</c>: service.name. Falls back to the application name.</item>
/// <item><c>OTEL_RESOURCE_ATTRIBUTES</c>: extra resource attributes, <c>key=value,key=value</c>. <c>deployment.environment.name</c> goes here.</item>
/// <item><c>OTEL_EXPORTER_OTLP_ENDPOINT</c>: the collector. Unset: nothing is exported.</item>
/// <item><c>OTEL_EXPORTER_OTLP_PROTOCOL</c>: <c>grpc</c> (default) or <c>http/protobuf</c>.</item>
/// <item><c>OTEL_METRIC_EXPORT_INTERVAL</c>: milliseconds between metric pushes, clamped to 5 s - 5 min. Default 30 s.</item>
/// </list>
/// </remarks>
public sealed class TelemetryOptions
{
    public const string EnvironmentAttribute = "deployment.environment.name";
    /// <summary>Deprecated semconv name, still accepted as input.</summary>
    public const string LegacyEnvironmentAttribute = "deployment.environment";

    public static readonly TimeSpan DefaultMetricExportInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MinMetricExportInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxMetricExportInterval = TimeSpan.FromMinutes(5);

    public required string ServiceName { get; init; }
    public string? ServiceVersion { get; init; }
    public required string ServiceInstanceId { get; init; }
    public required string Environment { get; init; }
    /// <summary>Base collector endpoint, e.g. <c>http://otel-collector:4317</c>; null when export is off.</summary>
    public Uri? OtlpEndpoint { get; init; }
    public OtlpWireProtocol Protocol { get; init; } = OtlpWireProtocol.Grpc;
    public TimeSpan MetricExportInterval { get; init; } = DefaultMetricExportInterval;
    /// <summary>Every resource attribute, service.* and the environment included. Identical on every signal.</summary>
    public required IReadOnlyDictionary<string, object> ResourceAttributes { get; init; }

    public bool ExportEnabled => OtlpEndpoint is not null;

    public static TelemetryOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment, string? serviceVersion = null)
    {
        var attributes = ParseResourceAttributes(configuration["OTEL_RESOURCE_ATTRIBUTES"]);

        var serviceName = FirstNonEmpty(configuration["OTEL_SERVICE_NAME"])
            ?? environment.ApplicationName;

        var deploymentEnvironment = FirstNonEmpty(
                attributes.GetValueOrDefault(EnvironmentAttribute),
                attributes.GetValueOrDefault(LegacyEnvironmentAttribute))
            ?? environment.EnvironmentName.ToLowerInvariant();
        attributes.Remove(LegacyEnvironmentAttribute);

        // The pod name in Kubernetes: stable for the pod's life and the same value k8sattributes adds.
        var instanceId = FirstNonEmpty(configuration["HOSTNAME"]) ?? System.Environment.MachineName;

        var resource = new Dictionary<string, object>();
        foreach (var (key, value) in attributes)
            resource[key] = value;
        resource["service.name"] = serviceName;
        resource["service.instance.id"] = instanceId;
        resource[EnvironmentAttribute] = deploymentEnvironment;
        if (!string.IsNullOrWhiteSpace(serviceVersion))
            resource["service.version"] = serviceVersion;

        return new TelemetryOptions
        {
            ServiceName = serviceName,
            ServiceVersion = string.IsNullOrWhiteSpace(serviceVersion) ? null : serviceVersion,
            ServiceInstanceId = instanceId,
            Environment = deploymentEnvironment,
            OtlpEndpoint = ParseEndpoint(FirstNonEmpty(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])),
            Protocol = ParseProtocol(configuration["OTEL_EXPORTER_OTLP_PROTOCOL"]),
            MetricExportInterval = ParseMetricExportInterval(configuration["OTEL_METRIC_EXPORT_INTERVAL"]),
            ResourceAttributes = resource,
        };
    }

    /// <summary>
    /// The endpoint for one signal (<c>traces</c>, <c>metrics</c>, <c>logs</c>). gRPC takes the base endpoint;
    /// http/protobuf needs the signal path, which the exporters do not add when the endpoint is set in code.
    /// </summary>
    public Uri? SignalEndpoint(string signal)
    {
        if (OtlpEndpoint is null)
            return null;
        if (Protocol == OtlpWireProtocol.Grpc)
            return OtlpEndpoint;
        return new Uri(OtlpEndpoint.AbsoluteUri.TrimEnd('/') + "/v1/" + signal);
    }

    /// <summary>
    /// service.version from the assembly's informational version: the short commit when the build stamped
    /// one (<c>1.0.0+&lt;sha&gt;</c>; the image build passes <c>SOURCE_REVISION</c>), else the version itself.
    /// </summary>
    public static string? ServiceVersionOf(System.Reflection.Assembly assembly) =>
        ServiceVersionFrom(System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly)?.InformationalVersion);

    public static string? ServiceVersionFrom(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return null;
        var separator = informationalVersion.IndexOf('+');
        if (separator < 0 || separator == informationalVersion.Length - 1)
            return informationalVersion.Trim().TrimEnd('+');
        var revision = informationalVersion[(separator + 1)..].Trim();
        return revision.Length > 7 ? revision[..7] : revision;
    }

    /// <summary>Applies <see cref="ResourceAttributes"/> to an OpenTelemetry SDK resource.</summary>
    public ResourceBuilder ConfigureResource(ResourceBuilder resource) => resource
        .AddService(ServiceName, serviceVersion: ServiceVersion, autoGenerateServiceInstanceId: false, serviceInstanceId: ServiceInstanceId)
        .AddAttributes(ResourceAttributes.Where(a => !a.Key.StartsWith("service.", StringComparison.Ordinal)));

    public static Dictionary<string, string> ParseResourceAttributes(string? value)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
                continue;
            var key = pair[..separator].Trim();
            var attributeValue = Uri.UnescapeDataString(pair[(separator + 1)..].Trim());
            if (attributeValue.Length > 0)
                result[key] = attributeValue;
        }

        return result;
    }

    public static Uri? ParseEndpoint(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;

    public static OtlpWireProtocol ParseProtocol(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "http/protobuf" => OtlpWireProtocol.HttpProtobuf,
            _ => OtlpWireProtocol.Grpc,
        };

    public static TimeSpan ParseMetricExportInterval(string? milliseconds)
    {
        if (!double.TryParse(milliseconds, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ms) || double.IsNaN(ms))
            return DefaultMetricExportInterval;

        var interval = TimeSpan.FromMilliseconds(Math.Clamp(ms, MinMetricExportInterval.TotalMilliseconds, MaxMetricExportInterval.TotalMilliseconds));
        return interval;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
