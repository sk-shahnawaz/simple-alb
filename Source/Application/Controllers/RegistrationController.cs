using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using Shared;

namespace Application;

[ApiController]
public sealed class RegistrationController(
    ILogger<RegistrationController> logger,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMemoryCache memoryCache)
    
    : ControllerBase
{
    const string memoryCacheKey = "registrationId";

    [HttpGet]
    [Route("/api/register-self", Name = nameof(RegisterSelf))]
    public async Task<IResult> RegisterSelf(
        [FromServices] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        try
        {
            var httpContext = httpContextAccessor.HttpContext;

            if (httpContext == null)
            {
                return Results.Problem(title: "Invalid request.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var httpClient = httpClientFactory.CreateClient("lb");
            logger.LogError("Load balancer URL: {loadBalancerUrl}",httpClient.BaseAddress);

            var application = new ApplicationDto
            {
                Scheme = httpContext.Request.Scheme,
                Host = httpContext.Request.Host.Host,
                Port = httpContext.Request.Host.Port ?? 8080,
                Path = "/dummypath",
                HealthCheckPath = configuration["Application:HealthCheckPath"],
                Timeout = 1
            };

            var jsonContent = JsonContent.Create(application);

            var sendPayloadToLoadBalancer = httpClient.PostAsync(
                "/api/register-application",
                jsonContent,
                cancellationToken);

            var loadBalancerTimeout = configuration["LoadBalancer:Timeout"];
            var loadBalancerTimeoutInSec = loadBalancerTimeout != null ?
                double.Parse(loadBalancerTimeout) : 10;

            if (await Task.WhenAny(
                [
                        sendPayloadToLoadBalancer,
                        Task.Delay(
                            TimeSpan.FromSeconds(
                                loadBalancerTimeoutInSec),
                            cancellationToken)
                      ])
                != sendPayloadToLoadBalancer)
            {
                logger.LogError(
                    "Load balancer server failed to respond within expected duration.");

                return Results.Problem(title: "Timeout",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var response = await sendPayloadToLoadBalancer;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Application was not registered to Load Balancer.");

                return Results.Problem(title: "Failed to register application to Load Balancer",
                    statusCode: StatusCodes.Status424FailedDependency,
                    detail: null);
            }

            var registrationResponse = 
                await response.Content.ReadFromJsonAsync<RegistrationResponseDto>(
                    cancellationToken: cancellationToken);

            if (registrationResponse != null)
            {
                memoryCache.Set(memoryCacheKey, registrationResponse.Id);
            }
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in application.");

            return Results.Problem(title: "Error occurred in application",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: null);
        }
    }

    [HttpGet]
    [Route("/api/deregister-self", Name = nameof(DeregisterSelf))]
    public async Task<IResult> DeregisterSelf(CancellationToken cancellationToken)
    {
        try
        {
            var registrationId = memoryCache.Get(memoryCacheKey)?.ToString();
            if (registrationId == null)
            {
                return Results.Problem(title: "Application not registered to Load Balancer yet",
                        statusCode: StatusCodes.Status403Forbidden);
            }

            var httpClient = httpClientFactory.CreateClient("lb");

            var deleteAppFromLoadBalancer = httpClient.DeleteAsync(
                $"/api/deregister-application/{registrationId}",
                cancellationToken);

            var loadBalancerTimeout = configuration["LoadBalancer:Timeout"];
            var loadBalancerTimeoutInSec = loadBalancerTimeout != null ?
                double.Parse(loadBalancerTimeout) : 10;

            if (await Task.WhenAny(
                [
                        deleteAppFromLoadBalancer,
                        Task.Delay(
                            TimeSpan.FromSeconds(
                                loadBalancerTimeoutInSec),
                            cancellationToken)
                      ])
                != deleteAppFromLoadBalancer)
            {
                logger.LogError(
                    "Load balancer server failed to respond within expected duration.");

                return Results.Problem(title: "Timeout",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var response = await deleteAppFromLoadBalancer;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Application failed to be de-registered from Load Balancer.");

                return Results.Problem(title: "Failed to de-register application from Load Balancer",
                    statusCode: StatusCodes.Status424FailedDependency,
                    detail: null);
            }

            memoryCache.Remove(memoryCacheKey);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in application.");

            return Results.Problem(title: "Error occurred in application",
                statusCode: StatusCodes.Status500InternalServerError,
                detail: null);
        }
    }
}
