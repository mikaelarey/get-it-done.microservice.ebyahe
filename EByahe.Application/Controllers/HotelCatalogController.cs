using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, AllowAnonymous, Route("api/hotel-catalog")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HotelCatalogController(IHotelCatalogRepository catalog, IDocumentStorage storage) : ControllerBase
{
  [HttpGet]
  public async Task<ActionResult<PartnerRecordsPage<HotelCatalogEntry>>> Search(
    [FromQuery, StringLength(200)] string? search, [FromQuery, Range(1, 100)] int guests = 1,
    [FromQuery, Range(1, int.MaxValue)] int? roomType = null,
    [FromQuery, RegularExpression("^(name|price-asc|price-desc)$")] string sort = "name",
    [FromQuery, Range(1, 1000000)] int page = 1, CancellationToken ct = default) =>
    Ok(await catalog.SearchAsync(search, guests, roomType, sort, page, ct));

  [HttpGet("types")]
  public async Task<ActionResult<IReadOnlyList<RoomTypeOption>>> Types(CancellationToken ct) => Ok(await catalog.TypesAsync(ct));

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<HotelCatalogDetails>> Details(Guid id, [FromQuery, Range(1, 100)] int guests = 1,
    [FromQuery, Range(1, int.MaxValue)] int? roomType = null, CancellationToken ct = default) =>
    await catalog.GetAsync(id, guests, roomType, ct) is { } hotel ? Ok(hotel) : NotFound();

  [HttpGet("rooms/{roomId:guid}/images/{imageId:guid}")]
  public async Task<IActionResult> Image(Guid roomId, Guid imageId, CancellationToken ct)
  {
    var image = await catalog.GetImageAsync(roomId, imageId, ct);
    if (image is null) return NotFound();
    var bytes = await storage.ReadAsync(new StoredDocument(image.StorageBucket, image.StorageKey), ct);
    if (bytes.LongLength != image.SizeBytes || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), image.Sha256, StringComparison.OrdinalIgnoreCase))
      throw new DocumentStorageException("The stored room photo could not be verified.");
    Response.Headers["X-Content-Type-Options"] = "nosniff";
    return File(bytes, image.ContentType);
  }
}
