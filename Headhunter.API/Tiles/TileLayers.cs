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
        /// <summary>The upstream URL for one tile. {z}/{x}/{y}, and {r} for "@2x" on retina requests.</summary>
        public Uri TileUri(int z, int x, int y, bool retina, string? key)
        {
            var url = UrlTemplate
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString())
                .Replace("{r}", retina ? "@2x" : "");
            if (!string.IsNullOrEmpty(key))
            {
                url += $"?{KeyParameter}={Uri.EscapeDataString(key)}";
            }
            return new Uri(url);
        }
    }

    // ArcGIS keys: ArcGisApiKey (optional; the basemaps below are served without one too).
    // Stadia: StadiaApiKey, else Stadia's domain authentication on the Referer the browser sent.
    public static readonly IReadOnlyDictionary<string, Layer> All = new[]
    {
        new Layer("arcgis-imagery", "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", "jpg", 19, "ArcGisApiKey", "token"),
        new Layer("arcgis-hillshade", "https://services.arcgisonline.com/ArcGIS/rest/services/Elevation/World_Hillshade/MapServer/tile/{z}/{y}/{x}", "jpg", 16, "ArcGisApiKey", "token"),
        new Layer("esri-ocean", "https://services.arcgisonline.com/ArcGIS/rest/services/Ocean/World_Ocean_Base/MapServer/tile/{z}/{y}/{x}", "jpg", 13, "ArcGisApiKey", "token"),
        new Layer("osm", "https://tile.openstreetmap.org/{z}/{x}/{y}.png", "png", 19),
        new Layer("stamen-watercolor", "https://tiles.stadiamaps.com/tiles/stamen_watercolor/{z}/{x}/{y}.jpg", "jpg", 16, "StadiaApiKey", "api_key"),
        new Layer("stamen-toner", "https://tiles.stadiamaps.com/tiles/stamen_toner/{z}/{x}/{y}{r}.png", "png", 20, "StadiaApiKey", "api_key"),
        new Layer("alidade-smooth", "https://tiles.stadiamaps.com/tiles/alidade_smooth/{z}/{x}/{y}{r}.png", "png", 20, "StadiaApiKey", "api_key"),
        new Layer("alidade-smooth-dark", "https://tiles.stadiamaps.com/tiles/alidade_smooth_dark/{z}/{x}/{y}{r}.png", "png", 20, "StadiaApiKey", "api_key"),
    }.ToDictionary(l => l.Name);
}
