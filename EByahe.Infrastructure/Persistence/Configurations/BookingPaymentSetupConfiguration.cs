using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EByahe.Infrastructure.Persistence.Configurations;

public sealed class BusBookingStatusLogConfiguration : IEntityTypeConfiguration<BusBookingStatusLog>
{
  public void Configure(EntityTypeBuilder<BusBookingStatusLog> builder)
  {
    builder.ToTable("BusBookingStatusLogs"); builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).ValueGeneratedNever();
    builder.Property(x => x.Remarks).HasMaxLength(512);
    builder.HasOne<BusBooking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.BookingId, x.ChangedAtUtc });
    builder.HasIndex(x => x.EventId).IsUnique().HasFilter("[EventId] IS NOT NULL");
  }
}
