using Headhunter.API.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace Headhunter.Tests.Telemetry;

[TestFixture]
public class AppForwardedHeadersTests
{
    // The chain in production: Cloudflare -> Traefik (trusts Cloudflare, sets X-Real-Ip, appends the edge
    // to X-Forwarded-For) -> Headhunter, so Traefik is the peer. Addresses as in grif-backend's 2026-10-08 trace.
    private const string Client = "68.41.224.97";
    private const string CloudflareEdge = "172.70.80.242";
    private const string TraefikPod = "10.42.0.163";

    private static readonly Dictionary<string, string?> ProductionSettings = new()
    {
        ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
        ["ForwardedHeaders:ForwardedForHeaderName"] = "X-Real-IP",
    };

    private static async Task<(string? RemoteIp, string Scheme, string Host)> Send(Dictionary<string, string?> settings, string peer, string? clientSuppliedXff = null, HttpClient? cloudflare = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        if (cloudflare is null)
            builder.Services.ConfigureAppForwardedHeaders(builder.Configuration);
        else
            await builder.Services.ConfigureAppForwardedHeadersAsync(builder.Configuration, cloudflare);

        await using var app = builder.Build();
        app.UseForwardedHeaders();
        app.Run(context => context.Response.WriteAsync($"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}|{context.Request.Host}"));
        await app.StartAsync();

        var xff = string.Join(", ", new[] { clientSuppliedXff, Client, CloudflareEdge }.Where(x => x is not null));
        var context = await app.GetTestServer().SendAsync(c =>
        {
            c.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            c.Request.Host = new HostString("headhunter.gurnt.com");
            c.Request.Headers["X-Forwarded-For"] = xff;
            c.Request.Headers["X-Real-IP"] = Client;
            c.Request.Headers["CF-Connecting-IP"] = Client;
            c.Request.Headers["X-Forwarded-Proto"] = "https";
            c.Request.Headers["X-Forwarded-Host"] = "headhunter.gurnt.com";
        });

        using var reader = new StreamReader(context.Response.Body);
        var parts = (await reader.ReadToEndAsync()).Split('|');
        return (parts[0], parts[1], parts[2]);
    }

    [Test]
    public async Task WithoutConfiguration_NothingIsApplied_TheCurrentProductionBehaviour()
    {
        var (remoteIp, _, _) = await Send(new(), TraefikPod);
        Assert.That(remoteIp, Is.EqualTo(TraefikPod), "only loopback is trusted by default, so the Traefik pod is reported as the client");
    }

    [Test]
    public async Task TrustingThePodNetwork_UsesTheClientTraefikResolved()
    {
        var (remoteIp, scheme, host) = await Send(ProductionSettings, TraefikPod);

        Assert.Multiple(() =>
        {
            Assert.That(remoteIp, Is.EqualTo(Client));
            Assert.That(scheme, Is.EqualTo("https"), "UseHttpsRedirection needs the scheme Traefik saw");
            Assert.That(host, Is.EqualTo("headhunter.gurnt.com"));
        });
    }

    [Test]
    public async Task ClientSuppliedXForwardedFor_CannotSpoofTheAddress()
    {
        var (remoteIp, _, _) = await Send(ProductionSettings, TraefikPod, clientSuppliedXff: "127.0.0.1");
        Assert.That(remoteIp, Is.EqualTo(Client));
    }

    [Test]
    public async Task UntrustedPeer_IsLeftAlone()
    {
        var (remoteIp, _, _) = await Send(ProductionSettings, "192.0.2.9");
        Assert.That(remoteIp, Is.EqualTo("192.0.2.9"));
    }

