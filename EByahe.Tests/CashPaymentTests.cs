using System.Net;
using System.Net.Http.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private const string Cashier = "partner-cashier";
  private async Task<(BusBookingResponse Booking, Guid PartnerId)> CashBookingAsync()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var created = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest());
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var booking = (await created.Content.ReadFromJsonAsync<BusBookingResponse>())!;
    await using var db = CreateDatabase();
    var partnerId = (await db.ScheduledTrips.SingleAsync()).PartnerId;
    db.PartnerUsers.Add(new PartnerUser { PartnerId = partnerId, RefUser = MembershipUser(Cashier) });
    await db.SaveChangesAsync();
    return (booking, partnerId);
  }
  private static PaymentCompletedEvent CashCompletion(BusBookingResponse booking, Guid partnerId) => Completion(booking) with
  { Provider = "cash", ProviderSessionId = "", ProviderPaymentId = "", PaymentType = "Cash", PartnerId = partnerId, AcceptedBy = Cashier };

  [SqlServerFact]
  public async Task PartnerCashSummaryRequiresReadAndConfirmAndChecksMembershipDepartureAndCapacity()
  {
    var (booking, partnerId) = await CashBookingAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var path = $"/api/partner/bookings/{booking.Id}/cash-payment-summary";
    foreach (var roles in new[] { BusRoles("booking.read"), BusRoles("booking.confirm"), ReviewRoles(), BookingRoles() })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: Cashier, resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "another-owner", resourceAccess: BusRoles("booking.read", "booking.confirm")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: Cashier, resourceAccess: BusRoles("booking.read", "booking.confirm")));
    using var response = await client.GetAsync(path); Assert.True(response.Headers.CacheControl!.NoStore);
    var summary = (await response.Content.ReadFromJsonAsync<PartnerCashBookingSummary>())!;
    Assert.True(summary.CanAcceptCash); Assert.Equal(partnerId, summary.PartnerId); Assert.Equal(booking.Total, summary.Total);
    Assert.Equal(PartnerApiFactory.Subject, summary.OwnerId); Assert.Equal(1, summary.Passengers);
    var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("contact", json); Assert.DoesNotContain("fullName", json);
    await using var db = CreateDatabase(); var trip = await db.ScheduledTrips.SingleAsync();
    trip.AvailableSeats = 0; await db.SaveChangesAsync();
    Assert.False((await client.GetFromJsonAsync<PartnerCashBookingSummary>(path))!.CanAcceptCash);
    trip.AvailableSeats = 40; trip.DepartureAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
    Assert.False((await client.GetFromJsonAsync<PartnerCashBookingSummary>(path))!.CanAcceptCash);
    var membership = await db.PartnerUsers.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
  }

  [SqlServerFact]
  public async Task CashCompletionIsAtomicAuditedAndIdempotentAndRejectsWrongPartnersActorsAndAmounts()
  {
    var (booking, partnerId) = await CashBookingAsync(); var payment = CashCompletion(booking, partnerId);
    using var consumer = new TestConsumer();
    foreach (var invalid in new[] { payment with { PartnerId = Guid.NewGuid() }, payment with { AcceptedBy = "another-owner" },
      payment with { AmountCentavos = 100 }, payment with { PaymentType = "GCash" } })
      await Assert.ThrowsAsync<InvalidDataException>(() => ProcessPaymentAsync(consumer, PaymentRecord(invalid, ++cashOffset)));
    Assert.Equal(0, consumer.Commits);
    await using (var db = CreateDatabase())
    {
      await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectCashReservation ON ScheduledTripLogs AFTER INSERT AS THROW 51001, 'Simulated audit failure', 1;");
      await Assert.ThrowsAsync<DbUpdateException>(() => ProcessPaymentAsync(consumer, PaymentRecord(payment, ++cashOffset)));
      Assert.Equal("Pending Payment", (await db.BusBookings.AsNoTracking().SingleAsync()).PaymentStatus);
      Assert.Equal(40, (await db.ScheduledTrips.AsNoTracking().SingleAsync()).AvailableSeats);
      await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectCashReservation");
    }
    await ProcessPaymentAsync(consumer, PaymentRecord(payment, ++cashOffset));
    await ProcessPaymentAsync(consumer, PaymentRecord(payment, cashOffset));
    await ProcessPaymentAsync(consumer, PaymentRecord(payment, ++cashOffset));
    await using var check = CreateDatabase(); Assert.False(check.Database.HasPendingModelChanges());
    var saved = await check.BusBookings.SingleAsync();
    Assert.Equal("Payment Success", saved.PaymentStatus); Assert.Equal("Confirmed", saved.Status); Assert.Equal("Cash", saved.PaymentType);
    Assert.Equal(payment.PaymentId, saved.PaymentId); Assert.Null(saved.ProviderPaymentId); Assert.Equal(payment.OccurredAtUtc, saved.PaidAtUtc);
    Assert.Equal(1, saved.ReservedSeats); Assert.False(saved.PaymentReviewRequired); Assert.Equal(39, (await check.ScheduledTrips.SingleAsync()).AvailableSeats);
    var log = Assert.Single(await check.BusBookingStatusLogs.Where(x => x.EventId == payment.EventId).ToListAsync());
    Assert.Equal(Cashier, log.ChangedBy); Assert.Equal(Cashier, (await check.ScheduledTripLogs.SingleAsync()).CreatedBy);
  }
  private long cashOffset;

  [SqlServerFact]
  public async Task ConcurrentCashReceiptsNeverOversellAndPreservePaymentsThatNeedManualReview()
  {
    var (first, partnerId) = await CashBookingAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var created = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest());
    var second = (await created.Content.ReadFromJsonAsync<BusBookingResponse>())!;
    await using (var db = CreateDatabase()) { var trip = await db.ScheduledTrips.SingleAsync(); trip.AvailableSeats = 1; await db.SaveChangesAsync(); }
    using var consumerA = new TestConsumer(); using var consumerB = new TestConsumer();
    await Task.WhenAll(ProcessPaymentAsync(consumerA, PaymentRecord(CashCompletion(first, partnerId), 100)),
      ProcessPaymentAsync(consumerB, PaymentRecord(CashCompletion(second, partnerId), 101)));
    await using var check = CreateDatabase(); var bookings = await check.BusBookings.ToListAsync();
    Assert.Equal(0, (await check.ScheduledTrips.SingleAsync()).AvailableSeats); Assert.Equal(1, bookings.Sum(x => x.ReservedSeats));
    Assert.Single(bookings, x => x.Status == "Confirmed"); Assert.Single(bookings, x => x.PaymentReviewRequired && x.ReservedSeats == 0);
    Assert.All(bookings, x => { Assert.Equal("Payment Success", x.PaymentStatus); Assert.NotNull(x.PaymentId); });
    Assert.Equal(1, consumerA.Commits); Assert.Equal(1, consumerB.Commits);
  }
}
