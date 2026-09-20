using System.Globalization;
using System.Windows.Data;

namespace MonthlyReportGenerator.ViewModels;

/// <summary>主窗口壳：公共配置 + 三个标签页（懒加载切换）。</summary>
public class ShellViewModel : ObservableObject
{
    public ProfileViewModel Profile { get; } = new();

    private MonthlyReportViewModel? _monthly;
    private DailyReportViewModel? _daily;
    private WeeklyReportViewModel? _weekly;

    public MonthlyReportViewModel Monthly => _monthly ??= new MonthlyReportViewModel(Profile);
    public DailyReportViewModel Daily => _daily ??= new DailyReportViewModel(Profile);
    public WeeklyReportViewModel Weekly => _weekly ??= new WeeklyReportViewModel(Profile);

    private int _selectedTab;
    public int SelectedTab
    {
        get => _selectedTab;
        set { if (Set(ref _selectedTab, value)) OnPropertyChanged(nameof(CurrentPage)); }
    }

    /// <summary>当前页 ViewModel（ContentControl + DataTemplate 按类型映射视图）。</summary>
    public object CurrentPage => SelectedTab switch
    {
        0 => Monthly,
        1 => Daily,
        _ => Weekly,
    };

    public ShellViewModel()
    {
        Profile.Load();
    }

    public void Shutdown()
    {
        _monthly?.Shutdown();
        _daily?.Shutdown();
        _weekly?.Shutdown();
        Profile.Save();
    }
}

/// <summary>Tab 切换转换器：SelectedTab == 参数 时按钮选中；取消选中不改变当前页。</summary>
public class EqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? (parameter ?? Binding.DoNothing) : Binding.DoNothing;
}
