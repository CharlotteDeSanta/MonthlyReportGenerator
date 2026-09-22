using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MonthlyReportGenerator.Models;
using MonthlyReportGenerator.Services;

namespace MonthlyReportGenerator.ViewModels;

public class MonthlyReportViewModel : ObservableObject
{
    private readonly ProfileViewModel _profile;
    private readonly DispatcherTimer _saveTimer;
    private (int Year, int Month) _loaded = (0, 0);
    private string _notice = "";
    private bool _isDirty;
    private bool _suppressDirty;

    /// <summary>当年节假日数据是否不可信（缺失），用于告警与导出拦截。</summary>
    private bool _holidayUnreliable;
    private string _holidayNotice = "";

    public ObservableCollection<DailyEntry> Days { get; } = new();

    /// <summary>底部“月度汇总统计”表格行。</summary>
    public ObservableCollection<SummaryItem> SummaryRows { get; } = new();

    /// <summary>底部“项目地点汇总”表格行。</summary>
    public ObservableCollection<LocationSummaryRow> LocationSummaries { get; } = new();

    public int[] Years { get; }
    public int[] Months { get; } = Enumerable.Range(1, 12).ToArray();

    /// <summary>时间选择-小时列表（00–23，支持跨午夜班次；用列尾“×”按钮清空）。</summary>
    public IReadOnlyList<string> HourOptions { get; } = BuildRange(0, 23).ToList();

    /// <summary>时间选择-分钟列表（00–59，1 分钟一档）。</summary>
    public IReadOnlyList<string> MinuteOptions { get; } = BuildRange(0, 59).ToList();

    private static List<string> BuildRange(int start, int end)
    {
        var list = new List<string>();
        for (var i = start; i <= end; i++)
            list.Add(i.ToString("00"));
        return list;
    }

    private int _year;
    public int Year
    {
        get => _year;
        set { if (Set(ref _year, value)) OnYearOrMonthChanged(); }
    }

    private int _month;
    public int Month
    {
        get => _month;
        set { if (Set(ref _month, value)) OnYearOrMonthChanged(); }
    }

    public string TitlePreview => $"{Year}年AGV项目工作月报";

    private string _warningText = "";
    public string WarningText
    {
        get => _warningText;
        private set => Set(ref _warningText, value);
    }

    /// <summary>提示文本颜色：节假日数据缺失等严重问题用红色，普通提示用橙色。</summary>
    private string _warningBrush = NormalWarningBrush;
    public string WarningBrush
    {
        get => _warningBrush;
        private set => Set(ref _warningBrush, value);
    }

    private const string NormalWarningBrush = "#E8A33D";
    private const string CriticalWarningBrush = "#C42B1C";

    /// <summary>导出进行中的提示（空串 = 空闲）。</summary>
    private string _busyText = "";
    public string BusyText
    {
        get => _busyText;
        private set => Set(ref _busyText, value);
    }

    public ICommand ExportCommand { get; }
    public ICommand ClearCommand { get; }

    public MonthlyReportViewModel(ProfileViewModel profile)
    {
        _profile = profile;
        var today = DateOnly.FromDateTime(DateTime.Today);
        _year = today.Year;
        _month = today.Month;
        Years = Enumerable.Range(2026, 10).ToArray(); // 2026–2035

        ExportCommand = new RelayCommand(() => _ = ExportAsync(), () => !IsBusy && !_holidayUnreliable);
        ClearCommand = new RelayCommand(ClearAll, () => !IsBusy);

        ReloadMonth();
        _ = EnsureHolidaysAsync();

        // 每 10 秒自动落盘草稿（防崩溃丢数据）
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _saveTimer.Tick += (_, _) => SaveDraftNow();
        _saveTimer.Start();
    }

    // ---------------- 月份切换与数据加载 ----------------

    private void OnYearOrMonthChanged()
    {
        OnPropertyChanged(nameof(TitlePreview));
        ReloadMonth();
        _ = EnsureHolidaysAsync();
    }

