using System.Net.Mime;

namespace LoadBalancer;

public sealed class RouteChanger

    : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!Utilities.IsEligiblePath(context.Request.Path))
        {
            await next(context);
        }
        else
        {
            if (context.Request.Method == HttpMethod.Get.ToString())
            {
                context.Items[Utilities.KeyRouteParameters] = context.Request.Path;
                context.Items[Utilities.KeyQueryStringParameters] = context.Request.QueryString;
                
                context.Request.Path = "/";

                await next(context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                context.Response.ContentType = MediaTypeNames.Text.Plain;
                await context.Response.WriteAsync("Not allowed.");
                return;
            }
        }
    }
}
