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
            options.UseSqlServer(builder.Configuration.GetConnectionString("Headhunter"));
        });
        builder.Services.RegisterGeocodingService();

        var host = builder.Build();
        host.Run();
    }
}