using System.Security.Cryptography;
using EByahe.Domain.Models;

namespace EByahe.Application.Services;

internal static class RoomImageUploads
{
  public const int MaxImages = 8;
  public const int MaxFileBytes = 5 * 1024 * 1024;
  public const int MaxRequestBytes = 42 * 1024 * 1024;

  // Validate the entire selection before writing any objects to storage.
  public static async Task<IReadOnlyList<(RoomImage Image, byte[] Bytes)>> ReadAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
  {
    if (files.Count > MaxImages) throw new InvalidDataException("Choose up to 8 room photos.");
    var result = new List<(RoomImage, byte[])>();
    foreach (var file in files) {
      var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
      var type = PartnerDocumentFormat.ContentType(name);
      if (name.Length is 0 or > 200 || name.Any(char.IsControl)
        || type is not ("image/jpeg" or "image/png" or "image/webp"))
        throw new InvalidDataException("Room photos must be JPEG, PNG or WebP images with filenames up to 200 characters.");
      if (file.Length is <= 0 or > MaxFileBytes) throw new InvalidDataException("Each room photo must be non-empty and no larger than 5 MB.");
      await using var stream = file.OpenReadStream();
      using var buffer = new MemoryStream();
      var chunk = new byte[81920]; int read;
      while ((read = await stream.ReadAsync(chunk, ct)) != 0) {
        if (buffer.Length + read > MaxFileBytes) throw new InvalidDataException("Each room photo must be no larger than 5 MB.");
        await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
      }
      var bytes = buffer.ToArray();
      if (bytes.LongLength != file.Length || !PartnerDocumentFormat.Matches(bytes, type))
        throw new InvalidDataException("A room photo is invalid or its contents do not match its file extension.");
      var image = new RoomImage { FileName = name, ContentType = type, SizeBytes = bytes.LongLength,
        Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
      image.StorageKey = $"ebyahe/room-images/{image.Id:N}/{image.Sha256.ToLowerInvariant()}";
      result.Add((image, bytes));
    }
    return result;
  }
}
