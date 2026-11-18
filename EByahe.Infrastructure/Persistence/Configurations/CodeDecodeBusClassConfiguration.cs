using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class CodeDecodeBusClassConfiguration : IEntityTypeConfiguration<CodeDecodeBusClass>
{
  public void Configure(EntityTypeBuilder<CodeDecodeBusClass> setup)
  {
    setup.ToTable("CodeDecodeBusClasses"); setup.HasKey(x => x.Id);
    setup.Property(x => x.Id).ValueGeneratedNever();
    setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
    setup.Property(x => x.Decode).HasMaxLength(100).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
    setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
    setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    setup.HasData(CodeDecodeValues.Values["BusClasses"].Select((decode, index) => new CodeDecodeBusClass
    {
      Id = Guid.Parse($"b055c1a5-0000-4000-8000-{index + 1:000000000000}"), Code = index + 1, Decode = decode,
      CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-18T00:00:00+00:00")
    }));
  }
}
