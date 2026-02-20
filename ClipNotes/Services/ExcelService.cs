using System.IO;
using System.Linq;
using ClipNotes.Models;
using ClosedXML.Excel;

namespace ClipNotes.Services;

public class ExcelService
{
    public void GenerateReport(SessionData session, string xlsxPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(xlsxPath)!);
        var sessionDir = session.SessionFolder;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("ClipNotes");

        // Header
        ws.Cell(1, 1).Value = "#";
        ws.Cell(1, 2).Value = "Timecode";
        ws.Cell(1, 3).Value = "Тип";
        ws.Cell(1, 4).Value = "Текст";
        ws.Cell(1, 5).Value = "Аудио";
        ws.Cell(1, 6).Value = "Транскрипт";

        var headerRange = ws.Range(1, 1, 1, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F0F0");

        // Summary markers go first (only if at least one exists)
        var hasSummary = session.Markers.Any(m => m.Type == MarkerType.Summary);
        var orderedMarkers = hasSummary
            ? session.Markers.OrderBy(m => m.Type == MarkerType.Summary ? 0 : 1).ThenBy(m => m.Index).ToList()
            : session.Markers;

        for (int i = 0; i < orderedMarkers.Count; i++)
        {
            var m = orderedMarkers[i];
            var row = i + 2;

            ws.Cell(row, 1).Value = m.Index;
            ws.Cell(row, 2).Value = m.TimestampFormatted;
            ws.Cell(row, 3).Value = m.Type.ToString();
            ws.Cell(row, 4).Value = m.Text;

            var audioAbsPath = Path.IsPathRooted(m.AudioFilePath ?? "")
                ? m.AudioFilePath! : Path.Combine(sessionDir, m.AudioFilePath ?? "");
            if (!string.IsNullOrEmpty(m.AudioFilePath) && File.Exists(audioAbsPath))
            {
                var cell = ws.Cell(row, 5);
                cell.Value = Path.GetFileName(m.AudioFilePath);
                var audioLink = Path.IsPathRooted(m.AudioFilePath)
                    ? m.AudioFilePath.Replace(Path.DirectorySeparatorChar, '/')
                    : "../" + m.AudioFilePath.Replace(Path.DirectorySeparatorChar, '/');
                cell.SetHyperlink(new XLHyperlink(audioLink));
                cell.Style.Font.FontColor = XLColor.Blue;
                cell.Style.Font.Underline = XLFontUnderlineValues.Single;
            }

            var txtAbsPath = Path.IsPathRooted(m.TextFilePath ?? "")
                ? m.TextFilePath! : Path.Combine(sessionDir, m.TextFilePath ?? "");
            if (!string.IsNullOrEmpty(m.TextFilePath) && File.Exists(txtAbsPath))
            {
                var cell = ws.Cell(row, 6);
                cell.Value = Path.GetFileName(m.TextFilePath);
                var textLink = Path.IsPathRooted(m.TextFilePath)
                    ? m.TextFilePath.Replace(Path.DirectorySeparatorChar, '/')
                    : "../" + m.TextFilePath.Replace(Path.DirectorySeparatorChar, '/');
                cell.SetHyperlink(new XLHyperlink(textLink));
                cell.Style.Font.FontColor = XLColor.Blue;
                cell.Style.Font.Underline = XLFontUnderlineValues.Single;
            }
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(xlsxPath);
    }
}
