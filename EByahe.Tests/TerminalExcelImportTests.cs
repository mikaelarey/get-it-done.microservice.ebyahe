using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EByahe.Application.Services;
using Microsoft.AspNetCore.Http;

namespace EByahe.Tests;

public sealed class TerminalExcelImportTests
{
  internal static readonly string[] Headers = ["Terminal name", "Region", "Province", "City / Municipality", "Barangay", "Street / Building address", "Location"];
  internal static byte[] Workbook(string[][] rows, bool shared = false, bool formula = false, string sheetName = "Terminals")
  {
    using var stream = new MemoryStream();
    using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
    {
      var book = document.AddWorkbookPart(); book.Workbook = new Workbook();
      var part = book.AddNewPart<WorksheetPart>(); var data = new SheetData(); part.Worksheet = new Worksheet(data);
      book.Workbook.Append(new Sheets(new Sheet { Id = book.GetIdOfPart(part), SheetId = 1, Name = sheetName }));
      var strings = shared ? book.AddNewPart<SharedStringTablePart>() : null;
      if (strings is not null) strings.SharedStringTable = new SharedStringTable();
      for (var index = 0; index < rows.Length; index++)
      {
        var row = new Row { RowIndex = (uint)index + 1 }; data.Append(row);
        for (var column = 0; column < rows[index].Length; column++)
        {
          var cell = new Cell { CellReference = $"{(char)('A' + column)}{index + 1}" };
          if (shared)
          {
            cell.DataType = CellValues.SharedString; cell.CellValue = new CellValue(strings!.SharedStringTable!.ChildElements.Count.ToString());
            strings.SharedStringTable.Append(new SharedStringItem(new Text(rows[index][column])));
          }
          else { cell.DataType = CellValues.InlineString; cell.InlineString = new InlineString(new Text(rows[index][column])); }
          if (formula && index == 1 && column == 0) cell.CellFormula = new CellFormula("1+1");
          if (index > 0 && column == 6) cell.CellFormula = new CellFormula("\"Untrusted location\"");
          row.Append(cell);
        }
      }
    }
    return stream.ToArray();
  }
  internal static string[] Valid(string name = "Main terminal") => [name, "3 - Region I", "2 - Ilocos Norte", "1 - Adams", "1 - Adams (Pob.)", "100 Main street", ""];
  private static Task<TerminalExcelImport.Parsed> Parse(byte[] bytes, string name = "terminals.xlsx") =>
    TerminalExcelImport.ReadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name), Guid.NewGuid(), default);

  [Theory]
  [InlineData(false)] [InlineData(true)]
  public async Task ReadsDropdownCodesAndStreetAddressAndIgnoresLocationPreviewAndBlankRows(bool shared)
  {
    var row = Valid(" Main terminal "); row[5] = " First street\nBuilding 2 ";
    var parsed = await Parse(Workbook([Headers, row, ["", "", "", "", "", "", ""]], shared));
    Assert.Empty(parsed.Errors); var result = Assert.Single(parsed.Rows);
    Assert.Equal(2, result.Row); Assert.Equal("Main terminal", result.Terminal.Name); Assert.Equal("First street\nBuilding 2", result.Terminal.Address);
    Assert.Equal(3, result.Terminal.CodeDecodeRegion); Assert.Equal(2, result.Terminal.CodeDecodeProvince);
    Assert.Equal(1, result.Terminal.CodeDecodeMunicipality); Assert.Equal(1, result.Terminal.CodeDecodeBarangay);
  }
  [Fact]
  public async Task RejectsInvalidSelectionsDuplicatesFormulasAndOversizedFieldsWithRowNumbers()
  {
    var bad = Valid(new string('A', 255)); bad[1] = "NCR"; bad[4] = "0"; bad[5] = new string('A', 501);
    var parsed = await Parse(Workbook([Headers, Valid(), Valid("MAIN TERMINAL"), bad], formula: true));
    Assert.Contains(parsed.Errors, error => error.Row == 2 && error.Message.Contains("formula"));
    Assert.Contains(parsed.Errors, error => error.Row == 3 && error.Field == "Terminal name" && error.Message.Contains("row 2"));
    Assert.Contains(parsed.Errors, error => error.Row == 4 && error.Field == "Region");
    Assert.Contains(parsed.Errors, error => error.Row == 4 && error.Field == "Barangay");
    Assert.Contains(parsed.Errors, error => error.Row == 4 && error.Field == "Street / Building address");
  }
  [Fact]
  public async Task RejectsMalformedEmptyWrongSheetHeadersAndUnsupportedFiles()
  {
    foreach (var bytes in new[] { new byte[] { 1, 2, 3 }, Workbook([Headers]), Workbook([Headers, Valid()], sheetName: "Buses"),
      Workbook([["Incorrect headers"], Valid()]), Workbook([Headers, [.. Valid(), "extra"]]) })
      Assert.NotEmpty((await Parse(bytes)).Errors);
    Assert.NotEmpty((await Parse(Workbook([Headers, Valid()]), "terminals.xls")).Errors);
    var tooLarge = new FormFile(new MemoryStream(), 0, TerminalExcelImport.MaximumBytes + 1, "file", "terminals.xlsx");
    Assert.NotEmpty((await TerminalExcelImport.ReadAsync(tooLarge, Guid.NewGuid(), default)).Errors);
  }
  [Fact]
  public async Task Accepts500RowsAndRejects501Rows()
  {
    var rows = Enumerable.Range(1, 500).Select(index => Valid($"Terminal {index}")).Prepend(Headers).ToArray();
    var result = await Parse(Workbook(rows)); Assert.Empty(result.Errors); Assert.Equal(500, result.Rows.Count);
    Assert.NotEmpty((await Parse(Workbook([.. rows, Valid("Terminal 501")]))).Errors);
  }
}
