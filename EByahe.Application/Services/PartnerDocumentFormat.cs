using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace EByahe.Application.Services;

internal static class PartnerDocumentFormat
{
  public static string? ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
  {
    ".pdf" => "application/pdf",
    ".jpg" or ".jpeg" => "image/jpeg",
    ".png" => "image/png",
    ".gif" => "image/gif",
    ".webp" => "image/webp",
    ".bmp" => "image/bmp",
    ".doc" => "application/msword",
    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    _ => null
  };

  public static bool Matches(byte[] bytes, string contentType)
  {
    ReadOnlySpan<byte> content = bytes;
    return contentType switch
    {
      "application/pdf" => content.StartsWith("%PDF-"u8),
      "image/jpeg" => content.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
      "image/png" => content.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
      "image/gif" => content.StartsWith("GIF87a"u8) || content.StartsWith("GIF89a"u8),
      "image/webp" => content.Length >= 16 && content.StartsWith("RIFF"u8) &&
        content.Slice(8, 4).SequenceEqual("WEBP"u8) &&
        (content.Slice(12, 4).SequenceEqual("VP8 "u8) || content.Slice(12, 4).SequenceEqual("VP8L"u8) || content.Slice(12, 4).SequenceEqual("VP8X"u8)),
      "image/bmp" => content.Length >= 26 && content.StartsWith("BM"u8),
      "application/msword" => IsLegacyWord(content),
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => IsOpenXmlWord(bytes),
      _ => false
    };
  }

  private static bool IsLegacyWord(ReadOnlySpan<byte> content)
  {
    if (content.Length < 512 || !content.StartsWith(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 })) return false;
    // OLE is shared by Excel and other formats. Require a Word stream directory entry too.
    var wordName = Encoding.Unicode.GetBytes("WordDocument\0");
    for (var offset = 512; offset <= content.Length - 128; offset += 128)
    {
      var entry = content.Slice(offset, 128);
      if (entry[66] == 2 && BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(64, 2)) == wordName.Length &&
          entry.StartsWith(wordName)) return true;
    }
    return false;
  }

  private static bool IsOpenXmlWord(byte[] content)
  {
    try
    {
      using var stream = new MemoryStream(content, writable: false);
      using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
      var types = archive.GetEntry("[Content_Types].xml");
      if (types is null || types.Length > 65536 || archive.GetEntry("_rels/.rels") is null) return false;
      using var typeStream = types.Open();
      using var reader = XmlReader.Create(typeStream, new XmlReaderSettings
      {
        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536
      });
      var document = XDocument.Load(reader);
      XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
      var main = document.Root?.Elements(ns + "Override").SingleOrDefault(element =>
        (string?)element.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
      var part = (string?)main?.Attribute("PartName");
      return part?.StartsWith('/') == true && archive.GetEntry(part[1..]) is { Length: > 0 };
    }
    catch (Exception error) when (error is InvalidDataException or IOException or XmlException or InvalidOperationException)
    {
      return false;
    }
  }
}
