namespace Shared;

public sealed class ApplicationDto
{
    public string? Scheme { get; init; }
    public string? Host { get; init; }
    public int Port { get; init; }
    public string? Path { get; init; }   
    public string? HealthCheckPath { get; init; }
    public int Timeout { get; init; }
}
