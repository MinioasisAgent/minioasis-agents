using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Minioasis.Api.Shared.Factories;
using Minioasis.Api.Shared.Handlers;
using Minioasis.Api.Shared.Mappers;
using Minioasis.Api.Shared.Models;
using Minioasis.Api.Shared.Providers;
using Minioasis.Api.Shared.Writers;
using Minioasis.Application.Shared.Errors;

namespace Minioasis.Api.Shared.Extensions;

public static class ApiErrorHandlingExtensions
{
    /// <summary>
    /// Registers the error contract: the message source and its checked catalog, the writer, the mapper, the
    /// status-code handler, and the request activity listener. Binding failures are answered, never thrown, in
    /// every environment, so they take one path (R8, R8a).
    /// </summary>
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddSingleton(_ => ErrorMessageSource.FromEmbeddedResource());
        services.AddSingleton(provider => ErrorMessageCatalogFactory.Create(
            provider.GetRequiredService<ErrorMessageSource>(),
            provider.GetRequiredService<ApplicationErrorDefinitionRegistry>()));
        services.AddSingleton<ErrorMessageProvider>();
        services.AddSingleton<ErrorResponseWriter>();
        services.AddSingleton<ApplicationResultHttpMapper>();
        services.AddSingleton<StatusCodeProblemHandler>();
        services.AddRequestActivityListener();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
        return services;
    }

    /// <summary>
    /// Adds the exception handler, which answers every unhandled exception with the masked 500 and its single
    /// <c>Error</c> entry while ASP.NET's own entry is suppressed (R3, R5), and the status-code handler (R6 to R8a).
    /// </summary>
    public static IApplicationBuilder UseApiErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = context => context.RequestServices.GetRequiredService<ErrorResponseWriter>()
                .WriteUnhandledAsync(context, context.Features.GetRequiredFeature<IExceptionHandlerFeature>().Error),
            SuppressDiagnosticsCallback = _ => true,
        });
        app.UseStatusCodePages(context => context.HttpContext.RequestServices
            .GetRequiredService<StatusCodeProblemHandler>()
            .HandleAsync(context));
        return app;
    }
}
