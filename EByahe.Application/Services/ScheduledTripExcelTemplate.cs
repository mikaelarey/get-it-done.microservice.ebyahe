using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Services;

public static class ScheduledTripExcelTemplate
{
  public const string FileName = "Scheduled-trip-import-template.xlsx";
  public static readonly string[] Headers = ["Origin terminal", "Destination terminal", "Bus", "Departure time (Manila)", "Duration (minutes)", "Fare (PHP)"];
  public static string TerminalLabel(PartnerTripTerminal terminal) => $"{terminal.Name} ({terminal.Location})";
  public static string BusLabel(PartnerTripBus bus) => $"{bus.FleetNumber} - {bus.PlateNumber ?? "No plate"}";
  public static string DurationFormula(int row) =>
    $"IF(OR(A{row}=\"\",B{row}=\"\"),\"\",IFERROR(VLOOKUP(SUBSTITUTE(SUBSTITUTE(SUBSTITUTE(A{row}&CHAR(9)&B{row},\"~\",\"~~\"),\"*\",\"~*\"),\"?\",\"~?\"),RouteTravelTimes,2,FALSE),\"\"))";

  private static string[] UniqueLabels(IEnumerable<string> labels)
  {
    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    return labels.Select(label => {
      var choice = label; var number = 1;
      while (!used.Add(choice)) choice = $"{label} [{++number}]";
      return choice;
    }).ToArray();
  }

