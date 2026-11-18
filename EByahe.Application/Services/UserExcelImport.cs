using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Services;

public static class UserExcelImport
{
  public const int MaximumRows = 500;
  public const long MaximumBytes = 5 * 1024 * 1024;
  private static readonly string[] Headers = ["First name", "Last name", "Email address"];
  public sealed record Parsed(IReadOnlyList<PartnerUserImportRow> Rows, IReadOnlyList<PartnerUserImportError> Errors);

  public static async Task<Parsed> ReadAsync(IFormFile file, string category, CancellationToken cancellationToken)
  {
    static Parsed Invalid(string message) => new([], [new(0, "File", message)]);
    if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
      return Invalid("Upload an Excel .xlsx file using the user template.");
    if (file.Length is <= 0 or > MaximumBytes) return Invalid("The Excel file must be non-empty and no larger than 5 MB.");
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream, cancellationToken);
    try
    {
      stream.Position = 0;
      using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
      {
        if (zip.Entries.Count > 128 || zip.Entries.Sum(entry => entry.Length) > 20 * 1024 * 1024)
          return Invalid("This workbook is too large. Use the user template with up to 500 users.");
      }
      stream.Position = 0;
      using var document = SpreadsheetDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = 4 * 1024 * 1024 });
      if (document.DocumentType != SpreadsheetDocumentType.Workbook) return Invalid("Upload a standard .xlsx workbook without macros.");
      var workbook = document.WorkbookPart;
      if (workbook is null) return Invalid("This workbook has no worksheets. Download a new template.");
      var sheet = workbook.Workbook?.Sheets?.Elements<Sheet>().SingleOrDefault(sheet => sheet.Name?.Value == "Users");
      if (sheet?.Id?.Value is not { } sheetId || workbook.GetPartById(sheetId) is not WorksheetPart worksheet)
        return Invalid("The workbook must contain a Users sheet. Download a new template.");
      var strings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(item => item.InnerText).ToArray() ?? [];
      var rows = new List<PartnerUserImportRow>(); var errors = new List<PartnerUserImportError>();
      var emails = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var headerFound = false; var scanned = 0; var dataRows = 0; var previousRow = 0;
      using var reader = OpenXmlReader.Create(worksheet);
      while (reader.Read())
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;
        if (++scanned > 10000) return Invalid("Remove unused rows and upload up to 500 users.");
        if (reader.LoadCurrentElement() is not Row row) return Invalid("The Users sheet contains an invalid row.");
        var number = checked((int)(row.RowIndex?.Value ?? (uint)(previousRow + 1)));
        if (number <= previousRow) return Invalid("The worksheet contains invalid row numbers. Copy your users into a new template.");
        previousRow = number;
        var values = new string[Headers.Length]; var position = 0; var seenColumns = new HashSet<int>(); var rowErrors = new List<PartnerUserImportError>();
        foreach (var cell in row.Elements<Cell>())
        {
          if (cell.CellReference?.Value is { } reference)
          {
            var match = Regex.Match(reference, "^([A-Z]+)[1-9][0-9]*$");
            if (!match.Success) return Invalid("The worksheet contains an invalid cell address. Copy your users into a new template.");
            position = 0; foreach (var letter in match.Groups[1].Value) position = checked(position * 26 + letter - 'A' + 1);
            position--;
          }
          if (!seenColumns.Add(position)) return Invalid("The worksheet contains duplicate cells. Copy your users into a new template.");
          if (cell.CellFormula is not null) rowErrors.Add(new(number, position >= 0 && position < Headers.Length ? Headers[position] : "Row", "Enter a value instead of a formula."));
          if (cell.DataType?.Value == CellValues.Boolean || cell.DataType?.Value == CellValues.Error)
            rowErrors.Add(new(number, position >= 0 && position < Headers.Length ? Headers[position] : "Row", "Enter plain text, without Excel errors or TRUE/FALSE values."));
          var value = cell.DataType?.Value == CellValues.InlineString ? cell.InlineString?.InnerText ?? "" : cell.CellValue?.Text ?? "";
          if (cell.DataType?.Value == CellValues.SharedString)
          {
            if (!int.TryParse(value, out var index) || index < 0 || index >= strings.Length) return Invalid("This workbook contains invalid text. Copy your users into a new template.");
            value = strings[index];
          }
          if (value.Any(char.IsControl)) rowErrors.Add(new(number, position >= 0 && position < Headers.Length ? Headers[position] : "Row", "Values must not contain line breaks or control characters."));
          value = value.Trim();
          if (position >= 0 && position < Headers.Length) values[position] = value;
          else if (value.Length != 0) rowErrors.Add(new(number, "Row", "Use only the three template columns. Download a new template without Permissions."));
          position++;
        }
        if (!headerFound)
        {
          if (number != 1 || rowErrors.Count != 0 || !Headers.Select((header, index) => string.Equals(header, values[index], StringComparison.OrdinalIgnoreCase)).All(match => match))
            return Invalid("Keep row 1 unchanged: First name, Last name, Email address. Download a new template without Permissions.");
          headerFound = true; continue;
        }
        if (values.All(string.IsNullOrWhiteSpace) && rowErrors.Count == 0) continue;
        if (++dataRows > MaximumRows) return Invalid("Upload at most 500 users at a time.");
        var roles = PartnerMemberRoles.Allowed(category).Where(role => role is "booking.read" or "bus.read" or "terminal.read").ToArray();
        var request = new EByahe.Application.Controllers.AddPartnerMemberRequest { FirstName = values[0] ?? "", LastName = values[1] ?? "",
          Email = (values[2] ?? "").ToLowerInvariant(), Roles = roles };
        var validation = new List<ValidationResult>(); Validator.TryValidateObject(request, new ValidationContext(request), validation, true);
        foreach (var issue in validation) {
          var field = issue.MemberNames.FirstOrDefault() switch { "FirstName" => Headers[0], "LastName" => Headers[1], "Email" => Headers[2], "Roles" => "Permissions", _ => "Row" };
          rowErrors.Add(new(number, field, issue.ErrorMessage ?? "Enter a valid value."));
        }
        if (request.Email.Length != 0 && !emails.TryAdd(request.Email, number)) rowErrors.Add(new(number, Headers[2], $"This email address is repeated from row {emails[request.Email]}."));
        errors.AddRange(rowErrors); rows.Add(new(number, request.FirstName, request.LastName, request.Email, roles));

      }
      return !headerFound ? Invalid("The Users sheet is empty. Download a new template.")
        : dataRows == 0 ? Invalid("Add at least one user to the Users sheet, starting on row 2.") : new(rows, errors);
    }
    catch (Exception error) when (error is OpenXmlPackageException or InvalidDataException or System.Xml.XmlException
      or FormatException or OverflowException or ArgumentException or InvalidOperationException)
    { return Invalid("We could not read this Excel workbook. Copy your users into a new template and save it as .xlsx."); }
  }
}
