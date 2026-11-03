using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using Microsoft.Extensions.Options;

namespace EByahe.Infrastructure.Storage;

public sealed class S3DocumentStorage(IAmazonS3 client, IOptions<DocumentStorageOptions> options) : IDocumentStorage
{
  public string Bucket => options.Value.Bucket;

  public async Task PutAsync(string key, byte[] content, string contentType, string sha256,
    CancellationToken cancellationToken = default)
  {
    if (content.Length is <= 0 or > PartnerApplicationDocument.MaxFileSize
      || !string.Equals(Convert.ToHexString(SHA256.HashData(content)), sha256, StringComparison.OrdinalIgnoreCase))
      throw new DocumentStorageException("The document size or checksum is invalid.");
    Validate(new StoredDocument(Bucket, key));
    try
    {
      using var stream = new MemoryStream(content, writable: false);
      var request = new PutObjectRequest
      {
        BucketName = Bucket, Key = key, InputStream = stream, ContentType = contentType,
        UseChunkEncoding = false, AutoCloseStream = false, DisablePayloadSigning = false
      };
      request.Metadata.Add("sha256", sha256.ToLowerInvariant());
      await client.PutObjectAsync(request, cancellationToken);
    }
    catch (Exception exception) when (exception is AmazonS3Exception or HttpRequestException)
    { throw new DocumentStorageException("The document could not be uploaded to object storage."); }
  }

  public async Task<byte[]> ReadAsync(StoredDocument document, CancellationToken cancellationToken = default)
  {
    Validate(document);
    try
    {
      using var response = await client.GetObjectAsync(document.Bucket, document.Key, cancellationToken);
      using var buffer = new MemoryStream();
      var chunk = new byte[81920];
      int count;
      while ((count = await response.ResponseStream.ReadAsync(chunk, cancellationToken)) != 0)
      {
        if (buffer.Length + count > PartnerApplicationDocument.MaxFileSize)
          throw new DocumentStorageException("The stored document exceeds the allowed size.");
        await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
      }
      return buffer.ToArray();
    }
    catch (Exception exception) when (exception is AmazonS3Exception or HttpRequestException)
    { throw new DocumentStorageException("The document could not be read from object storage."); }
  }

  public async Task DeleteAsync(StoredDocument document, CancellationToken cancellationToken = default)
  {
    Validate(document);
    try { await client.DeleteObjectAsync(document.Bucket, document.Key, cancellationToken); }
    catch (Exception exception) when (exception is AmazonS3Exception or HttpRequestException)
    { throw new DocumentStorageException("The uncommitted upload could not be removed from object storage."); }
  }

  private void Validate(StoredDocument document)
  {
    if (document.Bucket != Bucket || !document.Key.StartsWith("ebyahe/partner-applications/", StringComparison.Ordinal)
      || document.Key.Length > 512 || document.Key.Contains("..", StringComparison.Ordinal)
      || document.Key.Contains('\\') || document.Key.Any(char.IsControl))
      throw new DocumentStorageException("The document storage reference is invalid for this environment.");
  }
}
