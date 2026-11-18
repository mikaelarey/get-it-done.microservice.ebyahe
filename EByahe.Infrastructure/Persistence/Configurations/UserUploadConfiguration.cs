using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EByahe.Infrastructure.Persistence.Configurations;
public sealed class UserUploadBatchConfiguration : IEntityTypeConfiguration<UserUploadBatch> {
  public void Configure(EntityTypeBuilder<UserUploadBatch> b) {
    b.ToTable("UserUploadBatches", t => t.HasCheckConstraint("CK_UserUploadBatches_Count", "[RequestedCount] BETWEEN 1 AND 500"));
    b.HasKey(x => x.UploadId); b.Property(x => x.UploadId).ValueGeneratedNever();
    b.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
    b.HasOne<Upload>().WithMany().HasForeignKey(x => x.UploadId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<Partner>().WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => x.PartnerId);
  }
}
public sealed class UploadMemberRequestConfiguration : IEntityTypeConfiguration<UploadMemberRequest> {
  public void Configure(EntityTypeBuilder<UploadMemberRequest> b) {
    b.ToTable("UploadMemberRequests"); b.HasKey(x => new { x.UploadId, x.RequestId });
    b.HasOne<UserUploadBatch>().WithMany().HasForeignKey(x => x.UploadId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => x.RequestId).IsUnique();
  }
}
