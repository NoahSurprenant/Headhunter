using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.CSVDump;

public class Program
{
    public static void Main(string[] args)
    {
        // The image runs this from /app, so appsettings.json (log level Warning) sits next to the dll, not in the working directory.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddDbContextFactory<HeadhunterContext>(options =>
        {
            options.UseHeadhunterDatabase(builder.Configuration);
        });

        var host = builder.Build();
        host.Run();
    }
}