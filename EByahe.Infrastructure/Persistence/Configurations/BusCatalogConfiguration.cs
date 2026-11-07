using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
  public void Configure(EntityTypeBuilder<Partner> b)
  {
    b.ToTable("Partners", t => t.HasCheckConstraint("CK_Partners_Status", "[Status] IN ('Approved', 'Suspended')"));
    b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
    b.Property(x => x.Code).HasMaxLength(64).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
    b.Property(x => x.DisplayName).HasMaxLength(255).IsRequired();
    b.Property(x => x.Status).HasMaxLength(32).IsRequired();
    b.Property(x => x.OwnerUserId).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2");
    b.HasOne<PartnerApplication>().WithMany().HasForeignKey(x => x.PartnerApplicationId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => x.PartnerApplicationId).IsUnique().HasFilter("[PartnerApplicationId] IS NOT NULL");
  }
}
public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
  public void Configure(EntityTypeBuilder<Location> b)
  {
    b.ToTable("Locations"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
    b.Property(x => x.Code).HasMaxLength(64).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
    b.Property(x => x.Name).HasMaxLength(100).IsRequired();
    b.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
  }
}
public sealed class BusTerminalConfiguration : IEntityTypeConfiguration<BusTerminal>
{
  public void Configure(EntityTypeBuilder<BusTerminal> b)
  {
    b.ToTable("BusTerminals"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasAlternateKey(x => new { x.Id, x.PartnerId });
    b.Property(x => x.Name).HasMaxLength(254).IsRequired(); b.Property(x => x.Address).HasMaxLength(500).IsRequired();
    b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => new { x.PartnerId, x.LocationId, x.Name }).IsUnique();
  }
}
public sealed class BusConfiguration : IEntityTypeConfiguration<Bus>
{
  public void Configure(EntityTypeBuilder<Bus> b)
  {
    b.ToTable("Buses", t => t.HasCheckConstraint("CK_Buses_Capacity", "[SeatCapacity] BETWEEN 1 AND 100"));
    b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasAlternateKey(x => new { x.Id, x.PartnerId });
    b.Property(x => x.FleetNumber).HasMaxLength(64).IsRequired(); b.Property(x => x.BusClass).HasMaxLength(100).IsRequired();
    b.Property(x => x.PlateNumber).HasMaxLength(20).IsUnicode(false);
    b.HasIndex(x => x.PlateNumber).IsUnique().HasFilter("[PlateNumber] IS NOT NULL");
    b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => new { x.PartnerId, x.FleetNumber }).IsUnique();
  }
}
public sealed class BusRouteConfiguration : IEntityTypeConfiguration<BusRoute>
{
  public void Configure(EntityTypeBuilder<BusRoute> b)
  {
    b.ToTable("BusRoutes", t => {
      t.HasCheckConstraint("CK_BusRoutes_DifferentTerminals", "[OriginTerminalId] <> [DestinationTerminalId]");
      t.HasCheckConstraint("CK_BusRoutes_Duration", "[DurationMinutes] > 0");
    });
    b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasAlternateKey(x => new { x.Id, x.PartnerId });
    b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.OriginTerminal).WithMany().HasForeignKey(x => new { x.OriginTerminalId, x.PartnerId })
      .HasPrincipalKey(x => new { x.Id, x.PartnerId }).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.DestinationTerminal).WithMany().HasForeignKey(x => new { x.DestinationTerminalId, x.PartnerId })
      .HasPrincipalKey(x => new { x.Id, x.PartnerId }).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => new { x.PartnerId, x.OriginTerminalId, x.DestinationTerminalId }).IsUnique();
  }
}
public sealed class ScheduledTripConfiguration : IEntityTypeConfiguration<ScheduledTrip>
{
  public void Configure(EntityTypeBuilder<ScheduledTrip> b)
  {
    b.ToTable("ScheduledTrips", t => {
      t.HasCheckConstraint("CK_ScheduledTrips_Fare", "[BaseFare] > 0");
      t.HasCheckConstraint("CK_ScheduledTrips_AvailableSeats", "[AvailableSeats] IS NULL OR [AvailableSeats] BETWEEN 0 AND 100");
      t.HasCheckConstraint("CK_ScheduledTrips_Status", "[Status] IN ('Scheduled', 'Cancelled')");
    });
    b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.BaseFare).HasPrecision(18, 2); b.Property(x => x.Status).HasMaxLength(32).IsRequired();
    b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.Bus).WithMany().HasForeignKey(x => new { x.BusId, x.PartnerId })
      .HasPrincipalKey(x => new { x.Id, x.PartnerId }).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.BusRoute).WithMany().HasForeignKey(x => new { x.BusRouteId, x.PartnerId })
      .HasPrincipalKey(x => new { x.Id, x.PartnerId }).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => new { x.BusId, x.DepartureAtUtc }).IsUnique();
    b.HasIndex(x => new { x.Status, x.DepartureAtUtc, x.BusRouteId });
  }
}
