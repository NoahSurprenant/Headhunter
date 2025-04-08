using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Headhunter.Database;
public class DesignTimeContextFactory : IDesignTimeDbContextFactory<HeadhunterContext>
{
    public HeadhunterContext CreateDbContext(string[] args)
    {
        var configBuilder = new ConfigurationBuilder();

        configBuilder.SetBasePath(Directory.GetCurrentDirectory());

        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets<DesignTimeContextFactory>(optional: true, reloadOnChange: false);

        var config = configBuilder.Build();

        var connectionString = config.GetConnectionString("Headhunter")
            ?? throw new Exception("Failed to find Headhunter connection string in configuration");

        Console.WriteLine($"Using connection string: {connectionString}");

        var optionsBuilder = new DbContextOptionsBuilder<HeadhunterContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new HeadhunterContext(optionsBuilder.Options);
    }
}
