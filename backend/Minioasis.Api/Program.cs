using Minioasis.Api.Authentication;
using Minioasis.Api.Items.Endpoints;
using Minioasis.Api.Shared.Errors;
using Minioasis.Application;
using Minioasis.Koha;

// First, because the library reads the switch once (brief 002, part 7).
AppContext.SetSwitch(StaffAccessExtensions.BlockingRefreshSwitch, true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddKohaIntegration(builder.Configuration);
builder.Services.AddApiErrorContract();
builder.Services.AddStaffAccess(builder.Configuration);

var app = builder.Build();

app.UseApiErrorContract();
app.UseStaffAccess();
app.UseRouting();
app.MapItemEndpoints();

app.Run();
