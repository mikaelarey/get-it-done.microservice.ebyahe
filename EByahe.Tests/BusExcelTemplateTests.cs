using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using EByahe.Application.Services;
using EByahe.Domain.DataModels;
using Microsoft.AspNetCore.Http;

namespace EByahe.Tests;

public sealed class BusExcelTemplateTests
{
  internal static string[] Classes(byte[] bytes)
  {
    using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
    var workbook = document.WorkbookPart!;
    var sheet = workbook.Workbook!.Sheets!.Elements<Sheet>().Single(sheet => sheet.Name?.Value == "Instructions");
    var part = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!);
    return part.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Where(row => row.RowIndex?.Value >= 22)
      .Select(row => row.Elements<Cell>().Single().InnerText).ToArray();
  }

  internal static byte[] Fill(byte[] bytes, string busClass)
  {
    using var stream = new MemoryStream(); stream.Write(bytes); stream.Position = 0;
    using (var document = SpreadsheetDocument.Open(stream, true))
    {
      var workbook = document.WorkbookPart!;
      var sheet = workbook.Workbook!.Sheets!.Elements<Sheet>().Single(sheet => sheet.Name?.Value == "Buses");
      var worksheet = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!);
      var row = worksheet.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Single(row => row.RowIndex?.Value == 2);
      row.RemoveAllChildren<Cell>();
      var values = new[] { "BUS-NEW", "NEW-1010", busClass, "30" };
      for (var index = 0; index < values.Length; index++) row.Append(new Cell { CellReference = $"{(char)('A' + index)}2",
        DataType = CellValues.InlineString, InlineString = new InlineString(new Text(values[index])) });
    }
    return stream.ToArray();
  }

  [Fact]
  public async Task TemplateHasCurrentClassesNativeValidationAndNoSampleBuses()
  {
    var bytes = BusExcelTemplate.Generate([new(2, "Deluxe"), new(11, "Sleeper")]);
    Assert.Equal(new[] { "Deluxe", "Sleeper" }, Classes(bytes));
    using (var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false))
    {
      Assert.Empty(new OpenXmlValidator().Validate(document));
      var workbook = document.WorkbookPart!;
      var name = Assert.Single(workbook.Workbook!.DefinedNames!.Elements<DefinedName>(), name => name.Name?.Value == "BusClassOptions");
      Assert.Equal("'Instructions'!$A$22:$A$23", name.Text);
      var buses = workbook.Workbook.Sheets!.Elements<Sheet>().Single(sheet => sheet.Name?.Value == "Buses");
      var worksheet = ((WorksheetPart)workbook.GetPartById(buses.Id!.Value!)).Worksheet;
      var validation = Assert.Single(worksheet!.GetFirstChild<DataValidations>()!.Elements<DataValidation>(), rule => rule.Type?.Value == DataValidationValues.List);
      Assert.Equal("BusClassOptions", validation.Formula1!.Text); Assert.True(validation.ShowErrorMessage!.Value);
      Assert.Contains(worksheet.GetFirstChild<DataValidations>()!.Elements<DataValidation>(), rule => rule.Type?.Value == DataValidationValues.Whole
        && rule.Formula1!.Text == "1" && rule.Formula2!.Text == "100");
    }
    var empty = await BusExcelImport.ReadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "buses.xlsx"), Guid.NewGuid(), default);
    Assert.Empty(empty.Rows); Assert.Contains(empty.Errors, error => error.Message.Contains("at least one bus"));
    var filled = Fill(bytes, "Sleeper");
    var parsed = await BusExcelImport.ReadAsync(new FormFile(new MemoryStream(filled), 0, filled.Length, "file", "buses.xlsx"), Guid.NewGuid(), default);
    Assert.Empty(parsed.Errors); Assert.Equal("Sleeper", Assert.Single(parsed.Rows).Bus.BusClass);
  }

  [Fact]
  public void GeneratesLongClassListsWithOneCompleteRangeAndRemovesOldOptions()
  {
    var options = Enumerable.Range(1, 300).Select(index => new BusClassOption(index, $"Class {index}")).ToArray();
    var bytes = BusExcelTemplate.Generate(options);
    Assert.Equal(options.Select(option => option.Decode), Classes(bytes));
    using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
    Assert.Empty(new OpenXmlValidator().Validate(document));
    Assert.Equal("'Instructions'!$A$22:$A$321", document.WorkbookPart!.Workbook!.DefinedNames!.Elements<DefinedName>().Single().Text);
    Assert.Equal(new[] { "Only current class" }, Classes(BusExcelTemplate.Generate([new(99, "Only current class")])));
    Assert.Throws<ArgumentException>(() => BusExcelTemplate.Generate([]));
  }
}
