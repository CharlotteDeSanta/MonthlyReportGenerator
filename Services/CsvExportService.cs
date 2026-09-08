using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

public static class CsvExportService
{
    /// <summary>
    /// 把报表行序列写成 CSV：UTF-8 带 BOM（Excel 双击打开中文不乱码），CRLF 换行。
    /// </summary>
    public static void Write(string path, IReadOnlyList<ReportLine> lines)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.NewLine = "\r\n";

        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var line in lines)
        {
            foreach (var cell in line.Cells)
                csv.WriteField(cell);
            csv.NextRecord();
        }
    }
}
