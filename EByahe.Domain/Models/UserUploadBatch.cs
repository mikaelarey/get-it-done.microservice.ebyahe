namespace EByahe.Domain.Models;
// Processing state is separate from the immutable Uploads audit entry and UploadRecords links.
public sealed class UserUploadBatch {
  public Guid UploadId { get; set; }
  public Guid PartnerId { get; set; }
  public int RequestedCount { get; set; }
  public string RequestHash { get; set; } = "";
  public DateTimeOffset? FinishedAtUtc { get; set; }
}
public sealed class UploadMemberRequest {
  public Guid UploadId { get; set; }
  public Guid RequestId { get; set; }
  public PartnerMemberRequest Request { get; set; } = null!;
}
