using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MonthlyReportGenerator.Models;
using MonthlyReportGenerator.Services;

namespace MonthlyReportGenerator.ViewModels;

public class DailyReportViewModel : ObservableObject
{
    private readonly ProfileViewModel _profile;
    private readonly DispatcherTimer _saveTimer;
    private (int Year, int Month) _loaded = (0, 0);
    private string _notice = "";
    private bool _isDirty;

    public ObservableCollection<DailyDayEntry> Days { get; } = new();

    public int[] Years { get; }
    public int[] Months { get; } = Enumerable.Range(1, 12).ToArray();

    public ICommand ExportCommand { get; }
    public ICommand ClearCommand { get; }

    private int _year;
    public int Year
    {
        get => _year;
        set { if (Set(ref _year, value)) ReloadMonth(); }
    }

    private int _month;
    public int Month
    {
        get => _month;
        set { if (Set(ref _month, value)) ReloadMonth(); }
    }

    private string _noticeText = "";
    public string NoticeText
    {
        get => _noticeText;
        private set => Set(ref _noticeText, value);
    }

    public DailyReportViewModel(ProfileViewModel profile)
    {
        _profile = profile;
        var today = DateOnly.FromDateTime(DateTime.Today);
        _year = today.Year;
        _month = today.Month;
        Years = Enumerable.Range(2026, 10).ToArray(); // 2026–2035

        ExportCommand = new RelayCommand(Export);
        ClearCommand = new RelayCommand(ClearAll);

        ReloadMonth();

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _saveTimer.Tick += (_, _) => SaveDraftNow();
        _saveTimer.Start();
    }

    private void ReloadMonth()
    {
        if ((Year, Month) == _loaded) return;
        SaveDraftNow();
        _loaded = (Year, Month);

        foreach (var d in Days) d.PropertyChanged -= OnEntryChanged;
        Days.Clear();

        var daysInMonth = DateTime.DaysInMonth(Year, Month);
        var draft = DraftService.LoadDailyDraft(Year, Month);
        if (draft is not null && draft.Days.Count == daysInMonth)
        {
            foreach (var d in draft.Days) Attach(d);
            _notice = $"已恢复 {Year} 年 {Month} 月日报草稿";
        }
        else
        {
            var name = _profile.EmployeeName;
            for (var day = 1; day <= daysInMonth; day++)
                Attach(new DailyDayEntry
                {
                    Date = new DateOnly(Year, Month, day),
                    TechLeader = name,
                    Implementer = name,
                });
            _notice = "";
        }

        _isDirty = false;
        NoticeText = _notice;
    }

    private void Attach(DailyDayEntry entry)
    {
        entry.PropertyChanged += OnEntryChanged;
        Days.Add(entry);
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e) => _isDirty = true;

    private void Export()
    {
        SaveDraftNow();
        ExportHelper.ExportWithDialog(ReportExporter.DailyFileName(Year, Month),
            path => ReportExporter.ExportDaily(Days.ToList(), path));
    }

    private void ClearAll()
    {
        if (MessageBox.Show($"确定清空 {Year} 年 {Month} 月的日报内容？", "清空表单",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        foreach (var d in Days) d.PropertyChanged -= OnEntryChanged;
        Days.Clear();
        var name = _profile.EmployeeName;
        for (var day = 1; day <= DateTime.DaysInMonth(Year, Month); day++)
            Attach(new DailyDayEntry
            {
                Date = new DateOnly(Year, Month, day),
                TechLeader = name,
                Implementer = name,
            });

        _isDirty = true;
    }

    private void SaveDraftNow()
    {
        if (!_isDirty) return;
        DraftService.SaveDailyDraft(new DailyDraftData { Year = Year, Month = Month, Days = Days.ToList() });
        _isDirty = false;
    }

    /// <summary>窗口关闭时调用：保存草稿。</summary>
    public void Shutdown() => SaveDraftNow();
}