    [Test]
    public async Task XForwardedFor_WithoutCloudflareRanges_StopsAtTheCloudflareEdge()
    {
        // Without Cloudflare's ranges (FetchCloudflare off) the walk stops at the Cloudflare edge.
        var (remoteIp, _, _) = await Send(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:ForwardLimit"] = "2",
        }, TraefikPod);
        Assert.That(remoteIp, Is.EqualTo(CloudflareEdge));
    }

    [Test]
    public void Parse_SkipsInvalidEntries_AndReportsThem()
    {
        var (networks, proxies, invalid) = AppForwardedHeaders.Parse(new ForwardedHeadersSettings
        {
            KnownIPNetworks = ["10.42.0.0/16", "not-a-cidr", " ", "fd00::/8"],
            KnownProxies = ["10.43.0.10", "nope"],
        });

        Assert.Multiple(() =>
        {
            Assert.That(networks.Select(n => n.ToString()), Is.EqualTo(new[] { "10.42.0.0/16", "fd00::/8" }));
            Assert.That(proxies.Select(p => p.ToString()), Is.EqualTo(new[] { "10.43.0.10" }));
            Assert.That(invalid, Is.EqualTo(new[] { "not-a-cidr", "nope" }));
        });
    }

    [Test]
    public void ConfigureAppForwardedHeaders_AppliesSettings_AndReturnsInvalidEntries()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:KnownIPNetworks:1"] = "bogus",
            ["ForwardedHeaders:KnownProxies:0"] = "10.43.0.10",
            ["ForwardedHeaders:ForwardedForHeaderName"] = " X-Real-IP ",
            ["ForwardedHeaders:ForwardLimit"] = "2",
        }).Build();
        var services = new ServiceCollection();

        var invalid = services.ConfigureAppForwardedHeaders(configuration);
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(invalid, Is.EqualTo(new[] { "bogus" }));
            Assert.That(options.ForwardedHeaders, Is.EqualTo(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost));
            Assert.That(options.KnownIPNetworks.Select(n => n.ToString()), Is.EqualTo(new[] { "10.42.0.0/16" }));
            Assert.That(options.KnownProxies, Is.EqualTo(new[] { IPAddress.Parse("10.43.0.10") }));
            Assert.That(options.ForwardedForHeaderName, Is.EqualTo("X-Real-IP"));
            Assert.That(options.ForwardLimit, Is.EqualTo(2));
        });
    }

    [Test]
    public void ConfigureAppForwardedHeaders_NothingConfigured_KeepsLoopbackDefaults()
    {
        var services = new ServiceCollection();
        var invalid = services.ConfigureAppForwardedHeaders(new ConfigurationBuilder().Build());
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(invalid, Is.Empty);
            Assert.That(options.KnownProxies, Does.Contain(IPAddress.IPv6Loopback));
            Assert.That(options.ForwardedForHeaderName, Is.EqualTo(ForwardedHeadersDefaults.XForwardedForHeaderName));
            Assert.That(options.ForwardLimit, Is.EqualTo(1));
        });
    }

    // ---- Cloudflare ranges fetched at startup (ForwardedHeaders:FetchCloudflare) ----

    private const string CloudflareV4 = "173.245.48.0/20\n172.64.0.0/13\n";
    private const string CloudflareV6 = "2400:cb00::/32\n";

    /// <summary>Answers per URL; counts requests. A null entry throws, as a failed connection would.</summary>
    private sealed class FakeCloudflare(Dictionary<string, (HttpStatusCode Status, string Body)?> responses) : HttpMessageHandler
    {
        public List<string> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            if (!responses.TryGetValue(url, out var response) || response is null)
                throw new HttpRequestException($"connection refused: {url}");
            return Task.FromResult(new HttpResponseMessage(response.Value.Status) { Content = new StringContent(response.Value.Body) });
        }
    }

    private static FakeCloudflare Published() => new(new()
    {
        [ForwardedHeadersSettings.DefaultCloudflareIpsV4Url] = (HttpStatusCode.OK, CloudflareV4),
        [ForwardedHeadersSettings.DefaultCloudflareIpsV6Url] = (HttpStatusCode.OK, CloudflareV6),
    });

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ForwardedHeadersOptions OptionsOf(IServiceCollection services) =>
        services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

    [Test]
    public async Task FetchCloudflare_Off_ByDefault_MakesNoRequest()
    {
        var handler = Published();
        var services = new ServiceCollection();

        var setup = await services.ConfigureAppForwardedHeadersAsync(Config(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
        }), new HttpClient(handler));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requested, Is.Empty);
            Assert.That(setup.Fetched, Is.Empty);
            Assert.That(setup.FetchErrors, Is.Empty);
            Assert.That(OptionsOf(services).KnownIPNetworks.Select(n => n.ToString()), Is.EqualTo(new[] { "10.42.0.0/16" }));
        });
    }

    [Test]
    public async Task FetchCloudflare_On_TrustsTheConfiguredAndPublishedRanges()
    {
        var handler = Published();
        var services = new ServiceCollection();

        var setup = await services.ConfigureAppForwardedHeadersAsync(Config(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:KnownIPNetworks:1"] = "173.245.48.0/20", // also published: trusted once
            ["ForwardedHeaders:FetchCloudflare"] = "true",
        }), new HttpClient(handler));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requested, Is.EquivalentTo(new[] { ForwardedHeadersSettings.DefaultCloudflareIpsV4Url, ForwardedHeadersSettings.DefaultCloudflareIpsV6Url }));
            Assert.That(setup.Fetched.Select(n => n.ToString()), Is.EquivalentTo(new[] { "173.245.48.0/20", "172.64.0.0/13", "2400:cb00::/32" }));
            Assert.That(setup.FetchErrors, Is.Empty);
            Assert.That(OptionsOf(services).KnownIPNetworks.Select(n => n.ToString()),
                Is.EquivalentTo(new[] { "10.42.0.0/16", "173.245.48.0/20", "172.64.0.0/13", "2400:cb00::/32" }));
        });
    }

    [Test]
    public async Task FetchCloudflare_UsesTheConfiguredUrls()
    {
        var handler = new FakeCloudflare(new()
        {
            ["https://mirror.example/v4"] = (HttpStatusCode.OK, CloudflareV4),
            ["https://mirror.example/v6"] = (HttpStatusCode.OK, CloudflareV6),
        });

        var setup = await new ServiceCollection().ConfigureAppForwardedHeadersAsync(Config(new()
        {
            ["ForwardedHeaders:FetchCloudflare"] = "true",
            ["ForwardedHeaders:CloudflareIpsV4Url"] = "https://mirror.example/v4",
            ["ForwardedHeaders:CloudflareIpsV6Url"] = "https://mirror.example/v6",
        }), new HttpClient(handler));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requested, Is.EquivalentTo(new[] { "https://mirror.example/v4", "https://mirror.example/v6" }));
            Assert.That(setup.Fetched, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task FetchCloudflare_Failures_TrustNothingExtra_AndAreReported()
    {
        var handler = new FakeCloudflare(new()
        {
            [ForwardedHeadersSettings.DefaultCloudflareIpsV4Url] = (HttpStatusCode.ServiceUnavailable, "down"),
            [ForwardedHeadersSettings.DefaultCloudflareIpsV6Url] = null, // throws
        });
        var services = new ServiceCollection();

        var setup = await services.ConfigureAppForwardedHeadersAsync(Config(new()
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:FetchCloudflare"] = "true",
        }), new HttpClient(handler));

        Assert.Multiple(() =>
        {
            Assert.That(setup.Fetched, Is.Empty);
            Assert.That(setup.FetchErrors, Has.Count.EqualTo(2));
            Assert.That(setup.FetchErrors, Has.Some.Contains("HTTP 503"));
            Assert.That(setup.FetchErrors, Has.Some.Contains("HttpRequestException"));
            Assert.That(OptionsOf(services).KnownIPNetworks.Select(n => n.ToString()), Is.EqualTo(new[] { "10.42.0.0/16" }));
        });
    }

    [Test]
    public async Task FetchRanges_EmptyBody_IsAnError()
    {
        var handler = new FakeCloudflare(new() { ["https://cf.example/v4"] = (HttpStatusCode.OK, " \n \n") });

        var (networks, errors) = await AppForwardedHeaders.FetchRanges(new HttpClient(handler), "https://cf.example/v4");

        Assert.Multiple(() =>
        {
            Assert.That(networks, Is.Empty);
            Assert.That(errors, Is.EqualTo(new[] { "https://cf.example/v4: empty response" }));
        });
    }

    [Test]
    public async Task FetchRanges_GarbageLine_IsSkipped_TheRestKept()
    {
        var handler = new FakeCloudflare(new() { ["https://cf.example/v4"] = (HttpStatusCode.OK, "173.245.48.0/20\r\n<html>oops</html>\n172.64.0.0/13") });

        var (networks, errors) = await AppForwardedHeaders.FetchRanges(new HttpClient(handler), "https://cf.example/v4");

        Assert.Multiple(() =>
        {
            Assert.That(networks.Select(n => n.ToString()), Is.EqualTo(new[] { "173.245.48.0/20", "172.64.0.0/13" }));
            Assert.That(errors, Is.EqualTo(new[] { "https://cf.example/v4: invalid entry '<html>oops</html>'" }));
        });
    }

    [Test]
    public async Task WalkingXForwardedFor_WithFetchedCloudflareRanges_RecoversTheClient()
    {
        // The documented switch away from X-Real-IP: client, Cloudflare edge; Traefik is the peer.
        var settings = new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16",
            ["ForwardedHeaders:ForwardedForHeaderName"] = "X-Forwarded-For",
            ["ForwardedHeaders:ForwardLimit"] = "2",
            ["ForwardedHeaders:FetchCloudflare"] = "true",
        };

        var (remoteIp, _, _) = await Send(settings, TraefikPod, cloudflare: new HttpClient(Published()));
        var (spoofed, _, _) = await Send(settings, TraefikPod, clientSuppliedXff: "127.0.0.1", cloudflare: new HttpClient(Published()));

        Assert.Multiple(() =>
        {
            Assert.That(remoteIp, Is.EqualTo(Client));
            Assert.That(spoofed, Is.EqualTo(Client), "an X-Forwarded-For entry the client typed sits left of the limit");
        });
    }

    [Test]
    public void ForwardedHeadersSetup_LogTo_ReportsEverything()
    {
        var sink = new CollectingSink();
        using var logger = new Serilog.LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        new ForwardedHeadersSetup(["bogus"], [System.Net.IPNetwork.Parse("172.64.0.0/13")], ["https://cf.example/v4: HTTP 503"]).LogTo(logger);

        Assert.That(sink.Events.Select(e => (e.Level, e.RenderMessage())), Is.EqualTo(new[]
        {
            (Serilog.Events.LogEventLevel.Warning, "Ignoring invalid ForwardedHeaders entry \"bogus\""),
            (Serilog.Events.LogEventLevel.Warning, "Cloudflare range fetch: \"https://cf.example/v4: HTTP 503\""),
            (Serilog.Events.LogEventLevel.Information, "Trusting Cloudflare network \"172.64.0.0/13\""),
        }));
    }

    private sealed class CollectingSink : Serilog.Core.ILogEventSink
    {
        public List<Serilog.Events.LogEvent> Events { get; } = [];
        public void Emit(Serilog.Events.LogEvent logEvent) => Events.Add(logEvent);
    }
}
