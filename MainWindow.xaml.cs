using System.Windows;
using System.Windows.Threading;
using MonthlyReportGenerator.ViewModels;
using Wpf.Ui.Controls;

namespace MonthlyReportGenerator;

public partial class MainWindow : FluentWindow
{
    private readonly ShellViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new ShellViewModel();
        DataContext = _viewModel;
        Closing += (_, _) => _viewModel.Shutdown();
        Loaded += OnWindowLoaded;
    }

    /// <summary>
    /// 启动预热：打开并立即收起一次下拉框，把弹层的一次性开销（AutomationPeer 级联、
    /// 弹层窗口创建、样式与字形缓存）在启动阶段付清，避免用户第一次点开下拉时卡顿。
    /// </summary>
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            LevelCombo.IsDropDownOpen = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                LevelCombo.IsDropDownOpen = false;
            }));
        }));
    }
}
