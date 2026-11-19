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
      table.HasCheckConstraint("CK_PartnerApplications_Geography", """
        ([CodeDecodeRegion] IS NULL AND [CodeDecodeProvince] IS NULL AND [CodeDecodeMunicipality] IS NULL AND [CodeDecodeBarangay] IS NULL)
        OR ([CodeDecodeRegion] IS NOT NULL AND [CodeDecodeProvince] IS NOT NULL AND [CodeDecodeMunicipality] IS NOT NULL AND [CodeDecodeBarangay] IS NOT NULL)
        """);
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
    builder.HasOne<CodeDecodeRegion>().WithMany().HasForeignKey(row => row.CodeDecodeRegion)
      .HasPrincipalKey(row => row.Code).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<CodeDecodeProvince>().WithMany().HasForeignKey(row => new { row.CodeDecodeProvince, row.CodeDecodeRegion })
      .HasPrincipalKey(row => new { row.Code, row.CodeDecodeRegion }).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<CodeDecodeMunicipality>().WithMany().HasForeignKey(row => new { row.CodeDecodeMunicipality, row.CodeDecodeProvince })
      .HasPrincipalKey(row => new { row.Code, row.CodeDecodeProvince }).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<CodeDecodeBarangay>().WithMany().HasForeignKey(row => new { row.CodeDecodeBarangay, row.CodeDecodeMunicipality })
      .HasPrincipalKey(row => new { row.Code, row.CodeDecodeMunicipality }).OnDelete(DeleteBehavior.Restrict);
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
