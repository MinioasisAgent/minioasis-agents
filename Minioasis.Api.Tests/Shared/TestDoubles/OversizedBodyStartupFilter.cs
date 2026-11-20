using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// On the server side, for a request marked by <see cref="HeaderName"/>, replaces the body with
/// <see cref="OversizedBodyStream"/>. The test server enforces no body limit, and a failing client-side stream does
/// not reach the server as a 413, so this is how the over-size case of R8 is produced.
/// </summary>
public sealed class OversizedBodyStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Oversized-Body";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.ContainsKey(HeaderName))
                {
                    context.Request.Body = new OversizedBodyStream();
                }

                await nextMiddleware(context);
            });
            next(app);
        };
}
