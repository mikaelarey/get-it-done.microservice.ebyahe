using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Application.Services;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EByahe.Application.Controllers;
[ApiController, Route("api/partner/users"), Authorize(Policy = PartnerPortalAuthorization.CreateMembersPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerUserImportsController(IPartnerMemberRepository members, IUploadRepository uploads) : ControllerBase {
  [HttpGet("template")]
  public IActionResult Template() => File(UserExcelTemplate.Generate(), UserExcelTemplate.ContentType, UserExcelTemplate.FileName);
  [HttpPost("import"), RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
  public async Task<ActionResult<PartnerUserImportResult>> Import([FromForm] ImportPartnerUsersRequest request, CancellationToken ct) {
    if (request.UploadId == Guid.Empty || request.PartnerId == Guid.Empty) return BadRequest(new PartnerUserImportResult(request.UploadId, 0, [new(0, "Partner", "Select a partner and a new upload.")]));
    var userId = User.FindFirst("sub")!.Value;
    var workspace = (await members.WorkspacesAsync(userId, ct)).SingleOrDefault(x => x.Id == request.PartnerId); if (workspace is null) return Forbid();
    try {
      var id = await uploads.AddAsync(userId, UploadTypeCodes.Users, ct, request.UploadId);
      var parsed = await UserExcelImport.ReadAsync(request.File, workspace.BusinessCategory, ct);
      if (parsed.Errors.Count != 0) return BadRequest(new PartnerUserImportResult(id, 0, parsed.Errors));
      var result = await members.ImportAsync(userId, request.PartnerId, parsed.Rows, id, ct);
      return result is null ? Forbid() : result.Errors.Count != 0 ? Conflict(result) : Accepted(result);
    } catch (PartnerMemberRequestConflictException) { return Conflict(new PartnerUserImportResult(request.UploadId, 0, [new(0, "File", "This upload has already been submitted with different details. Choose the file again to start a new upload.")])); }
  }
}
public sealed class ImportPartnerUsersRequest {
  public Guid PartnerId { get; set; }
  public Guid UploadId { get; set; }
  [Required] public IFormFile File { get; set; } = null!;
}
