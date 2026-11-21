using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using EByahe.Application.Services;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/rooms"), Authorize(Policy = PartnerPortalAuthorization.ReadRoomsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerRoomsController(IPartnerRoomRepository rooms, IDocumentStorage storage, ILogger<PartnerRoomsController> logger) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;
  [HttpGet]
  public async Task<ActionResult<PartnerRecordsPage<PartnerRoom>>> List([FromQuery] PartnerRecordsQuery query, [FromQuery] Guid? hotelId, CancellationToken ct)
  {
    if (hotelId == Guid.Empty) return BadRequest();
    return Ok(await rooms.ListAsync(UserId, query.Page, query.PageSize, query.Search, hotelId, ct));
  }
  [HttpGet("hotels")]
  public Task<IReadOnlyList<PartnerRoomHotel>> Hotels(CancellationToken ct) => rooms.HotelsAsync(UserId, ct);
  [HttpGet("types")]
  public Task<IReadOnlyList<RoomTypeOption>> Types(CancellationToken ct) => rooms.TypesAsync(ct);
  [HttpGet("{id:guid}")]
  public async Task<ActionResult<PartnerRoom>> Details(Guid id, CancellationToken ct) =>
    await rooms.GetAsync(UserId, id, ct) is { } room ? Ok(room) : NotFound();
  [HttpPost, Consumes("application/json"), Authorize(Policy = PartnerPortalAuthorization.WriteRoomsPolicy)]
  public async Task<ActionResult<PartnerRoom>> Add(AddPartnerRoomRequest request, CancellationToken ct)
  {
    try {
      var room = await rooms.AddAsync(UserId, request.ToCommand(), ct);
      return room is null ? Forbid() : StatusCode(201, room);
    } catch (PartnerRoomConflictException) { return Duplicate(); }
      catch (PartnerRoomTypeException) { return InvalidType(); }
      catch (PartnerRoomHotelInactiveException) { return BadRequest(new ProblemDetails { Title = "Select an active hotel before adding a room." }); }
  }
  [HttpPut("{id:guid}"), Consumes("application/json"), Authorize(Policy = PartnerPortalAuthorization.WriteRoomsPolicy)]
  public async Task<ActionResult<PartnerRoom>> Update(Guid id, UpdatePartnerRoomRequest request, CancellationToken ct)
  {
    try {
      var room = await rooms.UpdateAsync(UserId, id, request.ToCommand(), request.IsActive!.Value, ct);
      return room is null ? NotFound() : Ok(room);
    } catch (PartnerRoomConflictException) { return Duplicate(); }
      catch (PartnerRoomTypeException) { return InvalidType(); }
  }
  [HttpDelete("{id:guid}"), Authorize(Policy = PartnerPortalAuthorization.WriteRoomsPolicy)]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => await rooms.DeleteAsync(UserId, id, ct) ? NoContent() : NotFound();

  [HttpPost, Consumes("multipart/form-data"), Authorize(Policy = PartnerPortalAuthorization.WriteRoomsPolicy)]
  [RequestSizeLimit(RoomImageUploads.MaxRequestBytes), RequestFormLimits(MultipartBodyLengthLimit = RoomImageUploads.MaxRequestBytes)]
  public Task<ActionResult<PartnerRoom>> AddWithImages([FromForm] AddPartnerRoomImagesRequest request, CancellationToken ct) =>
    SaveWithImages(null, request, true, ct);

  [HttpPut("{id:guid}"), Consumes("multipart/form-data"), Authorize(Policy = PartnerPortalAuthorization.WriteRoomsPolicy)]
  [RequestSizeLimit(RoomImageUploads.MaxRequestBytes), RequestFormLimits(MultipartBodyLengthLimit = RoomImageUploads.MaxRequestBytes)]
  public Task<ActionResult<PartnerRoom>> UpdateWithImages(Guid id, [FromForm] UpdatePartnerRoomImagesRequest request, CancellationToken ct) =>
    SaveWithImages(id, request, request.IsActive!.Value, ct);

  private async Task<ActionResult<PartnerRoom>> SaveWithImages(Guid? id, AddPartnerRoomImagesRequest request, bool isActive, CancellationToken ct)
  {
    if (id.HasValue) {
      var current = await rooms.GetAsync(UserId, id.Value, ct);
      if (current is null || current.HotelId != request.HotelId) return NotFound();
      if (request.RetainedImageIds.Distinct().Count() != request.RetainedImageIds.Count
        || request.RetainedImageIds.Any(imageId => current.Images?.Any(image => image.Id == imageId) != true)) return InvalidImages();
    } else {
      var hotel = (await rooms.HotelsAsync(UserId, ct)).SingleOrDefault(hotel => hotel.Id == request.HotelId);
      if (hotel is null) return Forbid();
      if (!hotel.IsActive) return BadRequest(new ProblemDetails { Title = "Select an active hotel before adding a room." });
      if (request.RetainedImageIds.Count != 0) return InvalidImages();
    }
    if (request.Images.Count + request.RetainedImageIds.Count > RoomImageUploads.MaxImages) return InvalidImages();
    if (!(await rooms.TypesAsync(ct)).Any(type => string.Equals(type.Decode, request.RoomType.Trim(), StringComparison.OrdinalIgnoreCase))) return InvalidType();
    IReadOnlyList<(RoomImage Image, byte[] Bytes)> selection;
    try { selection = await RoomImageUploads.ReadAsync(request.Images, ct); }
    catch (InvalidDataException error) { return BadRequest(new ProblemDetails { Title = error.Message }); }
    var uploaded = new List<RoomImage>(); var committed = false; var saveAttempted = false;
    try {
      foreach (var (image, bytes) in selection) {
        image.StorageBucket = storage.Bucket;
        uploaded.Add(image); // Include a put that may have partially succeeded in cleanup.
        await storage.PutAsync(image.StorageKey, bytes, image.ContentType, image.Sha256, ct);
      }
      var command = request.ToCommand() with { NewImages = selection.Select(item => item.Image).ToArray(), RetainedImageIds = request.RetainedImageIds };
      saveAttempted = true;
      var room = id.HasValue ? await rooms.UpdateAsync(UserId, id.Value, command, isActive, ct) : await rooms.AddAsync(UserId, command, ct);
      if (room is null) return id.HasValue ? NotFound() : Forbid();
      committed = true; return StatusCode(id.HasValue ? 200 : 201, room);
    } catch (PartnerRoomConflictException) { return Duplicate(); }
      catch (PartnerRoomTypeException) { return InvalidType(); }
      catch (PartnerRoomImagesException) { return InvalidImages(); }
      catch (PartnerRoomHotelInactiveException) { return BadRequest(new ProblemDetails { Title = "Select an active hotel before adding a room." }); }
    finally {
      if (!committed && uploaded.Count > 0) {
        // Preserve stored objects if a database commit succeeded but its acknowledgement was lost.
        var canClean = !saveAttempted;
        if (saveAttempted) {
          try { canClean = !await rooms.AnyImageExistsAsync(uploaded.Select(image => image.Id).ToArray(), CancellationToken.None); }
          catch { logger.LogWarning("Could not confirm the room image commit outcome; objects were retained for reconciliation."); }
        }
        if (canClean) {
          using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
          foreach (var image in uploaded) {
            try { await storage.DeleteAsync(new StoredDocument(image.StorageBucket, image.StorageKey), cleanup.Token); }
            catch (Exception error) { logger.LogWarning(error, "Could not clean up an uncommitted room image {ImageId}.", image.Id); }
          }
        }
      }
    }
  }

  [HttpGet("{id:guid}/images/{imageId:guid}")]
  public async Task<IActionResult> Image(Guid id, Guid imageId, CancellationToken ct)
  {
    var image = await rooms.GetImageAsync(UserId, id, imageId, ct);
    if (image is null) return NotFound();
    var bytes = await storage.ReadAsync(new StoredDocument(image.StorageBucket, image.StorageKey), ct);
    if (bytes.LongLength != image.SizeBytes || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), image.Sha256, StringComparison.OrdinalIgnoreCase))
      throw new DocumentStorageException("The stored room photo could not be verified.");
    Response.Headers["X-Content-Type-Options"] = "nosniff";
    return File(bytes, image.ContentType);
  }
  private BadRequestObjectResult InvalidImages() => BadRequest(new ProblemDetails { Title = "Choose up to 8 photos belonging to this room. Reload the room if its photos have changed." });
  private BadRequestObjectResult InvalidType() => BadRequest(new ProblemDetails { Title = "Select a room type from the configured choices." });
  private ConflictObjectResult Duplicate() => Conflict(new ProblemDetails { Title = "A room with this name or number already exists in this hotel." });
}
