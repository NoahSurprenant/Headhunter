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

        // Environment variables last: the migrations bundle in the image (Dockerfile) gets
        // ConnectionStrings__Headhunter from the homelab migration Job.
        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets<DesignTimeContextFactory>(optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        var config = configBuilder.Build();

        var connectionString = config.GetConnectionString("Headhunter")
            ?? throw new Exception("Failed to find Headhunter connection string in configuration");

        // Not the connection string itself: it carries the password, and the migration Job's output goes to Loki.
        var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        Console.WriteLine($"Using database {target.InitialCatalog} on {target.DataSource}");

        var optionsBuilder = new DbContextOptionsBuilder<HeadhunterContext>();
        // Migrations (the bundle in the migration Job) build indexes over all 8M voters, which can take minutes on a
        // small server: well past the default 30 s command timeout.
        optionsBuilder.UseSqlServer(connectionString, o => o.CommandTimeout((int)TimeSpan.FromMinutes(30).TotalSeconds));

        return new HeadhunterContext(optionsBuilder.Options);
    }
}
