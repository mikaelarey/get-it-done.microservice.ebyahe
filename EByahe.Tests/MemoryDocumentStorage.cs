using System.Collections.Concurrent;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Interfaces;

namespace EByahe.Tests;

internal sealed class MemoryDocumentStorage : IDocumentStorage
{
  public string Bucket => "ebyahe-test-documents";
  public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
  private int putCalls;
  public int PutCalls => putCalls;
  public Func<Task>? BeforePut { get; set; }
  public int ReadCalls { get; private set; }
  public int FailAtPut { get; set; }
  public bool CorruptReads { get; set; }
  public bool FailDelete { get; set; }

  public async Task PutAsync(string key, byte[] content, string contentType, string sha256, CancellationToken cancellationToken = default)
  {
    if (BeforePut is not null) await BeforePut();
    var call = Interlocked.Increment(ref putCalls);
    Objects[key] = content.ToArray();
    if (call == FailAtPut) throw new DocumentStorageException("Simulated partial upload failure.");
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
