using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class IncomingConfiguration : IEntityTypeConfiguration<Incoming>
{
  public void Configure(EntityTypeBuilder<Incoming> builder)
  {
    builder.ToTable("Incoming", table => table.HasCheckConstraint("CK_Incoming_Status",
      "[Status] IN ('Received', 'Processing', 'Processed', 'Failed')"));
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.ConsumerGroup).HasMaxLength(200).IsUnicode(false).IsRequired();
    builder.Property(row => row.Topic).HasMaxLength(249).IsUnicode(false).IsRequired();
    builder.Property(row => row.Status).HasMaxLength(32).IsUnicode(false).IsRequired();
    builder.Property(row => row.Error).HasMaxLength(1000);
    builder.HasIndex(row => new { row.ConsumerGroup, row.Topic, row.Partition, row.Offset }).IsUnique();
    builder.HasIndex(row => row.MessageId);
    builder.HasIndex(row => row.ReceivedDateTime);
    builder.HasMany(row => row.Logs).WithOne().HasForeignKey(row => row.IncomingId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
