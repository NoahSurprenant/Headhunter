using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Headhunter.API.Tiles;

/// <summary>Map tiles from <see cref="TileLayers"/>, fetched upstream for the browser.</summary>
[ApiController]
[Route("tiles")]
public class TilesController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TilesController> logger) : ControllerBase
{
    public const string HttpClientName = "tiles";

    // Kept by browsers and Cloudflare when the provider doesn't say: tiles change rarely.
    private const string DefaultCacheControl = "public, max-age=604800";

    [HttpGet("{layer}/{z:int}/{x:int}/{y:int}.{extension}")]
    public async Task<IActionResult> Tile(string layer, int z, int x, int y, string extension, [FromQuery] bool retina, CancellationToken ct)
    {
        if (!TileLayers.All.TryGetValue(layer, out var l) || extension != l.Extension)
        {
            return NotFound();
        }
        if (z < 0 || z > l.MaxZoom || x < 0 || y < 0 || x >= 1 << z || y >= 1 << z)
        {
            return NotFound();
        }

        var key = l.KeySetting is null ? null : configuration[l.KeySetting];
        using var request = new HttpRequestMessage(HttpMethod.Get, l.TileUri(z, x, y, retina, key));
        // Stadia's domain authentication, when there is no key: the page the tile is for.
        if (string.IsNullOrEmpty(key) && Request.Headers.Referer is { Count: > 0 } referer)
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Referer, referer.ToString());
        }

        using var response = await httpClientFactory.CreateClient(HttpClientName)
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Tile {Layer}/{Z}/{X}/{Y}: upstream answered {Status}", layer, z, x, y, (int)response.StatusCode);
            return StatusCode(response.StatusCode == System.Net.HttpStatusCode.NotFound ? 404 : 502);
        }

        Response.Headers.CacheControl = response.Headers.CacheControl?.ToString() is { Length: > 0 } cc ? cc : DefaultCacheControl;
        var body = await response.Content.ReadAsByteArrayAsync(ct);
        return File(body, response.Content.Headers.ContentType?.MediaType ?? (l.Extension == "png" ? "image/png" : "image/jpeg"));
    }
}
