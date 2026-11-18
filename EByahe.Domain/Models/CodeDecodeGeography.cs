namespace EByahe.Domain.Models;

public sealed class CodeDecodeRegion
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public string Description { get; set; } = "";
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; }
}

public sealed class CodeDecodeProvince
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public int CodeDecodeRegion { get; set; }
  public CodeDecodeRegion Region { get; set; } = null!;
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; }
}

public sealed class CodeDecodeMunicipality
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public int CodeDecodeProvince { get; set; }
  public CodeDecodeProvince Province { get; set; } = null!;
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; }
}

public sealed class CodeDecodeBarangay
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public int Code { get; set; }
  public string Decode { get; set; } = "";
  public int CodeDecodeMunicipality { get; set; }
  public CodeDecodeMunicipality Municipality { get; set; } = null!;
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset DateTimeCreated { get; set; }
}
