using Minioasis.Api.Items.Endpoints;
using Minioasis.Api.Shared.Errors;
using Minioasis.Application;
using Minioasis.Koha;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddKohaIntegration(builder.Configuration);
builder.Services.AddApiErrorContract();

var app = builder.Build();

app.UseApiErrorContract();
app.MapItemEndpoints();

app.Run();
