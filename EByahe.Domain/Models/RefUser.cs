namespace EByahe.Domain.Models;

public sealed class RefUser
{
  public const int IdMaxLength = 255;
  public const int NameMaxLength = 255;
  public const int EmailMaxLength = 254;
  public const int ProviderMaxLength = 100;
  public const string ManualProvider = "Get It Done";
  public const int RoleMaxLength = 32;
  public const string AdministratorRole = "Administrator";
  public const string NormalRole = "Normal";

  public string Id { get; set; } = string.Empty;
  public string FirstName { get; set; } = string.Empty;
  public string LastName { get; set; } = string.Empty;
  public string Email { get; set; } = string.Empty;
  public string Provider { get; set; } = ManualProvider;
  public string Role { get; set; } = NormalRole;
  public DateTimeOffset CreatedAtUtc { get; set; }
}
