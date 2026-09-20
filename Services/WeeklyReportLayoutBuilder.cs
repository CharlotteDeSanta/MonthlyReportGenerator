using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

/// <summary>
/// 周报（周工作总结计划）布局构建，8 列：
/// 类别｜星期｜工作内容｜阶段要点｜需要协调的内容｜计划完成时间｜进度状态｜备注。
/// “本周/下周工作总结”标签在 A 列向下合并 7 行（与模板一致）。
/// </summary>
public static class WeeklyReportLayoutBuilder
{
    public const int ColumnCount = 8;

    public static List<ReportLine> Build(
        WeeklyReportMeta meta,
        IReadOnlyList<WeekDayEntry> thisWeek,
        IReadOnlyList<WeekDayEntry> nextWeek)
    {
        var lines = new List<ReportLine>(2 + 2 + 1 + thisWeek.Count + nextWeek.Count);

        lines.Add(new ReportLine(LineRole.Title, LayoutUtil.Row(ColumnCount, "周工作总结计划")));

        lines.Add(new ReportLine(LineRole.Meta, LayoutUtil.Row(ColumnCount,
            "项目名称", meta.ProjectName, "",
            "日期", meta.ReportDate.ToString("yyyy-MM-dd"),
            "周数", meta.WeekNumber.ToString(), "")));

        lines.Add(new ReportLine(LineRole.Meta, LayoutUtil.Row(ColumnCount,
            "填写人", meta.Writer, "",
            "是否按上周计划完成", meta.FollowedLastPlan ? "是" : "否",
            "未完成项", meta.Unfinished, "")));

        lines.Add(new ReportLine(LineRole.DetailHeader, LayoutUtil.Row(ColumnCount,
            "类别", "星期", "工作内容", "阶段要点", "需要协调的内容", "计划完成时间", "进度状态", "备注")));

        AddSection(lines, "本周工作总结", LineRole.WeekThis, thisWeek);
        AddSection(lines, "下周工作计划", LineRole.WeekNext, nextWeek);

        return lines;
    }

    private static void AddSection(
        List<ReportLine> lines,
        string label,
        LineRole sectionRole,
        IReadOnlyList<WeekDayEntry> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var d = rows[i];
            var isFirst = i == 0;
            lines.Add(new ReportLine(
                isFirst ? sectionRole : LineRole.WeeklyRow,
                LayoutUtil.Row(ColumnCount,
                    isFirst ? label : "",
                    d.WeekdayText,
                    d.Content,
                    d.StagePoints,
                    d.Coordination,
                    d.PlanFinishDate,
                    d.Progress,
                    d.Remark),
                MergeDownCols: isFirst ? new[] { 1 } : null,
                MergeDownCount: isFirst ? rows.Count : 1));
        }
    }
}
