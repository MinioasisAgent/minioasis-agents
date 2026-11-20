using Minioasis.Api.Shared.Extensions;
using Minioasis.Api.Shared.Models;
using Minioasis.Application.Shared.Extensions;
using Minioasis.Koha.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddApiErrorHandling();

// Reads and checks the Koha settings now, so that a missing or invalid variable stops the application before it
// starts (R10 to R13). Plain http is allowed only in Development (D5, R11). Koha is not contacted (D6, R14).
builder.Services.AddKohaIntegration(builder.Configuration, allowInsecureHttp: builder.Environment.IsDevelopment());

var app = builder.Build();

// The startup check of the error definitions and messages (R9): resolving the catalog checks the registry and
// errors.en.json, so an inconsistency stops the application before any request is served.
app.Services.GetRequiredService<ErrorMessageCatalog>();

app.UseApiErrorHandling();
app.UseRouting();
app.MapEndpointModules();

app.Run();
