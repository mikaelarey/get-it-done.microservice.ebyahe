namespace EByahe.Domain.DataModels;

public static class DocumentStorageKey
{
  // Each byte-content version has its own key so historical references remain immutable.
  public static string Create(Guid applicationId, Guid documentId, string sha256) =>
    $"ebyahe/partner-applications/{applicationId:N}/{documentId:N}/{sha256.ToLowerInvariant()}";
}
