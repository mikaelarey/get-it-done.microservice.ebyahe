using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Services;

public static class BusExcelImport
{
  public const int MaximumRows = 500;
  public const long MaximumBytes = 5 * 1024 * 1024;
  private static readonly string[] Headers = ["Fleet number", "Plate number", "Bus class", "Seat capacity"];
  public sealed record Parsed(IReadOnlyList<PartnerBusImportRow> Rows, IReadOnlyList<PartnerBusImportError> Errors);

  public static async Task<Parsed> ReadAsync(IFormFile file, Guid partnerId, CancellationToken cancellationToken)
  {
    static Parsed Invalid(string message) => new([], [new(0, "File", message)]);
    if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
      return Invalid("Upload an Excel .xlsx file using the bus template.");
    if (file.Length is <= 0 or > MaximumBytes) return Invalid("The Excel file must be non-empty and no larger than 5 MB.");
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream, cancellationToken);
    try
    {
      stream.Position = 0;
      using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
      {
        if (zip.Entries.Count > 128 || zip.Entries.Sum(entry => entry.Length) > 20 * 1024 * 1024)
          return Invalid("This workbook is too large. Use the bus template with up to 500 buses.");
      }
      stream.Position = 0;
      using var document = SpreadsheetDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = 4 * 1024 * 1024 });
      if (document.DocumentType != SpreadsheetDocumentType.Workbook) return Invalid("Upload a standard .xlsx workbook without macros.");
      var workbook = document.WorkbookPart;
      if (workbook is null) return Invalid("This workbook has no worksheets. Download a new template.");
      var sheet = workbook.Workbook?.Sheets?.Elements<Sheet>().SingleOrDefault(sheet => sheet.Name?.Value == "Buses");
      if (sheet?.Id?.Value is not { } sheetId || workbook.GetPartById(sheetId) is not WorksheetPart worksheet)
        return Invalid("The workbook must contain a Buses sheet. Download a new template.");
      var strings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(item => item.InnerText).ToArray() ?? [];
      var rows = new List<PartnerBusImportRow>(); var errors = new List<PartnerBusImportError>();
      var fleets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var plates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var headerFound = false; var scanned = 0; var dataRows = 0; var previousRow = 0;
      using var reader = OpenXmlReader.Create(worksheet);
      while (reader.Read())
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;
        if (++scanned > 10000) return Invalid("Remove unused rows and upload up to 500 buses.");
        if (reader.LoadCurrentElement() is not Row row) return Invalid("The Buses sheet contains an invalid row.");
        var number = checked((int)(row.RowIndex?.Value ?? (uint)(previousRow + 1)));
        if (number <= previousRow) return Invalid("The worksheet contains invalid row numbers. Copy your buses into a new template.");
        previousRow = number;
        var values = new string[4]; var position = 0; var seenColumns = new HashSet<int>(); var rowErrors = new List<PartnerBusImportError>();
        foreach (var cell in row.Elements<Cell>())
        {
          if (cell.CellReference?.Value is { } reference)
          {
            var match = Regex.Match(reference, "^([A-Z]+)[1-9][0-9]*$");
            if (!match.Success) return Invalid("The worksheet contains an invalid cell address. Copy your buses into a new template.");
            position = 0; foreach (var letter in match.Groups[1].Value) position = checked(position * 26 + letter - 'A' + 1);
            position--;
          }
          if (!seenColumns.Add(position)) return Invalid("The worksheet contains duplicate cells. Copy your buses into a new template.");
          if (cell.CellFormula is not null) rowErrors.Add(new(number, position is >= 0 and < 4 ? Headers[position] : "Row", "Enter a value instead of a formula."));
          if (cell.DataType?.Value == CellValues.Boolean || cell.DataType?.Value == CellValues.Error)
            rowErrors.Add(new(number, position is >= 0 and < 4 ? Headers[position] : "Row", "Enter plain text or a seat count, without Excel errors or TRUE/FALSE values."));
          var value = cell.DataType?.Value == CellValues.InlineString ? cell.InlineString?.InnerText ?? "" : cell.CellValue?.Text ?? "";
          if (cell.DataType?.Value == CellValues.SharedString)
          {
            if (!int.TryParse(value, out var index) || index < 0 || index >= strings.Length) return Invalid("This workbook contains invalid text. Copy your buses into a new template.");
            value = strings[index];
          }
          if (value.Any(char.IsControl)) rowErrors.Add(new(number, position is >= 0 and < 4 ? Headers[position] : "Row", "Values must not contain line breaks or control characters."));
          value = value.Trim();
          if (position is >= 0 and < 4) values[position] = value;
          else if (value.Length != 0) rowErrors.Add(new(number, "Row", "Use only the four template columns."));
          position++;
        }
        if (!headerFound)
        {
          if (number != 1 || rowErrors.Count != 0 || !Headers.Select((header, index) => string.Equals(header, values[index], StringComparison.OrdinalIgnoreCase)).All(match => match))
            return Invalid("Keep row 1 unchanged: Fleet number, Plate number, Bus class, Seat capacity.");
          headerFound = true; continue;
        }
        if (values.All(string.IsNullOrWhiteSpace) && rowErrors.Count == 0) continue;
        if (++dataRows > MaximumRows) return Invalid("Upload at most 500 buses at a time.");
        var capacityValid = decimal.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var capacity)
          && capacity is >= 1 and <= 100 && decimal.Truncate(capacity) == capacity;
        var request = new AddPartnerBusRequest { PartnerId = partnerId, FleetNumber = values[0] ?? "", PlateNumber = values[1] ?? "",
          BusClass = values[2] ?? "", SeatCapacity = capacityValid ? (int)capacity : 0 };
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validation, true);
        foreach (var issue in validation)
        {
          var field = issue.MemberNames.FirstOrDefault() switch { "FleetNumber" => Headers[0], "PlateNumber" => Headers[1], "BusClass" => Headers[2], "SeatCapacity" => Headers[3], _ => "Row" };
          rowErrors.Add(new(number, field, field == Headers[3] ? "Seat capacity must be a whole number from 1 to 100." : issue.ErrorMessage ?? "Enter a valid value."));
        }
        var command = request.ToCommand();
        foreach (var (value, field, seen) in new[] { (command.FleetNumber, Headers[0], fleets), (command.PlateNumber, Headers[1], plates) })
          if (value.Length != 0 && !seen.TryAdd(value, number)) rowErrors.Add(new(number, field, $"This value is repeated from row {seen[value]}."));
        errors.AddRange(rowErrors); rows.Add(new(number, command));
      }
      return !headerFound ? Invalid("The Buses sheet is empty. Download a new template.")
        : dataRows == 0 ? Invalid("Add at least one bus to the Buses sheet, starting on row 2.") : new(rows, errors);
    }
    catch (Exception error) when (error is OpenXmlPackageException or InvalidDataException or System.Xml.XmlException
      or FormatException or OverflowException or ArgumentException or InvalidOperationException)
    { return Invalid("We could not read this Excel workbook. Copy your buses into a new template and save it as .xlsx."); }
  }
}
