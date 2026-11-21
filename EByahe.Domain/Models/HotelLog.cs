namespace EByahe.Domain.Models;

public sealed class HotelLog
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid HotelId { get; set; }
  public string CreatedBy { get; set; } = "";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
  public string Action { get; set; } = "";
  public string? PreviousValue { get; set; }
  public string CurrentValue { get; set; } = "";
}
