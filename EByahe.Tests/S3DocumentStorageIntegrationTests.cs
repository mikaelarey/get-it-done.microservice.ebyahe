using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Interfaces;
using EByahe.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EByahe.Tests;

public sealed class StorageFactAttribute : FactAttribute
{
  public StorageFactAttribute()
  {
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EBYAHE_TEST_STORAGE_CONFIG")))
      Skip = "Set EBYAHE_TEST_STORAGE_CONFIG to the local Docker storage-login.json to test SeaweedFS.";
  }
}

public sealed class S3DocumentStorageIntegrationTests
{
  [StorageFact]
  public async Task DockerSeaweedFsSupportsPrivateSignedUploadReadAndDeletion()
  {
    using var login = JsonDocument.Parse(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("EBYAHE_TEST_STORAGE_CONFIG")!));
    var root = login.RootElement;
    var endpoint = root.GetProperty("endpoint").GetString()!;
    Assert.True(new Uri(endpoint).IsLoopback, "Integration tests may only use local Docker storage.");
    var dev = root.GetProperty("environments").GetProperty("dev");
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ConnectionStrings:EByaheDatabase"] = "Server=(local);Database=UnusedStorageTest;Integrated Security=True",
      ["Storage:Endpoint"] = endpoint, ["Storage:Region"] = root.GetProperty("region").GetString(),
      ["Storage:Bucket"] = dev.GetProperty("bucket").GetString(),
      ["Storage:AccessKey"] = dev.GetProperty("accessKey").GetString(),
      ["Storage:SecretKey"] = dev.GetProperty("secretKey").GetString()
    }).Build();
    await using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
    var storage = services.GetRequiredService<IDocumentStorage>();
    var content = "%PDF-1.7\nSeaweedFS integration fixture\n%%EOF"u8.ToArray();
    var sha = Convert.ToHexString(SHA256.HashData(content));
    var key = DocumentStorageKey.Create(Guid.NewGuid(), Guid.NewGuid(), sha);
    var reference = new StoredDocument(storage.Bucket, key);
    try
    {
      await storage.PutAsync(key, content, "application/pdf", sha);
      Assert.Equal(content, await storage.ReadAsync(reference));
      using var client = new HttpClient();
      using var unsigned = await client.GetAsync(endpoint.TrimEnd('/') + "/" + storage.Bucket + "/" + key);
      Assert.Equal(HttpStatusCode.Forbidden, unsigned.StatusCode);
      await storage.DeleteAsync(reference);
      await Assert.ThrowsAsync<DocumentStorageException>(() => storage.ReadAsync(reference));
    }
    finally { await storage.DeleteAsync(reference); }
  }
}
