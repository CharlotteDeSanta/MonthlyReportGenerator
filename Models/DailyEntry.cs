using System.ComponentModel;
using System.Globalization;

namespace MonthlyReportGenerator.Models;

/// <summary>
/// 月报明细的一行（当月每天一行，共 28~31 行）。
/// 工作时长 / 加班时长 / 项目出勤均为只读派生值，自动计算，不手填。
/// </summary>
public class DailyEntry : INotifyPropertyChanged
{
    private string _location = "";
    private string _workContent = "";
    private string _startTimeText = "";
    private string _endTimeText = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>该行对应的日期（整月自动生成，不可编辑）。</summary>
    public DateOnly Date { get; init; }

    /// <summary>上班地点。支持：公司 / 项目名 / 调休 / 休息 / “公司/项目名” / “调休/项目名”。</summary>
    public string Location
    {
        get => _location;
        set => SetField(ref _location, (value ?? "").Trim(), nameof(Location), nameof(ContainsRest),
            nameof(IsPureRest), nameof(IsMixedRest), nameof(HasProject),
            nameof(OvertimeHours), nameof(ProjectAttendance));
    }

    public string WorkContent
    {
        get => _workContent;
        set => SetField(ref _workContent, value ?? "", nameof(WorkContent));
    }

    /// <summary>上班时间文本（HH:mm）。非法格式按空处理，校验在 ViewModel 层提示。</summary>
    public string StartTimeText
    {
        get => _startTimeText;
        set => SetField(ref _startTimeText, (value ?? "").Trim(),
            nameof(StartTimeText), nameof(StartHour), nameof(StartMinute),
            nameof(StartTime), nameof(WorkHours),
            nameof(OvertimeHours), nameof(ProjectAttendance), nameof(HasInvalidTime));
    }

    /// <summary>下班时间文本（HH:mm）。</summary>
    public string EndTimeText
    {
        get => _endTimeText;
        set => SetField(ref _endTimeText, (value ?? "").Trim(),
            nameof(EndTimeText), nameof(EndHour), nameof(EndMinute),
            nameof(EndTime), nameof(WorkHours),
            nameof(OvertimeHours), nameof(ProjectAttendance), nameof(HasInvalidTime));
    }

    // ---------- 时分拆分（供小时/分钟下拉绑定） ----------

    private static bool TrySplitTime(string text, out string hour, out string minute)
    {
        hour = "";
        minute = "";
        if (!TimeSpan.TryParseExact(text, new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out var t))
            return false;
        hour = ((int)t.TotalHours).ToString("00");
        minute = t.Minutes.ToString("00");
        return true;
    }

    /// <summary>上班时间-小时部分（绑定小时下拉）。先选小时默认补 :00。</summary>
    public string StartHour
    {
        get => TrySplitTime(StartTimeText, out var hour, out _) ? hour : "";
        set
        {
            if (value is null) return; // 双向绑定清空时的 null 回写，直接忽略
            if (StartHour == value) return;
            var minute = StartMinute;
            StartTimeText = value.Length == 2 ? $"{value}:{(minute.Length == 2 ? minute : "00")}" : "";
        }
    }

    /// <summary>上班时间-分钟部分（绑定分钟下拉）。</summary>
    public string StartMinute
    {
        get => TrySplitTime(StartTimeText, out _, out var minute) ? minute : "";
        set
        {
            if (value is null) return;
            if (StartMinute == value) return;
            var hour = StartHour;
            StartTimeText = hour.Length == 2 && value.Length == 2 ? $"{hour}:{value}" : "";
        }
    }

    /// <summary>下班时间-小时部分。</summary>
    public string EndHour
    {
        get => TrySplitTime(EndTimeText, out var hour, out _) ? hour : "";
        set
        {
            if (value is null) return;
            if (EndHour == value) return;
            var minute = EndMinute;
            EndTimeText = value.Length == 2 ? $"{value}:{(minute.Length == 2 ? minute : "00")}" : "";
        }
    }

    /// <summary>下班时间-分钟部分。</summary>
    public string EndMinute
    {
        get => TrySplitTime(EndTimeText, out _, out var minute) ? minute : "";
        set
        {
            if (value is null) return;
            if (EndMinute == value) return;
            var hour = EndHour;
            EndTimeText = hour.Length == 2 && value.Length == 2 ? $"{hour}:{value}" : "";
        }
    }

