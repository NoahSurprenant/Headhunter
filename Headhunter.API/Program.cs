
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Headhunter.API;

public class Program
{
    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Debug(Serilog.Events.LogEventLevel.Verbose)
                .WriteTo.Console(Serilog.Events.LogEventLevel.Verbose)
                .MinimumLevel.Verbose()
                .CreateBootstrapLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services));

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddDbContextFactory<HeadhunterContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("Headhunter"));
            });
            builder.Services.AddHttpClient();

            var app = builder.Build();

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


            app.MapControllers();

            if (app.Environment.IsDevelopment() is false)
            {
                app.MapFallbackToFile("index.html");
            }

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext<Program>().Fatal(ex, "Fatal error");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
        return 0;
    }
}
