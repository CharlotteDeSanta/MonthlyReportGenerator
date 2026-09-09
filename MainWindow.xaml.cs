using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonthlyReportGenerator.Models;
using MonthlyReportGenerator.ViewModels;
using Wpf.Ui.Controls;
using DataGrid = System.Windows.Controls.DataGrid;
using Button = System.Windows.Controls.Button;

namespace MonthlyReportGenerator;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Closing += (_, _) => _viewModel.Shutdown();
    }

    /// <summary>
    /// 单击即进入编辑（WPF DataGrid 默认需双击），让 ComboBox/TextBox 单元格单击即可交互。
    /// </summary>
    private void OnDataGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var cell = FindAncestor<DataGridCell>(source);
        if (cell is null || cell.IsEditing || cell.IsReadOnly) return;

        if (sender is DataGrid grid)
            grid.BeginEdit(e);
    }

    /// <summary>
    /// “×”按钮：第一次单击使该行进入编辑态，第二次单击触发清空该条记录的上/下班时间。
    /// 清空采用整行替换方式，保证界面与汇总同步刷新。
    /// </summary>
    private void OnClearTimeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not DailyEntry entry ||
            button.Tag is not string tag)
            return;

        _viewModel.ClearEntryTime(entry, tag == "start");
    }

    /// <summary>
    /// 时间下拉获得焦点且当前时间未填写时，自动预填默认时间（上班 09:00 / 下班 17:00），
    /// 与原网页版 input[type=time] 的 focus 行为一致。Tag 形如 "start:09" / "end:17"。
    /// </summary>
    private void OnTimeComboFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo ||
            combo.DataContext is not MonthlyReportGenerator.Models.DailyEntry entry ||
            combo.Tag is not string tag)
            return;

        var parts = tag.Split(':');
        if (parts.Length != 2) return;

        if (parts[0] == "start")
        {
            if (entry.StartTimeText.Length == 0) entry.StartHour = parts[1];
        }
        else
        {
            if (entry.EndTimeText.Length == 0) entry.EndHour = parts[1];
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null && current is not T)
            current = VisualTreeHelper.GetParent(current);
        return current as T;
    }
}
