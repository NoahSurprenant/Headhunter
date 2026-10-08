using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Headhunter.Tests;

/// <summary>Boots Headhunter.API in memory, pointed at the test SQL Server.</summary>
public class HeadhunterApiFactory(string environment) : WebApplicationFactory<API.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Headhunter", SqlServerFixture.ConnectionString);
    }
}
