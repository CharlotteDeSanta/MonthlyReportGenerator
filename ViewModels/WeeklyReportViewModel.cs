using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MonthlyReportGenerator.Models;
using MonthlyReportGenerator.Services;

namespace MonthlyReportGenerator.ViewModels;

public class WeeklyReportViewModel : ObservableObject
{
    private readonly ProfileViewModel _profile;
    private readonly DispatcherTimer _saveTimer;
    private (int Year, int Week) _loaded = (0, 0);
    private string _notice = "";
    private bool _isDirty;

    public ObservableCollection<WeekDayEntry> ThisWeek { get; } = new();
    public ObservableCollection<WeekDayEntry> NextWeek { get; } = new();

    public int[] Years { get; } = Enumerable.Range(2026, 10).ToArray(); // 2026–2035

    public WeeklyReportMeta Meta { get; } = new();

    public string[] YesNoOptions { get; } = { "是", "否" };

    public ICommand ExportCommand { get; }
    public ICommand ClearCommand { get; }

    private int _year;
    public int Year
    {
        get => _year;
        set
        {
            if (Set(ref _year, value))
            {
                OnPropertyChanged(nameof(Weeks));
                OnPropertyChanged(nameof(WeekRangeText));
                ReloadWeek();
            }
        }
    }

    private int _week;
    public int Week
    {
        get => _week;
        set
        {
            if (Set(ref _week, value))
            {
                OnPropertyChanged(nameof(WeekRangeText));
                ReloadWeek();
            }
        }
    }

    /// <summary>可选周数（按年动态：52 或 53，ISO 周）。</summary>
    public int[] Weeks => Enumerable.Range(1, WeekHelper.LastIsoWeekOfYear(Year)).ToArray();

    public string WeekRangeText
    {
        get
        {
            var monday = WeekHelper.MondayOfIsoWeek(Year, Week);
            return $"{Year}-W{Week:D2}（{monday:MM-dd} ~ {monday.AddDays(6):MM-dd}）";
        }
    }

    /// <summary>填表日期（默认今天，导出写入“日期”字段）。</summary>
    public string ReportDateText => DateTime.Today.ToString("yyyy-MM-dd");

    /// <summary>“是否按上周计划完成”的文本形式（下拉绑定）。</summary>
    public string FollowedLastPlanText
    {
        get => Meta.FollowedLastPlan ? "是" : "否";
        set
        {
            var next = value == "是";
            if (Meta.FollowedLastPlan != next)
            {
                Meta.FollowedLastPlan = next;
                OnPropertyChanged();
            }
        }
    }

    private string _noticeText = "";
    public string NoticeText
    {
        get => _noticeText;
        private set => Set(ref _noticeText, value);
    }

    public WeeklyReportViewModel(ProfileViewModel profile)
    {
        _profile = profile;
        var today = DateOnly.FromDateTime(DateTime.Today);
        _year = today.Year;
        _week = WeekHelper.IsoWeekOf(today);
        Meta.Writer = profile.EmployeeName;
        Meta.ReportDate = today;
        Meta.PropertyChanged += (_, _) => _isDirty = true;

        ExportCommand = new RelayCommand(Export);
        ClearCommand = new RelayCommand(ClearAll);

        ReloadWeek();

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _saveTimer.Tick += (_, _) => SaveDraftNow();
        _saveTimer.Start();
    }

    private void ReloadWeek()
    {
        if ((Year, Week) == _loaded) return;
        SaveDraftNow();
        _loaded = (Year, Week);

        DetachAll(ThisWeek);
        DetachAll(NextWeek);
        ThisWeek.Clear();
        NextWeek.Clear();

        var monday = WeekHelper.MondayOfIsoWeek(Year, Week);
        var draft = DraftService.LoadWeeklyDraft(Year, Week);
        if (draft is not null && draft.ThisWeek.Count == 7 && draft.NextWeek.Count == 7)
        {
            Meta.ProjectName = draft.Meta.ProjectName;
            Meta.Writer = draft.Meta.Writer;
            Meta.FollowedLastPlan = draft.Meta.FollowedLastPlan;
            Meta.Unfinished = draft.Meta.Unfinished;
            foreach (var d in draft.ThisWeek) Attach(ThisWeek, d);
            foreach (var d in draft.NextWeek) Attach(NextWeek, d);
            _notice = $"已恢复 {Year}-W{Week:D2} 周报草稿";
        }
        else
        {
            for (var i = 0; i < 7; i++)
            {
                Attach(ThisWeek, new WeekDayEntry { DayIndex = i, Date = monday.AddDays(i) });
                Attach(NextWeek, new WeekDayEntry { DayIndex = i, Date = monday.AddDays(i) });
            }
            _notice = "";
        }

        OnPropertyChanged(nameof(FollowedLastPlanText));
        _isDirty = false;
        NoticeText = _notice;
    }

    private void Attach(ObservableCollection<WeekDayEntry> list, WeekDayEntry entry)
    {
        entry.PropertyChanged += OnEntryChanged;
        list.Add(entry);
    }

    private void DetachAll(ObservableCollection<WeekDayEntry> list)
    {
        foreach (var d in list) d.PropertyChanged -= OnEntryChanged;
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e) => _isDirty = true;

    private void Export()
    {
        SaveDraftNow();
        ExportHelper.ExportWithDialog(ReportExporter.WeeklyFileName(Year, Week),
            path => ReportExporter.ExportWeekly(Meta, ThisWeek.ToList(), NextWeek.ToList(), path));
    }

    private void ClearAll()
    {
        if (MessageBox.Show($"确定清空 {Year}-W{Week:D2} 周报的全部填写内容？", "清空表单",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DetachAll(ThisWeek);
        DetachAll(NextWeek);
        ThisWeek.Clear();
        NextWeek.Clear();

        var monday = WeekHelper.MondayOfIsoWeek(Year, Week);
        for (var i = 0; i < 7; i++)
        {
            Attach(ThisWeek, new WeekDayEntry { DayIndex = i, Date = monday.AddDays(i) });
            Attach(NextWeek, new WeekDayEntry { DayIndex = i, Date = monday.AddDays(i) });
        }

        _isDirty = true;
    }

    private void SaveDraftNow()
    {
        if (!_isDirty) return;
        DraftService.SaveWeeklyDraft(new WeeklyDraftData
        {
            Year = Year,
            Week = Week,
            Meta = Meta,
            ThisWeek = ThisWeek.ToList(),
            NextWeek = NextWeek.ToList(),
        });
        _isDirty = false;
    }

    /// <summary>窗口关闭时调用：保存草稿。</summary>
    public void Shutdown() => SaveDraftNow();
}
