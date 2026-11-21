namespace EByahe.Domain.Models;

public sealed class Hotel
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid PartnerId { get; set; }
  public Partner Partner { get; set; } = null!;
  public string Name { get; set; } = "";
  public string Address { get; set; } = "";
  public int CodeDecodeRegion { get; set; }
  public CodeDecodeRegion Region { get; set; } = null!;
  public int CodeDecodeProvince { get; set; }
  public CodeDecodeProvince Province { get; set; } = null!;
  public int CodeDecodeMunicipality { get; set; }
  public CodeDecodeMunicipality Municipality { get; set; } = null!;
  public int CodeDecodeBarangay { get; set; }
  public CodeDecodeBarangay Barangay { get; set; } = null!;
  public string ContactNumber { get; set; } = "";
  public string Email { get; set; } = "";
  public string? Description { get; set; }
  public bool IsActive { get; set; } = true;
  public bool IsDeleted { get; set; }
  public string? DeletedBy { get; set; }
  public DateTimeOffset? DeletedAtUtc { get; set; }
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
