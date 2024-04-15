using Microsoft.AspNetCore.Mvc;

using Serilog;

namespace Application;

public static class Program
{
    private static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        builder.Services.AddMemoryCache();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHealthChecks();
        builder.Services.AddControllers();

        builder.Services.AddHttpClient("lb", option =>
        {
            var loadBalancerUri = configuration["LoadBalancer:Uri"]
            ?? throw new InvalidProgramException("Load Balancer not configured.");

            option.BaseAddress = new Uri(loadBalancerUri);
        });

        var app = builder.Build();

        var healthCheckPath = app.Configuration["Application:HealthCheckPath"] 
            ?? "/health";

        app.MapHealthChecks(healthCheckPath);

        app.UseRouting();

        app.MapGet("/", () =>
        {
            return Results.LocalRedirect($"~/{healthCheckPath}");
        });

        app.MapControllers();

        app.MapGet("/{**catchAll}", (
            string catchAll,
            [FromServices] IHttpContextAccessor httpContextAccessor) =>
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return Results.Problem(title: "Invalid request.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Json(new
            {
                ServerIp = httpContext.Connection.LocalIpAddress?.ToString(),
                ServerPort = httpContext.Connection.LocalPort,
                ClientIpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                ClientPort = httpContext.Connection.RemotePort,
                RouteParameters = catchAll,
                QueryStrings = httpContext.Request.QueryString.Value ?? null,
                TimeStamp = DateTimeOffset.UtcNow
            });
        });

        await app.RunAsync();
    }
}