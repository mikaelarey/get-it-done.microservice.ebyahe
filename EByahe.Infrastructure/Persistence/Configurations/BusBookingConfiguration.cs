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
      table.HasCheckConstraint("CK_BusBookings_Status", "[CodeDecodeBookingStatus] IN (1, 2, 3)");
      table.HasCheckConstraint("CK_BusBookings_ReservedSeats", "[ReservedSeats] BETWEEN 0 AND 20 AND ([ReservedSeats] = 0 OR ([ScheduledTripId] IS NOT NULL AND [CodeDecodeBookingStatus] = 2 AND [CodeDecodePaymentStatus] = 3))");
      table.HasCheckConstraint("CK_BusBookings_CancelledPayment", "([CodeDecodeBookingStatus] = 3 AND [CodeDecodePaymentStatus] = 4) OR ([CodeDecodeBookingStatus] <> 3 AND [CodeDecodePaymentStatus] <> 4)");
      table.HasCheckConstraint("CK_BusBookings_ConfirmedPayment", "[CodeDecodeBookingStatus] <> 2 OR ([CodeDecodePaymentStatus] = 3 AND [ReservedSeats] > 0)");
    });
    builder.HasKey(row => row.Id);
    builder.Property(row => row.ReservedSeats).HasDefaultValue(0);
    builder.HasOne<ScheduledTrip>().WithMany().HasForeignKey(row => row.ScheduledTripId).OnDelete(DeleteBehavior.Restrict);
    builder.Property(row => row.Id).ValueGeneratedNever();
    builder.Property(row => row.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    builder.Property(row => row.Reference).HasMaxLength(35).IsUnicode(false).IsRequired();
    builder.Property(row => row.Total).HasPrecision(18, 2);
    builder.Property(row => row.RequestHash).HasMaxLength(64).IsUnicode(false).IsRequired();
    builder.Property(row => row.DetailsJson).IsRequired();
    builder.HasIndex(row => row.Reference).IsUnique();
    builder.Property(row => row.ProviderPaymentId).HasMaxLength(100).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
    builder.HasIndex(row => row.PaymentId).IsUnique().HasFilter("[PaymentId] IS NOT NULL");
    builder.HasIndex(row => row.ProviderPaymentId).IsUnique().HasFilter("[ProviderPaymentId] IS NOT NULL");
    builder.HasIndex(row => new { row.CreatedBy, row.RequestId }).IsUnique();
    builder.HasIndex(row => new { row.CreatedBy, row.CreatedAtUtc });
  }
}
