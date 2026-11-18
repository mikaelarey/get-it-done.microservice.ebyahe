using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EByahe.Application.Services;
using Microsoft.AspNetCore.Http;

namespace EByahe.Tests;

public sealed class BusExcelImportTests
{
  internal static byte[] Workbook(string[][] rows, string sheetName = "Buses", bool formula = false, bool sharedStrings = false)
  {
    using var stream = new MemoryStream();
    using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
    {
      var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook();
      var sheet = workbook.AddNewPart<WorksheetPart>(); var data = new SheetData(); sheet.Worksheet = new Worksheet(data);
      workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(sheet), SheetId = 1, Name = sheetName }));
      var strings = sharedStrings ? workbook.AddNewPart<SharedStringTablePart>() : null;
      if (strings is not null) strings.SharedStringTable = new SharedStringTable();
      for (var index = 0; index < rows.Length; index++)
      {
        var row = new Row { RowIndex = (uint)index + 1 }; data.Append(row);
        for (var column = 0; column < rows[index].Length; column++)
        {
          var value = rows[index][column];
          var cell = new Cell { CellReference = $"{(char)('A' + column)}{index + 1}" };
          if (sharedStrings)
          {
            var table = strings!.SharedStringTable!;
            cell.DataType = CellValues.SharedString; cell.CellValue = new CellValue(table.ChildElements.Count.ToString());
            table.Append(new SharedStringItem(new Text(value)));
          }
          else if (column == 3 && index != 0) { cell.DataType = CellValues.Number; cell.CellValue = new CellValue(value); }
          else { cell.DataType = CellValues.InlineString; cell.InlineString = new InlineString(new Text(value)); }
          if (formula && index == 1 && column == 3) cell.CellFormula = new CellFormula("20+20");
          row.Append(cell);
        }
      }
    }
    return stream.ToArray();
  }
  internal static readonly string[] Headers = ["Fleet number", "Plate number", "Bus class", "Seat capacity"];
  private static Task<BusExcelImport.Parsed> Parse(byte[] bytes, string name = "buses.xlsx") =>
    BusExcelImport.ReadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name), Guid.NewGuid(), default);

  [Theory]
  [InlineData(false)] [InlineData(true)]
  public async Task ReadsInlineAndSharedStringsPreservesIdentifiersAndNormalizesValues(bool sharedStrings)
  {
    var parsed = await Parse(Workbook([Headers, [" bus-001 ", "abc-1234", " Deluxe ", "40.0"], ["", "", "", ""], ["00002", "XYZ 0002", "Luxury", "50"]], sharedStrings: sharedStrings));
    Assert.Empty(parsed.Errors); Assert.Equal(2, parsed.Rows.Count);
    Assert.Equal(2, parsed.Rows[0].Row); Assert.Equal(4, parsed.Rows[1].Row);
    Assert.Equal("BUS-001", parsed.Rows[0].Bus.FleetNumber); Assert.Equal("ABC-1234", parsed.Rows[0].Bus.PlateNumber);
    Assert.Equal("Deluxe", parsed.Rows[0].Bus.BusClass); Assert.Equal(40, parsed.Rows[0].Bus.SeatCapacity);
    Assert.Equal("00002", parsed.Rows[1].Bus.FleetNumber);
  }
  [Theory]
  [InlineData("0")] [InlineData("101")] [InlineData("2.5")] [InlineData("NaN")] [InlineData("")]
  public async Task RejectsInvalidCapacitiesWithTheExcelRowNumber(string capacity)
  {
    var parsed = await Parse(Workbook([Headers, ["BUS-001", "ABC-1234", "Deluxe", capacity]]));
    Assert.Contains(parsed.Errors, error => error.Row == 2 && error.Field == "Seat capacity");
  }
  [Fact]
  public async Task RejectsDuplicateValuesFormulasControlsAndMissingRequiredCells()
  {
    var parsed = await Parse(Workbook([Headers, ["BUS-001", "abc-1234", "Deluxe", "40"], ["bus-001", "ABC-1234", "Luxury", "50"],
      ["BUS-003\n", "bad/plate", "", "40"]], formula: true));
    Assert.Contains(parsed.Errors, error => error.Row == 2 && error.Message.Contains("formula"));
    Assert.Contains(parsed.Errors, error => error.Row == 3 && error.Field == "Fleet number" && error.Message.Contains("row 2"));
    Assert.Contains(parsed.Errors, error => error.Row == 3 && error.Field == "Plate number");
    Assert.Contains(parsed.Errors, error => error.Row == 4 && error.Field == "Bus class");
    Assert.Contains(parsed.Errors, error => error.Row == 4 && error.Field == "Plate number");
  }
  [Fact]
  public async Task RejectsEmptyMalformedWrongSheetHeadersAndUnsupportedFiles()
  {
    foreach (var bytes in new[] { Workbook([Headers]), Workbook([Headers], "Sheet1"), Workbook([["Fleet", "Plate", "Class", "Seats"]]), new byte[] { 1, 2, 3 } })
      Assert.NotEmpty((await Parse(bytes)).Errors);
    Assert.NotEmpty((await Parse(Workbook([Headers]), "buses.xls")).Errors);
    Assert.NotEmpty((await Parse(Workbook([Headers, ["BUS-1", "ABC-1234", "Deluxe", "40", "extra"]]))).Errors);
    var tooLarge = new FormFile(new MemoryStream(), 0, BusExcelImport.MaximumBytes + 1, "file", "buses.xlsx");
    Assert.NotEmpty((await BusExcelImport.ReadAsync(tooLarge, Guid.NewGuid(), default)).Errors);
  }
  [Fact]
  public async Task AcceptsFiveHundredRowsAndRejectsTheNextRow()
  {
    var rows = Enumerable.Range(1, 500).Select(i => new[] { $"BUS-{i}", $"ABC-{i}", "Deluxe", "40" }).Prepend(Headers).ToArray();
    var parsed = await Parse(Workbook(rows)); Assert.Empty(parsed.Errors); Assert.Equal(500, parsed.Rows.Count);
    Assert.NotEmpty((await Parse(Workbook([.. rows, ["BUS-501", "ABC-501", "Deluxe", "40"]]))).Errors);
  }
}
