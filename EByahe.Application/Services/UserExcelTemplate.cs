using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
namespace EByahe.Application.Services;
public static class UserExcelTemplate {
  public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
  public const string FileName = "User-import-template.xlsx";
  public static byte[] Generate() {
    using var stream = new MemoryStream();
    using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook)) {
      var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook(new Sheets());
      var styles = workbook.AddNewPart<WorkbookStylesPart>();
      var thin = new Border(new LeftBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FFD4DFEF" } },
        new RightBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FFD4DFEF" } },
        new TopBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FFD4DFEF" } },
        new BottomBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = "FFD4DFEF" } }, new DiagonalBorder());
      styles.Stylesheet = new Stylesheet(
        new Fonts(new Font { FontSize = new FontSize { Val = 11 }, FontName = new FontName { Val = "Calibri" } }, new Font { Bold = new Bold(), Color = new Color { Rgb = "FFFFFFFF" }, FontSize = new FontSize { Val = 11 }, FontName = new FontName { Val = "Calibri" } }) { Count = 2 },
        new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
          new Fill(new PatternFill(new ForegroundColor { Rgb = "FF16459B" }, new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid })) { Count = 3 },
        new Borders(new Border(), thin) { Count = 2 }, new CellStyleFormats(new CellFormat()) { Count = 1 },
        new CellFormats(new CellFormat(), new CellFormat { FontId = 0, FillId = 0, BorderId = 1, NumberFormatId = 49, ApplyNumberFormat = true, ApplyBorder = true,
          Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center } },
          new CellFormat { FontId = 1, FillId = 2, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true,
            Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center } }) { Count = 3 },
        new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
      void Sheet(string name, uint id, double[] widths, IReadOnlyList<string[]> rows, int count) {
        var part = workbook.AddNewPart<WorksheetPart>(); var data = new SheetData();
        part.Worksheet = new Worksheet(new SheetViews(new SheetView(new Pane { State = PaneStateValues.Frozen, VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft }) { WorkbookViewId = 0 }),
          new Columns(widths.Select((width, index) => new Column { Min = (uint)index + 1, Max = (uint)index + 1, Width = width, CustomWidth = true })), data);
        for (var index = 0; index < count; index++) {
          var row = new Row { RowIndex = (uint)index + 1, Height = index == 0 ? 24 : 20, CustomHeight = true };
          for (var column = 0; column < widths.Length; column++) row.Append(new Cell { CellReference = ((char)('A' + column)).ToString() + (index + 1),
            StyleIndex = index == 0 ? 2U : 1U, DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(index < rows.Count && column < rows[index].Length ? rows[index][column] : "")) });
          data.Append(row);
        }
        workbook.Workbook.Sheets!.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = id, Name = name });
      }
      Sheet("Users", 1, [25, 25, 40], [["First name", "Last name", "Email address"]], 501);
      List<string[]> notes = [ ["Item", "Instructions"],
        ["Getting started", "Enter one user on each row of Users, starting at row 2. Keep the header row unchanged."],
        ["Limits", "Maximum 500 users per file; .xlsx only; maximum 5 MB. Do not use formulas."],
        ["Required fields", "First name, Last name, Email address. Do not repeat email addresses."],
        ["Default access", "Bus operator users receive View bookings, View buses and View terminals, plus the standard Partner portal and account roles. Permissions are assigned automatically."],
        ["Partner", "Select the partner in the upload dialog."],
        ["Existing accounts", "Existing accounts keep their profile and password. Default view permissions are added; existing permissions are retained."],
        ["New accounts", "A temporary password is generated securely. The original uploader can reveal it in user details."],
        ["Processing", "The file is validated before any requests are queued. Accounts appear as processing completes."],
        ["Example first name", "Maria"], ["Example last name", "Santos"], ["Example email", "maria@example.com"] ];
      Sheet("Instructions", 2, [30, 125], notes, notes.Count);
      workbook.Workbook.Save();
    }
    return stream.ToArray();
  }
}
