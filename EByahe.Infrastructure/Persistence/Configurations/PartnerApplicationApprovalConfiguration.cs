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
      const string statuses = "('Pending', 'UnderReview', 'NeedsChanges', 'Approved', 'Rejected')";
      table.HasCheckConstraint("CK_PartnerApplicationApprovals_Status", "[Status] IN " + statuses);
      table.HasCheckConstraint("CK_PartnerApplicationApprovals_PreviousStatus", "[PreviousStatus] IS NULL OR [PreviousStatus] IN " + statuses);
    });
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.PreviousStatus).HasMaxLength(32).IsUnicode(false);
    builder.Property(row => row.Status).HasMaxLength(32).IsUnicode(false).IsRequired();
    builder.Property(row => row.ChangedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    builder.Property(row => row.Remarks).HasMaxLength(2000);
    builder.HasIndex(row => new { row.PartnerApplicationId, row.DateTimeCreated });
    builder.HasIndex(row => new { row.Status, row.DateTimeCreated });
    builder.HasOne<PartnerApplication>().WithMany().HasForeignKey(row => row.PartnerApplicationId).OnDelete(DeleteBehavior.Restrict);
  }
}
