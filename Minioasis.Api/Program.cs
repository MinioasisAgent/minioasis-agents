using Minioasis.Api.Shared.Extensions;
using Minioasis.Api.Shared.Models;
using Minioasis.Application.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddApiErrorHandling();

var app = builder.Build();

// The startup check of the error definitions and messages (R9): resolving the catalog checks the registry and
// errors.en.json, so an inconsistency stops the application before any request is served.
app.Services.GetRequiredService<ErrorMessageCatalog>();

app.UseApiErrorHandling();
app.UseRouting();
app.MapEndpointModules();

app.Run();
