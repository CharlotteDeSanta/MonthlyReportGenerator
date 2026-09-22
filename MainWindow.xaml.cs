using System.Windows;
using System.Windows.Threading;
using MonthlyReportGenerator.ViewModels;
using Wpf.Ui.Controls;

namespace MonthlyReportGenerator;

public partial class MainWindow : FluentWindow
{
    /// <summary>供 App 在崩溃/退出路径上强制落盘使用。</summary>
    public ShellViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new ShellViewModel();
        DataContext = ViewModel;

        // 关闭：保存全部草稿 + 个人配置（FlushDrafts 内部保证只执行一次）
        Closing += (_, _) => (Application.Current as App)?.OnMainWindowClosed();

        Loaded += OnWindowLoaded;
    }

    /// <summary>
    /// 启动预热：打开并立即收起一次下拉框，把弹层的一次性开销（AutomationPeer 级联、
    /// 弹层窗口创建、样式与字形缓存）在启动阶段付清，避免用户第一次点开下拉时卡顿；
    /// 随后在空闲时段预构建日报/周报页面，消除首次点击标签的顿挫。
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

            ViewModel.PreloadPages(Dispatcher);
        }));
    }
}
