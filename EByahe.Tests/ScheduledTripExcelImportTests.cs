using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using EByahe.Application.Services;
using EByahe.Domain.DataModels;
using Microsoft.AspNetCore.Http;

namespace EByahe.Tests;

public sealed class ScheduledTripExcelImportTests
{
  private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2035-06-01T15:00:00Z"); // 23:00 in Manila
  internal static string[] Values(Guid bus, Guid origin, Guid destination, string time = "07:30", string duration = "120", string fare = "550.25") =>
    [origin.ToString(), destination.ToString(), bus.ToString(), time, duration, fare];
  internal static byte[] Workbook(params (string Name, string[][] Rows)[] sheets)
  {
    using var stream = new MemoryStream();
    using (var document = SpreadsheetDocument.Create(stream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
    {
      var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook(new Sheets()); uint id = 0;
      foreach (var (name, rows) in sheets)
      {
        var part = workbook.AddNewPart<WorksheetPart>(); var data = new SheetData(); part.Worksheet = new Worksheet(data);
        var all = new[] { ScheduledTripExcelTemplate.Headers }.Concat(rows).ToArray();
        for (var i = 0; i < all.Length; i++)
        {
          var row = new Row { RowIndex = (uint)i + 1 }; data.Append(row);
          for (var j = 0; j < all[i].Length; j++) row.Append(new Cell { CellReference = $"{(char)('A' + j)}{i + 1}", DataType = CellValues.InlineString, InlineString = new InlineString(new Text(all[i][j])) });
        }
        workbook.Workbook!.Sheets!.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = ++id, Name = name });
      }
      workbook.Workbook.Save();
    }
    return stream.ToArray();
  }
  private static Task<ScheduledTripExcelImport.Parsed> Parse(byte[] bytes, DateTimeOffset? now = null, string name = "trips.xlsx", PartnerTripOptions? options = null) => ScheduledTripExcelImport.ReadAsync(
    new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name), options?.Workspaces[0].Id ?? Guid.NewGuid(), now ?? Now, default, options);

  [Fact]
  public async Task GeneratedTemplateHasDatabaseDropdownsCompactBorderedDateSheetsAndParsesFilledRows()
  {
    var partner = Guid.NewGuid(); var bus = new PartnerTripBus(Guid.NewGuid(), partner, "BUS-001", "ABC-1234", "Deluxe", 40);
    PartnerTripTerminal[] terminals = [new(Guid.NewGuid(), partner, "First terminal", "Origin city"), new(Guid.NewGuid(), partner, "Last terminal", "Destination city")];
    var options = new PartnerTripOptions([new(partner, "Bus company")], [bus, new(Guid.NewGuid(), Guid.NewGuid(), "Other partner bus", "NO", "Deluxe", 30)], terminals, []);
    using var stream = new MemoryStream(); stream.Write(ScheduledTripExcelTemplate.Generate(options, partner, new(2035, 6, 2), new(2035, 6, 3))); stream.Position = 0;
    using (var document = SpreadsheetDocument.Open(stream, true))
    {
      var issues = new OpenXmlValidator().Validate(document).ToArray(); Assert.True(issues.Length == 0, string.Join(" | ", issues.Select(issue => issue.Description + " at " + issue.Path?.XPath)));
      var workbook = document.WorkbookPart!; var sheets = workbook.Workbook!.Sheets!.Elements<Sheet>().ToArray();
      Assert.Equal(new[] { "2035-06-02", "2035-06-03", "Instructions", "_Lists" }, sheets.Select(sheet => sheet.Name!.Value));
      var lists = (WorksheetPart)workbook.GetPartById(sheets[3].Id!.Value!); Assert.Equal(SheetStateValues.Hidden, sheets[3].State!.Value);
      Assert.Contains("ABC-1234", lists.Worksheet!.InnerText); Assert.DoesNotContain("Other partner bus", lists.Worksheet!.InnerText);
      var lookupRows = lists.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).ToArray();
      var labels = lookupRows.SelectMany(row => row.Elements<Cell>().Take(2)).Select(cell => cell.InnerText).ToArray();
      Assert.Contains("First terminal (Origin city)", labels); Assert.Contains("BUS-001 - ABC-1234", labels);
      foreach (var id in terminals.Select(terminal => terminal.Id).Append(bus.Id))
      {
        Assert.DoesNotContain(labels, label => label.Contains(id.ToString()));
        Assert.All(sheets.Take(3), sheet => Assert.DoesNotContain(id.ToString(), ((WorksheetPart)workbook.GetPartById(sheet.Id!.Value!)).Worksheet!.InnerText));
      }
      var departureChoices = lists.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).Take(24).Select(row => row.Elements<Cell>().ElementAt(2).InnerText).ToArray();
      Assert.Equal(Enumerable.Range(0, 24).Select(hour => new TimeOnly(hour, 0).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)), departureChoices);
      Assert.Equal("12:00 AM", departureChoices[0]); Assert.Equal("12:00 PM", departureChoices[12]); Assert.Equal("11:00 PM", departureChoices[23]);
      Assert.Equal("'_Lists'!$C$2:$C$25", workbook.Workbook.DefinedNames!.Elements<DefinedName>().Single(name => name.Name == "DepartureTimes").Text);
      foreach (var sheet in sheets.Take(2))
      {
        var part = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!); var rows = part.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().ToArray();
        Assert.Equal(501, rows.Length); Assert.Equal(24, rows[0].Height!.Value); Assert.All(rows.Skip(1), row => Assert.Equal(20, row.Height!.Value));
        Assert.Equal(5, part.Worksheet!.GetFirstChild<DataValidations>()!.Elements<DataValidation>().Count());
        var departureValidation = part.Worksheet!.GetFirstChild<DataValidations>()!.Elements<DataValidation>().Single(rule => rule.SequenceOfReferences!.InnerText == "D2:D501");
        Assert.Equal(DataValidationValues.List, departureValidation.Type!.Value); Assert.Equal("DepartureTimes", departureValidation.Formula1!.Text);
        Assert.All(rows.SelectMany(row => row.Elements<Cell>()), cell => Assert.Equal(1U, workbook.WorkbookStylesPart!.Stylesheet!.CellFormats!.Elements<CellFormat>().ElementAt((int)cell.StyleIndex!.Value).BorderId!.Value));
        string[] values = [ScheduledTripExcelTemplate.TerminalLabel(terminals[0]), ScheduledTripExcelTemplate.TerminalLabel(terminals[1]), ScheduledTripExcelTemplate.BusLabel(bus), "7:00 AM", "120", "550.25"];
        var index = 0; foreach (var cell in rows[1].Elements<Cell>()) SetValue(cell, values[index++]); part.Worksheet!.Save();
      }
    }
    var parsed = await Parse(stream.ToArray()); Assert.Empty(parsed.Errors); Assert.Equal(2, parsed.Rows.Count);
    Assert.Equal(DateTimeOffset.Parse("2035-06-01T23:00:00Z"), parsed.Rows[0].Trip.DepartureAtUtc);
    Assert.Equal(bus.Id, parsed.Rows[0].Trip.BusId); Assert.Equal(terminals[0].Id, parsed.Rows[0].Trip.OriginTerminalId);
    Assert.Equal(terminals[1].Id, parsed.Rows[0].Trip.DestinationTerminalId); Assert.Equal(550.25m, parsed.Rows[0].Trip.BaseFare);
  }

  [Fact]
  public async Task FriendlyChoicesDistinguishDuplicateNamesAndRejectUnknownOrAmbiguousLookups()
  {
    var partner = Guid.NewGuid();
    PartnerTripTerminal[] terminals = [new(Guid.NewGuid(), partner, "Terminal", "City"), new(Guid.NewGuid(), partner, "Terminal", "City")];
    var bus = new PartnerTripBus(Guid.NewGuid(), partner, "BUS-001", "ABC-1234", "Deluxe", 40);
    var bytes = ScheduledTripExcelTemplate.Generate(new([new(partner, "Company")], [bus], terminals, []), partner, new(2035, 6, 2), new(2035, 6, 2));
    async Task<ScheduledTripExcelImport.Parsed> Filled(bool unknown = false, bool ambiguous = false)
    {
      using var stream = new MemoryStream(); stream.Write(bytes); stream.Position = 0;
      using (var document = SpreadsheetDocument.Open(stream, true))
      {
        var workbook = document.WorkbookPart!; var sheets = workbook.Workbook!.Sheets!.Elements<Sheet>().ToArray();
        var lists = (WorksheetPart)workbook.GetPartById(sheets.Single(sheet => sheet.Name == "_Lists").Id!.Value!);
        var choices = lists.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).Take(2).ToArray();
        var origin = choices[0].Elements<Cell>().First().InnerText; var destination = choices[1].Elements<Cell>().First().InnerText;
        Assert.Equal("Terminal (City)", origin); Assert.Equal("Terminal (City) [2]", destination);
        var sheet = (WorksheetPart)workbook.GetPartById(sheets[0].Id!.Value!);
        string[] values = [origin, unknown ? "Not in dropdown" : destination, choices[0].Elements<Cell>().ElementAt(1).InnerText, "7:00 AM", "120", "550.25"];
        var index = 0;
        foreach (var cell in sheet.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().ElementAt(1).Elements<Cell>())
          SetValue(cell, values[index++]);
        if (ambiguous) { choices[1].Elements<Cell>().First().InlineString = new InlineString(new Text(origin)); lists.Worksheet!.Save(); }
        sheet.Worksheet!.Save();
      }
      return await Parse(stream.ToArray());
    }
    var parsed = await Filled(); Assert.Empty(parsed.Errors);
    var trip = Assert.Single(parsed.Rows).Trip;
    Assert.Equal(terminals[0].Id, trip.OriginTerminalId); Assert.Equal(terminals[1].Id, trip.DestinationTerminalId); Assert.Equal(bus.Id, trip.BusId);
    Assert.Contains((await Filled(unknown: true)).Errors, error => error.Field == "Destination terminal");
    Assert.NotEmpty((await Filled(ambiguous: true)).Errors);
    var legacy = Values(bus.Id, terminals[0].Id, terminals[1].Id); legacy[0] = "Terminal (City) | " + terminals[0].Id;
    Assert.Empty((await Parse(Workbook(("2035-06-02", [legacy])))).Errors);
  }

  internal static void SetValue(Cell cell, string value)
  {
    cell.CellFormula = null; cell.CellValue = null; cell.DataType = CellValues.InlineString; cell.InlineString = new InlineString(new Text(value));
  }

  [Fact]
  public async Task DurationFormulasUseCurrentDatabaseRoutesIgnoreCachedResultsAndSupportExcelSharedFormulas()
  {
    var partner = Guid.NewGuid(); var bus = new PartnerTripBus(Guid.NewGuid(), partner, "BUS-001", "ABC-1234", "Deluxe", 40);
    PartnerTripTerminal[] terminals = [new(Guid.NewGuid(), partner, "First", "City"), new(Guid.NewGuid(), partner, "Second", "City"), new(Guid.NewGuid(), partner, "Third", "City")];
    PartnerTripOptions options = new([new(partner, "Company")], [bus], terminals,
      [new(Guid.NewGuid(), partner, terminals[0].Id, terminals[1].Id, 60), new(Guid.NewGuid(), partner, terminals[1].Id, terminals[0].Id, 90)]);
    using var stream = new MemoryStream(); stream.Write(ScheduledTripExcelTemplate.Generate(options, partner, new(2035, 6, 2), new(2035, 6, 3))); stream.Position = 0;
    using (var document = SpreadsheetDocument.Open(stream, true))
    {
      var workbook = document.WorkbookPart!;
      var sheet = (WorksheetPart)workbook.GetPartById(workbook.Workbook!.Sheets!.Elements<Sheet>().First().Id!.Value!);
      var rows = sheet.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).Take(3).ToArray();
      for (var index = 0; index < 3; index++) {
        var cells = rows[index].Elements<Cell>().ToArray();
        SetValue(cells[0], ScheduledTripExcelTemplate.TerminalLabel(terminals[index == 1 ? 1 : 0]));
        SetValue(cells[1], ScheduledTripExcelTemplate.TerminalLabel(terminals[index == 1 ? 0 : index == 2 ? 2 : 1]));
        SetValue(cells[2], ScheduledTripExcelTemplate.BusLabel(bus)); SetValue(cells[3], "7:00 AM"); SetValue(cells[5], "100");
        cells[4].CellFormula = new CellFormula(index == 0 ? ScheduledTripExcelTemplate.DurationFormula(2) : "") { FormulaType = CellFormulaValues.Shared, SharedIndex = 0 };
        if (index == 0) cells[4].CellFormula!.Reference = "E2:E4";
        cells[4].CellValue = new CellValue("999");
      }
      sheet.Worksheet!.Save();
    }
    var parsed = await Parse(stream.ToArray(), options: options);
    Assert.Single(parsed.Errors, error => error.Row == 4 && error.Field == "Duration (minutes)");
    Assert.Equal(new[] { 60, 90 }, parsed.Rows.Take(2).Select(row => row.Trip.DurationMinutes));
    using (var document = SpreadsheetDocument.Open(stream, true)) {
      var workbook = document.WorkbookPart!; var sheet = (WorksheetPart)workbook.GetPartById(workbook.Workbook!.Sheets!.Elements<Sheet>().First().Id!.Value!);
      SetValue(sheet.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "E4"), "45"); sheet.Worksheet!.Save();
    }
    parsed = await Parse(stream.ToArray(), options: options); Assert.Empty(parsed.Errors); Assert.Equal(45, parsed.Rows[2].Trip.DurationMinutes);
    var refreshed = options with { Routes = [options.Routes[0] with { DurationMinutes = 75 }, options.Routes[1]] };
    Assert.Equal(75, (await Parse(stream.ToArray(), options: refreshed)).Rows[0].Trip.DurationMinutes);
    using (var document = SpreadsheetDocument.Open(stream, true)) {
      var workbook = document.WorkbookPart!; var sheet = (WorksheetPart)workbook.GetPartById(workbook.Workbook!.Sheets!.Elements<Sheet>().First().Id!.Value!);
      sheet.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "F2").CellFormula = new CellFormula("1+1"); sheet.Worksheet!.Save();
    }
    Assert.Contains((await Parse(stream.ToArray(), options: options)).Errors, error => error.Field == "Fare (PHP)");
  }

  [Fact]
  public async Task SharedDurationRangeMayStartBeforeItsMasterAfterEarlierRowsBecomeValues()
  {
    var partner = Guid.NewGuid(); var bus = new PartnerTripBus(Guid.NewGuid(), partner, "BUS-001", "ABC", "Deluxe", 40);
    PartnerTripTerminal[] terminals = [new(Guid.NewGuid(), partner, "Origin", "City"), new(Guid.NewGuid(), partner, "Destination", "City")];
    PartnerTripOptions options = new([new(partner, "Company")], [bus], terminals,
      [new(Guid.NewGuid(), partner, terminals[0].Id, terminals[1].Id, 60)]);
    using var stream = new MemoryStream(); stream.Write(ScheduledTripExcelTemplate.Generate(options, partner, new(2035, 6, 2), new(2035, 6, 2))); stream.Position = 0;
    using (var document = SpreadsheetDocument.Open(stream, true)) {
      var sheet = document.WorkbookPart!.WorksheetParts.First(part => part.Worksheet!.Descendants<Cell>().Any(cell => cell.CellReference == "E501"));
      foreach (var row in sheet.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).Take(64)) {
        var number = (int)row.RowIndex!.Value; var cells = row.Elements<Cell>().ToArray();
        if (number <= 6 || number == 8) {
          SetValue(cells[0], ScheduledTripExcelTemplate.TerminalLabel(terminals[0])); SetValue(cells[1], ScheduledTripExcelTemplate.TerminalLabel(terminals[1]));
          SetValue(cells[2], ScheduledTripExcelTemplate.BusLabel(bus)); SetValue(cells[3], "4:00 PM"); SetValue(cells[5], "600");
        }
        if (number <= 6) SetValue(cells[4], "60");
        else {
          cells[4].CellFormula = new CellFormula(number == 7 ? ScheduledTripExcelTemplate.DurationFormula(7) : "") { FormulaType = CellFormulaValues.Shared, SharedIndex = 0 };
          if (number == 7) cells[4].CellFormula!.Reference = "E2:E65";
          cells[4].CellValue = new CellValue(number == 8 ? "999" : "");
        }
      }
      sheet.Worksheet!.Save();
    }
    var parsed = await Parse(stream.ToArray(), options: options);
    Assert.Empty(parsed.Errors); Assert.Equal(new[] { 2, 3, 4, 5, 6, 8 }, parsed.Rows.Select(row => row.Row));
    Assert.All(parsed.Rows, row => Assert.Equal(60, row.Trip.DurationMinutes));
    // Range validation still forbids other columns, header cells and unbounded ranges.
    foreach (var invalid in new[] { "D2:E65", "E1:E65", "E2:E502", "E8:E65" }) {
      using (var document = SpreadsheetDocument.Open(stream, true)) {
        var sheet = document.WorkbookPart!.WorksheetParts.First(part => part.Worksheet!.Descendants<Cell>().Any(cell => cell.CellReference == "E501"));
        sheet.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "E7").CellFormula!.Reference = invalid; sheet.Worksheet!.Save();
      }
      Assert.Contains((await Parse(stream.ToArray(), options: options)).Errors, error => error.Row == 7 && error.Field == "Duration (minutes)");
    }
  }

  [Fact]
  public async Task ThirtyOneDateSheetsWithDurationFormulasStayWithinImportSizeLimits()
  {
    var partner = Guid.NewGuid();
    PartnerTripOptions options = new([new(partner, "Company")], [new(Guid.NewGuid(), partner, "BUS-001", "ABC", "Deluxe", 40)],
      [new(Guid.NewGuid(), partner, "First", "City"), new(Guid.NewGuid(), partner, "Last", "City")], []);
    var bytes = ScheduledTripExcelTemplate.Generate(options, partner, new(2035, 6, 2), new(2035, 7, 2));
    Assert.True(bytes.Length < ScheduledTripExcelImport.MaximumBytes);
    var parsed = await Parse(bytes, options: options);
    Assert.Equal("Add at least one trip, starting at row 2 on a date sheet.", Assert.Single(parsed.Errors).Message);
  }

  [Fact]
  public async Task RejectsPastManilaDatesAndElapsedTimesButAcceptsFutureTimesToday()
  {
    var bus = Guid.NewGuid(); var origin = Guid.NewGuid(); var destination = Guid.NewGuid();
    var parsed = await Parse(Workbook(("2035-05-31", [Values(bus, origin, destination)]), ("2035-06-01", [Values(bus, origin, destination, "22:59"), Values(bus, origin, destination, "23:30")])));
    Assert.Contains(parsed.Errors, error => error.Sheet == "2035-05-31" && error.Field == "Date");
    Assert.Contains(parsed.Errors, error => error.Sheet == "2035-06-01" && error.Row == 2 && error.Field == "Departure time (Manila)");
    Assert.DoesNotContain(parsed.Errors, error => error.Sheet == "2035-06-01" && error.Row == 3);
    // At 16:00 UTC the next calendar date has started in Manila.
    Assert.Contains((await Parse(Workbook(("2035-06-01", [Values(bus, origin, destination, "23:59")])), Now.AddHours(1))).Errors, error => error.Field == "Date");
  }

  [Fact]
  public async Task AcceptsExcelNumericTimeAndRejectsFullDateSerialOrInvalidTime()
  {
    var values = Values(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    async Task<ScheduledTripExcelImport.Parsed> Numeric(string number)
    {
      using var stream = new MemoryStream(); stream.Write(Workbook(("2035-06-02", [values]))); stream.Position = 0;
      using (var document = SpreadsheetDocument.Open(stream, true)) { var part = document.WorkbookPart!.WorksheetParts.Single(); var cell = part.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "D2"); cell.InlineString = null; cell.DataType = CellValues.Number; cell.CellValue = new CellValue(number); part.Worksheet!.Save(); }
      return await Parse(stream.ToArray());
    }
    var parsed = await Numeric("0.5"); Assert.Empty(parsed.Errors); Assert.Equal(4, Assert.Single(parsed.Rows).Trip.DepartureAtUtc.Hour);
    Assert.Contains((await Numeric("50000.5")).Errors, error => error.Field == "Departure time (Manila)");
    values[3] = "25:30"; Assert.Contains((await Parse(Workbook(("2035-06-02", [values])))).Errors, error => error.Field == "Departure time (Manila)");
    foreach (var (text, hour) in new[] { ("12:00 AM", 0), ("7:00 AM", 7), ("12:00 PM", 12), ("7:00 PM", 19), ("11:00 PM", 23) })
    {
      values[3] = text; var choice = await Parse(Workbook(("2035-06-02", [values])));
      Assert.Empty(choice.Errors); Assert.Equal(hour, Assert.Single(choice.Rows).Trip.DepartureAtUtc.ToOffset(TimeSpan.FromHours(8)).Hour);
    }
    values[3] = "13:00 PM"; Assert.Contains((await Parse(Workbook(("2035-06-02", [values])))).Errors, error => error.Field == "Departure time (Manila)");
  }

  [Fact]
  public async Task ReportsSheetAndRowForBadChoicesNumbersFormulasAndExtraColumns()
  {
    var same = Guid.NewGuid(); var values = Values(Guid.NewGuid(), same, same, duration: "1.5", fare: "1.234"); values[2] = "Unknown bus";
    var parsed = await Parse(Workbook(("2035-06-02", [values])));
    foreach (var field in new[] { "Bus", "Destination terminal", "Duration (minutes)", "Fare (PHP)" }) Assert.Contains(parsed.Errors, error => error.Sheet == "2035-06-02" && error.Row == 2 && error.Field == field);
    using var stream = new MemoryStream(); stream.Write(Workbook(("2035-06-02", [[..Values(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), "Extra"]]))); stream.Position = 0;
    using (var document = SpreadsheetDocument.Open(stream, true)) { var part = document.WorkbookPart!.WorksheetParts.Single(); part.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "D2").CellFormula = new CellFormula("0.5"); part.Worksheet!.Save(); }
    parsed = await Parse(stream.ToArray()); Assert.Contains(parsed.Errors, error => error.Message.Contains("formula")); Assert.Contains(parsed.Errors, error => error.Message.Contains("six template columns"));
  }

  [Fact]
  public async Task RejectsBadSheetNamesHeadersMissingRowsInvalidFilesAndExcessDates()
  {
    Assert.Contains((await Parse(Workbook(("June 2", [])))).Errors, error => error.Field == "Date");
    Assert.Contains((await Parse(Workbook(("2035-02-30", [])))).Errors, error => error.Field == "Date");
    Assert.NotEmpty((await Parse(Workbook(("2035-06-02", [])))).Errors);
    Assert.NotEmpty((await Parse([1, 2, 3])).Errors);
    Assert.NotEmpty((await Parse(Workbook(("2035-06-02", [])), name: "trips.xls")).Errors);
    Assert.NotEmpty((await Parse(Workbook(Enumerable.Range(0, 32).Select(day => (new DateOnly(2035, 6, 2).AddDays(day).ToString("yyyy-MM-dd"), Array.Empty<string[]>())).ToArray()))).Errors);
    Assert.NotEmpty((await Parse(BusExcelImportTests.Workbook([["Wrong header"]], "2035-06-02"))).Errors);
    Assert.NotEmpty((await ScheduledTripExcelImport.ReadAsync(new FormFile(new MemoryStream(), 0, ScheduledTripExcelImport.MaximumBytes + 1, "file", "trips.xlsx"), Guid.NewGuid(), Now, default)).Errors);
  }

  [Fact]
  public async Task EnforcesFiveHundredTripLimitAcrossAllSheets()
  {
    var values = Values(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); var rows = Enumerable.Range(0, 250).Select(_ => values).ToArray();
    Assert.Empty((await Parse(Workbook(("2035-06-02", rows), ("2035-06-03", rows)))).Errors);
    Assert.NotEmpty((await Parse(Workbook(("2035-06-02", rows), ("2035-06-03", [..rows, values])))).Errors);
  }
}
