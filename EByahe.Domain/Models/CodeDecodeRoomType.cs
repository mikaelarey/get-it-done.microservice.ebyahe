namespace EByahe.Domain.Models;

public sealed class CodeDecodeRoomType
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; } = DateTimeOffset.UtcNow;
}
