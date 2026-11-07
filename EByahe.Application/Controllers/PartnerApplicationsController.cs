using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;
using EByahe.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EByahe.Application.Controllers;

[ApiController]
[Authorize]
[Route("api/partner-applications")]
public sealed class PartnerApplicationsController(IPartnerApplicationService applications) : ControllerBase
{
  public const long MaxRequestSize = 52 * 1024 * 1024;

  [HttpGet("eligibility")]
  [ProducesResponseType<EByahe.Domain.DataModels.PartnerApplicationEligibility>(StatusCodes.Status200OK)]
  public async Task<ActionResult<EByahe.Domain.DataModels.PartnerApplicationEligibility>> Eligibility(CancellationToken cancellationToken)
  {
    Response.Headers.CacheControl = "private, no-store";
    return Ok(await applications.EligibilityAsync(User.FindFirst("sub")!.Value, cancellationToken));
  }

  [HttpGet]
  [ProducesResponseType<CustomerPartnerApplicationPage>(StatusCodes.Status200OK)]
  public async Task<ActionResult<CustomerPartnerApplicationPage>> List(
    [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 50)] int pageSize = 10,
    CancellationToken cancellationToken = default) =>
    Ok(await applications.ListAsync(User.FindFirst("sub")!.Value, page, pageSize, cancellationToken));

  [HttpGet("{id:guid}")]
  [ProducesResponseType<CustomerPartnerApplicationDetails>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<CustomerPartnerApplicationDetails>> Details(Guid id, CancellationToken cancellationToken)
  {
    var application = await applications.GetAsync(id, User.FindFirst("sub")!.Value, cancellationToken);
    return application is null ? NotFound() : Ok(application);
  }

  [HttpGet("{id:guid}/documents/{documentId:guid}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
  public async Task<IActionResult> Document(Guid id, Guid documentId, CancellationToken cancellationToken)
  {
    var document = await applications.GetDocumentAsync(id, documentId, User.FindFirst("sub")!.Value, cancellationToken);
    if (document is null) return NotFound();
    Response.Headers.CacheControl = "private, no-store";
    Response.Headers["X-Content-Type-Options"] = "nosniff";
    Response.Headers.ContentDisposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline")
      { FileNameStar = document.FileName }.ToString();
    return File(document.Content, document.ContentType, enableRangeProcessing: true);
  }

  [HttpPost]
  [Consumes("multipart/form-data")]
  [RequestSizeLimit(MaxRequestSize)]
  [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize, ValueLengthLimit = 8192, ValueCountLimit = 64)]
  [ProducesResponseType<PartnerApplicationResponse>(StatusCodes.Status201Created)]
  [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
  [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
  [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
  public async Task<ActionResult<PartnerApplicationResponse>> Submit([FromForm] CreatePartnerApplicationRequest request,
    CancellationToken cancellationToken) =>
    StatusCode(StatusCodes.Status201Created, await applications.SubmitAsync(request, User.FindFirst("sub")!.Value, cancellationToken));
}
