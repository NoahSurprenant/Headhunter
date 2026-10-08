using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Headhunter.Tests;

public class ApiStartupTests
{
    [Test]
    public async Task Development_ServesOpenApiDocumentForTheApi()
    {
        await using var factory = new HeadhunterApiFactory("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        Assert.Multiple(() =>
        {
            // ASP.NET Core 10 emits OpenAPI 3.1 by default (3.0 on .NET 9).
            Assert.That(root.GetProperty("openapi").GetString(), Does.StartWith("3.1"));
            Assert.That(paths.TryGetProperty("/Api/Voters", out _), Is.True, "POST /Api/Voters missing");
            Assert.That(paths.TryGetProperty("/Api/Voter/{id}", out _), Is.True, "GET /Api/Voter/{id} missing");
            Assert.That(paths.TryGetProperty("/Api/Area", out _), Is.True, "GET /Api/Area missing");
        });
    }

    [Test]
    public async Task Development_NSwagUiPointsAtTheOpenApiDocument()
    {
        await using var factory = new HeadhunterApiFactory("Development");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // NSwag redirects /swagger to its UI with the document URL in the query string.
        using var redirect = await client.GetAsync("/swagger");
        using var ui = await client.GetAsync("/swagger/index.html");

        Assert.Multiple(() =>
        {
            Assert.That(redirect.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(Uri.UnescapeDataString(redirect.Headers.Location?.ToString() ?? ""), Does.Contain("openapi/v1.json"));
            Assert.That(ui.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task Production_DoesNotExposeOpenApiDocument()
    {
        await using var factory = new HeadhunterApiFactory("Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        // Production maps a SPA fallback, so the document must not come back as JSON.
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.Not.EqualTo("application/json"));
    }

    [Test]
    public async Task Production_ApiEndpointAnswersWithDatabase()
    {
        await using var factory = new HeadhunterApiFactory("Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/Api/CitySuggestions?query=Test");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
