using EByahe.Application.Authorization;
using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/profile"), Authorize(Policy = PartnerPortalAuthorization.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerProfileController(IPartnerMemberRepository members) : ControllerBase
{
  [HttpGet("permissions")]
  public async Task<ActionResult<PartnerProfilePermissions>> Permissions(CancellationToken cancellationToken)
  {
    var workspaces = await members.WorkspacesAsync(User.FindFirst("sub")!.Value, cancellationToken);
    return Ok(new PartnerProfilePermissions(workspaces.SelectMany(workspace => PartnerMemberRoles.Allowed(workspace.BusinessCategory))
      .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
  }
}

public sealed record PartnerProfilePermissions(IReadOnlyList<string> AvailableRoles);
