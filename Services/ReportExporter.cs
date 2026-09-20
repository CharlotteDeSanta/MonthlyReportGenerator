using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

/// <summary>导出编排：构建统一布局 → 渲染 XLSX → 写文件。</summary>
public static class ReportExporter
{
    /// <summary>月报默认文件名：AGV月报_{姓名}_{年}_{月}.xlsx</summary>
    public static string DefaultFileName(ReportMeta meta) =>
        $"AGV月报_{meta.EmployeeName}_{meta.Year}_{meta.Month}.xlsx";

    /// <summary>日报默认文件名：实施记录日报_{年}-{月}.xlsx</summary>
    public static string DailyFileName(int year, int month) =>
        $"实施记录日报_{year}-{month:D2}.xlsx";

    /// <summary>周报默认文件名：周工作总结计划_{年}-W{周}.xlsx</summary>
    public static string WeeklyFileName(int year, int week) =>
        $"周工作总结计划_{year}-W{week:D2}.xlsx";

    public static void Export(ReportMeta meta, IReadOnlyList<DailyEntry> days, string filePath)
        => XlsxExportService.Write(filePath, ReportLayoutBuilder.Build(meta, days));

    public static void ExportDaily(IReadOnlyList<DailyDayEntry> days, string filePath)
        => XlsxExportService.Write(filePath, DailyReportLayoutBuilder.Build(days));

    public static void ExportWeekly(
        WeeklyReportMeta meta,
        IReadOnlyList<WeekDayEntry> thisWeek,
        IReadOnlyList<WeekDayEntry> nextWeek,
        string filePath)
        => XlsxExportService.Write(filePath, WeeklyReportLayoutBuilder.Build(meta, thisWeek, nextWeek));
}
