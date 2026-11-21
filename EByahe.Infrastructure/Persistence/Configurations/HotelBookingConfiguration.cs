using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class HotelBookingConfiguration : IEntityTypeConfiguration<HotelBooking>
{
  public void Configure(EntityTypeBuilder<HotelBooking> b)
  {
    b.ToTable("HotelBookings", table => {
      table.HasCheckConstraint("CK_HotelBookings_Stay", "[CheckOut] > [CheckIn]");
      table.HasCheckConstraint("CK_HotelBookings_Counts", "[RoomCount] BETWEEN 1 AND 100 AND [GuestCount] BETWEEN 1 AND 1000");
      table.HasCheckConstraint("CK_HotelBookings_Total", "[Total] >= 0");
    });
    b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
    b.Property(x => x.Reference).HasMaxLength(64).IsRequired(); b.HasIndex(x => x.Reference).IsUnique();
    b.Property(x => x.GuestName).HasMaxLength(200).IsRequired(); b.Property(x => x.GuestEmail).HasMaxLength(254).IsRequired();
    b.Property(x => x.GuestPhone).HasMaxLength(50).IsRequired(); b.Property(x => x.RoomName).HasMaxLength(200).IsRequired();
    b.Property(x => x.Total).HasPrecision(18, 2);
    b.Property(x => x.CreatedBy).HasMaxLength(255).UseCollation("Latin1_General_100_BIN2").IsRequired();
    b.HasOne<RefUser>().WithMany().HasForeignKey(x => x.CreatedBy).HasPrincipalKey(x => x.ExternalUserId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.BookingStatus).WithMany().HasForeignKey(x => x.CodeDecodeBookingStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    b.HasOne(x => x.PaymentStatus).WithMany().HasForeignKey(x => x.CodeDecodePaymentStatus).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    b.HasIndex(x => new { x.HotelId, x.CreatedAtUtc });
  }
}
