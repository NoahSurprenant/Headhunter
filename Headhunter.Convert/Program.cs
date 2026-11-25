using Headhunter.Database;
using Headhunter.Database.Conversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Copies a Headhunter database into an empty one, SQL Server <-> PostgreSQL:
//   Headhunter.Convert --Source:Provider SqlServer --Source:ConnectionString "..." --Target:Provider Postgres --Target:ConnectionString "..."
// (or the same as environment variables, Source__Provider and so on).
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();

DatabaseEndpoint Endpoint(string section)
{
    var provider = Enum.TryParse<DatabaseProvider>(configuration[$"{section}:Provider"], ignoreCase: true, out var p)
        ? p
        : throw new ConversionException($"{section}:Provider must be one of: {string.Join(", ", Enum.GetNames<DatabaseProvider>())}.");
    var connectionString = configuration[$"{section}:ConnectionString"];
    return new DatabaseEndpoint(provider, string.IsNullOrWhiteSpace(connectionString)
        ? throw new ConversionException($"{section}:ConnectionString is required.")
        : connectionString);
}

using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));
try
{
    var converter = new DatabaseConverter(loggerFactory.CreateLogger<DatabaseConverter>());
    var tables = await converter.ConvertAsync(Endpoint("Source"), Endpoint("Target"));
    foreach (var table in tables) Console.WriteLine($"{table.Name}: {table.Rows}");
    return 0;
}
catch (ConversionException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
