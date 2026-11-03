using System.Collections.Concurrent;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Interfaces;

namespace EByahe.Tests;

internal sealed class MemoryDocumentStorage : IDocumentStorage
{
  public string Bucket => "ebyahe-test-documents";
  public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
  public int PutCalls { get; private set; }
  public int ReadCalls { get; private set; }
  public int FailAtPut { get; set; }
  public bool CorruptReads { get; set; }
  public bool FailDelete { get; set; }

  public Task PutAsync(string key, byte[] content, string contentType, string sha256, CancellationToken cancellationToken = default)
  {
    PutCalls++;
    Objects[key] = content.ToArray();
    if (PutCalls == FailAtPut) throw new DocumentStorageException("Simulated partial upload failure.");
    return Task.CompletedTask;
  }

  public Task<byte[]> ReadAsync(StoredDocument document, CancellationToken cancellationToken = default)
  {
    ReadCalls++;
    return Task.FromResult(CorruptReads ? new byte[] { 1 } : Objects[document.Key].ToArray());
  }

  public Task DeleteAsync(StoredDocument document, CancellationToken cancellationToken = default)
  {
    if (FailDelete) throw new DocumentStorageException("Simulated cleanup failure.");
    Objects.TryRemove(document.Key, out _);
    return Task.CompletedTask;
  }
}
