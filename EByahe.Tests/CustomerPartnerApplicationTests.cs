using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EByahe.Application.Dtos.Responses;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task CustomerDocumentPreviewReturnsVerifiedPrivateContentAndSupportsRanges()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    using var created = await client.PostAsync("/api/partner-applications", form);
    var id = (await created.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!.Id;
    var details = (await client.GetFromJsonAsync<CustomerPartnerApplicationDetails>($"/api/partner-applications/{id}"))!;
    var document = details.Documents.Single(item => item.DocumentType == "bankAccountProof");
    var path = $"/api/partner-applications/{id}/documents/{document.Id}";
    using var response = await client.GetAsync(path);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(Pdf, await response.Content.ReadAsByteArrayAsync());
    Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
    Assert.Equal("inline", response.Content.Headers.ContentDisposition!.DispositionType);
    Assert.Equal(document.FileName, response.Content.Headers.ContentDisposition.FileNameStar);
    Assert.True(response.Headers.CacheControl!.Private);
    Assert.True(response.Headers.CacheControl.NoStore);
    Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    using var range = new HttpRequestMessage(HttpMethod.Get, path);
    range.Headers.Range = new RangeHeaderValue(0, 4);
    using var partial = await client.SendAsync(range);
    Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
    Assert.Equal(Pdf[..5], await partial.Content.ReadAsByteArrayAsync());
    factory.Storage.CorruptReads = true;
    using var corrupt = await client.GetAsync(path);
    Assert.Equal(HttpStatusCode.ServiceUnavailable, corrupt.StatusCode);
    Assert.DoesNotContain("StorageKey", await corrupt.Content.ReadAsStringAsync());
  }

  [SqlServerFact]
  public async Task CustomerDocumentPreviewRejectsGuestsAndOtherOwnersBeforeReadingStorage()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    using var created = await client.PostAsync("/api/partner-applications", form);
    var id = (await created.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!.Id;
    var details = (await client.GetFromJsonAsync<CustomerPartnerApplicationDetails>($"/api/partner-applications/{id}"))!;
    var documentId = details.Documents[0].Id;
    var reads = factory.Storage.ReadCalls;
    foreach (var path in new[] { $"/api/partner-applications/{id}/documents/{Guid.NewGuid()}",
      $"/api/partner-applications/{Guid.NewGuid()}/documents/{documentId}" })
    {
      using var missing = await client.GetAsync(path);
      Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
    var validPath = $"/api/partner-applications/{id}/documents/{documentId}";
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "another-customer"));
    using var forbidden = await client.GetAsync(validPath);
    Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    client.DefaultRequestHeaders.Authorization = null;
    using var guest = await client.GetAsync(validPath);
    Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
    Assert.Equal(reads, factory.Storage.ReadCalls);
  }

  [SqlServerFact]
  public async Task CustomerApplicationReadsArePaginatedAndRestrictedToTheValidatedOwner()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var ownIds = new List<Guid>();
    for (var index = 0; index < 2; index++)
    {
      using var form = ApplicationForm();
      using var created = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.Created, created.StatusCode);
      ownIds.Add((await created.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!.Id);
    }
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "another-customer"));
    using var otherForm = ApplicationForm();
    using var otherCreated = await client.PostAsync("/api/partner-applications", otherForm);
    var otherId = (await otherCreated.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!.Id;
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
    var first = (await client.GetFromJsonAsync<CustomerPartnerApplicationPage>("/api/partner-applications?page=1&pageSize=1&userId=another-customer"))!;
    var second = (await client.GetFromJsonAsync<CustomerPartnerApplicationPage>("/api/partner-applications?page=2&pageSize=1"))!;
    Assert.Equal(2, first.TotalCount);
    Assert.Equal(1, first.PageSize);
    Assert.Single(first.Items); Assert.Single(second.Items);
    Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
    Assert.Contains(first.Items[0].Id, ownIds); Assert.Contains(second.Items[0].Id, ownIds);
    using var forbidden = await client.GetAsync($"/api/partner-applications/{otherId}");
    Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: PartnerApiFactory.Subject.ToUpperInvariant()));
    var caseVariant = (await client.GetFromJsonAsync<CustomerPartnerApplicationPage>("/api/partner-applications"))!;
    Assert.Empty(caseVariant.Items);
    using var differentCase = await client.GetAsync($"/api/partner-applications/{ownIds[0]}");
    Assert.Equal(HttpStatusCode.NotFound, differentCase.StatusCode);
  }

  [SqlServerFact]
  public async Task CustomerDetailsReturnSavedDocumentsAndMaskedBankingWithoutPrivateStorageReferences()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    using var created = await client.PostAsync("/api/partner-applications", form);
    var id = (await created.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!.Id;
    using var response = await client.GetAsync($"/api/partner-applications/{id}");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var details = (await response.Content.ReadFromJsonAsync<CustomerPartnerApplicationDetails>())!;
    Assert.Equal("Example Partner Company", details.BusinessName);
    Assert.Equal("•••• 7890", details.MaskedAccountNumber);
    Assert.Equal(5, details.Documents.Count);
    Assert.Contains(details.Documents, document => document.DocumentType == "bankAccountProof");
    var raw = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain("storageKey", raw, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("storageBucket", raw, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("1234-5678-90", raw);
  }

  [SqlServerFact]
  public async Task CustomerReadEndpointsRejectGuestsAndInvalidPaginationAndAllowConfiguredGetCors()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    foreach (var query in new[] { "?page=0", "?pageSize=51", "?page=-1", "?page=1000001" })
    {
      using var response = await client.GetAsync("/api/partner-applications" + query);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/partner-applications");
    preflight.Headers.Add("Origin", "http://localhost:4204");
    preflight.Headers.Add("Access-Control-Request-Method", "GET");
    using var cors = await client.SendAsync(preflight);
    Assert.Contains("GET", string.Join(',', cors.Headers.GetValues("Access-Control-Allow-Methods")));
    client.DefaultRequestHeaders.Authorization = null;
    foreach (var path in new[] { "/api/partner-applications", $"/api/partner-applications/{Guid.NewGuid()}" })
    {
      using var response = await client.GetAsync(path);
      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
  }
}
