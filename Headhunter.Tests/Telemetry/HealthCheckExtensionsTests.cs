using Headhunter.API.Telemetry;
using Headhunter.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net;

namespace Headhunter.Tests.Telemetry;

[TestFixture]
public class HealthCheckExtensionsTests
{
    // Refused at once: no SQL Server listens on port 9.
    private const string UnreachableDatabase = "Server=127.0.0.1,9;Database=nope;User Id=sa;Password=x;Connect Timeout=2;TrustServerCertificate=True";

    private static async Task<WebApplication> StartApp(string connectionString, int? maxAllocatedMegabytes = null,
        IDictionary<string, string?>? settings = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (maxAllocatedMegabytes is { } max)
            builder.Configuration["HealthChecks:MaxAllocatedMegabytes"] = max.ToString();
        if (settings is not null)
            builder.Configuration.AddInMemoryCollection(settings);

        // As Program.cs registers it: the factory also registers the context itself, which the check resolves.
        builder.Services.AddDbContextFactory<HeadhunterContext>(options => options.UseSqlServer(connectionString));
        builder.Services.AddAppHealthChecks(builder.Configuration);

        var app = builder.Build();
        app.MapAppHealthChecks();
        await app.StartAsync();
        return app;
    }

    private static async Task<(int Status, string Body)> Get(WebApplication app, string path)
    {
        var context = await app.GetTestServer().SendAsync(c => c.Request.Path = path);
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    [Test]
    public async Task AllHealthy()
    {
        await using var app = await StartApp(SqlServerFixture.ConnectionString);

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((200, "Healthy")));
        });
    }

    [Test]
    public async Task DatabaseDown_FailsReadiness_ButNotLiveness()
    {
        await using var app = await StartApp(UnreachableDatabase);

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((503, "Unhealthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((503, "Unhealthy")));
        });
    }

    [Test]
    public async Task HighMemory_OnlyDegrades_AndNeverGatesReadiness()
    {
        await using var app = await StartApp(SqlServerFixture.ConnectionString, maxAllocatedMegabytes: 1);

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/health/live"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health/ready"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/health"), Is.EqualTo((200, "Degraded")));
        });
    }

    [Test]
    public void AddAppHealthChecks_RegistersChecksTagsAndThePublisher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAppHealthChecks(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToDictionary(r => r.Name);

        Assert.Multiple(() =>
        {
            Assert.That(registrations.Keys, Is.SupersetOf(new[] { "database", "memory" }));
            Assert.That(registrations["database"].Tags, Does.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registrations["database"].FailureStatus, Is.EqualTo(HealthStatus.Unhealthy));
            Assert.That(registrations["memory"].Tags, Does.Not.Contain(HealthCheckExtensions.ReadyTag));
            Assert.That(registrations["memory"].FailureStatus, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(registrations.Values.Count(r => r.Tags.Contains(HealthCheckExtensions.ReadyTag)), Is.EqualTo(2), "database and application lifecycle");
            Assert.That(provider.GetServices<IHealthCheckPublisher>(), Is.Not.Empty);
            Assert.That(provider.GetRequiredService<HealthCheckPaths>(), Is.EqualTo(HealthCheckPaths.Default));
        });
    }

    [TestCase("/health", true)]
    [TestCase("/health/ready", true)]
    [TestCase("/HEALTH/live", true)]
    [TestCase("/healthz", false)]
    [TestCase("/Api/health", false)]
    public void IsHealthPath_Defaults(string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.Multiple(() =>
        {
            Assert.That(HealthCheckPaths.Default.IsHealthPath(new PathString(path)), Is.EqualTo(expected));
            Assert.That(HealthCheckExtensions.IsHealthPath(context), Is.EqualTo(expected), "no services: the defaults");
        });
    }

    [Test]
    public void HealthCheckPaths_FromConfiguration()
    {
        var empty = HealthCheckPaths.FromConfiguration(new ConfigurationBuilder().Build());
        var configured = HealthCheckPaths.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HealthChecks:Path"] = "healthz/",
            ["HealthChecks:LivePath"] = " /livez ",
            ["HealthChecks:ReadyPath"] = "  ",
        }).Build());

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.EqualTo(HealthCheckPaths.Default));
            Assert.That(configured, Is.EqualTo(new HealthCheckPaths("/healthz", "/livez", "/health/ready")), "blank keeps the default");
            Assert.That(configured.IsHealthPath("/LIVEZ"), Is.True);
            Assert.That(configured.IsHealthPath("/health"), Is.False);
        });
    }

    [Test]
    public async Task ConfiguredPaths_AreMapped_AndRecognised()
    {
        await using var app = await StartApp(SqlServerFixture.ConnectionString, settings: new Dictionary<string, string?>
        {
            ["HealthChecks:Path"] = "/healthz",
            ["HealthChecks:LivePath"] = "/livez",
            ["HealthChecks:ReadyPath"] = "/readyz",
        });

        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Path = "/readyz";

        Assert.Multiple(async () =>
        {
            Assert.That(await Get(app, "/livez"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/readyz"), Is.EqualTo((200, "Healthy")));
            Assert.That(await Get(app, "/healthz"), Is.EqualTo((200, "Healthy")));
            Assert.That((await Get(app, "/health")).Status, Is.EqualTo(404));
            Assert.That(HealthCheckExtensions.IsHealthPath(context), Is.True);
        });
    }

    [Test]
    public async Task MapAppHealthChecks_WithoutAddAppHealthChecks_ReadsTheConfiguration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["HealthChecks:LivePath"] = "/livez";
        builder.Services.AddHealthChecks();
        await using var app = builder.Build();
        app.MapAppHealthChecks();
        await app.StartAsync();

        Assert.That(await Get(app, "/livez"), Is.EqualTo((200, "Healthy")));
    }

    [Test]
    public async Task Production_TheRealApp_ServesHealth_NotTheSpaFallback()
    {
        await using var factory = new HeadhunterApiFactory("Production");
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/health/live", "/health/ready", "/health" })
        {
            using var response = await client.GetAsync(path);
            Assert.Multiple(async () =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), path);
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/plain"), path);
                Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Healthy"), path);
                Assert.That(response.Headers.Contains(RequestTelemetryMiddleware.TraceResponseHeader), Is.True, path);
            });
        }
    }
}
