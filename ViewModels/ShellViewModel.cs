using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace MonthlyReportGenerator.ViewModels;

/// <summary>
/// 主窗口壳：公共配置 + 三个标签页。
/// 页面视图常驻（XAML 中三个视图始终存在，仅切换 Visibility，避免每次切换重建视觉树），
/// 页面 VM 首次需要时创建；其余页面在空闲时段预构建，消除首次切换卡顿。
/// </summary>
public class ShellViewModel : ObservableObject
{
    public ProfileViewModel Profile { get; } = new();

    private MonthlyReportViewModel? _monthly;
    private DailyReportViewModel? _daily;
    private WeeklyReportViewModel? _weekly;

    /// <summary>月报页 VM（视图 DataContext 绑定）。</summary>
    public MonthlyReportViewModel? MonthlyPage
    {
        get => _monthly;
        private set => Set(ref _monthly, value);
    }

    /// <summary>日报页 VM。</summary>
    public DailyReportViewModel? DailyPage
    {
        get => _daily;
        private set => Set(ref _daily, value);
    }

    /// <summary>周报页 VM。</summary>
    public WeeklyReportViewModel? WeeklyPage
    {
        get => _weekly;
        private set => Set(ref _weekly, value);
    }

    private int _selectedTab;
    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (Set(ref _selectedTab, value))
                EnsurePageForTab(value);
        }
    }

    public ShellViewModel()
    {
        Profile.Load();
        // 默认标签（月报）立即构建
        MonthlyPage = new MonthlyReportViewModel(Profile);
    }

    /// <summary>确保对应标签页的 VM 已创建（懒创建 + 常驻）。</summary>
    private void EnsurePageForTab(int tab)
    {
        switch (tab)
        {
            case 0 when _monthly is null:
                MonthlyPage = new MonthlyReportViewModel(Profile);
                break;
            case 1 when _daily is null:
                DailyPage = new DailyReportViewModel(Profile);
                break;
            default:
                if (_weekly is null)
                    WeeklyPage = new WeeklyReportViewModel(Profile);
                break;
        }
    }

    /// <summary>空闲时段分帧预构建其余页面，消除首次点击标签的顿挫。</summary>
    public void PreloadPages(Dispatcher dispatcher)
    {
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            EnsurePageForTab(1); // 日报
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                EnsurePageForTab(2); // 周报
            }));
        }));
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

/// <summary>标签页显隐转换器：SelectedTab == 参数 → Visible，否则 Collapsed。</summary>
public class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
