using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class RefUserConfiguration : IEntityTypeConfiguration<RefUser>
{
  public void Configure(EntityTypeBuilder<RefUser> builder)
  {
    builder.ToTable("RefUsers", table => table.HasCheckConstraint("CK_RefUsers_Role", "[Role] IN ('Administrator', 'Normal')"));
    builder.HasKey(user => user.Id).HasName("PK_RefUsers");
    builder.Property(user => user.Id).ValueGeneratedNever();
    builder.HasIndex(user => user.ExternalUserId).IsUnique().HasDatabaseName("UX_RefUsers_ExternalUserId");
    builder.Property(user => user.ExternalUserId).HasMaxLength(RefUser.ExternalUserIdMaxLength)
      .UseCollation("Latin1_General_100_BIN2").ValueGeneratedNever();
    builder.Property(user => user.FirstName).HasMaxLength(RefUser.NameMaxLength).IsRequired();
    builder.Property(user => user.LastName).HasMaxLength(RefUser.NameMaxLength).IsRequired();
    builder.Property(user => user.Email).HasMaxLength(RefUser.EmailMaxLength)
      .UseCollation("Latin1_General_100_CI_AS_SC").IsRequired();
    builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("UX_RefUsers_Email");
    builder.Property(user => user.Provider).HasMaxLength(RefUser.ProviderMaxLength)
      .HasDefaultValue(RefUser.ManualProvider).IsRequired();
    builder.Property(user => user.Role).HasMaxLength(RefUser.RoleMaxLength)
      .UseCollation("Latin1_General_100_BIN2").HasDefaultValue(RefUser.NormalRole).IsRequired();
    builder.Property(user => user.CreatedAtUtc).IsRequired();
  }
}
