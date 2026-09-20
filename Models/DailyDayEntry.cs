namespace MonthlyReportGenerator.Models;

/// <summary>日报的一条记录（每天两行：今日完成 / 明日计划）。</summary>
public class DailyDayEntry : BindableBase
{
    /// <summary>该行对应日期（整月每天一对）。</summary>
    public DateOnly Date { get; init; }

    public string WeekdayText => Date.DayOfWeek switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日",
    };

    /// <summary>休息日（周末/法定节假日，补班日除外）——导出时日期格标红。</summary>
    public bool IsRestDay =>
        (WorkCalendar.IsHoliday(Date) || WorkCalendar.IsWeekend(Date)) &&
        !WorkCalendar.IsMakeupWorkday(Date);

    private string _doneToday = "";
    public string DoneToday
    {
        get => _doneToday;
        set => SetField(ref _doneToday, value ?? "");
    }

    private string _planTomorrow = "";
    public string PlanTomorrow
    {
        get => _planTomorrow;
        set => SetField(ref _planTomorrow, value ?? "");
    }

    private string _techLeader = "";
    public string TechLeader
    {
        get => _techLeader;
        set => SetField(ref _techLeader, value ?? "");
    }

    private string _implementer = "";
    public string Implementer
    {
        get => _implementer;
        set => SetField(ref _implementer, value ?? "");
    }
}
