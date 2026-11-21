using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Services;

public static class ScheduledTripExcelImport
{
  public const int MaximumRows = 500;
  public const long MaximumBytes = 5 * 1024 * 1024;
  public static DateOnly ManilaDate(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(8)).DateTime);
  public sealed record Parsed(IReadOnlyList<PartnerScheduledTripImportRow> Rows, IReadOnlyList<PartnerScheduledTripImportError> Errors);

  public static async Task<Parsed> ReadAsync(IFormFile file, Guid partnerId, DateTimeOffset now, CancellationToken ct, PartnerTripOptions? options = null)
  {
    static Parsed Invalid(string message, string sheet = "") => new([], [new(sheet, 0, "File", message)]);
    if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) return Invalid("Upload a standard Excel .xlsx workbook using the scheduled trip template.");
    if (file.Length is <= 0 or > MaximumBytes) return Invalid("Choose a non-empty Excel file no larger than 5 MB.");
    using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct);
    try
    {
      stream.Position = 0;
      using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        if (zip.Entries.Count > 160 || zip.Entries.Sum(entry => entry.Length) > 20 * 1024 * 1024) return Invalid("This workbook is too large. Use up to 31 date sheets and 500 trips in total.");
      stream.Position = 0;
      using var document = SpreadsheetDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = 4 * 1024 * 1024 });
      if (document.DocumentType != SpreadsheetDocumentType.Workbook) return Invalid("Use a standard .xlsx workbook without macros.");
      var workbook = document.WorkbookPart;
      var sheets = workbook?.Workbook?.Sheets?.Elements<Sheet>().ToArray() ?? [];
      var dates = sheets.Where(sheet => sheet.Name?.Value is not ("Instructions" or "_Lists")).ToArray();
      if (workbook is null || dates.Length is < 1 or > 31) return Invalid("Use between 1 and 31 date sheets named YYYY-MM-DD.");
      if (sheets.Select(sheet => sheet.Name?.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sheets.Length) return Invalid("Each date must have a single sheet.");
      var strings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(item => item.InnerText).ToArray() ?? [];
      var terminalChoices = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
      var busChoices = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
      var lookupSheet = sheets.SingleOrDefault(sheet => sheet.Name?.Value == "_Lists");
      if (lookupSheet?.Id?.Value is { } lookupId && workbook.GetPartById(lookupId) is WorksheetPart lookup)
      {
        string Text(Cell cell)
        {
          if (cell.CellFormula is not null) throw new FormatException("Lookup formulas are not supported.");
          var value = cell.DataType?.Value == CellValues.InlineString ? cell.InlineString?.InnerText ?? "" : cell.CellValue?.Text ?? "";
          if (cell.DataType?.Value == CellValues.SharedString)
          {
            if (!int.TryParse(value, out var index) || index < 0 || index >= strings.Length) throw new FormatException("Invalid lookup text.");
            value = strings[index];
          }
          return value.Trim();
        }
        var lookupRows = lookup.Worksheet?.GetFirstChild<SheetData>()?.Elements<Row>().ToArray() ?? [];
        // Earlier templates carry the ID in each label and have only three lookup columns.
        var lookupHeaders = lookupRows.FirstOrDefault()?.Elements<Cell>().ToDictionary(cell => cell.CellReference?.Value ?? "", Text) ?? [];
        if (lookupHeaders.GetValueOrDefault("D1") == "Terminal ID" && lookupHeaders.GetValueOrDefault("E1") == "Bus ID")
        {
          foreach (var row in lookupRows.Skip(1))
          {
            ct.ThrowIfCancellationRequested();
            var cells = row.Elements<Cell>().ToDictionary(cell => Regex.Replace(cell.CellReference?.Value ?? "", "[0-9]", ""), Text);
            void Map(string labelColumn, string idColumn, Dictionary<string, Guid> choices)
            {
              var label = cells.GetValueOrDefault(labelColumn, ""); var value = cells.GetValueOrDefault(idColumn, "");
              if (label.Length == 0 && value.Length == 0) return;
              if (label.Length == 0 || !Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty || !choices.TryAdd(label, id))
                throw new FormatException("Invalid or ambiguous record lookup.");
            }
            Map("A", "D", terminalChoices); Map("B", "E", busChoices);
          }
        }
      }
      var rows = new List<PartnerScheduledTripImportRow>(); var errors = new List<PartnerScheduledTripImportError>(); var total = 0;
      var headers = ScheduledTripExcelTemplate.Headers;
      foreach (var sheet in dates)
      {
        var name = sheet.Name?.Value ?? "";
        if (!DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        { errors.Add(new(name, 0, "Date", "Name each date sheet YYYY-MM-DD, for example 2026-11-19.")); continue; }
        if (date < ManilaDate(now)) errors.Add(new(name, 0, "Date", "The sheet date is in the past in Manila time. Choose today or a future date."));
        if (sheet.Id?.Value is not { } id || workbook.GetPartById(id) is not WorksheetPart worksheet) return Invalid("The workbook contains an invalid worksheet.", name);
        var headerFound = false; var previous = 0; var scanned = 0;
        var sharedDurations = new Dictionary<uint, (int First, int Last)>();
        using var reader = OpenXmlReader.Create(worksheet);
        while (reader.Read())
        {
          ct.ThrowIfCancellationRequested();
          if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;
          if (++scanned > 10000 || reader.LoadCurrentElement() is not Row row) return Invalid("Remove unused rows and use the downloaded template.", name);
          var number = checked((int)(row.RowIndex?.Value ?? (uint)(previous + 1)));
          if (number <= previous) return Invalid("The worksheet contains invalid row numbers.", name);
          previous = number;
          var values = new string[headers.Length]; var numeric = new bool[headers.Length]; var seen = new HashSet<int>(); var position = 0;
          var issues = new List<PartnerScheduledTripImportError>();
          var automaticDuration = false;
          void Error(int column, string message) => issues.Add(new(name, number, column >= 0 && column < headers.Length ? headers[column] : "Row", message));
          foreach (var cell in row.Elements<Cell>())
          {
            if (cell.CellReference?.Value is { } reference)
            {
              var match = Regex.Match(reference, "^([A-Z]+)([1-9][0-9]*)$");
              if (!match.Success || match.Groups[2].Value != number.ToString(CultureInfo.InvariantCulture)) return Invalid("The worksheet contains an invalid cell address.", name);
              position = 0; foreach (var letter in match.Groups[1].Value) position = checked(position * 26 + letter - 'A' + 1); position--;
            }
            if (!seen.Add(position)) return Invalid("The worksheet contains duplicate cells.", name);
            var templateFormula = false;
            if (cell.CellFormula is { } formula) {
              if (position == 4) {
                templateFormula = formula.Text == ScheduledTripExcelTemplate.DurationFormula(number)
                  && (formula.FormulaType is null || formula.FormulaType.Value == CellFormulaValues.Normal || formula.FormulaType.Value == CellFormulaValues.Shared);
                if (formula.FormulaType?.Value == CellFormulaValues.Shared && formula.SharedIndex?.Value is { } group) {
                  if (templateFormula) {
                    // Excel can retain the group's original range after earlier cells are saved
                    // separately, e.g. the master is E7 but its range remains E2:E65.
                    var range = Regex.Match(formula.Reference?.Value ?? "", "^E([1-9][0-9]*):E([1-9][0-9]*)$");
                    templateFormula = range.Success && int.TryParse(range.Groups[1].Value, out var first)
                      && int.TryParse(range.Groups[2].Value, out var last) && first >= 2 && first <= number && number <= last && last <= MaximumRows + 1;
                    if (templateFormula) sharedDurations[group] = (int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture));
                  } else if (formula.Text.Length == 0 && sharedDurations.TryGetValue(group, out var range)) templateFormula = number >= range.First && number <= range.Last;
                }
                automaticDuration = templateFormula;
              }
              if (!templateFormula) Error(position, "Enter a value instead of a formula. Only the template's travel-time formula is supported.");
            }
            if (!templateFormula && (cell.DataType?.Value == CellValues.Boolean || cell.DataType?.Value == CellValues.Error)) Error(position, "Excel errors and TRUE/FALSE values are not supported.");
            var value = cell.DataType?.Value == CellValues.InlineString ? cell.InlineString?.InnerText ?? "" : cell.CellValue?.Text ?? "";
            if (cell.DataType?.Value == CellValues.SharedString)
            {
              if (!int.TryParse(value, out var index) || index < 0 || index >= strings.Length) return Invalid("The workbook contains invalid text.", name);
              value = strings[index];
            }
            if (value.Any(char.IsControl)) Error(position, "Values must not contain control characters or line breaks.");
            if (position >= 0 && position < headers.Length) { values[position] = position == 4 && automaticDuration ? "" : value.Trim(); numeric[position] = cell.DataType is null || cell.DataType.Value == CellValues.Number; }
            else if (!string.IsNullOrWhiteSpace(value)) Error(position, "Use only the six template columns.");
            position++;
          }
          if (!headerFound)
          {
            if (number != 1 || issues.Count != 0 || !headers.Select((header, index) => string.Equals(header, values[index], StringComparison.OrdinalIgnoreCase)).All(match => match))
              return Invalid("Keep row 1 unchanged: " + string.Join(", ", headers) + ".", name);
            headerFound = true; continue;
          }
          if (values.All(string.IsNullOrWhiteSpace) && issues.Count == 0) continue;
          if (++total > MaximumRows) return Invalid("Upload at most 500 trips across all date sheets.");
          Guid Choice(int column)
          {
            var value = values[column] ?? "";
            if ((column == 2 ? busChoices : terminalChoices).TryGetValue(value, out var mapped)) return mapped;
            var separator = value.LastIndexOf('|');
            if (Guid.TryParseExact((separator >= 0 ? value[(separator + 1)..] : value).Trim(), "D", out var choice) && choice != Guid.Empty) return choice;
            Error(column, "Select a record from the template dropdown. Download a new template to refresh the choices."); return Guid.Empty;
          }
          var origin = Choice(0); var destination = Choice(1); var bus = Choice(2);
          if (automaticDuration) values[4] = options?.Routes.SingleOrDefault(route => route.PartnerId == partnerId && route.OriginTerminalId == origin && route.DestinationTerminalId == destination)
            ?.DurationMinutes.ToString(CultureInfo.InvariantCulture) ?? "";
          if (origin != Guid.Empty && origin == destination) Error(1, "Origin and destination must be different terminals.");
          TimeOnly time = default; var timeValid = false;
          if (numeric[3])
          {
            if (double.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) && double.IsFinite(fraction) && fraction >= 0 && fraction < 1)
            { time = TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(Math.Min(86399, Math.Round(fraction * 86400)))); timeValid = true; }
          }
          else timeValid = TimeOnly.TryParseExact(values[3], ["h:mm tt", "hh:mm tt", "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
          if (!timeValid) Error(3, "Choose a departure time such as 7:00 AM or 7:00 PM from the dropdown.");
          var departure = new DateTimeOffset(date.ToDateTime(time), TimeSpan.FromHours(8)).ToUniversalTime();
          if (timeValid && departure <= now) Error(3, "Departure must be in the future in Manila time.");
          var durationValid = decimal.TryParse(values[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) && duration is >= 1 and <= 2880 && decimal.Truncate(duration) == duration;
          if (!durationValid) Error(4, automaticDuration ? "No active route travel time is available. Configure this direction in Travel time setup, then download a new template." : "Travel time must be a whole number of minutes from 1 to 2880.");
          var fareValid = decimal.TryParse(values[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var fare) && fare is >= 0.01m and <= 1000000m && decimal.Round(fare, 2) == fare;
          if (!fareValid) Error(5, "Fare must be from PHP 0.01 to PHP 1,000,000 with at most two decimal places.");
          if (durationValid && departure > DateTimeOffset.MaxValue.AddMinutes(-(int)duration)) Error(4, "Arrival is outside the supported date range.");
          errors.AddRange(issues);
          rows.Add(new(name, number, new(partnerId, bus, origin, destination, departure, durationValid ? (int)duration : 0, fareValid ? fare : 0)));
        }
        if (!headerFound) errors.Add(new(name, 0, "File", "This date sheet has no header. Copy a date sheet from the downloaded template."));
      }
      if (total == 0 && errors.Count == 0) return Invalid("Add at least one trip, starting at row 2 on a date sheet.");
      return new(rows, errors);
    }
    catch (Exception error) when (error is OpenXmlPackageException or InvalidDataException or System.Xml.XmlException or FormatException or OverflowException or ArgumentException or InvalidOperationException)
    { return Invalid("We could not read this workbook. Copy your trips into a new template and save it as .xlsx."); }
  }
}
