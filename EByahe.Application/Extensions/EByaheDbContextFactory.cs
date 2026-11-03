using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EByahe.Application.Extensions;

// EF tooling can build the context without running startup migrations.
public sealed class EByaheDbContextFactory : IDesignTimeDbContextFactory<EByaheDbContext>
{
  public EByaheDbContext CreateDbContext(string[] args)
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
      Args = args,
      ContentRootPath = Path.GetDirectoryName(typeof(EByaheDbContextFactory).Assembly.Location),
      ApplicationName = typeof(EByaheDbContextFactory).Assembly.GetName().Name
    });
    var connectionString = builder.Configuration.GetConnectionString("EByaheDatabase");
    if (string.IsNullOrWhiteSpace(connectionString))
      throw new InvalidOperationException("Configure ConnectionStrings:EByaheDatabase for EF tooling.");
    return new EByaheDbContext(new DbContextOptionsBuilder<EByaheDbContext>()
      .UseSqlServer(connectionString).Options);
  }
}
