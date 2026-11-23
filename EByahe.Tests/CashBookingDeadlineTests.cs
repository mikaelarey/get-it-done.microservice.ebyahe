using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Application.Services;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Messaging;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private sealed class BookingDeadlineClock(DateTimeOffset now) : TimeProvider
  {
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
  }

  [SqlServerFact]
  public async Task CashBookingRequiresAtLeast24HoursAndRegistersOneImmutablePaymentDeadline()
  {
    await SeedBookingTripAsync(); await using var db = CreateDatabase();
    var trip = await db.ScheduledTrips.SingleAsync(); var clock = new BookingDeadlineClock(trip.DepartureAtUtc.AddHours(-24));
    using var original = new PartnerApiFactory(ConnectionString);
    using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => {
      services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
    }));
    using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
    client.DefaultRequestHeaders.Authorization = new("Bearer", original.Token());
    var request = BookingRequest();
    using var created = await client.PostAsJsonAsync("/api/bus-bookings", request); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var booking = (await created.Content.ReadFromJsonAsync<BusBookingResponse>())!;
    clock.Now = clock.Now.AddTicks(1);
    using var rejected = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest());
    Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode); Assert.Contains("cash_booking_unavailable", await rejected.Content.ReadAsStringAsync());
    using var replay = await client.PostAsJsonAsync("/api/bus-bookings", request); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    var online = BookingRequest(); online.Payment.Method = "paymongo"; online.Payment.Provider = "PayMongo";
    using var allowed = await client.PostAsJsonAsync("/api/bus-bookings", online); Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    var message = Assert.Single(await db.OutboxMessages.Where(row => row.TopicSetup!.Decode == CashBookingDeadlineOutbox.DefaultTopic).ToArrayAsync());
    using var payload = JsonDocument.Parse(message.Payload); var root = payload.RootElement;
    Assert.Equal(booking.Id.ToString(), message.MessageKey); Assert.Equal(booking.Id, root.GetProperty("bookingId").GetGuid());
    Assert.Equal(booking.CreatedAtUtc.AddHours(24), root.GetProperty("dueAtUtc").GetDateTimeOffset());
    Assert.DoesNotContain("passengers", message.Payload); Assert.DoesNotContain("contact", message.Payload);
    Assert.Equal(2, await db.BusBookings.CountAsync()); Assert.Equal(40, (await db.ScheduledTrips.AsNoTracking().SingleAsync()).AvailableSeats);
  }

  [SqlServerFact]
  public async Task ExpiredCashBookingCancelsWithRemarksWithoutReservingSeatsAndReplaysAreIdempotent()
  {
    var (booking, partnerId) = await CashBookingAsync();
    await using (var db = CreateDatabase()) {
      var saved = await db.BusBookings.SingleAsync(); saved.CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-25); await db.SaveChangesAsync();
    }
    var cancellation = CashCompletion(booking, partnerId) with { EventType = "payment.cancelled", PaymentStatus = "Cancelled",
      AcceptedBy = null, CancellationReason = "cash_payment_expired" };
    using var consumer = new TestConsumer();
    await Assert.ThrowsAsync<InvalidDataException>(() => ProcessPaymentAsync(consumer, PaymentRecord(cancellation with { OccurredAtUtc = DateTimeOffset.UtcNow.AddHours(-2) }, 900)));
    await ProcessPaymentAsync(consumer, PaymentRecord(cancellation, 901)); await ProcessPaymentAsync(consumer, PaymentRecord(cancellation, 902));
    await using var check = CreateDatabase(); var cancelled = await check.BusBookings.AsNoTracking().SingleAsync();
    Assert.Equal("Cancelled", cancelled.Status); Assert.Equal("Cancelled", cancelled.PaymentStatus); Assert.Null(cancelled.PaymentId);
    Assert.Equal(0, cancelled.ReservedSeats); Assert.Equal(40, (await check.ScheduledTrips.SingleAsync()).AvailableSeats);
    var log = Assert.Single(await check.BusBookingStatusLogs.Where(row => row.Status == "Cancelled").ToArrayAsync());
    Assert.Equal(CashBookingPolicy.ExpiredRemark, log.Remarks); Assert.Null(log.ChangedBy);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var details = (await client.GetFromJsonAsync<BusBookingDetailsResponse>($"/api/bus-bookings/{booking.Id}"))!;
    Assert.Equal(CashBookingPolicy.ExpiredRemark, details.CancellationRemarks);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: Cashier, resourceAccess: BusRoles("booking.read", "booking.confirm")));
    Assert.False((await client.GetFromJsonAsync<PartnerCashBookingSummary>($"/api/partner/bookings/{booking.Id}/cash-payment-summary"))!.CanAcceptCash);
  }

  [SqlServerFact]
  public async Task ExistingUnpaidCashBookingsAreRegisteredWithoutExtendingTheirOriginalDeadline()
  {
    var (booking, _) = await CashBookingAsync(); await using var db = CreateDatabase();
    var saved = await db.BusBookings.AsNoTracking().SingleAsync(); var legacyId = Guid.NewGuid();
    db.BusBookings.Add(new BusBooking { Id = legacyId, RequestId = Guid.NewGuid(), ScheduledTripId = saved.ScheduledTripId,
      CreatedBy = saved.CreatedBy, Reference = "BK-" + legacyId.ToString("N").ToUpperInvariant(), CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
      Total = saved.Total, DetailsJson = saved.DetailsJson, RequestHash = saved.RequestHash, PaymentType = "Cash" });
    await db.SaveChangesAsync();
    await CashBookingRegistrationWorker.QueueMissingAsync(db, CashBookingDeadlineOutbox.DefaultTopic, default);
    await CashBookingRegistrationWorker.QueueMissingAsync(db, CashBookingDeadlineOutbox.DefaultTopic, default);
    var events = await db.OutboxMessages.Where(row => row.TopicSetup!.Decode == CashBookingDeadlineOutbox.DefaultTopic).ToArrayAsync();
    Assert.Equal(2, events.Length); var message = Assert.Single(events, row => row.MessageKey == legacyId.ToString());
    using var payload = JsonDocument.Parse(message.Payload); Assert.Equal(payload.RootElement.GetProperty("createdAtUtc").GetDateTimeOffset().AddHours(24),
      payload.RootElement.GetProperty("dueAtUtc").GetDateTimeOffset());
    Assert.True(payload.RootElement.GetProperty("dueAtUtc").GetDateTimeOffset() < DateTimeOffset.UtcNow);
  }
}
