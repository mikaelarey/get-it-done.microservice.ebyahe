using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class CodeDecodeRegionConfiguration : IEntityTypeConfiguration<CodeDecodeRegion>
{
  public void Configure(EntityTypeBuilder<CodeDecodeRegion> setup)
  {
    GeographySetupConfiguration.Configure(setup, "CodeDecodeRegions");
    setup.Property(x => x.Description).HasMaxLength(255).IsRequired();
  }
}

public sealed class CodeDecodeProvinceConfiguration : IEntityTypeConfiguration<CodeDecodeProvince>
{
  public void Configure(EntityTypeBuilder<CodeDecodeProvince> setup)
  {
    GeographySetupConfiguration.Configure(setup, "CodeDecodeProvinces");
    setup.HasAlternateKey(x => new { x.Code, x.CodeDecodeRegion });
    setup.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.CodeDecodeRegion)
      .HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    setup.HasIndex(x => new { x.CodeDecodeRegion, x.Decode });
  }
}

public sealed class CodeDecodeMunicipalityConfiguration : IEntityTypeConfiguration<CodeDecodeMunicipality>
{
  public void Configure(EntityTypeBuilder<CodeDecodeMunicipality> setup)
  {
    GeographySetupConfiguration.Configure(setup, "CodeDecodeMunicipalities");
    setup.HasAlternateKey(x => new { x.Code, x.CodeDecodeProvince });
    setup.HasOne(x => x.Province).WithMany().HasForeignKey(x => x.CodeDecodeProvince)
      .HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    setup.HasIndex(x => new { x.CodeDecodeProvince, x.Decode });
  }
}

public sealed class CodeDecodeBarangayConfiguration : IEntityTypeConfiguration<CodeDecodeBarangay>
{
  public void Configure(EntityTypeBuilder<CodeDecodeBarangay> setup)
  {
    GeographySetupConfiguration.Configure(setup, "CodeDecodeBarangays");
    setup.HasAlternateKey(x => new { x.Code, x.CodeDecodeMunicipality });
    setup.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.CodeDecodeMunicipality)
      .HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    setup.HasIndex(x => new { x.CodeDecodeMunicipality, x.Decode });
  }
}

internal static class GeographySetupConfiguration
{
  public static void Configure<TEntity>(EntityTypeBuilder<TEntity> setup, string table) where TEntity : class
  {
    setup.ToTable(table); setup.HasKey("Id");
    setup.Property<Guid>("Id").ValueGeneratedNever();
    setup.Property<int>("Code").UseIdentityColumn(1, 1); setup.HasAlternateKey("Code");
    setup.Property<string>("Decode").HasMaxLength(255).IsRequired();
    setup.Property<string>("CreatedBy").HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
    setup.Property<DateTimeOffset>("DateTimeCreated").HasDefaultValueSql("SYSUTCDATETIME()");
    setup.HasOne<RefUser>().WithMany().HasForeignKey("CreatedBy").HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
  }
}
