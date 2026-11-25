using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using CensusGeocoder;

namespace Headhunter.Geocoding;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddDbContextFactory<HeadhunterContext>(options =>
        {
            options.UseHeadhunterDatabase(builder.Configuration);
        });
        builder.Services.RegisterGeocodingService();

        var host = builder.Build();
        host.Run();
    }
}