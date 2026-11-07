using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EByahe.Infrastructure.Persistence.Configurations;
public sealed class TemporaryPasswordConfiguration : IEntityTypeConfiguration<TemporaryPassword>
{
  public void Configure(EntityTypeBuilder<TemporaryPassword> builder)
  {
    builder.ToTable("TemporaryPasswords"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
    builder.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    builder.HasIndex(x => x.RequestId).IsUnique(); builder.Property(x => x.ProtectedPassword).HasMaxLength(512).IsRequired();
  }
}
