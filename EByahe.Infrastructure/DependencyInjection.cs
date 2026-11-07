using EByahe.Domain.Interfaces;
using EByahe.Infrastructure.Initialization;
using EByahe.Infrastructure.Persistence;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Repositories;
using EByahe.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Amazon.Runtime;
using Amazon.S3;
using EByahe.Infrastructure.Storage;

namespace EByahe.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(this IServiceCollection services,
    IConfiguration configuration)
  {
    services.AddDbContext<EByaheDbContext>(options =>
    {
      var connectionString = configuration.GetConnectionString("EByaheDatabase");
      if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
          "Configure ConnectionStrings:EByaheDatabase using user secrets or environment variables.");
      options.UseSqlServer(connectionString);
    });
    services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<EByaheDbContext>());
    services.AddScoped<IIncomingRepository, IncomingRepository>();
    services.AddScoped<IPartnerApplicationRepository, PartnerApplicationRepository>();
    services.AddScoped<IPartnerApplicationReviewRepository, PartnerApplicationReviewRepository>();
    services.AddScoped<IPartnerDirectoryRepository, PartnerDirectoryRepository>();
    services.AddScoped<IBusBookingRepository, BusBookingRepository>();
    services.AddScoped<IAdminBusBookingRepository, AdminBusBookingRepository>();
    services.AddScoped<IPartnerPortalRepository, PartnerPortalRepository>();
    services.AddScoped<IBusCatalogRepository, BusCatalogRepository>();
    Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton(services, TimeProvider.System);
    services.AddScoped<BusCatalogSeeder>();
    services.AddOptions<DocumentStorageOptions>().Bind(configuration.GetSection("Storage"))
      .Validate(options => options.IsValid(), "Configure the Storage endpoint, bucket, region, and scoped credentials.");
    services.AddSingleton<IAmazonS3>(provider =>
    {
      var storage = provider.GetRequiredService<IOptions<DocumentStorageOptions>>().Value;
      return new AmazonS3Client(new BasicAWSCredentials(storage.AccessKey, storage.SecretKey), new AmazonS3Config
      {
        ServiceURL = storage.Endpoint, AuthenticationRegion = storage.Region, ForcePathStyle = true,
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        Timeout = TimeSpan.FromSeconds(30), MaxErrorRetry = 2
      });
    });
    services.AddSingleton<IDocumentStorage, S3DocumentStorage>();
    services.AddScoped<PartnerDocumentStorageMigration>();
    services.AddOptions<KafkaOptions>().Bind(configuration.GetSection(KafkaOptions.SectionName))
      .Validate(options => options.IsValid(), "Configure a valid Kafka topic, broker, consumer group and retry delay.")
      .ValidateOnStart();
    services.AddSingleton<IEventSettings>(provider => provider.GetRequiredService<IOptions<KafkaOptions>>().Value);
    services.AddSingleton<IEventConsumerFactory, KafkaEventConsumerFactory>();
    services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
    services.AddScoped<IOutboxRepository, OutboxRepository>();
    services.Configure<DatabaseInitializationOptions>(configuration.GetSection("Database"));
    services.AddScoped<IDatabaseMigrator, EByaheDatabaseMigrator>();
    services.AddScoped<EByaheInitializer>();
    return services;
  }
}
