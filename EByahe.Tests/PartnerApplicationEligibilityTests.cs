using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.DataModels;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task ActiveApplicationUniqueIndexAlsoProtectsDirectDatabaseWrites()
  {
    await using var database = CreateDatabase();
    database.PartnerApplications.Add(ReviewApplication("First application", "hotel", "Submitted", 1, "same-owner"));
    await database.SaveChangesAsync();
    database.PartnerApplications.Add(ReviewApplication("Duplicate application", "bus-operator", "UnderReview", 0, "same-owner"));
    var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    var sql = Assert.IsType<Microsoft.Data.SqlClient.SqlException>(error.InnerException);
    Assert.Contains(sql.Number, new[] { 2601, 2627 });
    Assert.Contains("UX_PartnerApplications_ActiveCreator", sql.Message);
    database.ChangeTracker.Clear();
    Assert.Equal(1, await database.PartnerApplications.CountAsync());
    Assert.Equal(1, await database.PartnerApplicationHistories.CountAsync());
    Assert.Equal(1, await database.Outgoing.CountAsync());
  }

  [SqlServerFact]
  public async Task OnlySubmittedAndUnderReviewBlockAnotherApplicationAcrossCategories()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    const string eligibilityUrl = "/api/partner-applications/eligibility";
    Assert.True((await client.GetFromJsonAsync<PartnerApplicationEligibility>(eligibilityUrl))!.CanSubmit);
    using var form = ApplicationForm("hotel");
    using var created = await client.PostAsync("/api/partner-applications", form);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var receipt = (await created.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    await using var database = CreateDatabase();
    var application = await database.PartnerApplications.SingleAsync();
    foreach (var status in new[] { "Submitted", "UnderReview" })
    {
      application.Status = status;
      await database.SaveChangesAsync();
      using var eligibilityResponse = await client.GetAsync(eligibilityUrl + "?userId=somebody-else");
      Assert.True(eligibilityResponse.Headers.CacheControl!.Private);
      Assert.True(eligibilityResponse.Headers.CacheControl.NoStore);
      var eligibility = (await eligibilityResponse.Content.ReadFromJsonAsync<PartnerApplicationEligibility>())!;
      Assert.False(eligibility.CanSubmit);
      Assert.Equal(new ActivePartnerApplication(receipt.Id, receipt.TrackingId, status), eligibility.ActiveApplication);
      var histories = await database.PartnerApplicationHistories.CountAsync();
      var outgoing = await database.Outgoing.CountAsync();
      using var duplicate = ApplicationForm("bus-operator");
      using var rejected = await client.PostAsync("/api/partner-applications", duplicate);
      Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
      using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
      Assert.Equal("active_partner_application", problem.RootElement.GetProperty("code").GetString());
      Assert.Equal(receipt.Id, problem.RootElement.GetProperty("activeApplication").GetProperty("id").GetGuid());
      Assert.Equal(5, factory.Storage.PutCalls);
      Assert.Equal(1, await database.PartnerApplications.CountAsync());
      Assert.Equal(5, await database.PartnerApplicationDocuments.CountAsync());
      Assert.Equal(1, await database.PartnerApplicationApprovals.CountAsync());
      Assert.Equal(histories, await database.PartnerApplicationHistories.CountAsync());
      Assert.Equal(outgoing, await database.Outgoing.CountAsync());
    }
    foreach (var finalStatus in new[] { "Approved", "Rejected" })
    {
      application.Status = finalStatus;
      await database.SaveChangesAsync();
      var eligibility = (await client.GetFromJsonAsync<PartnerApplicationEligibility>(eligibilityUrl))!;
      Assert.True(eligibility.CanSubmit);
      Assert.Null(eligibility.ActiveApplication);
      using var nextForm = ApplicationForm("bus-operator");
      using var next = await client.PostAsync("/api/partner-applications", nextForm);
      Assert.Equal(HttpStatusCode.Created, next.StatusCode);
      var nextId = (await next.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!.Id;
      application = (await database.PartnerApplications.FindAsync(nextId))!;
    }
    Assert.Equal(3, await database.PartnerApplications.CountAsync());
  }

  [SqlServerFact]
  public async Task EligibilityUsesOnlyTheAuthenticatedExactOwnerAndRequiresAuthentication()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    using var created = await client.PostAsync("/api/partner-applications", form);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    foreach (var subject in new[] { "other-user", PartnerApiFactory.Subject.ToUpperInvariant() })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: subject));
      var allowed = (await client.GetFromJsonAsync<PartnerApplicationEligibility>("/api/partner-applications/eligibility"))!;
      Assert.True(allowed.CanSubmit);
      Assert.Null(allowed.ActiveApplication);
      using var otherForm = ApplicationForm();
      using var other = await client.PostAsync("/api/partner-applications", otherForm);
      Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = null;
    using var unauthorized = await client.GetAsync("/api/partner-applications/eligibility");
    Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
  }

  [SqlServerFact]
  public async Task ConcurrentSubmissionsCommitOnlyOneApplicationAndCleanTheLosingUploads()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var firstClient = factory.AuthenticatedClient();
    using var secondClient = factory.AuthenticatedClient();
    var arrivals = 0;
    var bothUploading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    factory.Storage.BeforePut = async () =>
    {
      var arrival = Interlocked.Increment(ref arrivals);
      if (arrival == 2) bothUploading.TrySetResult();
      if (arrival <= 2) await bothUploading.Task.WaitAsync(TimeSpan.FromSeconds(20));
    };
    using var firstForm = ApplicationForm("hotel");
    using var secondForm = ApplicationForm("bus-operator");
    var responses = await Task.WhenAll(firstClient.PostAsync("/api/partner-applications", firstForm),
      secondClient.PostAsync("/api/partner-applications", secondForm));
    try
    {
      Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, responses.Select(row => row.StatusCode).OrderBy(row => row));
      await using var database = CreateDatabase();
      var application = await database.PartnerApplications.SingleAsync();
      Assert.Equal(PartnerApiFactory.Subject, application.CreatedBy);
      Assert.Equal(5, await database.PartnerApplicationDocuments.CountAsync());
      Assert.Equal(1, await database.PartnerApplicationHistories.CountAsync());
      Assert.Equal(1, await database.PartnerApplicationApprovals.CountAsync());
      Assert.Equal(1, await database.Outgoing.CountAsync());
      Assert.Equal(1, await database.OutboxMessages.CountAsync());
      Assert.Equal(10, factory.Storage.PutCalls);
      Assert.Equal(5, factory.Storage.Objects.Count);
      var keys = await database.PartnerApplicationDocuments.Select(row => row.StorageKey).ToArrayAsync();
      Assert.All(keys, key => Assert.True(factory.Storage.Objects.ContainsKey(key)));
    }
    finally { foreach (var response in responses) response.Dispose(); }
  }
}
