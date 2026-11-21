using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class HotelConfiguration : IEntityTypeConfiguration<Hotel>
{
  public void Configure(EntityTypeBuilder<Hotel> b)
  {
    b.ToTable("Hotels", table => table.HasCheckConstraint("CK_Hotels_Deletion",
      "([IsDeleted] = 0 AND [DeletedBy] IS NULL AND [DeletedAtUtc] IS NULL) OR ([IsDeleted] = 1 AND [DeletedBy] IS NOT NULL AND [DeletedAtUtc] IS NOT NULL)"));
    b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
    b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    b.Property(x => x.Name).HasMaxLength(200).IsRequired();
    b.Property(x => x.Address).HasMaxLength(1000).IsRequired();
    b.Property(x => x.ContactNumber).HasMaxLength(50).IsRequired();
    b.Property(x => x.Email).HasMaxLength(254).IsRequired();
    b.Property(x => x.Description).HasMaxLength(2000);
    b.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.Property(x => x.IsDeleted).HasDefaultValue(false);
    b.Property(x => x.DeletedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2");
    b.HasOne<RefUser>().WithMany().HasForeignKey(x => x.DeletedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.CodeDecodeRegion).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.Province).WithMany().HasForeignKey(x => new { x.CodeDecodeProvince, x.CodeDecodeRegion })
      .HasPrincipalKey(x => new { x.Code, x.CodeDecodeRegion }).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => new { x.CodeDecodeMunicipality, x.CodeDecodeProvince })
      .HasPrincipalKey(x => new { x.Code, x.CodeDecodeProvince }).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => new { x.CodeDecodeBarangay, x.CodeDecodeMunicipality })
      .HasPrincipalKey(x => new { x.Code, x.CodeDecodeMunicipality }).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => new { x.PartnerId, x.Name, x.CodeDecodeBarangay }).IsUnique().HasFilter("[IsDeleted] = 0");
  }
}

public sealed class HotelLogConfiguration : IEntityTypeConfiguration<HotelLog>
{
  public void Configure(EntityTypeBuilder<HotelLog> b)
  {
    b.ToTable("HotelLogs", table => table.HasCheckConstraint("CK_HotelLogs_Values",
      "[Action] IN ('Created','Updated','Deleted','Baseline') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)"));
    b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.Property(row => row.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    b.Property(row => row.Action).HasMaxLength(20).IsUnicode(false).IsRequired(); b.Property(row => row.CurrentValue).IsRequired();
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<Hotel>().WithMany().HasForeignKey(row => row.HotelId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => new { row.HotelId, row.DateTimeCreated });
  }
}
