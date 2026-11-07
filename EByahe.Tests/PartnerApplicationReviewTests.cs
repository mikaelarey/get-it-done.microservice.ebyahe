using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Application.Dtos.Responses;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static object ReviewRoles(bool admin = true, bool review = true) => new Dictionary<string, object>
  {
    ["get-it-done-web"] = new { roles = admin ? new[] { "admin" } : [] },
    ["get-it-done-ebyahe-api"] = new { roles = review ? new[] { "partner.review" } : [] }
  };

  private static PartnerApplication ReviewApplication(string name, string category, string status, int age, string creator)
  {
    var id = Guid.NewGuid();
    return new PartnerApplication
    {
      Id = id, TrackingId = $"APP-{id:N}".ToUpperInvariant(), CreatedBy = creator,
      BusinessName = name, BusinessCategory = category, Status = status,
      TradeName = name, EntityType = "corporation", Tin = "PRIVATE-TAX",
      Address = "Manila terminal", RepresentativeName = "Review Contact", RepresentativeEmail = "contact@example.com",
      RepresentativeMobile = "09171234567", BankName = "Private Bank", AccountNumber = "987654321234567890",
      AccountHolder = "Private Holder", SettlementFrequency = "weekly", SubmittedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-age)
    };
  }

  [SqlServerFact]
  public async Task ReviewApiRequiresBothExactClientRolesAndValidSignedAccessToken()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string url = "/api/admin/partner-applications";
    var protectedPaths = new[] { url, $"{url}/{Guid.NewGuid()}", $"{url}/{Guid.NewGuid()}/documents/{Guid.NewGuid()}" };
    foreach (var token in new string?[] { null, "invalid", factory.Token(resourceAccess: ReviewRoles(), expired: true),
      factory.Token(resourceAccess: ReviewRoles(), trusted: false), factory.Token(resourceAccess: ReviewRoles(), audience: "get-it-done-account-api"),
      factory.Token(resourceAccess: ReviewRoles(), issuer: "https://foreign.example"), factory.Token(resourceAccess: ReviewRoles(), type: "ID") })
    {
      client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);
      foreach (var path in protectedPaths)
      { using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
      foreach (var action in new[] { "review", "approve", "decline" })
      { using var response = await client.PostAsync($"{url}/{Guid.NewGuid()}/{action}", null); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
    }
    var wrongClients = new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "admin", "partner.review" } } };
    var mainRole = new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "admin" } },
      ["get-it-done-main-api"] = new { roles = new[] { "partner.review" } } };
    foreach (var roles in new object[] { new { }, ReviewRoles(admin: false), ReviewRoles(review: false), wrongClients, mainRole,
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = "admin" },
        ["get-it-done-ebyahe-api"] = new { roles = new[] { "partner.review" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles,
        realmAccess: new { roles = new[] { "admin", "partner.review" } }));
      foreach (var path in protectedPaths)
      { using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); }
      foreach (var action in new[] { "review", "approve", "decline" })
      { using var response = await client.PostAsync($"{url}/{Guid.NewGuid()}/{action}", null); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); }
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    using var allowed = await client.GetAsync(url); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    Assert.True(allowed.Headers.CacheControl!.NoStore);
    var empty = (await allowed.Content.ReadFromJsonAsync<PartnerApplicationReviewPage>())!;
    Assert.Empty(empty.Items); Assert.Equal(0, empty.Summary.TotalApplications);
    Assert.Empty(empty.Categories); Assert.Empty(empty.Statuses);
  }

  [SqlServerFact]
  public async Task ReviewListReadsAllApplicantsWithDatabasePagingFilteringSummaryAndNoPayoutData()
  {
    await using var db = CreateDatabase();
    var newest = ReviewApplication("Northbound Transit", "bus-operator", "Submitted", 1, "applicant-one");
    var hotel = ReviewApplication("Harbor Hotel", "hotel", "UnderReview", 2, "applicant-two");
    var approved = ReviewApplication("Approved Transport", "bus-operator", "Approved", 3, "applicant-three");
    var legacy = ReviewApplication("Legacy Hotel", "hotel", "Rejected", 4, "legacy");
    db.PartnerApplications.AddRange(newest, hotel, approved, legacy); await db.SaveChangesAsync();
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PartnerApplications SET CreatedBy = NULL WHERE Id = {legacy.Id}");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "reviewer", resourceAccess: ReviewRoles()));
    var first = (await client.GetFromJsonAsync<PartnerApplicationReviewPage>("/api/admin/partner-applications?page=1&pageSize=2"))!;
    Assert.Equal(4, first.TotalCount); Assert.Equal(1, first.Page); Assert.Equal(2, first.PageSize);
    Assert.Equal(new[] { newest.Id, hotel.Id }, first.Items.Select(x => x.Id));
    Assert.Equal(new PartnerApplicationReviewSummary(4, 2, 1), first.Summary);
    Assert.Equal(new[] { "bus-operator", "hotel" }, first.Categories);
    Assert.Equal(new[] { "Approved", "Rejected", "Submitted", "UnderReview" }, first.Statuses);
    var second = (await client.GetFromJsonAsync<PartnerApplicationReviewPage>("/api/admin/partner-applications?page=2&pageSize=2"))!;
    Assert.Equal(new[] { approved.Id, legacy.Id }, second.Items.Select(x => x.Id));
    var filtered = (await client.GetFromJsonAsync<PartnerApplicationReviewPage>(
      "/api/admin/partner-applications?search=%20harbor%20&category=hotel&status=UnderReview"))!;
    Assert.Equal(hotel.Id, Assert.Single(filtered.Items).Id); Assert.Equal(1, filtered.TotalCount);
    Assert.Equal(first.Summary, filtered.Summary); Assert.Equal(first.Categories, filtered.Categories);
    foreach (var field in new[] { newest.TrackingId, "Review Contact", "contact@example.com", "Manila terminal" })
    {
      var match = (await client.GetFromJsonAsync<PartnerApplicationReviewPage>("/api/admin/partner-applications?search=" + Uri.EscapeDataString(field)))!;
      Assert.Contains(match.Items, item => item.Id == newest.Id);
    }
    var missing = (await client.GetFromJsonAsync<PartnerApplicationReviewPage>("/api/admin/partner-applications?category=unknown"))!;
    Assert.Equal(0, missing.TotalCount); Assert.Empty(missing.Items);
    var json = await client.GetStringAsync("/api/admin/partner-applications");
    foreach (var field in new[] { "accountNumber", "bankName", "accountHolder", "tin", "representativeMobile", "documents", "storageKey", "PRIVATE-TAX", "Private Bank" })
      Assert.DoesNotContain("\"" + field + "\"", json);
    // The existing customer endpoint still scopes reads to its authenticated owner.
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "applicant-one"));
    var own = (await client.GetFromJsonAsync<EByahe.Application.Dtos.Responses.CustomerPartnerApplicationPage>("/api/partner-applications"))!;
    Assert.Equal(newest.Id, Assert.Single(own.Items).Id);
    Assert.Equal(4, await db.PartnerApplications.CountAsync());
  }

  [SqlServerFact]
  public async Task ReviewerCanReadOtherApplicantsDetailsAndVerifiedDocumentsWithoutChangingCustomerOwnership()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
    Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
    var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "reviewer", resourceAccess: ReviewRoles()));
    var url = $"/api/admin/partner-applications/{receipt.Id}";
    using var response = await client.GetAsync(url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(response.Headers.CacheControl!.NoStore);
    var details = (await response.Content.ReadFromJsonAsync<CustomerPartnerApplicationDetails>())!;
    Assert.Equal(receipt.Id, details.Id); Assert.Equal(receipt.TrackingId, details.TrackingId); Assert.Equal(5, details.Documents.Count);
    await using var db = CreateDatabase(); var original = await db.PartnerApplications.SingleAsync(x => x.Id == receipt.Id);
    var body = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain(original.AccountNumber, body); Assert.DoesNotContain("storageKey", body);
    Assert.DoesNotContain("storageBucket", body);
    var digits = new string(original.AccountNumber.Where(char.IsDigit).ToArray());
    Assert.EndsWith(digits[^4..], details.MaskedAccountNumber);
    using var customer = await client.GetAsync($"/api/partner-applications/{receipt.Id}"); Assert.Equal(HttpStatusCode.NotFound, customer.StatusCode);
    var proof = details.Documents.Single(x => x.DocumentType == "bankAccountProof"); var documentUrl = $"{url}/documents/{proof.Id}";
    using var document = await client.GetAsync(documentUrl);
    Assert.Equal(HttpStatusCode.OK, document.StatusCode); Assert.Equal(Pdf, await document.Content.ReadAsByteArrayAsync());
    Assert.Equal("application/pdf", document.Content.Headers.ContentType!.MediaType);
    Assert.Equal("inline", document.Content.Headers.ContentDisposition!.DispositionType);
    Assert.True(document.Headers.CacheControl!.Private); Assert.True(document.Headers.CacheControl.NoStore);
    Assert.Equal("nosniff", Assert.Single(document.Headers.GetValues("X-Content-Type-Options")));
    using var ranged = new HttpRequestMessage(HttpMethod.Get, documentUrl); ranged.Headers.Range = new RangeHeaderValue(0, 4);
    using var partial = await client.SendAsync(ranged);
    Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode); Assert.Equal(Pdf[..5], await partial.Content.ReadAsByteArrayAsync());
    var reads = factory.Storage.ReadCalls;
    foreach (var missingPath in new[] { $"/api/admin/partner-applications/{Guid.NewGuid()}", $"{url}/documents/{Guid.NewGuid()}",
      $"/api/admin/partner-applications/{Guid.NewGuid()}/documents/{proof.Id}" })
    { using var missing = await client.GetAsync(missingPath); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
    foreach (var deniedPath in new[] { url, documentUrl })
    { using var denied = await client.GetAsync(deniedPath); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
    Assert.Equal(reads, factory.Storage.ReadCalls);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "reviewer", resourceAccess: ReviewRoles()));
    factory.Storage.CorruptReads = true;
    using var corrupt = await client.GetAsync(documentUrl); Assert.Equal(HttpStatusCode.ServiceUnavailable, corrupt.StatusCode);
  }

  [SqlServerFact]
  public async Task ReviewerDecisionsPersistWithHistoryAreIdempotentAndRejectConflictingDecisions()
  {
    await SeedReviewOwnerAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    foreach (var (action, status, opposite) in new[] { ("approve", "Approved", "decline"), ("decline", "Rejected", "approve") })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
      using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
      var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
      var url = $"/api/admin/partner-applications/{receipt.Id}";
      using var ownerDenied = await client.PostAsync($"{url}/{action}", null);
      Assert.Equal(HttpStatusCode.Forbidden, ownerDenied.StatusCode);
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "decision-reviewer", resourceAccess: ReviewRoles()));
      var remarks = action == "decline" ? "  The required permit is not valid.  " : null;
      using var premature = await client.PostAsJsonAsync($"{url}/{action}", new { remarks });
      Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
      using var started = await client.PostAsync(url + "/review", null);
      Assert.Equal(HttpStatusCode.OK, started.StatusCode);
      Assert.Equal("UnderReview", (await started.Content.ReadFromJsonAsync<CustomerPartnerApplicationDetails>())!.Status);
      using var startRetry = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.OK, startRetry.StatusCode);
      using var response = await client.PostAsJsonAsync($"{url}/{action}", new { remarks });
      Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
      var details = (await response.Content.ReadFromJsonAsync<CustomerPartnerApplicationDetails>())!;
      Assert.Equal(status, details.Status); Assert.Equal(5, details.Documents.Count);
      using var repeat = await client.PostAsJsonAsync($"{url}/{action}", new { remarks = "A retry must not replace the original remarks." });
      Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
      using var conflict = await client.PostAsJsonAsync($"{url}/{opposite}", new { remarks = "Conflicting decision" }); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
      using var restart = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.Conflict, restart.StatusCode);
      await using var db = CreateDatabase();
      Assert.Equal(status, (await db.PartnerApplications.SingleAsync(x => x.Id == receipt.Id)).Status);
      var approvals = await db.PartnerApplicationApprovals.Where(x => x.PartnerApplicationId == receipt.Id).ToArrayAsync();
      Assert.Equal(3, approvals.Length);
      var review = Assert.Single(approvals, x => x.Status == "UnderReview");
      Assert.Equal("Pending", review.PreviousStatus); Assert.Equal("decision-reviewer", review.ChangedBy); Assert.Null(review.Remarks);
      var decision = Assert.Single(approvals, x => x.Status == status);
      Assert.Equal("decision-reviewer", decision.ChangedBy); Assert.Equal("UnderReview", decision.PreviousStatus);
      Assert.Equal(remarks?.Trim(), decision.Remarks);
      var histories = await db.PartnerApplicationHistories.Where(x => x.PartnerApplicationId == receipt.Id).ToArrayAsync();
      Assert.Equal(3, histories.Length);
      Assert.Single(histories, x => x.Status == "UnderReview");
      var history = Assert.Single(histories, x => x.Status == status);
      Assert.Contains("\"Status\":\"UnderReview\"", history.PreviousValue!);
      Assert.Contains($"\"Status\":\"{status}\"", history.CurrentValue);
      Assert.Equal(status, (await client.GetFromJsonAsync<CustomerPartnerApplicationDetails>(url))!.Status);
    }
    using var missing = await client.PostAsync($"/api/admin/partner-applications/{Guid.NewGuid()}/approve", null);
    Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
  }

  [SqlServerFact]
  public async Task ConcurrentReviewersCannotOverwriteEachOthersDecision()
  {
    await SeedReviewOwnerAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
    var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "concurrent-reviewer", resourceAccess: ReviewRoles()));
    var url = $"/api/admin/partner-applications/{receipt.Id}";
    using var review = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.OK, review.StatusCode);
    var responses = await Task.WhenAll(client.PostAsync(url + "/approve", null), client.PostAsJsonAsync(url + "/decline", new { remarks = "Invalid supporting document" }));
    try
    {
      Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
      Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
      var winner = (await responses.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<CustomerPartnerApplicationDetails>())!;
      await using var db = CreateDatabase();
      Assert.Equal(winner.Status, (await db.PartnerApplications.SingleAsync(x => x.Id == receipt.Id)).Status);
      Assert.Equal(3, await db.PartnerApplicationApprovals.CountAsync(x => x.PartnerApplicationId == receipt.Id));
      Assert.Equal(3, await db.PartnerApplicationHistories.CountAsync(x => x.PartnerApplicationId == receipt.Id));
    }
    finally { foreach (var response in responses) response.Dispose(); }
  }

  [SqlServerFact]
  public async Task ReviewDecisionValidatesRemarksWithoutChangingStatusOrAuditHistory()
  {
    await SeedReviewOwnerAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
    var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "reviewer", resourceAccess: ReviewRoles()));
    var url = $"/api/admin/partner-applications/{receipt.Id}";
    using var review = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.OK, review.StatusCode);
    using var emptyBody = await client.PostAsync(url + "/decline", null); Assert.Equal(HttpStatusCode.BadRequest, emptyBody.StatusCode);
    foreach (var remarks in new string?[] { null, "", " \t\r\n", new string('x', 2001) })
    {
      using var invalid = await client.PostAsJsonAsync(url + "/decline", new { remarks });
      Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
    using var tooLong = await client.PostAsJsonAsync(url + "/approve", new { remarks = new string('x', 2001) });
    Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    await using var db = CreateDatabase();
    Assert.Equal("UnderReview", (await db.PartnerApplications.AsNoTracking().SingleAsync(x => x.Id == receipt.Id)).Status);
    Assert.Equal(2, await db.PartnerApplicationApprovals.CountAsync(x => x.PartnerApplicationId == receipt.Id));
    Assert.Equal(2, await db.PartnerApplicationHistories.CountAsync(x => x.PartnerApplicationId == receipt.Id));
    var validRemarks = new string('x', 2000);
    using var approved = await client.PostAsJsonAsync(url + "/approve", new { remarks = validRemarks });
    Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
    var decision = await db.PartnerApplicationApprovals.SingleAsync(x => x.PartnerApplicationId == receipt.Id && x.Status == "Approved");
    Assert.Equal(validRemarks, decision.Remarks);
  }

  [SqlServerFact]
  public async Task ConcurrentReviewStartsCreateOneReviewTransition()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
    var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "reviewer", resourceAccess: ReviewRoles()));
    var url = $"/api/admin/partner-applications/{receipt.Id}/review";
    var responses = await Task.WhenAll(client.PostAsync(url, null), client.PostAsync(url, null));
    try
    {
      Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
      await using var db = CreateDatabase();
      Assert.Equal("UnderReview", (await db.PartnerApplications.SingleAsync(x => x.Id == receipt.Id)).Status);
      Assert.Equal(2, await db.PartnerApplicationApprovals.CountAsync(x => x.PartnerApplicationId == receipt.Id));
      Assert.Equal(2, await db.PartnerApplicationHistories.CountAsync(x => x.PartnerApplicationId == receipt.Id));
    }
    finally { foreach (var response in responses) response.Dispose(); }
  }

  [SqlServerFact]
  public async Task ReviewListRejectsInvalidPagingAndOversizedFilters()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    foreach (var query in new[] { "page=0", "page=1000001", "pageSize=0", "pageSize=51", "page=oops",
      "search=" + new string('x', 255), "category=" + new string('x', 33), "status=" + new string('x', 33) })
    {
      using var response = await client.GetAsync("/api/admin/partner-applications?" + query);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
  }
}
