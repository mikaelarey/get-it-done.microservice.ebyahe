using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class PartnerApplicationDocumentConfiguration : IEntityTypeConfiguration<PartnerApplicationDocument>
{
  public void Configure(EntityTypeBuilder<PartnerApplicationDocument> builder)
  {
    builder.ToTable("PartnerApplicationDocuments", table =>
    {
      table.HasCheckConstraint("CK_PartnerApplicationDocuments_DocumentType",
        "[DocumentType] IN ('registration', 'bir', 'permit', 'accreditation', 'bankAccountProof')");
      table.HasCheckConstraint("CK_PartnerApplicationDocuments_ContentType",
        "[ContentType] IN ('application/pdf', 'image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document')");
      table.HasCheckConstraint("CK_PartnerApplicationDocuments_SizeBytes",
        "[SizeBytes] > 0 AND [SizeBytes] <= 10485760");
    });
    builder.HasKey(row => row.Id);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.DocumentType).HasMaxLength(32).IsUnicode(false).IsRequired();
    builder.Property(row => row.FileName).HasMaxLength(255).IsRequired();
    builder.Property(row => row.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
    builder.Property(row => row.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
    builder.Property(row => row.StorageBucket).HasMaxLength(63).IsUnicode(false).IsRequired();
    builder.Property(row => row.StorageKey).HasMaxLength(512).IsUnicode(false).IsRequired();
    builder.HasIndex(row => new { row.PartnerApplicationId, row.DocumentType }).IsUnique();
  }
}
