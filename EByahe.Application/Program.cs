using EByahe.Infrastructure;
using EByahe.Application.Extensions;
using EByahe.Infrastructure.Initialization;

var builder = WebApplication.CreateBuilder(args);

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
  if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST");
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
