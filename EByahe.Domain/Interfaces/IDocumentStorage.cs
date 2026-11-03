namespace EByahe.Domain.Interfaces;

public sealed record StoredDocument(string Bucket, string Key);

public interface IDocumentStorage
{
  string Bucket { get; }
  Task PutAsync(string key, byte[] content, string contentType, string sha256, CancellationToken cancellationToken = default);
  Task<byte[]> ReadAsync(StoredDocument document, CancellationToken cancellationToken = default);
  Task DeleteAsync(StoredDocument document, CancellationToken cancellationToken = default);
}
