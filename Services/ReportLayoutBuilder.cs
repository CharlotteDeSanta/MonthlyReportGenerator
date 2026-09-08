using System.Globalization;
using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

/// <summary>
/// 把（元信息 + 整月明细）构建成统一的报表行序列，结构与示例 CSV 完全一致：
/// 标题 → 元信息 → 明细表头 → 28~31 行明细 → 空行 → 月度汇总统计 → 空行 → 项目地点汇总。
/// CSV 与 XLSX 渲染器共享同一份行序列，保证两者内容一致。
/// </summary>
public static class ReportLayoutBuilder
{
    public const int ColumnCount = 8;

    public static List<ReportLine> Build(ReportMeta meta, IReadOnlyList<DailyEntry> days)
    {
        var summary = ComputeSummary(days);
        var locations = ComputeLocationSummary(days);
        var lines = new List<ReportLine>(days.Count + 10);

        lines.Add(new ReportLine(LineRole.Title, Row(meta.Title)));
        lines.Add(new ReportLine(LineRole.Meta, Row(
            "姓名", meta.EmployeeName,
            "工程师等级", meta.Level.ToString(),
            "填报年份", meta.Year.ToString(),
            "填报月份", meta.Month.ToString())));
        lines.Add(new ReportLine(LineRole.DetailHeader, Row(
            "日期", "上班地点", "工作内容", "上班时间", "下班时间",
            "工作时长(h)", "加班时长(h)", "项目出勤")));

        foreach (var d in days)
        {
            lines.Add(new ReportLine(LineRole.Detail, Row(
                d.Date.ToString("yyyy-MM-dd"),
                d.Location,
                d.WorkContent,
                d.StartTimeText,
                d.EndTimeText,
                d.WorkHours.ToString("0.00", CultureInfo.InvariantCulture),
                d.OvertimeHours.ToString("0.00", CultureInfo.InvariantCulture),
                d.ProjectAttendance.ToString("0.00", CultureInfo.InvariantCulture))));
        }

        lines.Add(new ReportLine(LineRole.Separator, EmptyRow()));
        lines.Add(new ReportLine(LineRole.SummaryTitle, Row(
            "月度汇总统计", "",
            "实际上班天数(天)", "项目出勤天数(天)", "总工作时长(h)", "总加班时长(h)",
            "当月累计可调休天数(天)", "当月已调休天数(天)")));
        lines.Add(new ReportLine(LineRole.SummaryValues, Row("", "",
            summary.ActualWorkDays.ToString("0.0", CultureInfo.InvariantCulture),
            summary.ProjectAttendanceDays.ToString("0.00", CultureInfo.InvariantCulture),
            summary.TotalWorkHours.ToString("0.00", CultureInfo.InvariantCulture),
            summary.TotalOvertimeHours.ToString("0.00", CultureInfo.InvariantCulture),
            summary.AccruedRestDays.ToString("0.00", CultureInfo.InvariantCulture),
            summary.UsedRestDays.ToString("0.0", CultureInfo.InvariantCulture))));

        lines.Add(new ReportLine(LineRole.Separator, EmptyRow()));
        lines.Add(new ReportLine(LineRole.LocationTitle, Row("项目地点汇总")));
        lines.Add(new ReportLine(LineRole.LocationHeader, Row("项目地点", "出勤天数(天)", "加班时长(h)")));
        foreach (var loc in locations)
        {
            lines.Add(new ReportLine(LineRole.LocationRow, Row(
                loc.Location,
                loc.AttendanceDays.ToString("0.0", CultureInfo.InvariantCulture),
                loc.OvertimeHours.ToString("0.00", CultureInfo.InvariantCulture))));
        }

        return lines;
    }

    /// <summary>“月度汇总统计”六项指标（规则与原月报系统一致）。</summary>
    public static SummaryValues ComputeSummary(IReadOnlyList<DailyEntry> days)
    {
        var attendance = days.Sum(d => d.ProjectAttendance);
        var totalWork = days.Sum(d => d.WorkHours);
        var totalOvertime = days.Sum(d => d.OvertimeHours);

        // 实际上班天数：排除纯调休/休息；≥8h 记 1 天，4~8h 记 0.5 天，<4h 记 0
        decimal actualWorkDays = 0m;
        foreach (var d in days)
        {
            if (!d.IsPureRest && d.WorkHours > 0m)
                actualWorkDays += d.WorkHours >= 8m ? 1m : d.WorkHours >= 4m ? 0.5m : 0m;
        }

        // 已调休天数：仅工作日统计；纯调休/休息 +1，混合调休 +0.5
        decimal usedRestDays = 0m;
        foreach (var d in days)
        {
            if (WorkCalendar.IsWorkday(d.Date) && d.ContainsRest)
                usedRestDays += d.IsPureRest ? 1m : 0.5m;
        }

        // 可调休 = 总加班 ÷ 8，保留 2 位。示例 12.68/8 = 1.585 → 1.58（银行家舍入）
        var accrued = totalOvertime > 0m
            ? Math.Round(totalOvertime / 8m, 2, MidpointRounding.ToEven)
            : 0m;

        return new SummaryValues(actualWorkDays, attendance, totalWork, totalOvertime, accrued, usedRestDays);
    }

    /// <summary>
    /// “项目地点汇总”（与原系统 getProjectSummary 一致）：
    /// 取每行的第一个项目名（排除“公司/调休/休息”），出勤与加班全额计入；
    /// 按项目名字典序（当前区域文化）升序排列。
    /// </summary>
    public static List<LocationSummaryRow> ComputeLocationSummary(IReadOnlyList<DailyEntry> days)
    {
        var map = new Dictionary<string, (decimal Attendance, decimal Overtime)>(StringComparer.Ordinal);

        foreach (var d in days)
        {
            var project = d.GetProjectTokens().FirstOrDefault();
            if (project is null) continue;

            map.TryGetValue(project, out var agg);
            map[project] = (agg.Attendance + d.ProjectAttendance, agg.Overtime + d.OvertimeHours);
        }

        return map
            .Select(kv => new LocationSummaryRow(kv.Key, kv.Value.Attendance, kv.Value.Overtime))
            .OrderBy(r => r.Location, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>把变长值填充进固定 8 列的单元格数组（不足补空串）。</summary>
    private static string[] Row(params string[] values)
    {
        var cells = new string[ColumnCount];
        Array.Fill(cells, "");
        for (var i = 0; i < values.Length && i < ColumnCount; i++)
            cells[i] = values[i];
        return cells;
    }

    private static string[] EmptyRow() => Row();
}
