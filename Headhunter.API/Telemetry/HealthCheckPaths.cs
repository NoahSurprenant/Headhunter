namespace Headhunter.API.Telemetry;

/// <summary>
/// Where the health endpoints are mapped. Read from <c>HealthChecks:Path</c>, <c>HealthChecks:LivePath</c>
/// and <c>HealthChecks:ReadyPath</c>; an unset or blank value keeps the default.
/// </summary>
/// <remarks>
/// The same instance maps the endpoints and recognises them, so the trace filter and the request-log
/// level follow a moved path.
/// </remarks>
public sealed record HealthCheckPaths(PathString Health, PathString Live, PathString Ready)
{
    public const string DefaultHealthPath = "/health";
    public const string DefaultLivePath = "/health/live";
    public const string DefaultReadyPath = "/health/ready";

    public static readonly HealthCheckPaths Default = new(DefaultHealthPath, DefaultLivePath, DefaultReadyPath);

    public static HealthCheckPaths FromConfiguration(IConfiguration configuration) => new(
        Read(configuration["HealthChecks:Path"], DefaultHealthPath),
        Read(configuration["HealthChecks:LivePath"], DefaultLivePath),
        Read(configuration["HealthChecks:ReadyPath"], DefaultReadyPath));

    /// <summary>The registered paths, or <see cref="Default"/> when <c>AddAppHealthChecks</c> was not called.</summary>
    public static HealthCheckPaths From(IServiceProvider? services) => services?.GetService<HealthCheckPaths>() ?? Default;

    /// <summary>Any of the three paths, or below one of them, ignoring case.</summary>
    public bool IsHealthPath(PathString path) =>
        path.StartsWithSegments(Health, StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments(Live, StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments(Ready, StringComparison.OrdinalIgnoreCase);

    /// <summary>Trimmed, with one leading and no trailing slash: <c>healthz/</c> becomes <c>/healthz</c>.</summary>
    private static PathString Read(string? value, string fallback)
    {
        var trimmed = value?.Trim().Trim('/');
        return string.IsNullOrEmpty(trimmed) ? new PathString(fallback) : new PathString("/" + trimmed);
    }
}
