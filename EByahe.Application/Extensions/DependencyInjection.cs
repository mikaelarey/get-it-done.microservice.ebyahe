using EByahe.Application.Events;
using EByahe.Application.ExceptionHandlers;
using EByahe.Application.Interfaces;
using EByahe.Application.Services;

namespace EByahe.Application.Extensions;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddSingleton(TimeProvider.System);
    services.AddScoped<IncomingEventProcessor>();
    services.AddHostedService<IncomingEventWorker>();
    services.AddScoped<IPartnerApplicationService, PartnerApplicationService>();
    services.AddExceptionHandler<ApiExceptionHandler>();
    services.AddProblemDetails();
    return services;
  }
}
