namespace MonthlyReportGenerator.Models;

/// <summary>ISO 周（周一为一周开始）计算。</summary>
public static class WeekHelper
{
    /// <summary>某年 ISO 第 week 周的周一。</summary>
    public static DateOnly MondayOfIsoWeek(int year, int week)
    {
        var jan4 = new DateOnly(year, 1, 4); // ISO 第 1 周必包含 1 月 4 日
        var mondayOffset = ((int)jan4.DayOfWeek + 6) % 7; // 距周一的天数
        var week1Monday = jan4.AddDays(-mondayOffset);
        return week1Monday.AddDays((week - 1) * 7);
    }

    /// <summary>某日期所属的 ISO 周数。</summary>
    public static int IsoWeekOf(DateOnly date)
    {
        var mondayOffset = ((int)date.DayOfWeek + 6) % 7;
        var thursday = date.AddDays(3 - mondayOffset); // 该周的周四（ISO 周归属其所在年）
        var jan4 = new DateOnly(thursday.Year, 1, 4);
        var week1Monday = jan4.AddDays(-(((int)jan4.DayOfWeek + 6) % 7));
        return (thursday.DayNumber - week1Monday.DayNumber) / 7 + 1;
    }

    /// <summary>某年最后一个 ISO 周数（52 或 53）。</summary>
    public static int LastIsoWeekOfYear(int year) => IsoWeekOf(new DateOnly(year, 12, 28));
}
