using System.Text.RegularExpressions;

namespace EByahe.Infrastructure.Persistence;

internal static class PartnerKeywordSearch
{
  public const string Collation = "Latin1_General_100_CI_AS";
  public static string[] Tokens(string? search) => Regex.Split((search ?? "").Trim(), @"\s+")
    .Where(term => term.Length != 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  public static string Pattern(string term) => "%" + term.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";
}
