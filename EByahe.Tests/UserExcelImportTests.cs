using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using EByahe.Application.Services;
using Microsoft.AspNetCore.Http;
namespace EByahe.Tests;
public sealed class UserExcelImportTests {
  internal static readonly string[] Headers = ["First name", "Last name", "Email address"];
  internal static byte[] Workbook(params string[][] rows) => BusExcelImportTests.Workbook([Headers, .. rows], "Users", sharedStrings: true);
  private static Task<UserExcelImport.Parsed> Parse(byte[] bytes, string category = "bus-operator", string name = "users.xlsx") =>
    UserExcelImport.ReadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name), category, default);
  [Fact]
  public async Task UserTemplateHasCompactBorderedCellsInstructionsAndCanBeFilledAndParsed() {
    using var bytes = new MemoryStream(); bytes.Write(UserExcelTemplate.Generate()); bytes.Position = 0;
    using (var document = SpreadsheetDocument.Open(bytes, true)) {
      var issues = new OpenXmlValidator().Validate(document).ToArray(); Assert.True(issues.Length == 0, string.Join(" | ", issues.Select(issue => issue.Description + " at " + issue.Path?.XPath))); var workbook = document.WorkbookPart!;
      var users = (WorksheetPart)workbook.GetPartById(workbook.Workbook!.Sheets!.Elements<Sheet>().Single(x => x.Name == "Users").Id!.Value!);
      var rows = users.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().ToArray(); Assert.Equal(501, rows.Length);
      Assert.Equal(24, rows[0].Height!.Value); Assert.All(rows.Skip(1), row => Assert.Equal(20, row.Height!.Value));
      Assert.All(rows.SelectMany(row => row.Elements<Cell>()), cell => {
        var format = workbook.WorkbookStylesPart!.Stylesheet!.CellFormats!.Elements<CellFormat>().ElementAt((int)cell.StyleIndex!.Value);
        Assert.Equal(1U, format.BorderId!.Value);
      });
      Assert.Equal(Headers, rows[0].Elements<Cell>().Select(cell => cell.InnerText));
      var values = new[] { "Maria", "Santos", "maria@example.com" }; var index = 0;
      foreach (var cell in rows[1].Elements<Cell>()) cell.InlineString = new InlineString(new Text(values[index++]));
      users.Worksheet.Save();
    }
    var parsed = await Parse(bytes.ToArray()); Assert.Empty(parsed.Errors); Assert.Single(parsed.Rows); Assert.Equal(new[] { "booking.read", "bus.read", "terminal.read" }, parsed.Rows[0].Roles);
  }
  [Fact]
  public async Task UserImportNormalizesNamesAndEmailAndAssignsOnlyDefaultViewRoles() {
    var parsed = await Parse(Workbook([" Maria ", " Santos ", "MARIA@EXAMPLE.COM"], ["", "", ""]));
    Assert.Empty(parsed.Errors); var row = Assert.Single(parsed.Rows); Assert.Equal(2, row.Row); Assert.Equal("Maria", row.FirstName);
    Assert.Equal("maria@example.com", row.Email); Assert.Equal(new[] { "booking.read", "bus.read", "terminal.read" }, row.Roles);
  }
  [Fact]
  public async Task UserImportRejectsDuplicatesMissingValuesBadEmailsAndControlCharacters() {
    var parsed = await Parse(Workbook(["Maria", "Santos", "maria@example.com"], ["Other", "User", "MARIA@example.com"],
      ["", "Bad", "not-an-email"], ["New\n", "User", "new@example.com"]));
    Assert.Contains(parsed.Errors, x => x.Row == 3 && x.Field == "Email address");
    Assert.Contains(parsed.Errors, x => x.Row == 4 && x.Field == "First name"); Assert.Contains(parsed.Errors, x => x.Row == 4 && x.Field == "Email address");
    Assert.Contains(parsed.Errors, x => x.Row == 5 && x.Field == "First name");
    var hotel = await Parse(Workbook(["Hotel", "User", "hotel@example.com"]), "hotel");
    Assert.Empty(hotel.Errors); Assert.Equal(new[] { "booking.read" }, Assert.Single(hotel.Rows).Roles);
  }
  [Fact]
  public async Task UserImportRejectsMalformedWrongHeadersFormulasEmptyAndOversizedFiles() {
    foreach (var bytes in new[] { Workbook(), BusExcelImportTests.Workbook([Headers], "Sheet1"), BusExcelImportTests.Workbook([["Bad", "Headers"]], "Users"), new byte[] {1,2,3} })
      Assert.NotEmpty((await Parse(bytes)).Errors);
    Assert.NotEmpty((await Parse(Workbook(["A", "B", "a@example.com"]), name: "users.xls")).Errors);
    using var formula = new MemoryStream(); formula.Write(Workbook(["A", "B", "a@example.com"])); formula.Position = 0;
    using (var document = SpreadsheetDocument.Open(formula, true)) {
      var sheet = document.WorkbookPart!.WorksheetParts.Single();
      sheet.Worksheet!.Descendants<Cell>().Single(cell => cell.CellReference == "C2").CellFormula = new CellFormula("CONCAT(\"a\",\"@example.com\")");
      sheet.Worksheet.Save();
    }
    Assert.Contains((await Parse(formula.ToArray())).Errors, x => x.Message.Contains("formula"));
    Assert.NotEmpty((await UserExcelImport.ReadAsync(new FormFile(new MemoryStream(), 0, UserExcelImport.MaximumBytes + 1, "file", "users.xlsx"), "bus-operator", default)).Errors);
  }
  [Fact]
  public async Task UserImportRejectsOldPermissionColumnAndExtraPermissionValues() {
    var oldTemplate = BusExcelImportTests.Workbook([
      ["First name", "Last name", "Email address", "Permissions"],
      ["Maria", "Santos", "maria@example.com", "member.manage"]], "Users");
    Assert.Contains((await Parse(oldTemplate)).Errors, error => error.Message.Contains("new template without Permissions"));
    var extraColumn = await Parse(Workbook(["Maria", "Santos", "maria@example.com", "member.manage"]));
    Assert.Contains(extraColumn.Errors, error => error.Row == 2 && error.Message.Contains("three template columns"));
    Assert.Equal(new[] { "booking.read", "bus.read", "terminal.read" }, Assert.Single(extraColumn.Rows).Roles);
  }
  [Fact]
  public async Task UserImportAcceptsFiveHundredRowsAndRejectsFiveHundredAndOne() {
    var rows = Enumerable.Range(1,500).Select(i => new[] { "First", "Last", $"user{i}@example.com" }).ToArray();
    Assert.Empty((await Parse(Workbook(rows))).Errors);
    Assert.NotEmpty((await Parse(Workbook([..rows, ["Extra", "User", "extra@example.com"]]))).Errors);
  }
}
