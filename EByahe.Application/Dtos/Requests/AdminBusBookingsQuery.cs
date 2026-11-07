using System.ComponentModel.DataAnnotations;

namespace EByahe.Application.Dtos.Requests;

public sealed class AdminBusBookingsQuery
{
  [Range(1, 1000000)] public int Page { get; set; } = 1;
  [Range(1, 50)] public int PageSize { get; set; } = 10;
  [MaxLength(64)] public string? Search { get; set; }
  [MaxLength(32)] public string? Status { get; set; }
}
