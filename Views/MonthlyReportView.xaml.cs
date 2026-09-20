using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MonthlyReportGenerator.Models;
using MonthlyReportGenerator.ViewModels;
using DataGrid = System.Windows.Controls.DataGrid;

namespace MonthlyReportGenerator.Views;

public partial class MonthlyReportView : UserControl
{
    public MonthlyReportView()
    {
        InitializeComponent();
    }

    private MonthlyReportViewModel? Vm => DataContext as MonthlyReportViewModel;

    /// <summary>
    /// 单击即进入编辑（WPF DataGrid 默认需双击）。
    /// 时间列额外处理：进入编辑后立即弹出鼠标下方的时分下拉，实现“单击即可选择”。
    /// </summary>
    private void OnDataGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var cell = FindAncestor<DataGridCell>(source);
        if (cell is null || cell.IsEditing || cell.IsReadOnly) return;

        if (sender is not DataGrid grid) return;

        grid.BeginEdit(e);

        // 时间列：单击进入编辑后立即弹出鼠标下方的时分下拉（单击即可选择）
        if (cell.Column is DataGridTemplateColumn && cell.Column.Header is "上班时间" or "下班时间")
        {
            var point = e.GetPosition(grid);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                ComboBox? combo = null;
                VisualTreeHelper.HitTest(grid, null, hit =>
                {
                    if (hit.VisualHit is ComboBox hitCombo)
                    {
                        combo = hitCombo;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                }, new PointHitTestParameters(point));

                if (combo is not null)
                    combo.IsDropDownOpen = true;
            }));
        }
    }

    /// <summary>“×”按钮：单击即清空该条记录的上/下班时间（整行替换保证界面同步）。</summary>
    private void OnClearTimeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not DailyEntry entry ||
            button.Tag is not string tag)
            return;

        Vm?.ClearEntryTime(entry, tag == "start");
    }

    /// <summary>
    /// 时间下拉获得焦点且当前时间未填写时，自动预填默认时间（上班 09:00 / 下班 17:00），
    /// 与原网页版 input[type=time] 的 focus 行为一致。Tag 形如 "start:09" / "end:17"。
    /// </summary>
    private void OnTimeComboFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo ||
            combo.DataContext is not DailyEntry entry ||
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
