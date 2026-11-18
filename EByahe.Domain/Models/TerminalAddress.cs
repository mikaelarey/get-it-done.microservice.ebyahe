namespace EByahe.Domain.Models;

public static class TerminalAddress
{
  public static string Format(string street, string? barangay, string? municipality, string? province, string? region) =>
    string.Join(", ", new[] { street, barangay, municipality, province, region }
      .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
}
