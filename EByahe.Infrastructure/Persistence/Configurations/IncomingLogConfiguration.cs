using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class IncomingLogConfiguration : IEntityTypeConfiguration<IncomingLog>
{
  public void Configure(EntityTypeBuilder<IncomingLog> builder)
  {
    builder.ToTable("IncomingLogs", table => table.HasCheckConstraint("CK_IncomingLogs_Status",
      "[CodeDecodeIncomingStatus] IN (1, 2, 3, 4, 5, 6)"));
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.Error).HasMaxLength(1000);
    builder.HasIndex(row => new { row.IncomingId, row.RecordedDateTime });
  }
}
