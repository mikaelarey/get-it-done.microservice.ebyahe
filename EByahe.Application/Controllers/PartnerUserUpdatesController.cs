using EByahe.Application.Authorization;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace EByahe.Application.Controllers;
[ApiController, Route("api/partner/users"), Authorize(Policy = PartnerPortalAuthorization.ManageMembersPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerUserUpdatesController(IPartnerMemberRepository members, IHttpClientFactory clients, IConfiguration configuration, IHostEnvironment environment) : ControllerBase
{
  [HttpGet("{id:guid}/account-access")]
  public async Task<IActionResult> Access(Guid id, CancellationToken ct) =>
    await members.AccountAccessAsync(User.FindFirst("sub")!.Value, id, ct) is {} access ? Ok(access) : NotFound();

  [HttpGet("{id:guid}/edit")]
  public Task<IActionResult> Edit(Guid id, CancellationToken ct) => Forward(id, HttpMethod.Get, ct);
  [HttpPut("{id:guid}")]
  public Task<IActionResult> Update(Guid id, CancellationToken ct) => Forward(id, HttpMethod.Put, ct);

  private async Task<IActionResult> Forward(Guid id, HttpMethod method, CancellationToken ct)
  {
    if (await members.AccountAccessAsync(User.FindFirst("sub")!.Value, id, ct) is null) return NotFound();
    if (!Uri.TryCreate(configuration["PartnerAccounts:BaseUrl"], UriKind.Absolute, out var url)
      || (url.Scheme != "https" && !(environment.IsDevelopment() && url.Scheme == "http" && url.IsLoopback)) || url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0)
      return Problem(statusCode: 503, title: "The account service is not configured.");
    using var request = new HttpRequestMessage(method, new Uri(url.ToString().TrimEnd('/') + "/api/partner-members/" + id));
    request.Headers.Authorization = AuthenticationHeaderValue.Parse(Request.Headers.Authorization.ToString());
    if (method == HttpMethod.Put) {
      if (Request.ContentLength > 16384) return BadRequest();
      // Bound the request even when the client uses chunked transfer encoding.
      using var buffer = new MemoryStream(); var bytes = new byte[4096]; int count;
      while ((count = await Request.Body.ReadAsync(bytes, ct)) > 0) { if (buffer.Length + count > 16384) return BadRequest(); await buffer.WriteAsync(bytes.AsMemory(0, count), ct); }
      request.Content = new ByteArrayContent(buffer.ToArray()); request.Content.Headers.ContentType = new("application/json");
    }
    try {
      using var response = await clients.CreateClient("PartnerAccounts").SendAsync(request, ct);
      return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json", Content = await response.Content.ReadAsStringAsync(ct) };
    } catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException && !ct.IsCancellationRequested) {
      return Problem(statusCode: 503, title: "The account service is unavailable. Please try again.");
    }
  }
}
