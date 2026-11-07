using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class PartnerApplicationConfiguration : IEntityTypeConfiguration<PartnerApplication>
{
  public void Configure(EntityTypeBuilder<PartnerApplication> builder)
  {
    builder.ToTable("PartnerApplications", table =>
    {
      table.HasCheckConstraint("CK_PartnerApplications_BusinessCategory", "[BusinessCategory] IN ('hotel', 'bus-operator')");
      table.HasCheckConstraint("CK_PartnerApplications_EntityType", "[EntityType] IN ('sole-proprietorship', 'partnership', 'corporation')");
      table.HasCheckConstraint("CK_PartnerApplications_SettlementFrequency", "[SettlementFrequency] IN ('daily', 'weekly')");
      table.HasCheckConstraint("CK_PartnerApplications_Status", "[Status] IN ('Submitted', 'UnderReview', 'Approved', 'Rejected')");
    });
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.TrackingId).HasMaxLength(36).IsUnicode(false).IsRequired();
    builder.HasIndex(row => row.TrackingId).IsUnique();
    builder.Property(row => row.Status).HasMaxLength(32).IsUnicode(false).HasDefaultValue("Submitted").IsRequired();
    builder.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2");
    builder.HasIndex(row => new { row.CreatedBy, row.SubmittedAtUtc });
    builder.HasIndex(row => row.CreatedBy).IsUnique().HasDatabaseName("UX_PartnerApplications_ActiveCreator")
      .HasFilter("[CreatedBy] IS NOT NULL AND [Status] IN ('Submitted', 'UnderReview')");
    builder.Property(row => row.BusinessCategory).HasMaxLength(32).IsUnicode(false).IsRequired();
    builder.Property(row => row.BusinessName).HasMaxLength(255).IsRequired();
    builder.Property(row => row.TradeName).HasMaxLength(255).IsRequired();
    builder.Property(row => row.EntityType).HasMaxLength(32).IsUnicode(false).IsRequired();
    builder.Property(row => row.Tin).HasMaxLength(32).IsRequired();
    builder.Property(row => row.Address).HasMaxLength(2000).IsRequired();
    builder.Property(row => row.RepresentativeName).HasMaxLength(255).IsRequired();
    builder.Property(row => row.RepresentativeEmail).HasMaxLength(254).IsRequired();
    builder.Property(row => row.RepresentativeMobile).HasMaxLength(32).IsRequired();
    builder.Property(row => row.BankName).HasMaxLength(100).IsRequired();
    builder.Property(row => row.AccountNumber).HasMaxLength(64).IsRequired();
    builder.Property(row => row.AccountHolder).HasMaxLength(255).IsRequired();
    builder.Property(row => row.SettlementFrequency).HasMaxLength(16).IsUnicode(false).IsRequired();
    builder.HasIndex(row => new { row.Status, row.SubmittedAtUtc });
    builder.HasMany(row => row.Documents).WithOne().HasForeignKey(row => row.PartnerApplicationId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
