using System.ComponentModel.DataAnnotations;

namespace EByahe.Application.Dtos.Requests;

public sealed class PartnerRecordsQuery
{
  [Range(1, 1000000)] public int Page { get; set; } = 1;
  [Range(1, 50)] public int PageSize { get; set; } = 10;
}
