using System.Net;
using System.Net.Http.Headers;
using Headhunter.API.Tiles;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Headhunter.Tests;

/// <summary>The map tile proxy (Headhunter.API/Tiles), against a fake upstream.</summary>
public class TilesTests
{
    /// <summary>Records each upstream request and answers with <see cref="Status"/>.</summary>
    private sealed class FakeUpstream : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string? CacheControl { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            var response = new HttpResponseMessage(Status) { Content = new ByteArrayContent([1, 2, 3]) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            if (CacheControl is not null)
            {
                response.Headers.CacheControl = CacheControlHeaderValue.Parse(CacheControl);
            }
            return Task.FromResult(response);
        }
    }

    private FakeUpstream _upstream = null!;

    private HttpClient Client(params (string Key, string Value)[] settings)
    {
        _upstream = new FakeUpstream();
        var factory = new HeadhunterApiFactory("Production").WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
            builder.ConfigureTestServices(services =>
                services.Configure<HttpClientFactoryOptions>(TilesController.HttpClientName, o =>
                    o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = _upstream)));
        });
        return factory.CreateClient();
    }

    [Test]
    public async Task FetchesTheTileFromItsProvider_WithAnIdentifyingUserAgent()
    {
        using var client = Client();

        var response = await client.GetAsync("/tiles/osm/5/8/11.png");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));
            Assert.That(_upstream.Requests.Single().RequestUri, Is.EqualTo(new Uri("https://tile.openstreetmap.org/5/8/11.png")));
            Assert.That(_upstream.Requests.Single().Headers.UserAgent.ToString(), Does.StartWith("Headhunter/"));
        });
        Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task ArcGisTiles_AreRowThenColumn_WithTheServersKey()
    {
        using var client = Client(("ArcGisApiKey", "secret key"));

        await client.GetAsync("/tiles/arcgis-imagery/5/8/11.jpg");

        Assert.That(_upstream.Requests.Single().RequestUri!.AbsoluteUri, Is.EqualTo(
            "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/5/11/8?token=secret%20key"));
    }

    [Test]
    public async Task StadiaTiles_WithoutAKey_PassTheRefererOn_AndAskForRetinaWhenTold()
    {
        using var client = Client();
        var request = new HttpRequestMessage(HttpMethod.Get, "/tiles/stamen-toner/5/8/11.png?retina=true");
        request.Headers.Referrer = new Uri("https://headhunter.example/");

        await client.SendAsync(request);

        var upstream = _upstream.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(upstream.RequestUri, Is.EqualTo(new Uri("https://tiles.stadiamaps.com/tiles/stamen_toner/5/8/11@2x.png")));
            Assert.That(upstream.Headers.Referrer, Is.EqualTo(new Uri("https://headhunter.example/")));
        });
    }

    [Test]
    public async Task KeepsTheProvidersCacheHeaders_ElseAWeek()
    {
        using var client = Client();
        _upstream.CacheControl = "max-age=3600";
        var theirs = await client.GetAsync("/tiles/osm/1/0/0.png");
        _upstream.CacheControl = null;
        var ours = await client.GetAsync("/tiles/osm/1/0/1.png");

        Assert.Multiple(() =>
        {
            Assert.That(theirs.Headers.CacheControl?.MaxAge, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(ours.Headers.CacheControl?.ToString(), Is.EqualTo("public, max-age=604800"));
        });
    }

    // Not an open proxy: only the named layers, their extension, and coordinates that exist.
    [TestCase("/tiles/unknown/1/0/0.png")]
    [TestCase("/tiles/osm/1/0/0.jpg")]
    [TestCase("/tiles/osm/20/0/0.png")]
    [TestCase("/tiles/osm/2/4/0.png")]
    [TestCase("/tiles/osm/2/0/-1.png")]
    public async Task RefusesWhatIsntATile_WithoutAskingUpstream(string path)
    {
        using var client = Client();

        var response = await client.GetAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(_upstream.Requests, Is.Empty);
        });
    }

    [TestCase(HttpStatusCode.NotFound, HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.Unauthorized, HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway)]
    public async Task UpstreamErrors_AreNotFoundOrBadGateway(HttpStatusCode upstream, HttpStatusCode expected)
    {
        using var client = Client();
        _upstream.Status = upstream;

        var response = await client.GetAsync("/tiles/osm/1/0/0.png");

        Assert.That(response.StatusCode, Is.EqualTo(expected));
    }
}
