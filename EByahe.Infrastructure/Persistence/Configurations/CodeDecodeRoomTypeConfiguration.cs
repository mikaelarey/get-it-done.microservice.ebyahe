using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class CodeDecodeRoomTypeConfiguration : IEntityTypeConfiguration<CodeDecodeRoomType>
{
  public void Configure(EntityTypeBuilder<CodeDecodeRoomType> setup)
  {
    setup.ToTable("CodeDecodeRoomTypes"); setup.HasKey(row => row.Id);
    setup.Property(row => row.Id).ValueGeneratedNever();
    setup.Property(row => row.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(row => row.Code);
    setup.Property(row => row.Decode).HasMaxLength(100).IsRequired(); setup.HasIndex(row => row.Decode).IsUnique();
    setup.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
    setup.Property(row => row.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    setup.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    string[] types = ["Standard", "Superior", "Deluxe", "Suite", "Family", "Studio", "Single", "Double", "Twin", "Executive"];
    setup.HasData(types.Select((decode, index) => new CodeDecodeRoomType {
      Id = Guid.Parse($"c0de7001-0000-4000-8000-{index + 1:000000000000}"), Code = index + 1, Decode = decode,
      CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-22T00:00:00+00:00")
    }));
  }
}
