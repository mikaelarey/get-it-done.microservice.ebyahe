using System.Net;
using System.Net.Http.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task BookingPaymentStatusSubmissionIsPendingAndHistoryIsOwnedAndAppendOnly()
  {
    var booking = await CreateOnlineBookingAsync();
    Assert.Equal(0, booking.ReservedSeats); Assert.Equal("Pending Payment", booking.PaymentStatus);
    await using var db = CreateDatabase();
    Assert.Equal(40, (await db.ScheduledTrips.SingleAsync()).AvailableSeats);
    Assert.Equal(3, await db.CodeDecodeBookingStatuses.CountAsync()); Assert.Equal(4, await db.CodeDecodePaymentStatuses.CountAsync());
    Assert.Equal(4, await db.CodeDecodePaymentTypes.CountAsync());
    var log = await db.BusBookingStatusLogs.SingleAsync();
    Assert.Null(log.PreviousStatus); Assert.Equal("Submitted", log.Status);
    Assert.Equal("Pending Payment", log.PaymentStatus);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/bus-bookings/{booking.Id}/status-history")).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "another-owner"));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/bus-bookings/{booking.Id}/status-history")).StatusCode);
    log.Remarks = "Edited history";
    await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
  }

  [SqlServerFact]
  public async Task BookingPaymentStatusFailureThenSuccessReservesOnceAndLateFailureDoesNotUndoSuccess()
  {
    var booking = await CreateOnlineBookingAsync(); var success = Completion(booking) with { PaymentType = "GCash" };
    var failure = success with { EventId = Guid.NewGuid(), EventType = "payment.failed", PaymentStatus = "Payment Failed", ProviderPaymentId = "pay_failed" };
    using var consumer = new TestConsumer();
    await ProcessPaymentAsync(consumer, PaymentRecord(failure));
    await using (var db = CreateDatabase())
    {
      var saved = await db.BusBookings.SingleAsync();
      Assert.Equal("Submitted", saved.Status); Assert.Equal("Payment Failed", saved.PaymentStatus);
      Assert.Equal(0, saved.ReservedSeats); Assert.Equal(40, (await db.ScheduledTrips.SingleAsync()).AvailableSeats);
    }
    await ProcessPaymentAsync(consumer, PaymentRecord(success, 1));
    await ProcessPaymentAsync(consumer, PaymentRecord(success, 2));
    await ProcessPaymentAsync(consumer, PaymentRecord(failure with { EventId = Guid.NewGuid() }, 3));
    await using (var db = CreateDatabase())
    {
      var saved = await db.BusBookings.SingleAsync();
      Assert.Equal("Confirmed", saved.Status); Assert.Equal("Payment Success", saved.PaymentStatus);
      Assert.Equal("GCash", saved.PaymentType); Assert.Equal(1, saved.ReservedSeats);
      Assert.Equal(39, (await db.ScheduledTrips.SingleAsync()).AvailableSeats);
      Assert.Equal(4, await db.BusBookingStatusLogs.CountAsync());
    }
  }

  [SqlServerFact]
  public async Task BookingPaymentStatusCancellationIsLoggedAndCannotAcceptLaterPayment()
  {
    var booking = await CreateOnlineBookingAsync(); var success = Completion(booking);
    var cancelled = success with { EventId = Guid.NewGuid(), EventType = "payment.cancelled", PaymentStatus = "Cancelled", ProviderSessionId = "", ProviderPaymentId = "" };
    using var consumer = new TestConsumer();
    await ProcessPaymentAsync(consumer, PaymentRecord(cancelled));
    await Assert.ThrowsAsync<InvalidDataException>(() => ProcessPaymentAsync(consumer, PaymentRecord(success, 1)));
    await using var db = CreateDatabase(); var saved = await db.BusBookings.SingleAsync();
    Assert.Equal("Cancelled", saved.Status); Assert.Equal("Cancelled", saved.PaymentStatus); Assert.Equal(0, saved.ReservedSeats);
    Assert.Equal(40, (await db.ScheduledTrips.SingleAsync()).AvailableSeats);
    Assert.Contains("no longer available", (await db.BusBookingStatusLogs.SingleAsync(x => x.EventId == cancelled.EventId)).Remarks);
  }

  [SqlServerFact]
  public async Task BookingPaymentStatusConcurrentSuccessfulPaymentsCannotOverbookLastSeats()
  {
    await SeedBookingTripAsync();
    await using (var db = CreateDatabase())
    { var trip = await db.ScheduledTrips.SingleAsync(); trip.AvailableSeats = 1; await db.SaveChangesAsync(); }
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var receipts = new List<BusBookingResponse>();
    for (var i = 0; i < 2; i++)
    {
      var request = ReservationRequest(1); request.Payment.Type = "Maya";
      using var response = await client.PostAsJsonAsync("/api/bus-bookings", request);
      Assert.Equal(HttpStatusCode.Created, response.StatusCode);
      receipts.Add((await response.Content.ReadFromJsonAsync<BusBookingResponse>())!);
    }
    await Task.WhenAll(receipts.Select(async (booking, index) =>
    {
      using var consumer = new TestConsumer();
      var payment = Completion(booking) with { ProviderPaymentId = "pay_" + booking.Id.ToString("N"), PaymentType = "Maya" };
      await ProcessPaymentAsync(consumer, PaymentRecord(payment, index));
    }));
    await using (var db = CreateDatabase())
    {
      Assert.Equal(0, (await db.ScheduledTrips.SingleAsync()).AvailableSeats);
      Assert.Equal(1, await db.BusBookings.CountAsync(x => x.Status == "Confirmed"));
      Assert.Equal(1, await db.BusBookings.SumAsync(x => x.ReservedSeats));
      var exception = await db.BusBookings.SingleAsync(x => x.PaymentReviewRequired);
      Assert.Equal("Submitted", exception.Status); Assert.Equal("Payment Success", exception.PaymentStatus);
      Assert.Equal(0, exception.ReservedSeats);
      Assert.Contains("Manual refund", (await db.BusBookingStatusLogs.SingleAsync(x => x.BookingId == exception.Id && x.EventId != null)).Remarks);
    }
  }

  [SqlServerFact]
  public async Task BookingPaymentStatusDatabaseRejectsReservationBeforeSuccessfulConfirmation()
  {
    var booking = await CreateOnlineBookingAsync();
    await using var db = CreateDatabase(); var saved = await db.BusBookings.SingleAsync();
    saved.ReservedSeats = 1;
    await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
  }

  [SqlServerFact]
  public async Task BookingPaymentStatusMigrationReleasesLegacyUnpaidSeatsAndPreservesPaidSeats()
  {
    await SeedBookingTripAsync();
    await using var db = CreateDatabase();
    var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    var previous = migrations.Single(x => x.EndsWith("_AddBookingPaymentConfirmation", StringComparison.Ordinal));
    await db.GetService<IMigrator>().MigrateAsync(previous);
    var pending = Guid.NewGuid(); var paid = Guid.NewGuid();
    var request = System.Text.Json.JsonSerializer.Serialize(ReservationRequest(1), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [ScheduledTrips] SET [AvailableSeats] = 37 WHERE [Id] = {TestTripId}");
    foreach (var entry in new[] { (pending, "Submitted", 2), (paid, "Confirmed", 1) })
      await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [BusBookings] ([Id],[RequestId],[CreatedBy],[Reference],[CreatedAtUtc],[RequestHash],[DetailsJson],[ScheduledTripId],[ReservedSeats],[Status],[Total]) VALUES ({entry.Item1},{Guid.NewGuid()},{PartnerApiFactory.Subject},{"BK-" + entry.Item1.ToString("N").ToUpperInvariant()},{DateTimeOffset.UtcNow},{new string('A',64)},{request},{TestTripId},{entry.Item3},{entry.Item2},{772.5m})");
    await db.Database.MigrateAsync();
    var unpaid = await db.BusBookings.SingleAsync(x => x.Id == pending);
    var confirmed = await db.BusBookings.SingleAsync(x => x.Id == paid);
    Assert.Equal(0, unpaid.ReservedSeats); Assert.Equal("Pending Payment", unpaid.PaymentStatus);
    Assert.Equal(1, confirmed.ReservedSeats); Assert.Equal("Payment Success", confirmed.PaymentStatus);
    Assert.Equal(39, (await db.ScheduledTrips.SingleAsync()).AvailableSeats);
    Assert.Equal(2, await db.BusBookingStatusLogs.CountAsync());
    Assert.False(db.Database.HasPendingModelChanges());
  }
}
