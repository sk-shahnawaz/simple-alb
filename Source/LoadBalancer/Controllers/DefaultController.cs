using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace LoadBalancer;

[ApiController]
public sealed class DefaultController(

    ILogger<DefaultController> logger,
    ServerTraceLogger serverTraceLogger,
    IHttpContextAccessor httpContextAccessor,
    HttpClient httpClient)

    : ControllerBase
{
    [HttpGet("/", Name = nameof(GetAsync))]
    public async Task<IResult> GetAsync(
        [FromServices] RoundRobinScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext == null)
        {
            return Results.Problem(title: "Invalid request.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        serverTraceLogger.LogIncomingRequest(httpContext);

        var application = scheduler.GetLoadBalancedApplication();
        if (application == null)
        {
            return Results.Problem(title: "No downstream servers registered in application load balancer / no healthy downstram server available.",
                statusCode: StatusCodes.Status403Forbidden,
                detail: httpContext?.TraceIdentifier);
        }

        try
        {
            httpClient.BaseAddress = application.Uri;

            var requestUriBuilder = new StringBuilder(application.Path);
            if (httpContext.Items.TryGetValue(
                Utilities.KeyRouteParameters,
                out var routeParameters) && routeParameters is not null)
            {
                requestUriBuilder.Append(((PathString)routeParameters).Value);
            }
            if (httpContext.Items.TryGetValue(
                Utilities.KeyQueryStringParameters,
                out var queryStringParameters) && queryStringParameters is not null)
            {
                requestUriBuilder.Append(((QueryString)queryStringParameters).Value);
            }
            var requestUri = new Uri(
                requestUriBuilder.ToString(),
                UriKind.Relative);

            Utilities.AppendOriginalRequestHeaders(httpClient, httpContext);

            var getResponseFromDownstreamServer = httpClient.GetAsync(
                requestUri,
                cancellationToken);

            if (await Task.WhenAny(
                [
                        getResponseFromDownstreamServer,
                        Task.Delay(
                            TimeSpan.FromSeconds(
                                application.Timeout),
                            cancellationToken)
                      ])
                != getResponseFromDownstreamServer)
            {
                logger.LogError(
                    "Downstream server failed to respond within expected duration.");

                return Results.Problem(title: "Timeout",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var responseFromDownstreamServer = await getResponseFromDownstreamServer;
            responseFromDownstreamServer.EnsureSuccessStatusCode();

            var responseStream = await responseFromDownstreamServer.Content
                    .ReadAsStreamAsync(cancellationToken);

            var objectData = await JsonSerializer.DeserializeAsync<object>(
                responseStream, options: null, cancellationToken);

            return Results.Json(objectData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in application load balancer.");

            return Results.Problem(title: "Error occurred in application load balancer",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: httpContext?.TraceIdentifier);
        }
    }
}
