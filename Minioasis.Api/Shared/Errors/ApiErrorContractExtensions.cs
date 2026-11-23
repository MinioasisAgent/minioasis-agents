using Microsoft.AspNetCore.Diagnostics;
using Minioasis.Application.Shared.Errors;

namespace Minioasis.Api.Shared.Errors;

internal static class ApiErrorContractExtensions
{
    /// <summary>Registers the components of the API error contract.</summary>
    public static IServiceCollection AddApiErrorContract(this IServiceCollection services)
    {
        services.AddSingleton(provider => ErrorMessageCatalog.Load(
            provider.GetRequiredService<ApplicationErrorRegistry>(),
            HttpErrorDefinitions.All,
            ErrorMessageCatalog.ReadEmbeddedMessages()));
        services.AddSingleton<ProblemResponseFactory>();
        services.AddSingleton<ApplicationResultHttpMapper>();
        services.AddSingleton<UnhandledExceptionHandler>();
        services.AddSingleton<RoutingFailureHandler>();
        return services;
    }

    /// <summary>
    /// Checks the error registry and Api's errors of HTTP itself against the message file, so that the application refuses to start on an
    /// inconsistency (ER-011), then adds the exception handler and the shape for routing failures.
    /// </summary>
    public static WebApplication UseApiErrorContract(this WebApplication app)
    {
        app.Services.GetRequiredService<ErrorMessageCatalog>();

        var exceptionHandler = app.Services.GetRequiredService<UnhandledExceptionHandler>();
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = exceptionHandler.HandleAsync,
            SuppressDiagnosticsCallback = context =>
                context.ExceptionHandledBy == ExceptionHandledType.ExceptionHandlerDelegate,
        });

        var routingFailureHandler = app.Services.GetRequiredService<RoutingFailureHandler>();
        app.UseStatusCodePages(context => routingFailureHandler.HandleAsync(context.HttpContext));
        return app;
    }
}