    // ---------------- 派生值（只读） ----------------

    private static bool TryParseTime(string text, out TimeSpan time) =>
        TimeSpan.TryParseExact(text, new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out time);

    public TimeSpan? StartTime => TryParseTime(StartTimeText, out var t) ? t : null;
    public TimeSpan? EndTime => TryParseTime(EndTimeText, out var t) ? t : null;

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

    /// <summary>休息日（周末/法定节假日，补班日除外）——用于行高亮。</summary>
    public bool IsRestDay =>
        (WorkCalendar.IsHoliday(Date) || WorkCalendar.IsWeekend(Date)) &&
        !WorkCalendar.IsMakeupWorkday(Date);

    /// <summary>工作时长 = 下班 − 上班；下班早于上班按跨午夜（+24h）计算，保留 2 位小数。</summary>
    public decimal WorkHours
    {
        get
        {
            if (StartTime is not { } start || EndTime is not { } end) return 0m;
            var diff = end - start;
            if (diff < TimeSpan.Zero) diff += TimeSpan.FromDays(1);
            if (diff == TimeSpan.Zero) return 0m;
            return Math.Round((decimal)diff.TotalHours, 2, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>地点是否含“调休”或“休息”（与原系统一致，两者同义）。</summary>
    public bool ContainsRest => Location.Contains("调休") || Location.Contains("休息");

    /// <summary>纯调休/休息：地点恰为“调休”或“休息”。</summary>
    public bool IsPureRest => Location is "调休" or "休息";

    /// <summary>混合调休：含“调休/休息”且带“/”（如“调休/项目名”）。</summary>
    public bool IsMixedRest => ContainsRest && Location.Contains('/');

    /// <summary>地点中除“公司”“调休”之外的项目名 token（如 无锡国药）。</summary>
    public IReadOnlyList<string> GetProjectTokens() =>
        Location.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t is not "公司" and not "调休" and not "休息")
                .ToList();

    public bool HasProject => GetProjectTokens().Count > 0;

    /// <summary>
    /// 加班时长（自动计算，与原系统一致）：
    /// 周末/法定节假日（含补班的周末）→ 全部工作时长；
    /// 工作日混合调休（调休占 4h）→ 超过 4h 的部分；
    /// 其余工作日 → 8 小时制，超过 8h 的部分。
    /// </summary>
    public decimal OvertimeHours
    {
        get
        {
            var h = WorkHours;
            if (h <= 0m) return 0m;
            if (WorkCalendar.IsHoliday(Date) || WorkCalendar.IsWeekend(Date)) return h;
            if (IsMixedRest) return Math.Max(h - 4m, 0m);
            return Math.Max(h - 8m, 0m);
        }
    }

    /// <summary>
    /// 项目出勤（自动计算）：
    /// 无项目 token（空/公司/调休）→ 0；
    /// “公司/项目名”→ 固定 0.5（半天外勤）；
    /// 其余按工作时长：&lt;4h→0，4~8h→0.5，≥8h→1。
    /// </summary>
    public decimal ProjectAttendance
    {
        get
        {
            if (!HasProject) return 0m;
            if (Location.Contains("公司")) return 0.5m;
            var h = WorkHours;
            if (h < 4m) return 0m;
            if (h < 8m) return 0.5m;
            return 1m;
        }
    }

    /// <summary>时间填了但格式非法（跨午夜合法，下班早于上班按次日计算）。</summary>
    public bool HasInvalidTime =>
        (StartTimeText.Length > 0 && StartTime is null) ||
        (EndTimeText.Length > 0 && EndTime is null);

    public bool HasFilledTime => StartTimeText.Length > 0 || EndTimeText.Length > 0;

    /// <summary>节假日数据加载完成后调用：刷新依赖日历的派生值（加班/出勤/行高亮）。</summary>
    public void RefreshDerived()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRestDay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OvertimeHours)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProjectAttendance)));
    }

    // ---------------- INPC ----------------

    private void SetField(ref string field, string value, params string[] notify)
    {
        if (field == value) return;
        field = value;
        foreach (var name in notify)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
