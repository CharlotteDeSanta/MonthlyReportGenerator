using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using MonthlyReportGenerator.Models;
using MonthlyReportGenerator.Services;

namespace MonthlyReportGenerator.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// 底部统计表格的一行（指标名 + 显示值）
public sealed record SummaryItem(string Name, string Value);

public class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _saveTimer;
    private (int Year, int Month) _loaded = (0, 0);
    private string _notice = "";
    private bool _isDirty;
    private bool _suppressDirty;

    public ObservableCollection<DailyEntry> Days { get; } = new();

    /// 底部“月度汇总统计”表格行
    public ObservableCollection<SummaryItem> SummaryRows { get; } = new();

    /// 底部“项目地点汇总”表格行
    public ObservableCollection<LocationSummaryRow> LocationSummaries { get; } = new();

    public int[] Years { get; }
    public int[] Months { get; } = Enumerable.Range(1, 12).ToArray();
    public EngineerLevel[] Levels { get; } = Enum.GetValues<EngineerLevel>();

    /// 时间选择-小时列表（00–23，支持跨午夜班次；用列尾“×”按钮清空）
    public IReadOnlyList<string> HourOptions { get; } = BuildRange(0, 23).ToList();

    /// 时间选择-分钟列表（00–59，1 分钟一档）
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

    private string _employeeName = "";
    public string EmployeeName
    {
        get => _employeeName;
        set
        {
            if (Set(ref _employeeName, value))
            {
                OnPropertyChanged(nameof(TitlePreview));
                SaveProfileNow();
            }
        }
    }

    private EngineerLevel _level = EngineerLevel.新进工程师;
    public EngineerLevel Level
    {
        get => _level;
        set { if (Set(ref _level, value)) SaveProfileNow(); }
    }

    public string TitlePreview => $"{Year}年AGV项目工作月报";

    private string _warningText = "";
    public string WarningText
    {
        get => _warningText;
        private set => Set(ref _warningText, value);
    }

    public ICommand ExportCsvCommand { get; }
    public ICommand ExportXlsxCommand { get; }
    public ICommand ClearCommand { get; }

    public MainViewModel()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        _year = today.Year;
        _month = today.Month;
        Years = Enumerable.Range(2026, 10).ToArray(); // 2026–2035

        ExportCsvCommand = new RelayCommand(() => Export(ExportFormat.Csv));
        ExportXlsxCommand = new RelayCommand(() => Export(ExportFormat.Xlsx));
        ClearCommand = new RelayCommand(ClearAll);

        var profile = DraftService.LoadProfile();
        if (profile is not null)
        {
            _employeeName = profile.EmployeeName;
            _level = profile.Level;
        }

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

    /// 确保当年节假日数据可用（内置表→本地缓存→网络），加载后刷新行内派生值与汇总
    private async Task EnsureHolidaysAsync()
    {
        var year = Year;
        await HolidayService.EnsureYearAsync(year);
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
        RefreshSummary();
    }

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
        WarningText = string.Join("；", new[] { _notice, warn }.Where(x => x.Length > 0));
    }

    // ---------------- 导出 ----------------

    private List<string> GetValidationErrors()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(EmployeeName))
            errors.Add("请填写姓名");

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
        EmployeeName = EmployeeName.Trim(),
        Level = Level,
    };

    private void Export(ExportFormat format)
    {
        var errors = GetValidationErrors();
        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), "无法导出",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var meta = BuildMeta();
        var dialog = new SaveFileDialog
        {
            Title = format == ExportFormat.Csv ? "导出 CSV" : "导出 XLSX",
            Filter = format == ExportFormat.Csv
                ? "CSV 文件 (*.csv)|*.csv"
                : "Excel 工作簿 (*.xlsx)|*.xlsx",
            FileName = ReportExporter.DefaultFileName(meta, format),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            SaveProfileNow();
            SaveDraftNow();
            ReportExporter.Export(meta, Days.ToList(), format, dialog.FileName);
            MessageBox.Show($"已导出：{dialog.FileName}", "导出成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // 完整异常写入日志文件，便于定位（弹窗不可复制）
            try
            {
                var logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MonthlyReportGenerator");
                Directory.CreateDirectory(logFolder);
                var logPath = Path.Combine(logFolder, "export-error.log");
                File.AppendAllText(logPath,
                    $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===={Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");

                MessageBox.Show(
                    $"导出失败：{ex.Message}{Environment.NewLine}{Environment.NewLine}详细信息已写入：{logPath}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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
    }

    private void SaveProfileNow()
    {
        DraftService.SaveProfile(new ProfileData { EmployeeName = EmployeeName, Level = Level });
    }

    /// 窗口关闭时调用：保存草稿与配置
    public void Shutdown()
    {
        SaveDraftNow();
        SaveProfileNow();
    }
}
