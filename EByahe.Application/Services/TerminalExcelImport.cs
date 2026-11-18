using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EByahe.Application.Controllers;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Services;

public static class TerminalExcelImport
{
  public const int MaximumRows = 500;
  public const long MaximumBytes = 5 * 1024 * 1024;
  public static readonly string[] Headers = ["Terminal name", "Region", "Province", "City / Municipality", "Barangay", "Street / Building address", "Location"];
  public sealed record Parsed(IReadOnlyList<PartnerTerminalImportRow> Rows, IReadOnlyList<PartnerTerminalImportError> Errors);

  public static async Task<Parsed> ReadAsync(IFormFile file, Guid partnerId, CancellationToken cancellationToken)
  {
    static Parsed Invalid(string message) => new([], [new(0, "File", message)]);
    if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
      return Invalid("Upload an Excel .xlsx file using the terminal template.");
    if (file.Length is <= 0 or > MaximumBytes) return Invalid("The Excel file must be non-empty and no larger than 5 MB.");
    using var stream = new MemoryStream(); await file.CopyToAsync(stream, cancellationToken);
    try
    {
      stream.Position = 0;
      using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        if (zip.Entries.Count > 128 || zip.Entries.Sum(entry => entry.Length) > 64 * 1024 * 1024)
          return Invalid("This workbook is too large. Use the terminal template with up to 500 terminals.");
      stream.Position = 0;
      using var document = SpreadsheetDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = 24 * 1024 * 1024 });
      if (document.DocumentType != SpreadsheetDocumentType.Workbook) return Invalid("Upload a standard .xlsx workbook without macros.");
      var workbook = document.WorkbookPart;
      var sheet = workbook?.Workbook?.Sheets?.Elements<Sheet>().SingleOrDefault(sheet => sheet.Name?.Value == "Terminals");
      if (sheet?.Id?.Value is not { } sheetId || workbook!.GetPartById(sheetId) is not WorksheetPart worksheet)
        return Invalid("The workbook must contain a Terminals sheet. Download a new template.");
      var strings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(item => item.InnerText).ToArray() ?? [];
      var rows = new List<PartnerTerminalImportRow>(); var errors = new List<PartnerTerminalImportError>();
      var seen = new Dictionary<(int Barangay, string Name), int>();
      var headerFound = false; var scanned = 0; var dataRows = 0; var previousRow = 0;
      using var reader = OpenXmlReader.Create(worksheet);
      while (reader.Read())
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;
        if (++scanned > 10000) return Invalid("Remove unused rows and upload up to 500 terminals.");
        if (reader.LoadCurrentElement() is not Row row) return Invalid("The Terminals sheet contains an invalid row.");
        var number = checked((int)(row.RowIndex?.Value ?? (uint)(previousRow + 1)));
        if (number <= previousRow) return Invalid("The worksheet contains invalid row numbers. Copy your terminals into a new template.");
        previousRow = number;
        var values = new string[7]; var position = 0; var seenColumns = new HashSet<int>(); var rowErrors = new List<PartnerTerminalImportError>();
        foreach (var cell in row.Elements<Cell>())
        {
          if (cell.CellReference?.Value is { } reference)
          {
            var match = Regex.Match(reference, "^([A-Z]+)([1-9][0-9]*)$");
            if (!match.Success || !int.TryParse(match.Groups[2].Value, out var cellRow) || cellRow != number)
              return Invalid("The worksheet contains an invalid cell address. Copy your terminals into a new template.");
            position = 0; foreach (var letter in match.Groups[1].Value) position = checked(position * 26 + letter - 'A' + 1);
            position--;
          }
          if (!seenColumns.Add(position)) return Invalid("The worksheet contains duplicate cells.");
          // Location is a workbook preview only. Its formula/cache is never evaluated or trusted.
          if (number > 1 && position == 6) { position++; continue; }
          if (cell.CellFormula is not null) rowErrors.Add(new(number, position is >= 0 and < 7 ? Headers[position] : "Row", "Enter a value instead of a formula."));
          if (cell.DataType?.Value == CellValues.Boolean || cell.DataType?.Value == CellValues.Error)
            rowErrors.Add(new(number, position is >= 0 and < 7 ? Headers[position] : "Row", "Enter plain text or a valid geography option, without Excel errors or TRUE/FALSE values."));
          var value = cell.DataType?.Value == CellValues.InlineString ? cell.InlineString?.InnerText ?? "" : cell.CellValue?.Text ?? "";
          if (cell.DataType?.Value == CellValues.SharedString)
          {
            if (!int.TryParse(value, out var index) || index < 0 || index >= strings.Length) return Invalid("This workbook contains invalid text. Copy your terminals into a new template.");
            value = strings[index];
          }
          if (value.Any(character => char.IsControl(character) && !(position == 5 && character is '\r' or '\n' or '\t')))
            rowErrors.Add(new(number, position is >= 0 and < 7 ? Headers[position] : "Row", "Values must not contain control characters."));
          value = value.Trim();
          if (position is >= 0 and < 7) values[position] = value;
          else if (value.Length != 0) rowErrors.Add(new(number, "Row", "Use only the seven template columns."));
          position++;
        }
        if (!headerFound)
        {
          if (number != 1 || rowErrors.Count != 0 || !Headers.Select((header, index) => string.Equals(header, values[index], StringComparison.OrdinalIgnoreCase)).All(match => match))
            return Invalid("Keep the seven headers on row 1 unchanged. Download a new terminal template.");
          headerFound = true; continue;
        }
        if (values.Take(6).All(string.IsNullOrWhiteSpace) && rowErrors.Count == 0) continue;
        if (++dataRows > MaximumRows) return Invalid("Upload at most 500 terminals at a time.");
        int Code(int column)
        {
          var value = values[column] ?? ""; var separator = value.IndexOf(" - ", StringComparison.Ordinal);
          return int.TryParse(separator < 0 ? value : value[..separator], out var code) && code > 0 ? code : 0;
        }
        var request = new AddPartnerTerminalRequest { PartnerId = partnerId, Name = values[0] ?? "", Address = values[5] ?? "",
          CodeDecodeRegion = Code(1), CodeDecodeProvince = Code(2), CodeDecodeMunicipality = Code(3), CodeDecodeBarangay = Code(4) };
        var validation = new List<ValidationResult>(); Validator.TryValidateObject(request, new ValidationContext(request), validation, true);
        foreach (var issue in validation)
        {
          var field = issue.MemberNames.FirstOrDefault() switch { "Name" => Headers[0], "Address" => Headers[5], "CodeDecodeRegion" => Headers[1], "CodeDecodeProvince" => Headers[2], "CodeDecodeMunicipality" => Headers[3], "CodeDecodeBarangay" => Headers[4], _ => "Row" };
          rowErrors.Add(new(number, field, issue.ErrorMessage ?? "Enter a valid value."));
        }
        var key = (request.CodeDecodeBarangay, request.Name.ToUpperInvariant());
        if (!seen.TryAdd(key, number)) rowErrors.Add(new(number, "Terminal name", $"This terminal and barangay are repeated from row {seen[key]}."));
        errors.AddRange(rowErrors);
        rows.Add(new(number, new(partnerId, request.CodeDecodeRegion, request.CodeDecodeProvince, request.CodeDecodeMunicipality, request.CodeDecodeBarangay, request.Name, request.Address)));
      }
      return !headerFound ? Invalid("The Terminals sheet is empty. Download a new template.")
        : dataRows == 0 ? Invalid("Add at least one terminal to the Terminals sheet, starting on row 2.") : new(rows, errors);
    }
    catch (Exception error) when (error is OpenXmlPackageException or InvalidDataException or System.Xml.XmlException
      or FormatException or OverflowException or ArgumentException or InvalidOperationException)
    { return Invalid("We could not read this workbook. Copy your terminals into a new template and save it as .xlsx."); }
  }
}
