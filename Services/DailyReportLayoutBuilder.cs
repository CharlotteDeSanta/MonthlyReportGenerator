using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

/// <summary>
/// 日报（实施记录日报）布局构建：标题 → 表头 → 每天两行（今日完成/明日计划）。
/// 5 列：日期｜完成/计划｜每日完成内容｜项目技术负责人｜项目实施人员。
/// 日期与人员列跨两行合并（与模板一致）。
/// </summary>
public static class DailyReportLayoutBuilder
{
    public const int ColumnCount = 5;

    public static List<ReportLine> Build(IReadOnlyList<DailyDayEntry> days)
    {
        var lines = new List<ReportLine>(days.Count * 2 + 2);

        lines.Add(new ReportLine(LineRole.Title, LayoutUtil.Row(ColumnCount, "实施记录日报"), ColumnCount: ColumnCount));
        lines.Add(new ReportLine(LineRole.DetailHeader, LayoutUtil.Row(ColumnCount,
            "日期", "完成/计划", "每日完成内容", "项目技术负责人", "项目实施人员"), ColumnCount: ColumnCount));

        foreach (var d in days)
        {
            lines.Add(new ReportLine(LineRole.DailyDone, LayoutUtil.Row(ColumnCount,
                d.Date.ToString("yyyy-MM-dd"), "今日完成", d.DoneToday, d.TechLeader, d.Implementer),
                RestDay: d.IsRestDay,
                MergeDownCols: new[] { 1, 4, 5 },
                MergeDownCount: 2,
                ColumnCount: ColumnCount));

            lines.Add(new ReportLine(LineRole.DailyPlan, LayoutUtil.Row(ColumnCount,
                "", "明日计划", d.PlanTomorrow, "", ""), ColumnCount: ColumnCount));
        }

        return lines;
    }
}
