using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class IncomingLogConfiguration : IEntityTypeConfiguration<IncomingLog>
{
  public void Configure(EntityTypeBuilder<IncomingLog> builder)
  {
    builder.ToTable("IncomingLogs", table => table.HasCheckConstraint("CK_IncomingLogs_Status",
      "[Status] IN ('Received', 'Processing', 'Processed', 'Failed', 'Acknowledged', 'AcknowledgementFailed')"));
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.Status).HasMaxLength(32).IsUnicode(false).IsRequired();
    builder.Property(row => row.Error).HasMaxLength(1000);
    builder.HasIndex(row => new { row.IncomingId, row.RecordedDateTime });
  }
}
