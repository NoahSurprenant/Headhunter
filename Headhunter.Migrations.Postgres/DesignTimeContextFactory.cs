using Headhunter.Database;
using Microsoft.EntityFrameworkCore.Design;

namespace Headhunter.Migrations.Postgres;

/// <summary>dotnet ef and the Postgres migrations bundle, with this project as the startup project.</summary>
public class DesignTimeContextFactory : IDesignTimeDbContextFactory<HeadhunterContext>
{
    public HeadhunterContext CreateDbContext(string[] args) => DesignTimeContext.Create(DatabaseProvider.Postgres);
}
