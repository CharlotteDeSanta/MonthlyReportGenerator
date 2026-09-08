namespace MonthlyReportGenerator.Models;

/// <summary>月报元信息（标题行 + 元信息行）。</summary>
public class ReportMeta
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string EmployeeName { get; set; } = "";
    public EngineerLevel Level { get; set; } = EngineerLevel.新进工程师;

    /// <summary>标题：如“2026年AGV项目工作月报”。</summary>
    public string Title => $"{Year}年AGV项目工作月报";
}
