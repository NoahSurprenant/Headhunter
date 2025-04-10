using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Web;

namespace Headhunter.API;

[ApiController]
[Route("[controller]")]
public class ProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProxyController> _logger;
    public ProxyController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ProxyController> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet()]
    public async Task<IActionResult> Proxy(CancellationToken ct)
    {
        var full = Request.Query.First().Key;
        var builder = new UriBuilder(full);

        var tileRequest = builder.Path.EndsWith("MapServer/") is false;

        var queryParams = HttpUtility.ParseQueryString(builder.Query);

        var token = _configuration.GetValue<string>("ArcGisApiKey");
        if (token is not null)
        {
            queryParams["token"] = token;
        }
        else
        {
            _logger.LogWarning("ArcGisApiKey is null, using client key");
        }

        if (tileRequest)
        {
            var values = builder.Path.Trim('/').Split('/');
            var newPath = $"https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{values[^3]}/{values[^2]}/{values[^1]}";
            builder = new UriBuilder(newPath);
        }
        else
        {
            builder = new UriBuilder("https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/");
        }

        builder.Query = queryParams.ToString();
        var newUri = builder.Uri;

        var request = new HttpRequestMessage(HttpMethod.Get, newUri);

        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, "Error fetching data from Cesium API.");
        }

        // Map cache headers
        foreach (var header in response.Headers)
        {
            var key = header.Key;
            if (key is "Age" or "Cache-Control" or "Date" or "ETag")
                Response.Headers.Append(header.Key, new StringValues([.. header.Value]));
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return File(stream, response.Content.Headers.ContentType?.ToString()!);
    }
}
