using System.IO.Compression;
using System.Text;

namespace EByahe.Tests;

internal static class PartnerUploadTestFiles
{
  public static byte[] Doc(string streamName = "WordDocument")
  {
    var bytes = new byte[1024];
    new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }.CopyTo(bytes, 0);
    var name = Encoding.Unicode.GetBytes(streamName + '\0');
    name.CopyTo(bytes, 512);
    BitConverter.GetBytes((ushort)name.Length).CopyTo(bytes, 512 + 64);
    bytes[512 + 66] = 2;
    return bytes;
  }

  public static byte[] Docx(bool word = true, bool malformedTypes = false)
  {
    using var buffer = new MemoryStream();
    using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
    {
      void Write(string path, string value)
      {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open());
        writer.Write(value);
      }
      Write("[Content_Types].xml", malformedTypes ? "<invalid" :
        $"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/{(word ? "word/document.xml" : "xl/workbook.xml")}\" ContentType=\"application/vnd.openxmlformats-officedocument.{(word ? "wordprocessingml.document" : "spreadsheetml.sheet")}.main+xml\"/></Types>");
      Write("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"/>");
      Write(word ? "word/document.xml" : "xl/workbook.xml", "<document/>");
    }
    return buffer.ToArray();
  }

  public static IEnumerable<object[]> Allowed()
  {
    yield return new object[] { "proof.pdf", "application/pdf", "%PDF-1.7\nProof\n%%EOF"u8.ToArray() };
    yield return new object[] { "proof.JPG", "image/jpeg", new byte[] { 0xff, 0xd8, 0xff, 1 } };
    yield return new object[] { "proof.png", "image/png", new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a } };
    yield return new object[] { "proof.gif", "image/gif", "GIF89a"u8.ToArray() };
    yield return new object[] { "proof.webp", "image/webp", "RIFF0000WEBPVP8 "u8.ToArray() };
    yield return new object[] { "proof.bmp", "image/bmp", "BM"u8.ToArray().Concat(new byte[24]).ToArray() };
    yield return new object[] { "proof.doc", "application/msword", Doc() };
    yield return new object[] { "proof.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Docx() };
  }
}
