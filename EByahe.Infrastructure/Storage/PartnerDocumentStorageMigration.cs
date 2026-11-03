using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using EByahe.Domain.DataModels;
using EByahe.Domain.Interfaces;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EByahe.Infrastructure.Storage;

/** Moves existing byte columns and historical Base64 versions before the guarded removal migration runs. */
public sealed class PartnerDocumentStorageMigration(EByaheDbContext database, IServiceProvider services,
  ILogger<PartnerDocumentStorageMigration> logger)
{
  private readonly Dictionary<string, StoredDocument> verified = [];

  public async Task MigrateAsync(CancellationToken cancellationToken = default)
  {
    await database.Database.OpenConnectionAsync(cancellationToken);
    if (await ScalarAsync("SELECT COL_LENGTH('PartnerApplicationDocuments', 'Content')", cancellationToken) is null or DBNull) return;
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    await ExecuteAsync("""
      DECLARE @result int;
      EXEC @result = sp_getapplock @Resource = 'Ebyahe:PartnerDocumentStorageMigration',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
      IF @result < 0 THROW 51000, 'Could not acquire the partner document migration lock.', 1;
      """, cancellationToken);
    var documents = 0;
    var histories = 0;
    while (true)
    {
      Guid id, applicationId;
      byte[] bytes;
      string sha256, contentType;
      long size;
      await using (var command = Command("""
        SELECT TOP(1) [Id], [PartnerApplicationId], [Content], [Sha256], [ContentType], [SizeBytes]
        FROM [PartnerApplicationDocuments]
        WHERE [Content] IS NOT NULL OR [StorageBucket] IS NULL OR [StorageKey] IS NULL
        ORDER BY [Id]
        """))
      await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
      {
        if (!await reader.ReadAsync(cancellationToken)) break;
        if (reader.IsDBNull(2)) throw new InvalidOperationException("A legacy document has neither file data nor a storage reference. Restore it before migrating.");
        id = reader.GetGuid(0); applicationId = reader.GetGuid(1); bytes = (byte[])reader.GetValue(2);
        sha256 = reader.GetString(3); contentType = reader.GetString(4); size = reader.GetInt64(5);
      }
      var stored = await UploadAndVerifyAsync(applicationId, id, bytes, contentType, sha256, size, cancellationToken);
      await ExecuteAsync("""
        UPDATE [PartnerApplicationDocuments] SET [StorageBucket] = @bucket, [StorageKey] = @key, [Content] = NULL WHERE [Id] = @id
        """, cancellationToken, ("@bucket", stored.Bucket), ("@key", stored.Key), ("@id", id));
      documents++;
    }
    while (true)
    {
      Guid id, applicationId;
      string? previous;
      string current;
      await using (var command = Command("""
        SELECT TOP(1) h.[Id], h.[PartnerApplicationId], h.[PreviousValue], h.[CurrentValue]
        FROM [PartnerApplicationHistories] h
        WHERE EXISTS (
          SELECT 1 FROM (VALUES(h.[PreviousValue]), (h.[CurrentValue])) s([Payload])
          CROSS APPLY OPENJSON(s.[Payload], '$.Documents') d
          CROSS APPLY OPENJSON(d.[value]) p WHERE p.[key] = 'Content'
        ) ORDER BY h.[Id]
        """))
      await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
      {
        if (!await reader.ReadAsync(cancellationToken)) break;
        id = reader.GetGuid(0); applicationId = reader.GetGuid(1);
        previous = reader.IsDBNull(2) ? null : reader.GetString(2); current = reader.GetString(3);
      }
      previous = await RewriteHistoryAsync(previous, applicationId, cancellationToken);
      current = (await RewriteHistoryAsync(current, applicationId, cancellationToken))!;
      // A storage relocation preserves business history IDs/status/timestamps and all original file versions.
      await ExecuteAsync("UPDATE [PartnerApplicationHistories] SET [PreviousValue] = @previous, [CurrentValue] = @current WHERE [Id] = @id",
        cancellationToken, ("@previous", previous), ("@current", current), ("@id", id));
      histories++;
    }
    await transaction.CommitAsync(cancellationToken);
    logger.LogInformation("Moved {DocumentCount} legacy documents and converted {HistoryCount} history snapshots to verified object references.", documents, histories);
  }

  private async Task<string?> RewriteHistoryAsync(string? json, Guid applicationId, CancellationToken cancellationToken)
  {
    if (json is null) return null;
    var snapshot = JsonNode.Parse(json)!.AsObject();
    foreach (var node in snapshot["Documents"]?.AsArray() ?? [])
    {
      var document = node!.AsObject();
      if (!document.ContainsKey("Content")) continue;
      if (document["Content"] is not null)
      {
        var id = Guid.Parse(document["Id"]!.GetValue<string>());
        var bytes = Convert.FromBase64String(document["Content"]!.GetValue<string>());
        var stored = await UploadAndVerifyAsync(applicationId, id, bytes,
          document["ContentType"]!.GetValue<string>(), document["Sha256"]!.GetValue<string>(),
          document["SizeBytes"]!.GetValue<long>(), cancellationToken);
        document["StorageBucket"] = stored.Bucket; document["StorageKey"] = stored.Key;
      }
      if (string.IsNullOrWhiteSpace(document["StorageBucket"]?.GetValue<string>()) || string.IsNullOrWhiteSpace(document["StorageKey"]?.GetValue<string>()))
        throw new InvalidOperationException("A historical document has no recoverable content or storage reference.");
      document.Remove("Content");
    }
    return snapshot.ToJsonString();
  }

  private async Task<StoredDocument> UploadAndVerifyAsync(Guid applicationId, Guid id, byte[] bytes,
    string contentType, string sha256, long size, CancellationToken cancellationToken)
  {
    if (bytes.LongLength != size || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), sha256, StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("A legacy document failed its size/checksum check; the database copy was retained.");
    var key = DocumentStorageKey.Create(applicationId, id, sha256);
    if (verified.TryGetValue(key, out var existing)) return existing;
    var storage = services.GetRequiredService<IDocumentStorage>();
    var reference = new StoredDocument(storage.Bucket, key);
    await storage.PutAsync(key, bytes, contentType, sha256, cancellationToken);
    var downloaded = await storage.ReadAsync(reference, cancellationToken);
    if (downloaded.LongLength != size || !string.Equals(Convert.ToHexString(SHA256.HashData(downloaded)), sha256, StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("Object verification failed; the database copy was retained.");
    verified.Add(key, reference);
    return reference;
  }

  private DbCommand Command(string sql, params (string Name, object? Value)[] parameters)
  {
    var command = database.Database.GetDbConnection().CreateCommand();
    command.CommandText = sql;
    command.CommandTimeout = 120;
    command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();
    foreach (var (name, value) in parameters)
    {
      var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value;
      command.Parameters.Add(parameter);
    }
    return command;
  }

  private async Task<object?> ScalarAsync(string sql, CancellationToken cancellationToken)
  {
    await using var command = Command(sql);
    return await command.ExecuteScalarAsync(cancellationToken);
  }

  private async Task ExecuteAsync(string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
  {
    await using var command = Command(sql, parameters);
    await command.ExecuteNonQueryAsync(cancellationToken);
  }
}
