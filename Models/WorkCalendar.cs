namespace MonthlyReportGenerator.Models;

/// <summary>
/// 工作日历（与原月报系统一致）：
/// 法定节假日与周末 → 非工作日（出勤按全部时长计加班）；
/// 调休补班日（周末上班）→ 工作日（按 8 小时制计加班）。
/// 数据来源：https://timor.tech/api/holiday/year/{year}（2025/2026 已内置；
/// 未内置的年份按“仅周末”规则回退，后续发布后可补充）。
/// </summary>
public static class WorkCalendar
{
    private static readonly Dictionary<int, (string[] Holidays, string[] Workdays)> Data = new()
    {
        [2025] = (
            Holidays: new[]
            {
                "2025-01-01",
                "2025-01-28", "2025-01-29", "2025-01-30", "2025-01-31",
                "2025-02-01", "2025-02-02", "2025-02-03", "2025-02-04",
                "2025-04-04", "2025-04-05", "2025-04-06",
                "2025-05-01", "2025-05-02", "2025-05-03", "2025-05-04", "2025-05-05",
                "2025-05-31", "2025-06-01", "2025-06-02",
                "2025-10-01", "2025-10-02", "2025-10-03", "2025-10-04",
                "2025-10-05", "2025-10-06", "2025-10-07", "2025-10-08",
            },
            Workdays: new[]
            {
                "2025-01-26", "2025-02-08", "2025-04-27", "2025-09-28", "2025-10-11",
            }),
        [2026] = (
            Holidays: new[]
            {
                "2026-01-01", "2026-01-02", "2026-01-03",
                "2026-02-15", "2026-02-16", "2026-02-17", "2026-02-18", "2026-02-19",
                "2026-02-20", "2026-02-21", "2026-02-22", "2026-02-23",
                "2026-04-04", "2026-04-05", "2026-04-06",
                "2026-05-01", "2026-05-02", "2026-05-03", "2026-05-04", "2026-05-05",
                "2026-06-19", "2026-06-20", "2026-06-21",
                "2026-09-25", "2026-09-26", "2026-09-27",
                "2026-10-01", "2026-10-02", "2026-10-03", "2026-10-04",
                "2026-10-05", "2026-10-06", "2026-10-07",
            },
            Workdays: new[]
            {
                "2026-01-04", "2026-02-14", "2026-02-28", "2026-05-09", "2026-09-20", "2026-10-10",
            }),
    };

    private static readonly object Sync = new();

    private static readonly HashSet<int> BuiltInYears = new() { 2025, 2026 };

    public static bool IsBuiltIn(int year) => BuiltInYears.Contains(year);

    public static bool HasData(int year)
    {
        lock (Sync) return Data.ContainsKey(year);
    }

    /// <summary>注册某年的节假日/补班日数据（由 HolidayService 从缓存或网络加载后调用）。</summary>
    public static void Register(int year, string[] holidays, string[] workdays)
    {
        lock (Sync) Data[year] = (holidays, workdays);
    }

    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static bool IsHoliday(DateOnly date)
    {
        lock (Sync)
            return Data.TryGetValue(date.Year, out var y) &&
                   y.Holidays.Contains(date.ToString("yyyy-MM-dd"));
    }

    public static bool IsMakeupWorkday(DateOnly date)
    {
        lock (Sync)
            return Data.TryGetValue(date.Year, out var y) &&
                   y.Workdays.Contains(date.ToString("yyyy-MM-dd"));
    }

    /// <summary>是否工作日：补班日是工作日；法定节假日与周末不是。</summary>
    public static bool IsWorkday(DateOnly date) =>
        IsMakeupWorkday(date) || (!IsHoliday(date) && !IsWeekend(date));
}
