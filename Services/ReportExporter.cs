using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

public enum ExportFormat
{
    Csv,
    Xlsx,
}

/// <summary>导出编排：构建统一布局 → 按格式渲染 → 写文件。</summary>
public static class ReportExporter
{
    /// <summary>默认文件名：AGV月报_{姓名}_{年}_{月}.{扩展名}</summary>
    public static string DefaultFileName(ReportMeta meta, ExportFormat format)
    {
        var ext = format == ExportFormat.Csv ? "csv" : "xlsx";
        return $"AGV月报_{meta.EmployeeName}_{meta.Year}_{meta.Month}.{ext}";
    }

    public static void Export(ReportMeta meta, IReadOnlyList<DailyEntry> days, ExportFormat format, string filePath)
    {
        var lines = ReportLayoutBuilder.Build(meta, days);

        if (format == ExportFormat.Csv)
            CsvExportService.Write(filePath, lines);
        else
            XlsxExportService.Write(filePath, lines);
    }
}
