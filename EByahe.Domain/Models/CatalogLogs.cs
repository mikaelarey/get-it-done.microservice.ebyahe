namespace EByahe.Domain.Models;

public static class UploadTypeCodes
{
  public const int Buses = 1, Terminals = 2, ScheduledTrips = 3, Users = 4;
}

public sealed class CodeDecodeUploadType
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; }
}

public sealed class Upload
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string CreatedBy { get; set; } = "";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
  public int UploadType { get; set; }
}

public sealed class UploadRecord
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid UploadId { get; set; }
  public Guid? BusId { get; set; }
  public Guid? TerminalId { get; set; }
  public Guid? ScheduledTripId { get; set; }
  public Guid? RefUserId { get; set; }
}

public sealed class BusLog
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid BusId { get; set; }
  public Guid? UploadId { get; set; }
  public string CreatedBy { get; set; } = "";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
  public string Action { get; set; } = "";
  public string? PreviousValue { get; set; }
  public string CurrentValue { get; set; } = "";
}

public sealed class TerminalLog
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid TerminalId { get; set; }
  public Guid? UploadId { get; set; }
  public string CreatedBy { get; set; } = "";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
  public string Action { get; set; } = "";
  public string? PreviousValue { get; set; }
  public string CurrentValue { get; set; } = "";
}

public sealed class ScheduledTripLog
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid ScheduledTripId { get; set; }
  public Guid? UploadId { get; set; }
  public string CreatedBy { get; set; } = "";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
  public string Action { get; set; } = "";
  public string? PreviousValue { get; set; }
  public string CurrentValue { get; set; } = "";
}
