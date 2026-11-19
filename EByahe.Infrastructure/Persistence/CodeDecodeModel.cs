using Microsoft.EntityFrameworkCore;
using EByahe.Domain.Models;
namespace EByahe.Infrastructure.Persistence;

internal static class CodeDecodeModel
{
  public static void Configure(ModelBuilder builder)
  {
    builder.Entity<EByahe.Domain.Models.RefUser>().HasData(new RefUser { Id = Guid.Parse("00000000-0000-4000-8000-000000000001"), ExternalUserId = "System", FirstName = "System", LastName = "", Email = "system@internal.getitdone.invalid", Provider = "System", Role = "Normal", CreatedAtUtc = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });

    {
      var setup = builder.Entity<CodeDecodeOutboxStatus>();
      setup.ToTable("CodeDecodeOutboxStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeOutboxStatus { Id = Guid.Parse("0afad4b3-0da3-4752-8e4e-0cac382dc8f0"), Code = 1, Decode = "Pending", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeOutboxStatus { Id = Guid.Parse("06423476-5b10-405e-8333-56d4dc82cd52"), Code = 2, Decode = "Publishing", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeOutboxStatus { Id = Guid.Parse("9f599a8f-e086-4197-8481-1d7879e3097b"), Code = 3, Decode = "Failed", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeOutboxStatus { Id = Guid.Parse("da074c9f-db81-44e6-81e2-e6df1b58183b"), Code = 4, Decode = "Published", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodeOutgoingStatus>();
      setup.ToTable("CodeDecodeOutgoingStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeOutgoingStatus { Id = Guid.Parse("8998740b-c0f6-4597-8c3c-529821f8291a"), Code = 1, Decode = "Pending", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeOutgoingStatus { Id = Guid.Parse("48390a9b-7148-496f-831d-3c12d3efc534"), Code = 2, Decode = "Publishing", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeOutgoingStatus { Id = Guid.Parse("62d50054-008f-4918-8eff-9010bcdbf152"), Code = 3, Decode = "Failed", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeOutgoingStatus { Id = Guid.Parse("d017d6f7-cf95-4501-89db-f676ec40eb3e"), Code = 4, Decode = "Published", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeOutgoingStatus { Id = Guid.Parse("4aa7bd9f-7e04-4121-8c73-57d4c9978707"), Code = 5, Decode = "LeaseExpired", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodeIncomingStatus>();
      setup.ToTable("CodeDecodeIncomingStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeIncomingStatus { Id = Guid.Parse("575db140-d2ae-4093-807f-8b02de9d0232"), Code = 1, Decode = "Received", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeIncomingStatus { Id = Guid.Parse("408529bf-8eed-45e6-870b-c262281e619a"), Code = 2, Decode = "Processing", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeIncomingStatus { Id = Guid.Parse("af81901c-51fa-4e22-8fa7-c383d0433aec"), Code = 3, Decode = "Processed", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeIncomingStatus { Id = Guid.Parse("db3c2bb1-5fcd-40fc-840d-fa3198538fee"), Code = 4, Decode = "Failed", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeIncomingStatus { Id = Guid.Parse("f90fe4d3-1c6f-4a5b-8c39-7cd090d8a01e"), Code = 5, Decode = "Acknowledged", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeIncomingStatus { Id = Guid.Parse("a9472be5-9710-468c-86b4-bdd86ea083ff"), Code = 6, Decode = "AcknowledgementFailed", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<BookingStatus>();
      setup.ToTable("CodeDecodeBookingStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new BookingStatus { Id = Guid.Parse("69de9984-dba6-493d-8e65-241a240dfdcf"), Code = 1, Decode = "Submitted", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new BookingStatus { Id = Guid.Parse("8879fc68-3c13-44ce-874f-4875987bcca5"), Code = 2, Decode = "Confirmed", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new BookingStatus { Id = Guid.Parse("476bcf03-0545-441d-8979-06829f5731bc"), Code = 3, Decode = "Cancelled", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<PaymentStatus>();
      setup.ToTable("CodeDecodePaymentStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new PaymentStatus { Id = Guid.Parse("5fee9e3e-7a19-40be-844e-34c094ed0fa5"), Code = 1, Decode = "Pending Payment", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new PaymentStatus { Id = Guid.Parse("9dd82075-f3cf-44bb-85da-4eaceba65243"), Code = 2, Decode = "Payment Failed", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new PaymentStatus { Id = Guid.Parse("46487216-b2b0-4f86-811a-eaa092ae3fe1"), Code = 3, Decode = "Payment Success", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new PaymentStatus { Id = Guid.Parse("1af56b0d-d074-4442-8a66-432ea0514344"), Code = 4, Decode = "Cancelled", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<PaymentType>();
      setup.ToTable("CodeDecodePaymentTypes"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new PaymentType { Id = Guid.Parse("595e2d36-ed5e-4d91-8a2d-ca1f0afc2d81"), Code = 1, Decode = "GCash", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new PaymentType { Id = Guid.Parse("743539c8-8ead-40ec-8400-8af393cadb3e"), Code = 2, Decode = "Maya", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new PaymentType { Id = Guid.Parse("ad3f2478-9226-4dca-8f14-536c3af1b857"), Code = 3, Decode = "Credit/Debit Card", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new PaymentType { Id = Guid.Parse("b77c0f51-0a7e-46d7-8332-7b158af304b1"), Code = 4, Decode = "Cash", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodeTopic>();
      setup.ToTable("CodeDecodeTopics"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(249).IsUnicode(false).UseCollation("Latin1_General_100_BIN2").IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeTopic { Id = Guid.Parse("479cf819-8d46-48a6-9479-000000000001"), Code = 1, Decode = "get-it-done.notifications.v1", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeTopic { Id = Guid.Parse("479cf819-8d46-48a6-9479-000000000002"), Code = 2, Decode = "get-it-done.ebyahe.partners.v1", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeTopic { Id = Guid.Parse("479cf819-8d46-48a6-9479-000000000003"), Code = 3, Decode = "get-it-done.ebyahe.partner-members.v1", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeTopic { Id = Guid.Parse("479cf819-8d46-48a6-9479-000000000004"), Code = 4, Decode = "get-it-done.account.users.v1", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeTopic { Id = Guid.Parse("479cf819-8d46-48a6-9479-000000000005"), Code = 5, Decode = "get-it-done.payment.completed.v1", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") });
    }
    builder.Entity<OutboxMessage>().HasOne(x => x.TopicSetup).WithMany().HasForeignKey(x => x.CodeDecodeTopic).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<Outgoing>().HasOne(x => x.TopicSetup).WithMany().HasForeignKey(x => x.CodeDecodeTopic).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<Incoming>().HasOne(x => x.TopicSetup).WithMany().HasForeignKey(x => x.CodeDecodeTopic).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    {
      var setup = builder.Entity<CodeDecodeEntityType>();
      setup.ToTable("CodeDecodeEntityTypes"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeEntityType { Id = Guid.Parse("b4c11a61-acb7-4d86-8b09-000000000001"), Code = 1, Decode = "Sole Proprietorship", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEntityType { Id = Guid.Parse("b4c11a61-acb7-4d86-8b09-000000000002"), Code = 2, Decode = "Partnership", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEntityType { Id = Guid.Parse("b4c11a61-acb7-4d86-8b09-000000000003"), Code = 3, Decode = "Corporation", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodeSettlementFrequency>();
      setup.ToTable("CodeDecodeSettlementFrequencies"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeSettlementFrequency { Id = Guid.Parse("b4c11a62-acb7-4d86-8b09-000000000001"), Code = 1, Decode = "Daily", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeSettlementFrequency { Id = Guid.Parse("b4c11a62-acb7-4d86-8b09-000000000002"), Code = 2, Decode = "Weekly", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodeRole>();
      setup.ToTable("CodeDecodeRoles"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(32).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeRole { Id = Guid.Parse("b4c11a63-acb7-4d86-8b09-000000000001"), Code = 1, Decode = "Normal", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeRole { Id = Guid.Parse("b4c11a63-acb7-4d86-8b09-000000000002"), Code = 2, Decode = "Administrator", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodeBank>();
      setup.ToTable("CodeDecodeBanks"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000001"), Code = 1, Decode = "BDO", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000002"), Code = 2, Decode = "BPI", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000003"), Code = 3, Decode = "Metrobank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000004"), Code = 4, Decode = "Land Bank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000005"), Code = 5, Decode = "PNB", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000006"), Code = 6, Decode = "RCBC", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000007"), Code = 7, Decode = "Security Bank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000008"), Code = 8, Decode = "UnionBank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000009"), Code = 9, Decode = "BDO Unibank, Inc.", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000010"), Code = 10, Decode = "Bank of the Philippine Islands (BPI)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000011"), Code = 11, Decode = "Metropolitan Bank and Trust Company (Metrobank)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000012"), Code = 12, Decode = "Land Bank of the Philippines (LANDBANK)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000013"), Code = 13, Decode = "Philippine National Bank (PNB)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000014"), Code = 14, Decode = "Security Bank Corporation", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000015"), Code = 15, Decode = "China Banking Corporation (China Bank)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000016"), Code = 16, Decode = "Union Bank of the Philippines (UnionBank)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000017"), Code = 17, Decode = "Rizal Commercial Banking Corporation (RCBC)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000018"), Code = 18, Decode = "Development Bank of the Philippines (DBP)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000019"), Code = 19, Decode = "Asia United Bank Corporation (AUB)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000020"), Code = 20, Decode = "Bank of Commerce", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000021"), Code = 21, Decode = "Philippine Veterans Bank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000022"), Code = 22, Decode = "Al-Amanah Islamic Investment Bank of the Philippines", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000023"), Code = 23, Decode = "BPI Family Savings Bank / BPI Direct BanKo", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000024"), Code = 24, Decode = "China Bank Savings, Inc.", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000025"), Code = 25, Decode = "Philippine Savings Bank (PSBank)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000026"), Code = 26, Decode = "City Savings Bank Inc.", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000027"), Code = 27, Decode = "Philippine Business Bank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000028"), Code = 28, Decode = "Equicom Savings Bank, Inc.", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000029"), Code = 29, Decode = "ISLA Bank (A Thrift Bank), Inc.", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000030"), Code = 30, Decode = "Maya Bank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000031"), Code = 31, Decode = "GoTyme Bank Corporation", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000032"), Code = 32, Decode = "Tonik Digital Bank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000033"), Code = 33, Decode = "UnionDigital Bank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000034"), Code = 34, Decode = "Maribank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000035"), Code = 35, Decode = "UNO Digital Bank", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBank { Id = Guid.Parse("b4c11a64-acb7-4d86-8b09-000000000036"), Code = 36, Decode = "Overseas Filipino Bank (OFBank)", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") });
    }
    builder.Entity<PartnerApplication>().HasOne<CodeDecodeEntityType>().WithMany().HasForeignKey(x => x.CodeDecodeEntityType).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<PartnerApplication>().HasOne<CodeDecodeSettlementFrequency>().WithMany().HasForeignKey(x => x.CodeDecodeSettlementFrequency).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<PartnerApplication>().HasOne(x => x.Bank).WithMany().HasForeignKey(x => x.CodeDecodeBank).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<RefUser>().HasOne<CodeDecodeRole>().WithMany().HasForeignKey(x => x.CodeDecodeRole).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    {
      var setup = builder.Entity<CodeDecodeEventType>();
      setup.ToTable("CodeDecodeEventTypes"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(100).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000001"), Code = 1, Decode = "partner-application.submitted", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000002"), Code = 2, Decode = "partner-application.under-review", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000003"), Code = 3, Decode = "partner-application.approved", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000004"), Code = 4, Decode = "partner-application.declined", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000005"), Code = 5, Decode = "partner.approved", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000006"), Code = 6, Decode = "partner.created", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000007"), Code = 7, Decode = "partner.updated", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000008"), Code = 8, Decode = "partner.member.requested", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000009"), Code = 9, Decode = "partner-user.updated", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000010"), Code = 10, Decode = "booking.created", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000011"), Code = 11, Decode = "bus.created", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000012"), Code = 12, Decode = "bus.updated", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000013"), Code = 13, Decode = "buses.imported", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000014"), Code = 14, Decode = "terminal.created", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000015"), Code = 15, Decode = "terminal.updated", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000016"), Code = 16, Decode = "terminals.imported", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000017"), Code = 17, Decode = "scheduled-trip.created", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000018"), Code = 18, Decode = "scheduled-trip.updated", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000019"), Code = 19, Decode = "scheduled-trips.imported", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeEventType { Id = Guid.Parse("93ede9f3-4a5c-498e-8613-000000000020"), Code = 20, Decode = "users.imported", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") });
    }
    builder.Entity<OutboxMessage>().HasOne<CodeDecodeEventType>().WithMany().HasForeignKey(x => x.CodeDecodeEventType).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<Outgoing>().HasOne<CodeDecodeEventType>().WithMany().HasForeignKey(x => x.CodeDecodeEventType).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    {
      var setup = builder.Entity<CodeDecodeDocumentType>();
      setup.ToTable("CodeDecodeDocumentTypes"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeDocumentType { Id = Guid.Parse("075dc7bd-87b3-4766-b600-0fb753b81d81"), Code = 1, Decode = "Registration", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeDocumentType { Id = Guid.Parse("d6437bc2-66c5-4e52-9458-a197dcb68f6b"), Code = 2, Decode = "BIR", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeDocumentType { Id = Guid.Parse("4271b5bd-ab67-4797-8239-e0b953bc7c93"), Code = 3, Decode = "Permit", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeDocumentType { Id = Guid.Parse("2184c469-e520-4a58-af41-3a66cb536e6f"), Code = 4, Decode = "Accreditation", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeDocumentType { Id = Guid.Parse("2f4a63db-e528-4ae9-a41b-fe606e520814"), Code = 5, Decode = "Bank Account Proof", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") });
    }
    builder.Entity<PartnerApplicationDocument>().HasOne<CodeDecodeDocumentType>().WithMany().HasForeignKey(x => x.CodeDecodeDocumentType).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    {
      var setup = builder.Entity<CodeDecodeBusinessCategory>();
      setup.ToTable("CodeDecodeBusinessCategories"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeBusinessCategory { Id = Guid.Parse("c60b60a7-5b9e-40bf-9d06-8fb38a5263fd"), Code = 1, Decode = "Bus Operator", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") },
        new CodeDecodeBusinessCategory { Id = Guid.Parse("af7f5cca-4f4a-48b1-9770-21b7e779d3eb"), Code = 2, Decode = "Hotel", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-19T00:00:00+00:00") });
    }
    builder.Entity<PartnerApplication>().HasOne<CodeDecodeBusinessCategory>().WithMany().HasForeignKey(x => x.CodeDecodeBusinessCategory).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<PartnerMemberRequest>().HasOne<CodeDecodeBusinessCategory>().WithMany().HasForeignKey(x => x.CodeDecodeBusinessCategory).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    {
      var setup = builder.Entity<CodeDecodePartnerApplicationStatus>();
      setup.ToTable("CodeDecodePartnerApplicationStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodePartnerApplicationStatus { Id = Guid.Parse("dee48a86-4cc9-427c-8611-b844ae188c55"), Code = 1, Decode = "Submitted", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerApplicationStatus { Id = Guid.Parse("b8a2faa4-467b-4a83-8716-0dc064db4fa0"), Code = 2, Decode = "UnderReview", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerApplicationStatus { Id = Guid.Parse("b5f8ce0b-69d6-40c1-8a1d-14f732468113"), Code = 3, Decode = "Approved", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerApplicationStatus { Id = Guid.Parse("1e9d93f4-44d8-4b81-83c4-3b1c4e7fff7c"), Code = 4, Decode = "Rejected", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodePartnerApplicationApprovalStatus>();
      setup.ToTable("CodeDecodePartnerApplicationApprovalStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodePartnerApplicationApprovalStatus { Id = Guid.Parse("caa490f5-961e-4bda-83bc-25dff02361e0"), Code = 1, Decode = "Pending", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerApplicationApprovalStatus { Id = Guid.Parse("3f1bed28-1627-47b6-8062-f22b423e0d07"), Code = 2, Decode = "UnderReview", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerApplicationApprovalStatus { Id = Guid.Parse("06bd8b55-41dc-47d7-89b5-4334dbc0b2e5"), Code = 3, Decode = "NeedsChanges", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerApplicationApprovalStatus { Id = Guid.Parse("8d7ef477-421d-4b8a-8d06-ee54d2efd88d"), Code = 4, Decode = "Approved", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerApplicationApprovalStatus { Id = Guid.Parse("08b13ca9-5f7b-47b6-82cb-53a76140be30"), Code = 5, Decode = "Rejected", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodePartnerStatus>();
      setup.ToTable("CodeDecodePartnerStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodePartnerStatus { Id = Guid.Parse("9ee1730f-14e4-4375-8e37-15c51ebddb76"), Code = 1, Decode = "Approved", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerStatus { Id = Guid.Parse("c39b77bc-0ca4-41ee-8254-e287222a1ed0"), Code = 2, Decode = "Suspended", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodeScheduledTripStatus>();
      setup.ToTable("CodeDecodeScheduledTripStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodeScheduledTripStatus { Id = Guid.Parse("ecde4545-b6ce-4c39-835d-f90b898fcfd2"), Code = 1, Decode = "Scheduled", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodeScheduledTripStatus { Id = Guid.Parse("d214bcab-649b-4c8c-8776-a450db4d0816"), Code = 2, Decode = "Cancelled", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    {
      var setup = builder.Entity<CodeDecodePartnerMemberRequestStatus>();
      setup.ToTable("CodeDecodePartnerMemberRequestStatuses"); setup.HasKey(x => x.Id);
      setup.Property(x => x.Id).ValueGeneratedNever();
      setup.Property(x => x.Code).UseIdentityColumn(1, 1); setup.HasAlternateKey(x => x.Code);
      setup.Property(x => x.Decode).HasMaxLength(64).IsUnicode(false).IsRequired(); setup.HasIndex(x => x.Decode).IsUnique();
      setup.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").HasDefaultValue("System").IsRequired();
      setup.Property(x => x.DateTimeCreated).HasDefaultValueSql("SYSUTCDATETIME()");
      setup.HasOne<EByahe.Domain.Models.RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
      setup.HasData(
        new CodeDecodePartnerMemberRequestStatus { Id = Guid.Parse("b81ce98a-7318-4480-8ccf-c8c695a18cb9"), Code = 1, Decode = "Pending", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerMemberRequestStatus { Id = Guid.Parse("7b66b932-30a3-4bc7-832d-5139015a3573"), Code = 2, Decode = "Completed", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") },
        new CodeDecodePartnerMemberRequestStatus { Id = Guid.Parse("fdd38f03-f8ad-4429-8591-5a22a0db01f0"), Code = 3, Decode = "Cancelled", CreatedBy = "System", DateTimeCreated = DateTimeOffset.Parse("2026-11-17T00:00:00+00:00") });
    }
    builder.Entity<BusBookingStatusLog>().Ignore(x => x.Status);
    builder.Entity<BusBookingStatusLog>().HasOne<BookingStatus>().WithMany().HasForeignKey(x => x.CodeDecodeBookingStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<BusBookingStatusLog>().Ignore(x => x.PreviousStatus);
    builder.Entity<BusBookingStatusLog>().HasOne<BookingStatus>().WithMany().HasForeignKey(x => x.CodeDecodePreviousBookingStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<BusBookingStatusLog>().Ignore(x => x.PaymentStatus);
    builder.Entity<BusBookingStatusLog>().HasOne<PaymentStatus>().WithMany().HasForeignKey(x => x.CodeDecodePaymentStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<BusBookingStatusLog>().Ignore(x => x.PreviousPaymentStatus);
    builder.Entity<BusBookingStatusLog>().HasOne<PaymentStatus>().WithMany().HasForeignKey(x => x.CodeDecodePreviousPaymentStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<BusBookingStatusLog>().Ignore(x => x.PaymentType);
    builder.Entity<BusBookingStatusLog>().HasOne<PaymentType>().WithMany().HasForeignKey(x => x.CodeDecodePaymentType).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<BusBooking>().Ignore(x => x.Status);
    builder.Entity<BusBooking>().HasOne<BookingStatus>().WithMany().HasForeignKey(x => x.CodeDecodeBookingStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<BusBooking>().Ignore(x => x.PaymentStatus);
    builder.Entity<BusBooking>().HasOne<PaymentStatus>().WithMany().HasForeignKey(x => x.CodeDecodePaymentStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<BusBooking>().Ignore(x => x.PaymentType);
    builder.Entity<BusBooking>().HasOne<PaymentType>().WithMany().HasForeignKey(x => x.CodeDecodePaymentType).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<Partner>().Ignore(x => x.Status);
    builder.Entity<Partner>().HasOne<CodeDecodePartnerStatus>().WithMany().HasForeignKey(x => x.CodeDecodePartnerStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<ScheduledTrip>().Ignore(x => x.Status);
    builder.Entity<ScheduledTrip>().HasOne<CodeDecodeScheduledTripStatus>().WithMany().HasForeignKey(x => x.CodeDecodeScheduledTripStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<Incoming>().Ignore(x => x.Status);
    builder.Entity<Incoming>().HasOne<CodeDecodeIncomingStatus>().WithMany().HasForeignKey(x => x.CodeDecodeIncomingStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<IncomingLog>().Ignore(x => x.Status);
    builder.Entity<IncomingLog>().HasOne<CodeDecodeIncomingStatus>().WithMany().HasForeignKey(x => x.CodeDecodeIncomingStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<OutboxMessage>().Ignore(x => x.Status);
    builder.Entity<OutboxMessage>().HasOne<CodeDecodeOutboxStatus>().WithMany().HasForeignKey(x => x.CodeDecodeOutboxStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<Outgoing>().Ignore(x => x.Status);
    builder.Entity<Outgoing>().HasOne<CodeDecodeOutgoingStatus>().WithMany().HasForeignKey(x => x.CodeDecodeOutgoingStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<PartnerApplication>().Ignore(x => x.Status);
    builder.Entity<PartnerApplication>().HasOne<CodeDecodePartnerApplicationStatus>().WithMany().HasForeignKey(x => x.CodeDecodePartnerApplicationStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<PartnerApplicationApproval>().Ignore(x => x.Status);
    builder.Entity<PartnerApplicationApproval>().HasOne<CodeDecodePartnerApplicationApprovalStatus>().WithMany().HasForeignKey(x => x.CodeDecodePartnerApplicationApprovalStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<PartnerApplicationApproval>().Ignore(x => x.PreviousStatus);
    builder.Entity<PartnerApplicationApproval>().HasOne<CodeDecodePartnerApplicationApprovalStatus>().WithMany().HasForeignKey(x => x.CodeDecodePreviousPartnerApplicationApprovalStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<PartnerApplicationHistory>().Ignore(x => x.Status);
    builder.Entity<PartnerApplicationHistory>().HasOne<CodeDecodePartnerApplicationStatus>().WithMany().HasForeignKey(x => x.CodeDecodePartnerApplicationStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    builder.Entity<PartnerMemberRequest>().Ignore(x => x.Status);
    builder.Entity<PartnerMemberRequest>().HasOne<CodeDecodePartnerMemberRequestStatus>().WithMany().HasForeignKey(x => x.CodeDecodePartnerMemberRequestStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    // Every audit actor must resolve to a durable local user identity.
    foreach (var entity in builder.Model.GetEntityTypes().ToArray())
      foreach (var actor in new[] { "CreatedBy", "UpdatedBy", "ChangedBy", "ChangedById" })
        if (entity.FindProperty(actor) is { ClrType: var propertyType } property && propertyType == typeof(string)
          && !entity.GetForeignKeys().Any(f => f.Properties.Contains(property)))
          builder.Entity(entity.ClrType).HasOne(typeof(EByahe.Domain.Models.RefUser)).WithMany().HasForeignKey(actor)
            .HasPrincipalKey("ExternalUserId").OnDelete(DeleteBehavior.Restrict);
    // SQL BIN2 subjects are case-sensitive; EF's default SQL Server string key comparer is not.
    foreach (var entity in builder.Model.GetEntityTypes())
      foreach (var property in entity.GetProperties().Where(x => x.ClrType == typeof(string) && x.GetCollation() == "Latin1_General_100_BIN2"))
        property.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<string>(
          (left, right) => string.Equals(left, right, StringComparison.Ordinal),
          value => StringComparer.Ordinal.GetHashCode(value), value => value));

  }
}
