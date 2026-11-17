using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class PartnerApplicationApprovalConfiguration : IEntityTypeConfiguration<PartnerApplicationApproval>
{
  public void Configure(EntityTypeBuilder<PartnerApplicationApproval> builder)
  {
    builder.ToTable("PartnerApplicationApprovals", table =>
    {
      const string statuses = "(1, 2, 3, 4, 5)";
      table.HasCheckConstraint("CK_PartnerApplicationApprovals_Status", "[CodeDecodePartnerApplicationApprovalStatus] IN " + statuses);
      table.HasCheckConstraint("CK_PartnerApplicationApprovals_PreviousStatus", "[CodeDecodePreviousPartnerApplicationApprovalStatus] IS NULL OR [CodeDecodePreviousPartnerApplicationApprovalStatus] IN " + statuses);
    });
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.ChangedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    builder.Property(row => row.Remarks).HasMaxLength(2000);
    builder.HasIndex(row => new { row.PartnerApplicationId, row.DateTimeCreated });
    builder.HasIndex(row => new { row.CodeDecodePartnerApplicationApprovalStatus, row.DateTimeCreated });
    builder.HasOne<PartnerApplication>().WithMany().HasForeignKey(row => row.PartnerApplicationId).OnDelete(DeleteBehavior.Restrict);
  }
}
