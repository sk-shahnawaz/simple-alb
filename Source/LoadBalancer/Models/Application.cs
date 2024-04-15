using Shared;

namespace LoadBalancer;

public sealed class Application
{
    public Ulid Id { get; init; }
    public string? Scheme { get; init; }
    public string? Host { get; init; }
    public int Port { get; init; }
    public string? Path { get; init; }
    public string? HealthCheckPath { get; init; }
    public int Timeout { get; init; }
    public Uri? Uri { get; init; }
    public DateTimeOffset CreatedOn { get; init; }
    public bool? Enabled { get; set; }

    public static implicit operator Application(ApplicationDto applicationDto) =>
        new()
        {
            Scheme = applicationDto.Scheme,
            Host = applicationDto.Host,
            Port = applicationDto.Port,
            Path = applicationDto.Path,
            HealthCheckPath = applicationDto.HealthCheckPath,
            Timeout = applicationDto.Timeout,
            
            Id = Ulid.NewUlid(),
            Uri = new($"{applicationDto.Scheme}://{applicationDto.Host}:{applicationDto.Port}"),
            CreatedOn = DateTimeOffset.UtcNow,
            Enabled = null
        };
}