    /// <summary>
    /// 确保当年节假日数据可用（内置表→本地缓存→网络），加载后刷新行内派生值与汇总。
    /// 关键点：数据不可信（三级全部落空）时必须显式告警并禁止导出，
    /// 否则会把按“仅周末”错算的加班时长当成正常月报交付。
    /// </summary>
    private async Task EnsureHolidaysAsync()
    {
        var year = Year;

        // EnsureYearAsync 内部已捕获全部异常，不会逃逸；这里不阻塞 UI。
        var result = await HolidayService.EnsureYearAsync(year);

        if (year != Year) return; // 期间用户切换了年份

        _suppressDirty = true;
        try
        {
            foreach (var d in Days) d.RefreshDerived();
        }
        finally
        {
            _suppressDirty = false;
        }

        _holidayNotice = result.Status == HolidayLoadStatus.Unavailable ? result.Describe() : "";
        SetHolidayReliability(result.IsReliable);

        RefreshSummary();
    }

    /// <summary>切换节假日可信度：同步命令可用性（不可信时禁用导出）。</summary>
    private void SetHolidayReliability(bool reliable)
    {
        if (_holidayUnreliable == !reliable) return;
        _holidayUnreliable = !reliable;
        OnPropertyChanged(nameof(IsHolidayDataReliable));
        (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>当年节假日数据是否可信（false 时禁止导出）。</summary>
    public bool IsHolidayDataReliable => !_holidayUnreliable;

    private void ReloadMonth()
    {
        if ((Year, Month) == _loaded) return;
        SaveDraftNow();
        _loaded = (Year, Month);

        foreach (var d in Days) d.PropertyChanged -= OnEntryChanged;
        Days.Clear();

        var daysInMonth = DateTime.DaysInMonth(Year, Month);
        var draft = DraftService.LoadDraft(Year, Month);
        if (draft is not null && draft.Days.Count == daysInMonth)
        {
            foreach (var d in draft.Days) Attach(d);
            _notice = $"已恢复 {Year} 年 {Month} 月草稿";
        }
        else
        {
            for (var day = 1; day <= daysInMonth; day++)
                Attach(new DailyEntry { Date = new DateOnly(Year, Month, day) });
            _notice = "";
        }

        _isDirty = false;
        RefreshSummary();
    }

    private void Attach(DailyEntry entry)
    {
        entry.PropertyChanged += OnEntryChanged;
        Days.Add(entry);
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppressDirty) _isDirty = true;
        RefreshSummary();
    }

    // ---------------- 实时汇总与提示 ----------------

    private void RefreshSummary()
    {
        var s = ReportLayoutBuilder.ComputeSummary(Days);

        SummaryRows.Clear();
        SummaryRows.Add(new SummaryItem("实际上班天数(天)", s.ActualWorkDays.ToString("0.0")));
        SummaryRows.Add(new SummaryItem("项目出勤天数(天)", s.ProjectAttendanceDays.ToString("0.00")));
        SummaryRows.Add(new SummaryItem("总工作时长(h)", s.TotalWorkHours.ToString("0.00")));
        SummaryRows.Add(new SummaryItem("总加班时长(h)", s.TotalOvertimeHours.ToString("0.00")));
        SummaryRows.Add(new SummaryItem("当月累计可调休天数(天)", s.AccruedRestDays.ToString("0.00")));
        SummaryRows.Add(new SummaryItem("当月已调休天数(天)", s.UsedRestDays.ToString("0.0")));

        LocationSummaries.Clear();
        foreach (var row in ReportLayoutBuilder.ComputeLocationSummary(Days))
            LocationSummaries.Add(row);

        var noWork = Days.Count(d => d.Location.Length > 0 && !d.ContainsRest && d.WorkHours <= 0m);
        var noLocation = Days.Count(d => d.HasFilledTime && d.Location.Length == 0);
        var warn = (noWork, noLocation) switch
        {
            (0, 0) => "",
            ( > 0, 0) => $"提示：{noWork} 行填写了地点但没有有效工时",
            (0, > 0) => $"提示：{noLocation} 行填写了时间但没有地点",
            _ => $"提示：{noWork} 行有地点无工时；{noLocation} 行有时间无地点",
        };

        // 节假日数据缺失属于严重问题（会让加班/出勤算错），单独前置告警并标红
        var parts = new[] { _holidayNotice, _notice, DraftService.StorageFailure ?? "", warn }
            .Where(x => x.Length > 0);
        WarningText = string.Join("；", parts);
        WarningBrush = _holidayUnreliable || DraftService.StorageFailure is not null
            ? CriticalWarningBrush
            : NormalWarningBrush;
    }

    // ---------------- 导出 ----------------

    /// <summary>导出前校验：不满足时返回阻止导出的原因列表。</summary>
    private List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (_holidayUnreliable)
            errors.Add(_holidayNotice +
                "。此时导出的加班时长与项目出勤可能不符合实际，因此已禁止导出；" +
                "请连接网络后重新打开本页（或联系维护人员在程序中内置该年节假日数据）。");

        if (string.IsNullOrWhiteSpace(_profile.EmployeeName))
            errors.Add("请在窗口顶部填写姓名");

        var invalidDates = Days
            .Where(d => d.HasInvalidTime)
            .Select(d => d.Date.ToString("MM-dd"))
            .ToList();
        if (invalidDates.Count > 0)
            errors.Add($"以下日期的时间填写有误（格式应为 HH:mm）：{string.Join("、", invalidDates)}");

        return errors;
    }

