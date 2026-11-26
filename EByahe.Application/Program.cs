using EByahe.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using EByahe.Application.Extensions;
using EByahe.Infrastructure.Initialization;

using EByahe.Application.Realtime;
using Microsoft.AspNetCore.SignalR;
using EByahe.Domain.Repositories;

var builder = WebApplication.CreateBuilder(args);
// Enable only for the private IIS sites behind our loopback ARR gateway.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    if (!builder.Configuration.GetValue<bool>("ReverseProxy:Enabled")) return;
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
    options.ForwardLimit = 1;
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddScoped<IRecordChangePublisher, RecordChangePublisher>();
builder.Services.AddControllers();
builder.Services.AddHttpClient("PartnerAccounts", client => client.Timeout = TimeSpan.FromSeconds(60))
  .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddApplication();
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.Configure<IISServerOptions>(options =>
  options.MaxRequestBodySize = EByahe.Application.Controllers.PartnerApplicationsController.MaxRequestSize);
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
  var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
  if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST", "PUT", "DELETE").AllowCredentials();
}));

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("MigratePartnerFilesOnly"))
{
  await using var scope = app.Services.CreateAsyncScope();
  await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().MigrateAsync(app.Lifetime.ApplicationStopping);
  return;
}
await app.Services.InitializeInfrastructureAsync(app.Lifetime.ApplicationStopping);
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Catalog:SeedDemoData"))
{
  await using var scope = app.Services.CreateAsyncScope();
  await scope.ServiceProvider.GetRequiredService<BusCatalogSeeder>().SeedAsync(app.Lifetime.ApplicationStopping);
}

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseExceptionHandler();
if (app.Configuration.GetValue<bool>("ReverseProxy:Enabled")) app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("Frontend");

// CORS does not validate WebSocket origins. Check the explicit allowlist for hub requests too.
app.Use(async (context, next) =>
{
  if (context.Request.Path.StartsWithSegments("/api/live")
    && context.Request.Headers.TryGetValue("Origin", out var origin)
    && !(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Contains(origin.ToString(), StringComparer.Ordinal))
  { context.Response.StatusCode = 403; return; }
  await next(context);
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RecordsHub>("/api/live", options => options.CloseOnAuthenticationExpiration = true);

app.Run();

public partial class Program;
