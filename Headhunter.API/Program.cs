using Headhunter.API.Telemetry;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Headhunter.API;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = LoggingExtensions.CreateBootstrapLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            var telemetry = TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment, TelemetryOptions.ServiceVersionOf(typeof(Program).Assembly));
            builder.AddSerilogLogging(telemetry);
            builder.Services.AddMetricsAndTracing(telemetry, builder.Configuration);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddDbContextFactory<HeadhunterContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("Headhunter"));
            });
            builder.Services.AddHttpClient();
            builder.Services.AddAppHealthChecks(builder.Configuration);
            (await builder.Services.ConfigureAppForwardedHeadersAsync(builder.Configuration)).LogTo(Log.Logger);

            var app = builder.Build();

            // Forwarded headers first: everything after sees the real client address and scheme.
            app.UseForwardedHeaders();
            app.UseRequestTelemetry();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwaggerUi(options =>
                {
                    options.DocumentPath = "openapi/v1.json";
                });
            }

            app.UseRouting();
            app.UseHttpsRedirection();

            if (app.Environment.IsDevelopment() is false)
            {
                app.UseStaticFiles();
            }

            app.UseAuthorization();

            app.MapAppHealthChecks();
            app.MapControllers();

            if (app.Environment.IsDevelopment() is false)
            {
                app.MapFallbackToFile("index.html");
            }

            await app.RunAsync();
        }
        // HostAbortedException is how tooling (dotnet ef, WebApplicationFactory) stops the app
        // right after building the host; it isn't a crash, so let it through unlogged.
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            Log.Logger.ForContext<Program>().Fatal(ex, "Fatal error");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
        return 0;
    }
}
