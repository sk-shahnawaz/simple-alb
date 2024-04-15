using MediatR;

namespace LoadBalancer;

public enum State { UNHEALTHY, HEALTHY }

public sealed class ApplicationMonitoringNotification(
    Application application,
    State state) 
    
    : INotification
{
    public Application Application { get; init; } = application; 
    public State State { get; init; } = state;
}


public sealed class ApplicationMonitoringNotificationHandler(
    ILogger<ApplicationMonitoringNotificationHandler> logger)

    : INotificationHandler<ApplicationMonitoringNotification>

{
    public async Task Handle(
        ApplicationMonitoringNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification is null || notification.Application is null)
        {
            return;
        }

        if (notification.State == State.HEALTHY)
        {
            if (!RoundRobinScheduler.HealthyApplications.ContainsKey(
                notification.Application.Uri!))
            {
                RoundRobinScheduler.HealthyApplications!.TryAdd(
                notification.Application.Uri!,
                notification.Application);
            }
        }

        if (notification.State == State.UNHEALTHY)
        {
            if (RoundRobinScheduler.HealthyApplications.ContainsKey(
                notification.Application.Uri!))
            {
                RoundRobinScheduler.HealthyApplications!.TryRemove(
                    notification.Application.Uri!, out _);
            } 
        }

        logger.LogDebug("Application health check status: {@data}", new
        {
            notification.Application,
            notification.State,
            Timestamp = DateTimeOffset.UtcNow
        });

        await Task.Yield();
    }
}
