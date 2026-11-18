using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class CodeDecodeUploadTypeConfiguration : IEntityTypeConfiguration<CodeDecodeUploadType>
{
  public void Configure(EntityTypeBuilder<CodeDecodeUploadType> b)
  {
    b.ToTable("CodeDecodeUploadTypes"); b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.Property(row => row.Code).UseIdentityColumn(1, 1); b.HasAlternateKey(row => row.Code);
    b.Property(row => row.Decode).HasMaxLength(100).IsRequired(); b.HasIndex(row => row.Decode).IsUnique();
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
    b.Property(row => row.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    var created = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);
    b.HasData(
      new CodeDecodeUploadType { Id = Guid.Parse("a11d0000-0000-4000-8000-000000000001"), Code = UploadTypeCodes.Buses, Decode = "Buses", DateTimeCreated = created },
      new CodeDecodeUploadType { Id = Guid.Parse("a11d0000-0000-4000-8000-000000000002"), Code = UploadTypeCodes.Terminals, Decode = "Terminals", DateTimeCreated = created },
      new CodeDecodeUploadType { Id = Guid.Parse("a11d0000-0000-4000-8000-000000000003"), Code = UploadTypeCodes.ScheduledTrips, Decode = "ScheduledTrips", DateTimeCreated = created },
      new CodeDecodeUploadType { Id = Guid.Parse("a11d0000-0000-4000-8000-000000000004"), Code = UploadTypeCodes.Users, Decode = "Users", DateTimeCreated = created });
  }
}

public sealed class UploadConfiguration : IEntityTypeConfiguration<Upload>
{
  public void Configure(EntityTypeBuilder<Upload> b)
  {
    b.ToTable("Uploads"); b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.Property(row => row.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<CodeDecodeUploadType>().WithMany().HasForeignKey(row => row.UploadType).HasPrincipalKey(row => row.Code).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => new { row.CreatedBy, row.DateTimeCreated });
  }
}

public sealed class UploadRecordConfiguration : IEntityTypeConfiguration<UploadRecord>
{
  public void Configure(EntityTypeBuilder<UploadRecord> b)
  {
    b.ToTable("UploadRecords", table => table.HasCheckConstraint("CK_UploadRecords_OneRecord",
      "(CASE WHEN [BusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TerminalId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [ScheduledTripId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [RefUserId] IS NULL THEN 0 ELSE 1 END) = 1"));
    b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.HasOne<Upload>().WithMany().HasForeignKey(row => row.UploadId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<Bus>().WithMany().HasForeignKey(row => row.BusId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<BusTerminal>().WithMany().HasForeignKey(row => row.TerminalId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<ScheduledTrip>().WithMany().HasForeignKey(row => row.ScheduledTripId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.RefUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => row.UploadId);
    b.HasIndex(row => new { row.UploadId, row.BusId }).IsUnique().HasFilter("[BusId] IS NOT NULL");
    b.HasIndex(row => new { row.UploadId, row.TerminalId }).IsUnique().HasFilter("[TerminalId] IS NOT NULL");
    b.HasIndex(row => new { row.UploadId, row.ScheduledTripId }).IsUnique().HasFilter("[ScheduledTripId] IS NOT NULL");
    b.HasIndex(row => new { row.UploadId, row.RefUserId }).IsUnique().HasFilter("[RefUserId] IS NOT NULL");
  }
}

public sealed class ScheduledTripLogConfiguration : IEntityTypeConfiguration<ScheduledTripLog>
{
  public void Configure(EntityTypeBuilder<ScheduledTripLog> b)
  {
    b.ToTable("ScheduledTripLogs", table => table.HasCheckConstraint("CK_ScheduledTripLogs_Values",
      "[Action] IN ('Created','Updated','Imported') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)"));
    b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.Property(row => row.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    b.Property(row => row.Action).HasMaxLength(20).IsUnicode(false).IsRequired(); b.Property(row => row.CurrentValue).IsRequired();
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<ScheduledTrip>().WithMany().HasForeignKey(row => row.ScheduledTripId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<Upload>().WithMany().HasForeignKey(row => row.UploadId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => new { row.ScheduledTripId, row.DateTimeCreated });
  }
}

public sealed class BusLogConfiguration : IEntityTypeConfiguration<BusLog>
{
  public void Configure(EntityTypeBuilder<BusLog> b)
  {
    b.ToTable("BusLogs", table => table.HasCheckConstraint("CK_BusLogs_Values",
      "[Action] IN ('Created','Updated','Imported') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)"));
    b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.Property(row => row.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    b.Property(row => row.Action).HasMaxLength(20).IsUnicode(false).IsRequired(); b.Property(row => row.CurrentValue).IsRequired();
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<Bus>().WithMany().HasForeignKey(row => row.BusId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<Upload>().WithMany().HasForeignKey(row => row.UploadId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => new { row.BusId, row.DateTimeCreated });
  }
}

public sealed class TerminalLogConfiguration : IEntityTypeConfiguration<TerminalLog>
{
  public void Configure(EntityTypeBuilder<TerminalLog> b)
  {
    b.ToTable("TerminalLogs", table => table.HasCheckConstraint("CK_TerminalLogs_Values",
      "[Action] IN ('Created','Updated','Imported') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)"));
    b.HasKey(row => row.Id); b.Property(row => row.Id).ValueGeneratedNever();
    b.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.Property(row => row.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
    b.Property(row => row.Action).HasMaxLength(20).IsUnicode(false).IsRequired(); b.Property(row => row.CurrentValue).IsRequired();
    b.HasOne<RefUser>().WithMany().HasForeignKey(row => row.CreatedBy).HasPrincipalKey(row => row.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<BusTerminal>().WithMany().HasForeignKey(row => row.TerminalId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne<Upload>().WithMany().HasForeignKey(row => row.UploadId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(row => new { row.TerminalId, row.DateTimeCreated });
  }
}