    private ReportMeta BuildMeta() => new()
    {
        Year = Year,
        Month = Month,
        EmployeeName = _profile.EmployeeName.Trim(),
        Level = _profile.Level,
    };

    private async Task ExportAsync()
    {
        var errors = GetValidationErrors();
        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), "无法导出",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var meta = BuildMeta();
        var snapshot = Days.ToList();
        SaveDraftNow();

        SetBusy(true, "正在导出，请稍候……");
        try
        {
            // 渲染 XLSX 实测约 0.45 秒（30 行），放到线程池执行，避免 UI 假死
            await ExportHelper.ExportWithDialogAsync(ReportExporter.DefaultFileName(meta),
                path => ReportExporter.Export(meta, snapshot, path));
        }
        catch (ExportReportedException)
        {
            // 失败已在 ExportHelper 内提示并记日志
        }
        catch (Exception ex)
        {
            ExportHelper.ReportError(ex);
        }
        finally
        {
            SetBusy(false, "");
        }
    }

    private bool _isBusy;

    /// <summary>导出中：禁用导出/清空按钮并给出提示。</summary>
    private bool IsBusy => _isBusy;

    private void SetBusy(bool busy, string text)
    {
        _isBusy = busy;
        BusyText = text;
        (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // ---------------- 清空与保存 ----------------

    private void ClearAll()
    {
        if (MessageBox.Show($"确定清空 {Year} 年 {Month} 月的全部填写内容？", "清空表单",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        // 整表重建：移除所有行并生成当月空行，绑定随新行重新建立，
        // 不依赖 ComboBox SelectedItem 的“源→目标”刷新（该链路在部分场景下不触发）。
        foreach (var d in Days)
            d.PropertyChanged -= OnEntryChanged;
        Days.Clear();
        for (var day = 1; day <= DateTime.DaysInMonth(Year, Month); day++)
            Attach(new DailyEntry { Date = new DateOnly(Year, Month, day) });

        _isDirty = true;
        RefreshSummary();
    }

    /// <summary>清空某条记录的上/下班时间：以整行替换方式刷新绑定，保证界面同步。</summary>
    public void ClearEntryTime(DailyEntry entry, bool isStart)
    {
        var index = Days.IndexOf(entry);
        if (index < 0) return;

        var replacement = new DailyEntry
        {
            Date = entry.Date,
            Location = entry.Location,
            WorkContent = entry.WorkContent,
        };
        if (isStart)
            replacement.EndTimeText = entry.EndTimeText;    // 保留下班时间
        else
            replacement.StartTimeText = entry.StartTimeText; // 保留上班时间

        entry.PropertyChanged -= OnEntryChanged;
        Days[index] = replacement;
        replacement.PropertyChanged += OnEntryChanged;

        _isDirty = true;
        RefreshSummary();
    }

    private void SaveDraftNow()
    {
        if (!_isDirty) return;
        DraftService.SaveDraft(new DraftData { Year = Year, Month = Month, Days = Days.ToList() });
        _isDirty = false;
        RefreshSummary(); // 把可能出现的保存失败提示反映到界面
    }

    /// <summary>窗口关闭时调用：保存草稿。</summary>
    public void Shutdown() => SaveDraftNow();

    /// <summary>崩溃/退出路径调用：无论是否有改动都强制落盘。</summary>
    public void FlushDraft()
    {
        if (!_isDirty) return;
        // 忽略 StorageFailure 以免在崩溃路径上再抛：DraftService 内部已记录
        DraftService.SaveDraft(new DraftData { Year = Year, Month = Month, Days = Days.ToList() });
        _isDirty = false;
    }
}
