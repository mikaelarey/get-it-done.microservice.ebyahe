using System.Text.RegularExpressions;

namespace EByahe.Infrastructure.Storage;

public sealed class DocumentStorageOptions
{
  public string Endpoint { get; set; } = string.Empty;
  public string Region { get; set; } = "us-east-1";
  public string Bucket { get; set; } = string.Empty;
  public string AccessKey { get; set; } = string.Empty;
  public string SecretKey { get; set; } = string.Empty;

  public bool IsValid() => Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
    && (endpoint.Scheme == Uri.UriSchemeHttps || endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)
    && endpoint.UserInfo.Length == 0 && endpoint.Query.Length == 0 && endpoint.Fragment.Length == 0
    && endpoint.AbsolutePath == "/" && !string.IsNullOrWhiteSpace(Region)
    && Regex.IsMatch(Bucket, "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$")
    && !string.IsNullOrWhiteSpace(AccessKey) && !string.IsNullOrWhiteSpace(SecretKey);
}
