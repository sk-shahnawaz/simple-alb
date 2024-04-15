using System.Collections.Concurrent;

namespace LoadBalancer;

public sealed class ApplicationRepository
{
    private readonly ConcurrentDictionary<Uri, Application> applications = [];

    public int ApplicationCount { get => applications.Count; }

    internal bool RegisterApplication(Application application) =>
        applications.TryAdd(application.Uri!, application);

    internal Application? GetApplication(Uri uri)
    {
        if (applications.TryGetValue(uri, out var application))
        {
            return null;
        }
        return application;
    }

    internal Application? GetApplication(Ulid id) =>
        applications.Values.SingleOrDefault(x => x.Id == id);

    internal bool DeregisterApplication(Uri uri) =>
        applications.TryRemove(uri, out _);

    internal ICollection<Application> GetAllApplications() => applications.Values;

    internal void EnableApplication(Uri uri) =>
        applications[uri].Enabled = true;

    internal void DisableApplication(Uri uri) =>
        applications[uri].Enabled = false;
}
