using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EByahe.Tests;
public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task CodeDecodeMigrationRoundTripPreservesBookingsAndStatusLogHistory()
  {
    var booking = await CreateOnlineBookingAsync();
    var failed = Completion(booking) with { EventType = "payment.failed", PaymentStatus = "Payment Failed", PaymentType = "Maya" };
    using var consumer = new TestConsumer(); await ProcessPaymentAsync(consumer, PaymentRecord(failed));
    await using var db = CreateDatabase();
    var logs = await db.BusBookingStatusLogs.OrderBy(x => x.ChangedAtUtc).Select(x => new { x.Id, x.Status, x.PaymentStatus, x.PreviousPaymentStatus, x.PaymentType, x.Remarks }).ToArrayAsync();
    await db.GetService<IMigrator>().MigrateAsync("20261117101408_RenamePaymentSetupTablesToCodeDecode");
    Assert.Equal("Submitted", await db.Database.SqlQueryRaw<string>("SELECT [Status] AS [Value] FROM BusBookings").SingleAsync());
    Assert.Equal("Payment Failed", await db.Database.SqlQueryRaw<string>("SELECT [PaymentStatus] AS [Value] FROM BusBookings").SingleAsync());
    await db.Database.MigrateAsync();
    var row = await db.BusBookings.SingleAsync(x => x.Status == "Submitted" && x.PaymentStatus == "Payment Failed" && x.PaymentType == "Maya");
    Assert.Equal(booking.Id, row.Id); Assert.Equal(0, row.ReservedSeats);
    Assert.Equal(logs, await db.BusBookingStatusLogs.OrderBy(x => x.ChangedAtUtc).Select(x => new { x.Id, x.Status, x.PaymentStatus, x.PreviousPaymentStatus, x.PaymentType, x.Remarks }).ToArrayAsync());
    Assert.Equal(40, (await db.ScheduledTrips.SingleAsync()).AvailableSeats);
  }
}
