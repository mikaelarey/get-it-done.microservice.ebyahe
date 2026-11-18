using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Services;

public static class BusExcelTemplate
{
  public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
  public const string FileName = "Bus-import-template.xlsx";

  public static byte[] Generate(IReadOnlyList<BusClassOption> classes)
  {
    if (classes.Count == 0) throw new ArgumentException("At least one bus class is required.", nameof(classes));
    using var layout = typeof(BusExcelTemplate).Assembly.GetManifestResourceStream("EByahe.Application.Assets.Templates.bus-import-template.xlsx")
      ?? throw new InvalidOperationException("The bus template layout is missing.");
    using var output = new MemoryStream(); layout.CopyTo(output); output.Position = 0;
    using (var document = SpreadsheetDocument.Open(output, true))
    {
      var workbook = document.WorkbookPart!;
      WorksheetPart Sheet(string name) => (WorksheetPart)workbook.GetPartById(
        workbook.Workbook!.Sheets!.Elements<Sheet>().Single(sheet => sheet.Name?.Value == name).Id!.Value!);
      var instructions = Sheet("Instructions");
      var data = instructions.Worksheet!.GetFirstChild<SheetData>()!;
      var sample = data.Elements<Row>().First(row => row.RowIndex?.Value == 22);
      var cellStyle = sample.Elements<Cell>().First().StyleIndex?.Value ?? 0;
      var rowHeight = sample.Height?.Value;
      foreach (var row in data.Elements<Row>().Where(row => row.RowIndex?.Value >= 22).ToArray()) row.Remove();
      for (var index = 0; index < classes.Count; index++)
      {
        var number = (uint)index + 22;
        var row = new Row { RowIndex = number };
        if (rowHeight is { } height) { row.Height = height; row.CustomHeight = true; }
        row.Append(new Cell { CellReference = $"A{number}", StyleIndex = cellStyle, DataType = CellValues.InlineString,
          InlineString = new InlineString(new Text(classes[index].Decode)) });
        data.Append(row);
      }
      if (instructions.Worksheet.SheetDimension is { } dimension) dimension.Reference = $"A1:D{Math.Max(31, classes.Count + 21)}";
      workbook.Workbook!.DefinedNames ??= new DefinedNames();
      foreach (var name in workbook.Workbook.DefinedNames.Elements<DefinedName>().Where(name => name.Name?.Value == "BusClassOptions").ToArray()) name.Remove();
      workbook.Workbook.DefinedNames.Append(new DefinedName { Name = "BusClassOptions", Text = $"'Instructions'!$A$22:$A${classes.Count + 21}" });
      var validation = Sheet("Buses").Worksheet!.GetFirstChild<DataValidations>()!.Elements<DataValidation>()
        .Single(rule => rule.Type?.Value == DataValidationValues.List);
      validation.Formula1 = new Formula1("BusClassOptions");
      validation.ShowErrorMessage = true; validation.ErrorStyle = DataValidationErrorStyleValues.Stop;
      validation.ErrorTitle = "Select a bus class";
      validation.Error = "Choose a class from the list. Download a new template to refresh the options.";
    }
    return output.ToArray();
  }
}
