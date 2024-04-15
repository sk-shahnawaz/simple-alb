using Quartz;
using MediatR;

namespace LoadBalancer;

public sealed class ApplicationHealthChecker(
    ILogger<ApplicationHealthChecker> logger,
    ApplicationRepository applicationRepository,
    IServiceScopeFactory serviceScopeFactory)

    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Job {jobKey} running on {runningOn}, instance ID: {instanceId}", 
            context.JobDetail.Key, context.FireTimeUtc, context.FireInstanceId);

        if (applicationRepository.ApplicationCount == 0)
        {
            await Task.CompletedTask;
        }

        var publisher = serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IPublisher>();
        var allApplications = applicationRepository.GetAllApplications();

        Parallel.ForEach(allApplications, async (application) =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = application.Uri
            };

            var requestUri = new Uri(
                application.HealthCheckPath!,
                UriKind.Relative);

            try
            {
                var getHealthCheckResponse = httpClient.GetAsync(
                    requestUri, context.CancellationToken);

                if (await Task.WhenAny(
                        [
                            getHealthCheckResponse,
                            Task.Delay(
                                TimeSpan.FromSeconds(
                                application.Timeout),
                                context.CancellationToken)
                              ])
                    != getHealthCheckResponse)
                {
                    logger.LogError(
                        "Downstream server {serverUri} failed to respond within expected durationn for health check request.", application.Uri);

                    applicationRepository.DisableApplication(application.Uri!);
                }

                var response = await getHealthCheckResponse;
                if (response.IsSuccessStatusCode)
                {
                    applicationRepository.EnableApplication(application.Uri!);

                    await publisher.Publish(
                        new ApplicationMonitoringNotification(
                            application, State.HEALTHY));
                }
                else
                {
                    applicationRepository.DisableApplication(application.Uri!);

                    await publisher.Publish(
                        new ApplicationMonitoringNotification(
                            application, State.UNHEALTHY));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in health check for {serverUri}", application.Uri);
                applicationRepository.DisableApplication(application.Uri!);
            }
        });
    }
}
