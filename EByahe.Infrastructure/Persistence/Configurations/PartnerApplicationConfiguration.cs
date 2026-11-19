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
      table.HasCheckConstraint("CK_PartnerApplications_Status", "[CodeDecodePartnerApplicationStatus] IN (1, 2, 3, 4)");
    });
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.TrackingId).HasMaxLength(36).IsUnicode(false).IsRequired();
    builder.HasIndex(row => row.TrackingId).IsUnique();
    builder.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2");
    builder.HasIndex(row => new { row.CreatedBy, row.SubmittedAtUtc });
    builder.HasIndex(row => row.CreatedBy).IsUnique().HasDatabaseName("UX_PartnerApplications_ActiveCreator")
      .HasFilter("[CreatedBy] IS NOT NULL AND [CodeDecodePartnerApplicationStatus] IN (1, 2)");
    builder.Property(row => row.BusinessName).HasMaxLength(255).IsRequired();
    builder.Property(row => row.TradeName).HasMaxLength(255).IsRequired();
    builder.Property(row => row.Tin).HasMaxLength(32).IsRequired();
    builder.Property(row => row.Address).HasMaxLength(2000).IsRequired();
    builder.Property(row => row.RepresentativeName).HasMaxLength(255).IsRequired();
    builder.Property(row => row.RepresentativeEmail).HasMaxLength(254).IsRequired();
    builder.Property(row => row.RepresentativeMobile).HasMaxLength(32).IsRequired();
    builder.Navigation(row => row.Bank).AutoInclude();
    builder.Property(row => row.AccountNumber).HasMaxLength(64).IsRequired();
    builder.Property(row => row.AccountHolder).HasMaxLength(255).IsRequired();
    builder.HasIndex(row => new { row.CodeDecodePartnerApplicationStatus, row.SubmittedAtUtc });
    builder.HasMany(row => row.Documents).WithOne().HasForeignKey(row => row.PartnerApplicationId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
