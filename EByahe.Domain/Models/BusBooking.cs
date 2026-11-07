namespace EByahe.Domain.Models;

public sealed class BusBooking
{
  public Guid Id { get; set; }
  public Guid RequestId { get; set; }
  public Guid? ScheduledTripId { get; set; }
  public int ReservedSeats { get; set; }
  public string CreatedBy { get; set; } = string.Empty;
  public string Reference { get; set; } = string.Empty;
  public DateTimeOffset CreatedAtUtc { get; set; }
  public string Status { get; set; } = "Submitted";
  public decimal Total { get; set; }
  public string RequestHash { get; set; } = string.Empty;
  // Validated intake snapshot. Contains safe payment metadata, never card numbers or CVVs.
  public string DetailsJson { get; set; } = string.Empty;
}