  public static byte[] Generate(PartnerTripOptions options, Guid partnerId, DateOnly startDate, DateOnly endDate)
  {
    if (endDate < startDate || endDate.DayNumber - startDate.DayNumber >= 31) throw new ArgumentOutOfRangeException(nameof(endDate));
    var buses = options.Buses.Where(bus => bus.PartnerId == partnerId).ToArray();
    var terminals = options.Terminals.Where(terminal => terminal.PartnerId == partnerId).ToArray();
    if (buses.Length == 0 || terminals.Length < 2) throw new ArgumentException("At least one active bus and two active terminals are required.");
    var terminalLabels = UniqueLabels(terminals.Select(TerminalLabel));
    var busLabels = UniqueLabels(buses.Select(BusLabel));
    var routes = options.Routes.Where(route => route.PartnerId == partnerId).ToArray();
    var labelsByTerminal = terminals.Select((terminal, index) => (terminal.Id, Label: terminalLabels[index])).ToDictionary(terminal => terminal.Id, terminal => terminal.Label);
    using var stream = new MemoryStream();
    using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
    {
      var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook(new Sheets());
      var styles = workbook.AddNewPart<WorkbookStylesPart>();
      styles.Stylesheet = new Stylesheet(
        new Fonts(new Font { FontSize = new FontSize { Val = 11 }, FontName = new FontName { Val = "Calibri" } },
          new Font { Bold = new Bold(), Color = new Color { Rgb = "FFFFFFFF" }, FontSize = new FontSize { Val = 11 }, FontName = new FontName { Val = "Calibri" } }) { Count = 2 },
        new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
          new Fill(new PatternFill(new ForegroundColor { Rgb = "FF16459B" }, new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid })) { Count = 3 },
        new Borders(new Border(), new Border(new LeftBorder { Style = BorderStyleValues.Thin }, new RightBorder { Style = BorderStyleValues.Thin },
          new TopBorder { Style = BorderStyleValues.Thin }, new BottomBorder { Style = BorderStyleValues.Thin }, new DiagonalBorder())) { Count = 2 },
        new CellStyleFormats(new CellFormat()) { Count = 1 },
        new CellFormats(new CellFormat(),
          new CellFormat { FontId = 0, BorderId = 1, ApplyBorder = true, Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center } },
          new CellFormat { FontId = 1, FillId = 2, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true, Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center } },
          new CellFormat { BorderId = 1, ApplyBorder = true, NumberFormatId = 18, ApplyNumberFormat = true },
          new CellFormat { BorderId = 1, ApplyBorder = true, NumberFormatId = 2, ApplyNumberFormat = true }) { Count = 5 },
        new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });

      WorksheetPart AddSheet(string name, double[] widths, IReadOnlyList<string[]> values, int rowCount, bool trip = false, bool hidden = false)
      {
        var part = workbook.AddNewPart<WorksheetPart>(); var data = new SheetData();
        part.Worksheet = new Worksheet(new SheetViews(new SheetView(new Pane { State = PaneStateValues.Frozen, VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft }) { WorkbookViewId = 0 }),
          new Columns(widths.Select((width, index) => new Column { Min = (uint)index + 1, Max = (uint)index + 1, Width = width, CustomWidth = true })), data);
        for (var rowNumber = 1; rowNumber <= rowCount; rowNumber++)
        {
          var row = new Row { RowIndex = (uint)rowNumber, Height = rowNumber == 1 ? 24 : 20, CustomHeight = true };
          for (var column = 0; column < widths.Length; column++) {
            var cell = new Cell { CellReference = $"{(char)('A' + column)}{rowNumber}", StyleIndex = rowNumber == 1 ? 2U : trip && column == 3 ? 3U : trip && column == 5 ? 4U : 1U };
            if (trip && column == 4 && rowNumber > 1) {
              cell.DataType = CellValues.String; cell.CellFormula = new CellFormula(DurationFormula(rowNumber)); cell.CellValue = new CellValue("");
            } else {
              cell.DataType = CellValues.InlineString;
              cell.InlineString = new InlineString(new Text(rowNumber <= values.Count && column < values[rowNumber - 1].Length ? values[rowNumber - 1][column] : ""));
            }
            row.Append(cell);
          }
          data.Append(row);
        }
        workbook.Workbook.Sheets!.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = (uint)workbook.Workbook.Sheets.ChildElements.Count + 1,
          Name = name, State = hidden ? SheetStateValues.Hidden : SheetStateValues.Visible });
        return part;
      }
      for (var number = startDate.DayNumber; number <= endDate.DayNumber; number++)
      {
        var part = AddSheet(DateOnly.FromDayNumber(number).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), [65, 65, 55, 26, 24, 18], [Headers], 501, trip: true);
        part.Worksheet!.Append(new AutoFilter { Reference = "A1:F501" });
        var validations = new DataValidations();
        foreach (var (range, formula) in new[] { ("A2:B501", "TerminalChoices"), ("C2:C501", "BusChoices"), ("D2:D501", "DepartureTimes") })
          validations.Append(new DataValidation { Type = DataValidationValues.List, AllowBlank = true, ShowErrorMessage = true, ErrorStyle = DataValidationErrorStyleValues.Stop,
            ErrorTitle = "Choose from the list", Error = formula == "DepartureTimes" ? "Choose an hourly departure time from the list, for example 7:00 AM or 7:00 PM."
              : "Choose an active record from this partner's list. Download a new template to refresh the choices.",
            SequenceOfReferences = new ListValue<StringValue> { InnerText = range }, Formula1 = new Formula1(formula) });
        foreach (var (range, type, min, max) in new[] { ("E2:E501", DataValidationValues.Whole, "1", "2880"), ("F2:F501", DataValidationValues.Decimal, "0.01", "1000000") })
          validations.Append(new DataValidation { Type = type, Operator = DataValidationOperatorValues.Between, AllowBlank = true, ShowErrorMessage = true,
            ErrorStyle = DataValidationErrorStyleValues.Stop, SequenceOfReferences = new ListValue<StringValue> { InnerText = range }, Formula1 = new Formula1(min), Formula2 = new Formula2(max) });
        validations.Count = (uint)validations.ChildElements.Count; part.Worksheet.Append(validations);
      }
      List<string[]> instructions = [ ["Item", "Instructions"],
        ["Partner", options.Workspaces.Single(workspace => workspace.Id == partnerId).Name],
        ["One date per sheet", "Each sheet name is its departure date in YYYY-MM-DD format. Use one sheet per date; duplicate a date sheet and rename it to add another date. Maximum 31 date sheets."],
        ["Dates and time", "Past Manila dates are rejected. On today's sheet, departure times must still be in the future. Choose an hourly departure from the dropdown, from 12:00 AM through 11:00 PM, in Manila time."],
        ["Origin and destination", "Choose two different terminals from the dropdown lists. These are active terminals from the selected partner's database records."],
        ["Bus", "Choose a bus by its fleet number and plate number. Available seats come from its current seat capacity."],
        ["Dropdown choices", "Choose the full label from the dropdown. Matching names are distinguished by a number in brackets. Keep the hidden lookup sheet unchanged so the selected records can be identified during upload."],
        ["Travel time", "Selecting origin and destination automatically fills the configured route's travel time. If it stays blank, configure that direction in Travel time setup and download a new template. Travel times are checked against the database during upload."],
        ["Fare", "Enter a fare from PHP 0.01 to PHP 1,000,000, with at most two decimal places."],
        ["Limits", "Maximum 500 trips across all date sheets; maximum 5 MB; standard .xlsx only. Keep headers unchanged. Keep the supplied travel-time formulas or enter minutes manually. Formulas in other columns are not supported."],
        ["Validation", "Every row is validated before any trips are saved. Overlapping schedules for the same bus, including across sheets and existing trips, are rejected."],
        ["Refresh choices", "Download again to refresh active buses and terminals. Records, routes and dates are checked again against the database during upload."],
        ["Existing routes", "Origin -> Destination: travel time (minutes)"] ];
      foreach (var route in routes) instructions.Add([
        $"{terminals.Single(terminal => terminal.Id == route.OriginTerminalId).Name} -> {terminals.Single(terminal => terminal.Id == route.DestinationTerminalId).Name}", route.DurationMinutes.ToString(CultureInfo.InvariantCulture)]);
      AddSheet("Instructions", [40, 130], instructions, instructions.Count);
      var lists = new List<string[]> { new[] { "Terminals", "Buses", "Departure times", "Terminal ID", "Bus ID", "Route", "Duration (minutes)" } };
      for (var i = 0; i < Math.Max(routes.Length, Math.Max(24, Math.Max(terminals.Length, buses.Length))); i++) lists.Add([
        i < terminals.Length ? terminalLabels[i] : "", i < buses.Length ? busLabels[i] : "",
        i < 24 ? new TimeOnly(i, 0).ToString("h:mm tt", CultureInfo.InvariantCulture) : "",
        i < terminals.Length ? terminals[i].Id.ToString("D") : "", i < buses.Length ? buses[i].Id.ToString("D") : "",
        i < routes.Length ? labelsByTerminal[routes[i].OriginTerminalId] + "\t" + labelsByTerminal[routes[i].DestinationTerminalId] : "",
        i < routes.Length ? routes[i].DurationMinutes.ToString(CultureInfo.InvariantCulture) : ""]);
      var lookup = AddSheet("_Lists", [65, 55, 20, 40, 40, 100, 24], lists, lists.Count, hidden: true);
      foreach (var row in lookup.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).Take(routes.Length)) {
        var cell = row.Elements<Cell>().ElementAt(6); var minutes = cell.InnerText;
        cell.InlineString = null; cell.DataType = CellValues.Number; cell.CellValue = new CellValue(minutes);
      }
      workbook.Workbook.DefinedNames = new DefinedNames(new DefinedName { Name = "TerminalChoices", Text = $"'_Lists'!$A$2:$A${terminals.Length + 1}" },
        new DefinedName { Name = "BusChoices", Text = $"'_Lists'!$B$2:$B${buses.Length + 1}" },
        new DefinedName { Name = "DepartureTimes", Text = "'_Lists'!$C$2:$C$25" },
        new DefinedName { Name = "RouteTravelTimes", Text = $"'_Lists'!$F$2:$G${Math.Max(2, routes.Length + 1)}" });
      workbook.Workbook.CalculationProperties = new CalculationProperties { CalculationMode = CalculateModeValues.Auto, FullCalculationOnLoad = true, ForceFullCalculation = true };
      workbook.Workbook.Save();
    }
    return stream.ToArray();
  }
}
