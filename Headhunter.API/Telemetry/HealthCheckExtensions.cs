using Headhunter.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Headhunter.API.Telemetry;

public static class HealthCheckExtensions
{
    /// <summary>Checks that must pass before the pod receives traffic.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Fallback when <c>HealthChecks:MaxAllocatedMegabytes</c> is unset (appsettings.json sets the same value;
    /// deployments set it per container limit): about 75% of a 512Mi limit.
    /// </summary>
    public const int DefaultMaxAllocatedMegabytes = 384;

    /// <summary>Whether the request is for a health endpoint, at the paths <see cref="AddAppHealthChecks"/> registered.</summary>
    public static bool IsHealthPath(HttpContext context) => HealthCheckPaths.From(context.RequestServices).IsHealthPath(context.Request.Path);

    /// <summary>
    /// <list type="bullet">
    /// <item><b>ready</b>: the database, and the host having started and not begun stopping.</item>
    /// <item><b>not gating</b>: managed memory, reported as Degraded only.</item>
    /// </list>
    /// Every result is also published as <c>dotnet.health_check.*</c> metrics every 30 s.
    /// </summary>
    public static IHealthChecksBuilder AddAppHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var maxAllocated = configuration.GetValue("HealthChecks:MaxAllocatedMegabytes", DefaultMaxAllocatedMegabytes);
        services.TryAddSingleton(HealthCheckPaths.FromConfiguration(configuration));

        var checks = services.AddHealthChecks()
            .AddApplicationLifecycleHealthCheck(ReadyTag)
            .AddDbContextCheck<HeadhunterContext>("database", HealthStatus.Unhealthy, [ReadyTag])
            .AddProcessAllocatedMemoryHealthCheck(maxAllocated, "memory", HealthStatus.Degraded);

        services.AddTelemetryHealthCheckPublisher(options => options.LogOnlyUnhealthy = true);
        return checks;
    }

    /// <summary>
    /// <c>/health/live</c> runs no checks: it answers while the process can serve requests, so a database
    /// outage never restarts the pod. <c>/health/ready</c> runs the <see cref="ReadyTag"/> checks.
    /// <c>/health</c> runs all of them. All three return only the status word, never details: they are
    /// public on the site's hostname. Those are the default paths; see <see cref="HealthCheckPaths"/> to
    /// move them.
    /// </summary>
    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        var paths = endpoints.ServiceProvider.GetService<HealthCheckPaths>()
            ?? HealthCheckPaths.FromConfiguration(endpoints.ServiceProvider.GetRequiredService<IConfiguration>());
        endpoints.MapHealthChecks(paths.Live, new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks(paths.Ready, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        endpoints.MapHealthChecks(paths.Health);
        return endpoints;
    }
}
