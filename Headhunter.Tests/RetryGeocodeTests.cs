using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CensusGeocoder;
using Headhunter.API;
using Headhunter.Database;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Headhunter.Tests;

/// <summary>Address/{id}/RetryGeocode (ApiController): a single-address retry against a fake Census geocoder.</summary>
public class RetryGeocodeTests
{
    /// <summary>Records each upstream request and answers with the configured body/status.</summary>
    private sealed class FakeUpstream : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = NoMatchBody;

        public const string NoMatchBody = """{"result":{"input":{},"addressMatches":[]}}""";
        public const string MatchBody = """
            {"result":{"input":{},"addressMatches":[
                {"tigerLine":{"side":"L","tigerLineId":"123456789"},
                 "coordinates":{"x":-84.55,"y":42.73},
                 "addressComponents":{"zip":"48933","streetName":"Pine","preType":"","city":"Lansing","preDirection":"",
                     "suffixDirection":"","fromAddress":"300","state":"MI","suffixType":"Ln","toAddress":"300",
                     "suffixQualifier":"","preQualifier":""},
                 "matchedAddress":"300 PINE LN, LANSING, MI, 48933"}
            ]}}
            """;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            var response = new HttpResponseMessage(Status) { Content = new StringContent(Body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }

    private FakeUpstream _upstream = null!;

    private HttpClient Client()
    {
        _upstream = new FakeUpstream();
        var factory = new HeadhunterApiFactory("Production").WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                // AddHttpClient<GeocodingService>() names its client after the type's short display name.
                services.Configure<HttpClientFactoryOptions>(nameof(GeocodingService), o =>
                    o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = _upstream))));
        return factory.CreateClient();
    }

    [TearDown]
    public async Task Cleanup()
    {
        _upstream?.Dispose();

        await using var context = SqlServerFixture.CreateContext();
        var address = await context.Addresses.FirstAsync(a => a.ID == TestData.PineFailedAddressId);
        address.Matched = false;
        address.Latitude = null;
        address.Longitude = null;
        await context.SaveChangesAsync();
    }

    [Test]
    public async Task MatchFound_UpdatesTheAddressAndReturnsItsNewCoordinates()
    {
        using var client = Client();
        _upstream.Body = FakeUpstream.MatchBody;

        using var response = await client.PostAsync($"/Api/Address/{TestData.PineFailedAddressId}/RetryGeocode", null);
        var result = await response.Content.ReadFromJsonAsync<RetryGeocodeResultDto>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result!.Matched, Is.True);
            Assert.That(result.Latitude, Is.EqualTo(42.73m));
            Assert.That(result.Longitude, Is.EqualTo(-84.55m));
        });

        await using var context = SqlServerFixture.CreateContext();
        var address = await context.Addresses.SingleAsync(a => a.ID == TestData.PineFailedAddressId);
        Assert.Multiple(() =>
        {
            Assert.That(address.Matched, Is.True);
            Assert.That(address.Latitude, Is.EqualTo(42.73m));
            Assert.That(address.Longitude, Is.EqualTo(-84.55m));
        });
    }

    [Test]
    public async Task NoMatch_LeavesTheAddressUnmatchedWithoutCoordinates()
    {
        using var client = Client();
        _upstream.Body = FakeUpstream.NoMatchBody;

        using var response = await client.PostAsync($"/Api/Address/{TestData.PineFailedAddressId}/RetryGeocode", null);
        var result = await response.Content.ReadFromJsonAsync<RetryGeocodeResultDto>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result!.Matched, Is.False);
            Assert.That(result.Latitude, Is.Null);
            Assert.That(result.Longitude, Is.Null);
        });
    }

    [Test]
    public async Task UnknownAddress_IsNotFound()
    {
        using var client = Client();

        using var response = await client.PostAsync($"/Api/Address/{Guid.NewGuid()}/RetryGeocode", null);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(_upstream.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task UpstreamFailure_IsBadGateway()
    {
        using var client = Client();
        _upstream.Status = HttpStatusCode.InternalServerError;
        _upstream.Body = "<html>oops</html>";

        using var response = await client.PostAsync($"/Api/Address/{TestData.PineFailedAddressId}/RetryGeocode", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
    }

    // No login to key a per-caller limit off (RetryGeocodeRateLimitOptions), so a tiny shared limit here
    // (PermitLimit 2, QueueLimit 0 from Program.cs) rejects the 3rd call outright, before it ever reaches the
    // geocoder, rather than queueing it.
    [Test]
    public async Task OverTheRateLimit_Returns429WithoutCallingTheGeocoder()
    {
        _upstream = new FakeUpstream { Body = FakeUpstream.NoMatchBody };
        using var client = new HeadhunterApiFactory("Production").WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RetryGeocodeRateLimit:PermitLimit", "2");
            builder.UseSetting("RetryGeocodeRateLimit:Window", "00:01:00");
            builder.ConfigureTestServices(services =>
                services.Configure<HttpClientFactoryOptions>(nameof(GeocodingService), o =>
                    o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = _upstream)));
        }).CreateClient();

        using var first = await client.PostAsync($"/Api/Address/{TestData.PineFailedAddressId}/RetryGeocode", null);
        using var second = await client.PostAsync($"/Api/Address/{TestData.PineFailedAddressId}/RetryGeocode", null);
        using var third = await client.PostAsync($"/Api/Address/{TestData.PineFailedAddressId}/RetryGeocode", null);

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(third.StatusCode, Is.EqualTo((HttpStatusCode)429));
            Assert.That(_upstream.Requests, Has.Count.EqualTo(2));
        });
    }
}
