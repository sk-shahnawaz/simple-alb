namespace LoadBalancer;

internal static class Utilities
{
    internal const string KeyRouteParameters = "ALB__RouteParameters";
    internal const string KeyQueryStringParameters = "ALB__QueryStringParameters";

    internal static void AppendOriginalRequestHeaders(
        HttpClient httpClient,
        HttpContext httpContext)
    {
        IEnumerable<string> headerValues;
        foreach (var header in httpContext.Request.Headers)
        {
            if (!httpClient.DefaultRequestHeaders.Contains(header.Key))
            {
                httpClient.DefaultRequestHeaders.Add(header.Key, value: header.Value);
            }
            else
            {
                headerValues = httpClient.DefaultRequestHeaders.GetValues(header.Key);
                headerValues = [.. headerValues, header.Value.ToString()];
                httpClient.DefaultRequestHeaders.Remove(header.Key);
                httpClient.DefaultRequestHeaders.Add(header.Key, values: headerValues);
            }
        }
    }

    internal static bool IsEligiblePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.Equals("/alb-health") ||
            path.Equals("/favicon.ico") ||
            path.StartsWith("/api"))
        {
            return false;
        }

        return true;
    }
}
