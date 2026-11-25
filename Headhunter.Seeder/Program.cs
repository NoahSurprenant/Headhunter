using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.Seeder;

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

        var host = builder.Build();
        host.Run();
    }
}