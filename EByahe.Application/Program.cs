using EByahe.Infrastructure;
using EByahe.Application.Extensions;
using EByahe.Infrastructure.Initialization;

using EByahe.Application.Realtime;
using Microsoft.AspNetCore.SignalR;
using EByahe.Domain.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddScoped<IRecordChangePublisher, RecordChangePublisher>();
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.Configure<IISServerOptions>(options =>
  options.MaxRequestBodySize = EByahe.Application.Controllers.PartnerApplicationsController.MaxRequestSize);
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
  var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
  if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST").AllowCredentials();
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
