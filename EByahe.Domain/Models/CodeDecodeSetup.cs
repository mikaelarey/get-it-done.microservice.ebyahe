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

public sealed class CodeDecodeTopic
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public interface ITopicRecord
{
  int CodeDecodeTopic { get; set; }
  CodeDecodeTopic? TopicSetup { get; set; }
  string Topic { get; }
  bool HasPendingTopic();
  void ClearPendingTopic();
}

public sealed class CodeDecodeEntityType
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeSettlementFrequency
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeRole
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeBank
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeEventType
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeDocumentType
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CodeDecodeBusinessCategory
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
    // API/event identifiers stay stable while the setup Decode contains the display label.
    ["EntityTypes"] = ["sole-proprietorship", "partnership", "corporation"],
    ["SettlementFrequencies"] = ["daily", "weekly"],
    ["Roles"] = ["Normal", "Administrator"],
    ["EventTypes"] = ["partner-application.submitted", "partner-application.under-review", "partner-application.approved", "partner-application.declined", "partner.approved", "partner.created", "partner.updated", "partner.member.requested", "partner-user.updated", "booking.created", "bus.created", "bus.updated", "buses.imported", "terminal.created", "terminal.updated", "terminals.imported", "scheduled-trip.created", "scheduled-trip.updated", "scheduled-trips.imported", "users.imported"],
    ["DocumentTypes"] = ["registration", "bir", "permit", "accreditation", "bankAccountProof"],
    ["BusinessCategories"] = ["bus-operator", "hotel"],
    ["BusClasses"] = ["First Class", "Deluxe", "Super Deluxe", "Luxury", "Executive Class", "Executive Luxury", "Executive Solo", "Regular Aircon", "Regular / Air-conditioned", "Air-conditioned / Direct"],
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
