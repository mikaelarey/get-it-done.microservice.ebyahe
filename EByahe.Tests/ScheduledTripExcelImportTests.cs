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
        workbook.Workbook.Sheets!.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = ++id, Name = name });
      }
      workbook.Workbook.Save();
    }
    return stream.ToArray();
  }
  private static Task<ScheduledTripExcelImport.Parsed> Parse(byte[] bytes, DateTimeOffset? now = null, string name = "trips.xlsx") => ScheduledTripExcelImport.ReadAsync(
    new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name), Guid.NewGuid(), now ?? Now, default);

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
      Assert.Contains("ABC-1234", lists.Worksheet!.InnerText); Assert.DoesNotContain("Other partner bus", lists.Worksheet.InnerText);
      var departureChoices = lists.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).Take(24).Select(row => row.Elements<Cell>().ElementAt(2).InnerText).ToArray();
      Assert.Equal(Enumerable.Range(0, 24).Select(hour => new TimeOnly(hour, 0).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)), departureChoices);
      Assert.Equal("12:00 AM", departureChoices[0]); Assert.Equal("12:00 PM", departureChoices[12]); Assert.Equal("11:00 PM", departureChoices[23]);
      Assert.Equal("'_Lists'!$C$2:$C$25", workbook.Workbook.DefinedNames!.Elements<DefinedName>().Single(name => name.Name == "DepartureTimes").Text);
      foreach (var sheet in sheets.Take(2))
      {
        var part = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!); var rows = part.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().ToArray();
        Assert.Equal(501, rows.Length); Assert.Equal(24, rows[0].Height!.Value); Assert.All(rows.Skip(1), row => Assert.Equal(20, row.Height!.Value));
        Assert.Equal(5, part.Worksheet.GetFirstChild<DataValidations>()!.Elements<DataValidation>().Count());
        var departureValidation = part.Worksheet.GetFirstChild<DataValidations>()!.Elements<DataValidation>().Single(rule => rule.SequenceOfReferences!.InnerText == "D2:D501");
        Assert.Equal(DataValidationValues.List, departureValidation.Type!.Value); Assert.Equal("DepartureTimes", departureValidation.Formula1!.Text);
        Assert.All(rows.SelectMany(row => row.Elements<Cell>()), cell => Assert.Equal(1U, workbook.WorkbookStylesPart!.Stylesheet!.CellFormats!.Elements<CellFormat>().ElementAt((int)cell.StyleIndex!.Value).BorderId!.Value));
        string[] values = [ScheduledTripExcelTemplate.TerminalLabel(terminals[0]), ScheduledTripExcelTemplate.TerminalLabel(terminals[1]), ScheduledTripExcelTemplate.BusLabel(bus), "7:00 AM", "120", "550.25"];
        var index = 0; foreach (var cell in rows[1].Elements<Cell>()) cell.InlineString = new InlineString(new Text(values[index++])); part.Worksheet.Save();
      }
    }
    var parsed = await Parse(stream.ToArray()); Assert.Empty(parsed.Errors); Assert.Equal(2, parsed.Rows.Count);
    Assert.Equal(DateTimeOffset.Parse("2035-06-01T23:00:00Z"), parsed.Rows[0].Trip.DepartureAtUtc);
    Assert.Equal(bus.Id, parsed.Rows[0].Trip.BusId); Assert.Equal(550.25m, parsed.Rows[0].Trip.BaseFare);
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
      using (var document = SpreadsheetDocument.Open(stream, true)) { var part = document.WorkbookPart!.WorksheetParts.Single(); var cell = part.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "D2"); cell.InlineString = null; cell.DataType = CellValues.Number; cell.CellValue = new CellValue(number); part.Worksheet.Save(); }
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
    using (var document = SpreadsheetDocument.Open(stream, true)) { var part = document.WorkbookPart!.WorksheetParts.Single(); part.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "D2").CellFormula = new CellFormula("0.5"); part.Worksheet.Save(); }
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
