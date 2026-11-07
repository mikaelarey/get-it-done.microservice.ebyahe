using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class PartnerMemberRequestConfiguration : IEntityTypeConfiguration<PartnerMemberRequest>
{
  public void Configure(EntityTypeBuilder<PartnerMemberRequest> builder)
  {
    builder.ToTable("PartnerMemberRequests"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
    builder.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    builder.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    builder.Property(x => x.FirstName).HasMaxLength(255).IsRequired();
    builder.Property(x => x.LastName).HasMaxLength(255).IsRequired();
    builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
    builder.Property(x => x.BusinessCategory).HasMaxLength(32).IsRequired();
    builder.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
    builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
    builder.HasIndex(x => new { x.PartnerId, x.Email }).IsUnique().HasFilter("[Status] = 'Pending'");
  }
}
