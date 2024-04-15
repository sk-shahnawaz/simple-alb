using System.Reflection;
using Microsoft.AspNetCore.HttpOverrides;

using Serilog;
using Serilog.Events;

using FluentValidation;
using MediatR.NotificationPublishers;

using Quartz;
using Quartz.AspNetCore;

using Shared;

namespace LoadBalancer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .MinimumLevel.Override(
            nameof(ServerTraceLogger), 
            LogEventLevel.Information)
                .CreateLogger();

        var builder = WebApplication.CreateBuilder(args);
        
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        builder.Services.AddSingleton<ServerTraceLogger>();
        builder.Services.AddSingleton<ApplicationRepository>();
        builder.Services.AddSingleton<RoundRobinScheduler>();

        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.NotificationPublisher = new TaskWhenAllPublisher();
            cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
        });

        builder.Services.AddScoped<IValidator<ApplicationDto>, ApplicationDtoValidator>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedHost |
                ForwardedHeaders.XForwardedProto |
                ForwardedHeaders.XForwardedFor;
        });

        builder.Services.AddTransient<RouteChanger>();

        builder.Services.AddHealthChecks();
        builder.Services.AddControllers();

        builder.Services.AddQuartz(option =>
        {
            var jobIdentifier = new JobKey(nameof(ApplicationHealthChecker));
            option.AddJob<ApplicationHealthChecker>(option =>
            {
                option.WithIdentity(jobIdentifier);
                option.DisallowConcurrentExecution();
            });
            option.AddTrigger(option =>
            {
                option.ForJob(jobIdentifier)
                      .WithIdentity($"{jobIdentifier}-trigger")
                      .WithSimpleSchedule(schedule =>
                      {
                          var healthCheckInterval =
                            builder.Configuration["LoadBalancer:RegisteredApplicationsHealthCheckInterval"];

                          healthCheckInterval ??= "10";

                          schedule.WithIntervalInSeconds(
                              int.Parse(healthCheckInterval));

                          schedule.RepeatForever();
                      });
            });
        });
        builder.Services.AddQuartzServer(option =>
        {
            option.AwaitApplicationStarted = true;
            option.WaitForJobsToComplete = true;
        });
        builder.Services.AddQuartzHostedService(
            q => q.WaitForJobsToComplete = true);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders =
                ForwardedHeaders.XForwardedHost |
                ForwardedHeaders.XForwardedFor | 
                ForwardedHeaders.XForwardedProto
        });

        app.UseMiddleware<RouteChanger>();

        app.UseRouting();

        app.MapControllers();

        app.MapHealthChecks("/alb-health");

        await app.RunAsync();
    }
}