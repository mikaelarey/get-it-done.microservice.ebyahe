using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using EByahe.Domain.Interfaces;
using EByahe.Application.Dtos.Responses;
using EByahe.Application.Dtos.Requests;
using EByahe.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController]
[Authorize(Policy = PartnerApplicationReviewAuthorization.Policy)]
[Route("api/admin/partner-applications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerApplicationReviewController(IPartnerApplicationReviewRepository applications) : ControllerBase
{
  [HttpPost("{id:guid}/review")]
  public Task<ActionResult<CustomerPartnerApplicationDetails>> Review(Guid id, CancellationToken cancellationToken) =>
    Decide(id, "UnderReview", null, cancellationToken);

  [HttpPost("{id:guid}/approve")]
  public Task<ActionResult<CustomerPartnerApplicationDetails>> Approve(Guid id,
    [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] PartnerApplicationDecisionRequest? request,
    CancellationToken cancellationToken) => Decide(id, "Approved", request?.Remarks, cancellationToken);

  [HttpPost("{id:guid}/decline")]
  public Task<ActionResult<CustomerPartnerApplicationDetails>> Decline(Guid id,
    [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] PartnerApplicationDecisionRequest? request,
    CancellationToken cancellationToken) => Decide(id, "Rejected", request?.Remarks, cancellationToken);

  private async Task<ActionResult<CustomerPartnerApplicationDetails>> Decide(Guid id, string status, string? remarks, CancellationToken cancellationToken)
  {
    var reviewer = User.FindFirst("sub")?.Value;
    if (string.IsNullOrWhiteSpace(reviewer)) return Unauthorized();
    if (status == "Rejected" && string.IsNullOrWhiteSpace(remarks))
    {
      ModelState.AddModelError(nameof(PartnerApplicationDecisionRequest.Remarks), "Remarks are required when declining an application.");
      return ValidationProblem(ModelState);
    }
    var result = await applications.DecideAsync(id, status, reviewer, remarks, cancellationToken);
    if (result.Application is null) return NotFound();
    if (result.Conflict) return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict,
      Title = "The application cannot be updated.", Detail = result.ConflictReason
        ?? "Start a review before approving or declining. Reload the application to see its current status." });
    return Ok(PartnerApplicationService.MapDetails(result.Application));
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType<CustomerPartnerApplicationDetails>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CustomerPartnerApplicationDetails>> Details(Guid id, CancellationToken cancellationToken)
  {
    var application = await applications.GetAsync(id, cancellationToken);
    return application is null ? NotFound() : Ok(PartnerApplicationService.MapDetails(application));
  }

  [HttpGet("{id:guid}/documents/{documentId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
  public async Task<IActionResult> Document(Guid id, Guid documentId, [FromServices] IDocumentStorage storage, CancellationToken cancellationToken)
  {
    var application = await applications.GetAsync(id, cancellationToken);
    var document = await PartnerApplicationService.ReadDocumentAsync(application, documentId, storage, cancellationToken);
    if (document is null) return NotFound();
    Response.Headers.CacheControl = "private, no-store";
    Response.Headers["X-Content-Type-Options"] = "nosniff";
    Response.Headers.ContentDisposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline")
      { FileNameStar = document.FileName }.ToString();
    return File(document.Content, document.ContentType, enableRangeProcessing: true);
  }

  [HttpGet]
  [ProducesResponseType<PartnerApplicationReviewPage>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PartnerApplicationReviewPage>> List(
    [FromQuery, Range(1, 1000000)] int page = 1,
    [FromQuery, Range(1, 50)] int pageSize = 5,
    [FromQuery, StringLength(254)] string? search = null,
    [FromQuery, StringLength(32)] string? category = null,
    [FromQuery, StringLength(32)] string? status = null,
    CancellationToken cancellationToken = default) =>
    Ok(await applications.ListAsync(page, pageSize, search, category, status, cancellationToken));
}
