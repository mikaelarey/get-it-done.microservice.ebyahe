namespace EByahe.Domain.Models;

public sealed class Room
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid HotelId { get; set; }
  public Hotel Hotel { get; set; } = null!;
  public string Name { get; set; } = "";
  public int CodeDecodeRoomType { get; set; }
  public CodeDecodeRoomType RoomType { get; set; } = null!;
  public int GuestCapacity { get; set; }
  public decimal NightlyRate { get; set; }
  public string? Description { get; set; }
  public bool IsActive { get; set; } = true;
  public bool IsDeleted { get; set; }
  public string? DeletedBy { get; set; }
  public DateTimeOffset? DeletedAtUtc { get; set; }
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
  public ICollection<RoomImage> Images { get; set; } = new List<RoomImage>();
}

public sealed class RoomImage
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid RoomId { get; set; }
  public Room Room { get; set; } = null!;
  public string FileName { get; set; } = "";
  public string ContentType { get; set; } = "";
  public long SizeBytes { get; set; }
  public string Sha256 { get; set; } = "";
  public string StorageBucket { get; set; } = "";
  public string StorageKey { get; set; } = "";
  public int SortOrder { get; set; }
  public bool IsDeleted { get; set; }
  public string CreatedBy { get; set; } = "";
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
  public string? DeletedBy { get; set; }
  public DateTimeOffset? DeletedAtUtc { get; set; }
}

public sealed class RoomLog
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid RoomId { get; set; }
  public string CreatedBy { get; set; } = "";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
  public string Action { get; set; } = "";
  public string? PreviousValue { get; set; }
  public string CurrentValue { get; set; } = "";
}
