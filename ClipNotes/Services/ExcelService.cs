using System.IO;
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

        for (int i = 0; i < session.Markers.Count; i++)
        {
            var m = session.Markers[i];
            var row = i + 2;

            ws.Cell(row, 1).Value = m.Index;
            ws.Cell(row, 2).Value = m.TimestampFormatted;
            ws.Cell(row, 3).Value = m.Type.ToString();
            ws.Cell(row, 4).Value = m.Text;

            if (!string.IsNullOrEmpty(m.AudioFilePath) && File.Exists(Path.Combine(sessionDir, m.AudioFilePath)))
            {
                var cell = ws.Cell(row, 5);
                cell.Value = Path.GetFileName(m.AudioFilePath);
                cell.SetHyperlink(new XLHyperlink(m.AudioFilePath));
                cell.Style.Font.FontColor = XLColor.Blue;
                cell.Style.Font.Underline = XLFontUnderlineValues.Single;
            }

            if (!string.IsNullOrEmpty(m.TextFilePath) && File.Exists(Path.Combine(sessionDir, m.TextFilePath)))
            {
                var cell = ws.Cell(row, 6);
                cell.Value = Path.GetFileName(m.TextFilePath);
                cell.SetHyperlink(new XLHyperlink(m.TextFilePath));
                cell.Style.Font.FontColor = XLColor.Blue;
                cell.Style.Font.Underline = XLFontUnderlineValues.Single;
            }
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(xlsxPath);
    }
}
