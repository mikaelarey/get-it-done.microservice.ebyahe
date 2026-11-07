using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class BusBookingConfiguration : IEntityTypeConfiguration<BusBooking>
{
  public void Configure(EntityTypeBuilder<BusBooking> builder)
  {
    builder.ToTable("BusBookings", table =>
    {
      table.HasCheckConstraint("CK_BusBookings_DetailsJson", "ISJSON([DetailsJson]) = 1");
      table.HasCheckConstraint("CK_BusBookings_Total", "[Total] >= 0");
      table.HasCheckConstraint("CK_BusBookings_Status", "[Status] = 'Submitted'");
      table.HasCheckConstraint("CK_BusBookings_ReservedSeats", "[ReservedSeats] BETWEEN 0 AND 20 AND ([ReservedSeats] = 0 OR [ScheduledTripId] IS NOT NULL)");
    });
    builder.HasKey(row => row.Id);
    builder.Property(row => row.ReservedSeats).HasDefaultValue(0);
    builder.HasOne<ScheduledTrip>().WithMany().HasForeignKey(row => row.ScheduledTripId).OnDelete(DeleteBehavior.Restrict);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    builder.Property(row => row.Reference).HasMaxLength(35).IsUnicode(false).IsRequired();
    builder.Property(row => row.Status).HasMaxLength(32).IsUnicode(false).IsRequired();
    builder.Property(row => row.Total).HasPrecision(18, 2);
    builder.Property(row => row.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
    builder.Property(row => row.DetailsJson).IsRequired();
    builder.HasIndex(row => row.Reference).IsUnique();
    builder.HasIndex(row => new { row.CreatedBy, row.RequestId }).IsUnique();
    builder.HasIndex(row => new { row.CreatedBy, row.CreatedAtUtc });
  }
}
