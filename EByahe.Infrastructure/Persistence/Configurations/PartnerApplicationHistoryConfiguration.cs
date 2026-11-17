using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class PartnerApplicationHistoryConfiguration : IEntityTypeConfiguration<PartnerApplicationHistory>
{
  public void Configure(EntityTypeBuilder<PartnerApplicationHistory> builder)
  {
    builder.ToTable("PartnerApplicationHistories", table =>
    {
      table.HasCheckConstraint("CK_PartnerApplicationHistories_PreviousValue", "[PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1");
      table.HasCheckConstraint("CK_PartnerApplicationHistories_CurrentValue", "ISJSON([CurrentValue]) = 1");
      table.HasCheckConstraint("CK_PartnerApplicationHistories_Status", "[CodeDecodePartnerApplicationStatus] IN (1, 2, 3, 4)");
    });
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.PreviousValue).HasColumnType("nvarchar(max)");
    builder.Property(row => row.CurrentValue).HasColumnType("nvarchar(max)").IsRequired();
    builder.HasIndex(row => new { row.PartnerApplicationId, row.DateTimeCreated });
    builder.HasOne<PartnerApplication>().WithMany().HasForeignKey(row => row.PartnerApplicationId).OnDelete(DeleteBehavior.Restrict);
  }
}
