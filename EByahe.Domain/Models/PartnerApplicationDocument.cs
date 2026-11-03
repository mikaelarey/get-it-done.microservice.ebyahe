namespace EByahe.Domain.Models;

public sealed class PartnerApplicationDocument
{
  public const int MaxFileSize = 10 * 1024 * 1024;
  public Guid Id { get; set; }
  public Guid PartnerApplicationId { get; set; }
  public string DocumentType { get; set; } = string.Empty;
  public string FileName { get; set; } = string.Empty;
  public string ContentType { get; set; } = string.Empty;
  public long SizeBytes { get; set; }
  public string Sha256 { get; set; } = string.Empty;
  public string StorageBucket { get; set; } = string.Empty;
  public string StorageKey { get; set; } = string.Empty;
  public DateTimeOffset UploadedAtUtc { get; set; }
}
