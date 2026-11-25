using Microsoft.EntityFrameworkCore.Design;

namespace Headhunter.Database;

/// <summary>dotnet ef and the SQL Server migrations bundle, with this project as the startup project.</summary>
public class DesignTimeContextFactory : IDesignTimeDbContextFactory<HeadhunterContext>
{
    public HeadhunterContext CreateDbContext(string[] args) => DesignTimeContext.Create(DatabaseProvider.SqlServer);
}
