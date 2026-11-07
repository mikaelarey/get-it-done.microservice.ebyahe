using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/users"), Authorize(Policy = PartnerPortalAuthorization.CreateMembersPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerMembersController(IPartnerMemberRepository members) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;
  [HttpGet("workspaces")]
  public async Task<ActionResult<IReadOnlyList<PartnerMemberWorkspace>>> Workspaces(CancellationToken cancellationToken) =>
    Ok(await members.WorkspacesAsync(UserId, cancellationToken));

  [HttpPost]
  public async Task<ActionResult<PartnerMemberStatus>> Add(AddPartnerMemberRequest request, CancellationToken cancellationToken)
  {
    if (request.RequestId == Guid.Empty || request.PartnerId == Guid.Empty
      || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName)
      || request.FirstName.Any(char.IsControl) || request.LastName.Any(char.IsControl)
      || request.Email.Any(char.IsControl)) return BadRequest();
    try
    {
      var result = await members.QueueAsync(UserId, new(request.RequestId, request.PartnerId,
        request.FirstName.Trim(), request.LastName.Trim(), request.Email.Trim().ToLowerInvariant()), cancellationToken);
      return result is null ? Forbid() : AcceptedAtAction(nameof(Status), new { id = result.Id }, result);
    }
    catch (PartnerMemberRequestConflictException) { return Conflict(new ProblemDetails { Title = "This request ID has already been used. Please reopen the form." }); }
  }

  [HttpGet("requests/{id:guid}")]
  public async Task<ActionResult<PartnerMemberStatus>> Status(Guid id, CancellationToken cancellationToken)
  {
    var result = await members.StatusAsync(UserId, id, cancellationToken);
    return result is null ? NotFound() : Ok(result);
  }
  [HttpGet("requests")]
  public async Task<ActionResult<IReadOnlyList<PartnerMemberStatus>>> Recent(CancellationToken cancellationToken) =>
    Ok(await members.RecentAsync(UserId, cancellationToken));
  [HttpGet("requests/{id:guid}/temporary-password")]
  public async Task<ActionResult<PartnerTemporaryPassword>> TemporaryPassword(Guid id, CancellationToken cancellationToken)
  {
    var password = await members.TemporaryPasswordAsync(UserId, id, cancellationToken);
    return password is null ? NotFound() : Ok(password);
  }
}
public sealed class AddPartnerMemberRequest
{
  public Guid RequestId { get; set; }
  public Guid PartnerId { get; set; }
  [Required, StringLength(255)] public string FirstName { get; set; } = "";
  [Required, StringLength(255)] public string LastName { get; set; } = "";
  [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
}
