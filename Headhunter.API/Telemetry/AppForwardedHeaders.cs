using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace Headhunter.API.Telemetry;

/// <summary>The <c>ForwardedHeaders</c> configuration section.</summary>
public sealed class ForwardedHeadersSettings
{
    public const string DefaultCloudflareIpsV4Url = "https://www.cloudflare.com/ips-v4/";
    public const string DefaultCloudflareIpsV6Url = "https://www.cloudflare.com/ips-v6/";

    /// <summary>CIDRs whose requests may set the client address, scheme and host, e.g. the pod network.</summary>
    public string[] KnownIPNetworks { get; set; } = [];
    /// <summary>Single proxy addresses, same meaning as <see cref="KnownIPNetworks"/>.</summary>
    public string[] KnownProxies { get; set; } = [];
    /// <summary>
    /// Header carrying the client address. Default <c>X-Forwarded-For</c>. Behind Traefik use
    /// <c>X-Real-IP</c>: Traefik has already resolved it against the Cloudflare ranges it trusts.
    /// </summary>
    public string? ForwardedForHeaderName { get; set; }
    /// <summary>How many proxy entries to walk back through. Default 1.</summary>
    public int? ForwardLimit { get; set; }
    /// <summary>
    /// Fetch Cloudflare's published ranges at startup and trust them too, for walking
    /// <c>X-Forwarded-For</c> past the Cloudflare edge. Off by default.
    /// </summary>
    public bool FetchCloudflare { get; set; }
    public string CloudflareIpsV4Url { get; set; } = DefaultCloudflareIpsV4Url;
    public string CloudflareIpsV6Url { get; set; } = DefaultCloudflareIpsV6Url;
}

/// <summary>What <see cref="AppForwardedHeaders.ConfigureAppForwardedHeadersAsync"/> trusted and skipped.</summary>
public sealed record ForwardedHeadersSetup(IReadOnlyList<string> Invalid, IReadOnlyList<System.Net.IPNetwork> Fetched, IReadOnlyList<string> FetchErrors)
{
    public void LogTo(Serilog.ILogger logger)
    {
        foreach (var invalid in Invalid)
            logger.Warning("Ignoring invalid ForwardedHeaders entry {Entry}", invalid);
        foreach (var error in FetchErrors)
            logger.Warning("Cloudflare range fetch: {Error}", error);
        foreach (var network in Fetched)
            logger.Information("Trusting Cloudflare network {Network}", network.ToString());
    }
}

public static class AppForwardedHeaders
{
    public const string SectionName = "ForwardedHeaders";
    public static readonly TimeSpan CloudflareFetchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Trusts forwarded headers only from the configured networks and proxies. With none configured the
    /// framework default stays: loopback only, which for a pod behind a proxy means nothing is applied.
    /// Entries that do not parse are skipped (trusting less, never more) and returned so they can be logged.
    /// Ignores <see cref="ForwardedHeadersSettings.FetchCloudflare"/>; use the async overload for that.
    /// </summary>
    public static IReadOnlyList<string> ConfigureAppForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = Settings(configuration);
        var (networks, proxies, invalid) = Parse(settings);

        services.Configure<ForwardedHeadersOptions>(options => Apply(options, settings, networks, proxies));
        return invalid;
    }

    /// <summary>
    /// As <see cref="ConfigureAppForwardedHeaders"/>, plus Cloudflare's published ranges when
    /// <c>ForwardedHeaders:FetchCloudflare</c> is true. A failed fetch, an error status, an empty body or a
    /// line that does not parse adds nothing for it and is reported, never thrown: the app starts trusting
    /// less, never more.
    /// </summary>
    public static async Task<ForwardedHeadersSetup> ConfigureAppForwardedHeadersAsync(this IServiceCollection services,
        IConfiguration configuration, HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        var settings = Settings(configuration);
        var (networks, proxies, invalid) = Parse(settings);
        var fetched = new List<System.Net.IPNetwork>();
        var errors = new List<string>();

        if (settings.FetchCloudflare)
        {
            var owned = client is null;
            client ??= new HttpClient { Timeout = CloudflareFetchTimeout };
            try
            {
                var results = await Task.WhenAll(
                    FetchRanges(client, settings.CloudflareIpsV4Url, cancellationToken),
                    FetchRanges(client, settings.CloudflareIpsV6Url, cancellationToken));
                foreach (var (ranges, rangeErrors) in results)
                {
                    fetched.AddRange(ranges);
                    errors.AddRange(rangeErrors);
                }
            }
            finally
            {
                if (owned)
                    client.Dispose();
            }
        }

        var trusted = networks.Concat(fetched).Distinct().ToList();
        services.Configure<ForwardedHeadersOptions>(options => Apply(options, settings, trusted, proxies));
        return new ForwardedHeadersSetup(invalid, fetched, errors);
    }

    /// <summary>One CIDR per line, as Cloudflare publishes them. Never throws; problems come back as errors.</summary>
    public static async Task<(List<System.Net.IPNetwork> Networks, List<string> Errors)> FetchRanges(HttpClient client, string url, CancellationToken cancellationToken = default)
    {
        var networks = new List<System.Net.IPNetwork>();
        var errors = new List<string>();
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                errors.Add($"{url}: HTTP {(int)response.StatusCode}");
                return (networks, errors);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            foreach (var line in body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
            {
                if (System.Net.IPNetwork.TryParse(line, out var network))
                    networks.Add(network);
                else
                    errors.Add($"{url}: invalid entry '{line}'");
            }

            if (networks.Count == 0 && errors.Count == 0)
                errors.Add($"{url}: empty response");
        }
        catch (Exception ex)
        {
            errors.Add($"{url}: {ex.GetType().Name}: {ex.Message}");
        }

        return (networks, errors);
    }

    private static ForwardedHeadersSettings Settings(IConfiguration configuration) =>
        configuration.GetSection(SectionName).Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

    public static (List<System.Net.IPNetwork> Networks, List<IPAddress> Proxies, List<string> Invalid) Parse(ForwardedHeadersSettings settings)
    {
        var networks = new List<System.Net.IPNetwork>();
        var proxies = new List<IPAddress>();
        var invalid = new List<string>();

        foreach (var entry in settings.KnownIPNetworks.Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            if (System.Net.IPNetwork.TryParse(entry.Trim(), out var network))
                networks.Add(network);
            else
                invalid.Add(entry);
        }

        foreach (var entry in settings.KnownProxies.Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            if (IPAddress.TryParse(entry.Trim(), out var address))
                proxies.Add(address);
            else
                invalid.Add(entry);
        }

        return (networks, proxies, invalid);
    }

    public static void Apply(ForwardedHeadersOptions options, ForwardedHeadersSettings settings, IReadOnlyCollection<System.Net.IPNetwork> networks, IReadOnlyCollection<IPAddress> proxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

        if (networks.Count > 0 || proxies.Count > 0)
        {
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in networks)
                options.KnownIPNetworks.Add(network);
            foreach (var proxy in proxies)
                options.KnownProxies.Add(proxy);
        }

        if (!string.IsNullOrWhiteSpace(settings.ForwardedForHeaderName))
            options.ForwardedForHeaderName = settings.ForwardedForHeaderName.Trim();

        if (settings.ForwardLimit is { } limit && limit > 0)
            options.ForwardLimit = limit;
    }
}
