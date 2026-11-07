using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class PartnerUserConfiguration : IEntityTypeConfiguration<PartnerUser>
{
  public void Configure(EntityTypeBuilder<PartnerUser> builder)
  {
    builder.ToTable("PartnerUsers");
    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).ValueGeneratedNever();
    builder.HasOne(x => x.Partner).WithMany(x => x.UserMemberships)
      .HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(x => x.RefUser).WithMany(x => x.PartnerMemberships)
      .HasForeignKey(x => x.RefUserId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.PartnerId, x.RefUserId }).IsUnique();
    builder.HasIndex(x => new { x.RefUserId, x.IsActive });
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
  }
}
