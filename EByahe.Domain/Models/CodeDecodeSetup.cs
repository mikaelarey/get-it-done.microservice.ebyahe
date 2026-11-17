namespace EByahe.Domain.Models;

public sealed class CodeDecodeOutboxStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeOutgoingStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeIncomingStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BookingStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PaymentStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PaymentType
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodePartnerApplicationStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodePartnerApplicationApprovalStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodePartnerStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeScheduledTripStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodePartnerMemberRequestStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public static class CodeDecodeValues
{
  public static readonly IReadOnlyDictionary<string, string[]> Values = new Dictionary<string, string[]>
  {
    ["OutboxStatuses"] = ["Pending", "Publishing", "Failed", "Published"],
    ["OutgoingStatuses"] = ["Pending", "Publishing", "Failed", "Published", "LeaseExpired"],
    ["IncomingStatuses"] = ["Received", "Processing", "Processed", "Failed", "Acknowledged", "AcknowledgementFailed"],
    ["BookingStatuses"] = ["Submitted", "Confirmed", "Cancelled"],
    ["PaymentStatuses"] = ["Pending Payment", "Payment Failed", "Payment Success", "Cancelled"],
    ["PaymentTypes"] = ["GCash", "Maya", "Credit/Debit Card", "Cash"],
    ["PartnerApplicationStatuses"] = ["Submitted", "UnderReview", "Approved", "Rejected"],
    ["PartnerApplicationApprovalStatuses"] = ["Pending", "UnderReview", "NeedsChanges", "Approved", "Rejected"],
    ["PartnerStatuses"] = ["Approved", "Suspended"],
    ["ScheduledTripStatuses"] = ["Scheduled", "Cancelled"],
    ["PartnerMemberRequestStatuses"] = ["Pending", "Completed", "Cancelled"],
  };
  public static int Code(string table, string decode) => Array.IndexOf(Values[table], decode) + 1;
  public static string Decode(string table, int code) => code > 0 && code <= Values[table].Length ? Values[table][code - 1] : "";
}
