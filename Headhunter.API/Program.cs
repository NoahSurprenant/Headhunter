
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.AddDbContextFactory<HeadhunterContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("Headhunter"));
        });

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
}
