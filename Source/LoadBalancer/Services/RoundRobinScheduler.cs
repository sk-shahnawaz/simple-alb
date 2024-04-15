using System.Collections.Concurrent;

namespace LoadBalancer;

public sealed class RoundRobinScheduler
{
    private readonly object locker = new();
    private int index;

    internal static ConcurrentDictionary<Uri, Application> HealthyApplications = [];

    public RoundRobinScheduler()
    {
        index = 0;
    }

    internal Application? GetLoadBalancedApplication()
    {
        lock (locker)
        {
            var totalNumberOfHealthyApplications = HealthyApplications.Count;

            if (index >= totalNumberOfHealthyApplications)
            {
                index = 0;
            }

            var application = HealthyApplications.ElementAtOrDefault(
                index).Value ?? null;

            if (application != null)
            {
                index++;
            }
            
            return application;
        }
    }
}
