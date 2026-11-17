using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class OutgoingConfiguration : IEntityTypeConfiguration<Outgoing>
{
  public void Configure(EntityTypeBuilder<Outgoing> builder)
  {
    builder.ToTable("Outgoing", table => table.HasCheckConstraint("CK_Outgoing_Status",
      "[CodeDecodeOutgoingStatus] IN (1, 2, 3, 4, 5)"));
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.EventType).HasMaxLength(100).IsRequired();
    builder.Property(row => row.Topic).HasMaxLength(249).IsUnicode(false).IsRequired();
    builder.Property(row => row.MessageKey).HasMaxLength(255).IsRequired();
    builder.Property(row => row.Payload).IsRequired();
    builder.Property(row => row.Error).HasMaxLength(1000);
    builder.HasIndex(row => new { row.MessageId, row.AttemptCount, row.CodeDecodeOutgoingStatus }).IsUnique();
    builder.HasIndex(row => row.RecordedDateTime);
    builder.HasIndex(row => row.PublishedDateTime);
  }
}
