using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
  public void Configure(EntityTypeBuilder<OutboxMessage> builder)
  {
    builder.ToTable("OutboxMessages", table => table.HasCheckConstraint("CK_OutboxMessages_Status",
      "[Status] IN ('Pending', 'Publishing', 'Failed', 'Published')"));
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.UserId).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    builder.Property(row => row.EventType).HasMaxLength(100).IsRequired();
    builder.Property(row => row.Topic).HasMaxLength(249).IsUnicode(false).IsRequired();
    builder.Property(row => row.MessageKey).HasMaxLength(255).IsRequired();
    builder.Property(row => row.Payload).IsRequired();
    builder.Property(row => row.Status).HasMaxLength(32).IsUnicode(false).IsRequired();
    builder.HasIndex(row => new { row.Status, row.NextAttemptDateTime, row.CreatedDateTime });
    builder.HasIndex(row => row.UserId);
    builder.HasMany(row => row.History).WithOne().HasForeignKey(row => row.MessageId).OnDelete(DeleteBehavior.Restrict);
  }
}
