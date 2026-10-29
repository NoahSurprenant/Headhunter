namespace Headhunter.API.Tiles;

/// <summary>
/// The map's base layers, fetched by <see cref="TilesController"/> on the browser's behalf: visitors only
/// ever talk to this site, provider keys stay on the server, and responses carry long cache headers so
/// browsers and Cloudflare (which caches .png and .jpg) answer repeat requests. Only these layers, by
/// name: the controller is not an open proxy.
/// </summary>
public static class TileLayers
{
    public sealed record Layer(string Name, string UrlTemplate, string Extension, int MaxZoom, string? KeySetting = null, string KeyParameter = "")
    {
        /// <summary>The upstream URL for one tile: {z}/{x}/{y} filled in, and the key when there is one.</summary>
        public Uri TileUri(int z, int x, int y, string? key)
        {
            var url = UrlTemplate
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());
            if (!string.IsNullOrEmpty(key))
            {
                url += $"?{KeyParameter}={Uri.EscapeDataString(key)}";
            }
            return new Uri(url);
        }
    }

    // ArcGisApiKey, when set, goes to ArcGIS as token; its basemaps below are served without one too.
    public static readonly IReadOnlyDictionary<string, Layer> All = new[]
    {
        new Layer("arcgis-imagery", "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", "jpg", 19, "ArcGisApiKey", "token"),
        new Layer("arcgis-hillshade", "https://services.arcgisonline.com/ArcGIS/rest/services/Elevation/World_Hillshade/MapServer/tile/{z}/{y}/{x}", "jpg", 16, "ArcGisApiKey", "token"),
        new Layer("esri-ocean", "https://services.arcgisonline.com/ArcGIS/rest/services/Ocean/World_Ocean_Base/MapServer/tile/{z}/{y}/{x}", "jpg", 13, "ArcGisApiKey", "token"),
        new Layer("osm", "https://tile.openstreetmap.org/{z}/{x}/{y}.png", "png", 19),
    }.ToDictionary(l => l.Name);
}
