using Serilog;

namespace LoadBalancer;

public class ServerTraceLogger
{
    const string messageTemplate =
        @"
    Received request from {remoteIpAddress}
    {method} / {protocol}
    Host: {host}
    User-Agent: {userAgent}
    Accept: {accept}";

    private readonly Serilog.ILogger contextualLogger;
    
    public ServerTraceLogger()
    {
        // https://stackoverflow.com/a/57419911
        contextualLogger = Log.ForContext(
            "SourceContext", nameof(ServerTraceLogger));
    }

    public void LogIncomingRequest(HttpContext httpContext)
    {
        contextualLogger.Information(messageTemplate,
            httpContext?.Connection?.RemoteIpAddress?.ToString(),
            httpContext?.Request.Method, httpContext?.Request.Protocol,
            httpContext?.Request.Host.Host,
            httpContext?.Request.Headers.UserAgent,
            httpContext?.Request.Headers.Accept);
    }
}
