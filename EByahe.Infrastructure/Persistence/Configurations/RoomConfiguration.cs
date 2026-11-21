using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
  public void Configure(EntityTypeBuilder<Room> b)
  {
    b.ToTable("Rooms", table => {
      table.HasCheckConstraint("CK_Rooms_Capacity", "[GuestCapacity] BETWEEN 1 AND 100");
      table.HasCheckConstraint("CK_Rooms_Rate", "[NightlyRate] BETWEEN 0 AND 99999999.99");
      table.HasCheckConstraint("CK_Rooms_Deletion", "([IsDeleted] = 0 AND [DeletedBy] IS NULL AND [DeletedAtUtc] IS NULL) OR ([IsDeleted] = 1 AND [DeletedBy] IS NOT NULL AND [DeletedAtUtc] IS NOT NULL)");
    });
    b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.HasOne(row => row.Hotel).WithMany().HasForeignKey(row => row.HotelId).OnDelete(DeleteBehavior.Restrict);
    b.Property(row => row.Name).HasMaxLength(200).IsRequired();
    b.HasOne(row => row.RoomType).WithMany().HasForeignKey(row => row.CodeDecodeRoomType).HasPrincipalKey(row => row.Code).OnDelete(DeleteBehavior.Restrict);
    b.Property(row => row.Description).HasMaxLength(2000);
    b.Property(row => row.NightlyRate).HasPrecision(18, 2);
    b.Property(row => row.IsDeleted).HasDefaultValue(false);
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.Property(row => row.DeletedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2");
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.DeletedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => new { row.HotelId, row.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
  }
}

public sealed class RoomLogConfiguration : IEntityTypeConfiguration<RoomLog>
{
  public void Configure(EntityTypeBuilder<RoomLog> b)
  {
    b.ToTable("RoomLogs", table => table.HasCheckConstraint("CK_RoomLogs_Values",
      "[Action] IN ('Created','Updated','Deleted') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)"));
    b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.Property(row => row.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    b.Property(row => row.Action).HasMaxLength(20).IsUnicode(false).IsRequired(); b.Property(row => row.CurrentValue).IsRequired();
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<Room>().WithMany().HasForeignKey(row => row.RoomId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => new { row.RoomId, row.DateTimeCreated });
  }
}

public sealed class RoomImageConfiguration : IEntityTypeConfiguration<RoomImage>
{
  public void Configure(EntityTypeBuilder<RoomImage> b)
  {
    b.ToTable("RoomImages", table => {
      table.HasCheckConstraint("CK_RoomImages_Size", "[SizeBytes] > 0 AND [SizeBytes] <= 5242880");
      table.HasCheckConstraint("CK_RoomImages_Type", "[ContentType] IN ('image/jpeg','image/png','image/webp')");
      table.HasCheckConstraint("CK_RoomImages_Deletion", "([IsDeleted] = 0 AND [DeletedBy] IS NULL AND [DeletedAtUtc] IS NULL) OR ([IsDeleted] = 1 AND [DeletedBy] IS NOT NULL AND [DeletedAtUtc] IS NOT NULL)");
    });
    b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.HasOne(row => row.Room).WithMany(row => row.Images).HasForeignKey(row => row.RoomId).OnDelete(DeleteBehavior.Restrict);
    b.Property(row => row.FileName).HasMaxLength(200).IsRequired();
    b.Property(row => row.ContentType).HasMaxLength(40).IsUnicode(false).IsRequired();
    b.Property(row => row.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
    b.Property(row => row.StorageBucket).HasMaxLength(255).IsRequired();
    b.Property(row => row.StorageKey).HasMaxLength(1024).IsRequired();
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.Property(row => row.DeletedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2");
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.DeletedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => new { row.RoomId, row.SortOrder });
  }
}
