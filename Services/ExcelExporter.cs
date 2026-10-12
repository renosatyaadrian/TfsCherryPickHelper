using System.IO;
using ClosedXML.Excel;
using TfsCherryPickHelper.Models;

namespace TfsCherryPickHelper.Services;

public static class ExcelExporter
{
    /// <summary>
    /// Exports cherry-pick items to an Excel file, sorted ascending by merge date
    /// (oldest merge first). Uses the configured output directory, defaulting to
    /// an "output" folder in the project root (next to the .csproj), NOT inside
    /// bin/obj so files survive clean/rebuild.
    /// Returns the full path of the generated file.
    /// </summary>
    public static string Export(IEnumerable<CherryPickItem> items, string crName, string? outputDirectory)
    {
        var sorted = items.OrderBy(x => x.MergeDate).ToList();

        var outputFolder = string.IsNullOrWhiteSpace(outputDirectory)
            ? GetDefaultOutputFolder()
            : outputDirectory;
        Directory.CreateDirectory(outputFolder);

        var fileName = $"CherryPickList_{SanitizeCrName(crName)}_{DateTime.Now:ddMMyy_HHmmss}.xlsx";
        var fullPath = Path.Combine(outputFolder, fileName);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("CherryPickList");

        sheet.Cell(1, 1).Value = "PR ID";
        sheet.Cell(1, 2).Value = "Commit Hash";
        sheet.Cell(1, 3).Value = "Merge Date";
        sheet.Cell(1, 4).Value = "Message";
        sheet.Cell(1, 5).Value = "Cherry-Pick Command";

        var headerRange = sheet.Range(1, 1, 1, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        for (var i = 0; i < sorted.Count; i++)
        {
            var row = i + 2;
            var item = sorted[i];

            sheet.Cell(row, 1).Value = item.PrId;
            if (!string.IsNullOrWhiteSpace(item.PrUrl))
            {
                sheet.Cell(row, 1).SetHyperlink(new XLHyperlink(item.PrUrl));
                sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Blue;
                sheet.Cell(row, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
            }
            sheet.Cell(row, 2).Value = item.CommitHash;
            sheet.Cell(row, 3).Value = item.MergeDate;
            sheet.Cell(row, 3).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            sheet.Cell(row, 4).Value = item.Message;
            sheet.Cell(row, 5).Value = item.CherryPickCommand;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        workbook.SaveAs(fullPath);
        return fullPath;
    }

    private static string SanitizeCrName(string crName) =>
        string.Join("_", crName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(' ', '_');

    /// <summary>
    /// Walks up from the executable folder to find the project root
    /// (the folder containing the .csproj), falling back to the executable folder.
    /// </summary>
    private static string GetDefaultOutputFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.csproj").Length == 0)
        {
            dir = dir.Parent;
        }

        var root = dir ?? new DirectoryInfo(AppContext.BaseDirectory);
        return Path.Combine(root.FullName, "output");
    }
}