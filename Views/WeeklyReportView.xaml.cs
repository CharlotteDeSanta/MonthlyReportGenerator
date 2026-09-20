using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DataGrid = System.Windows.Controls.DataGrid;

namespace MonthlyReportGenerator.Views;

public partial class WeeklyReportView : UserControl
{
    public WeeklyReportView()
    {
        InitializeComponent();
    }

    /// <summary>单击即进入编辑（与月报页交互一致）。</summary>
    private void OnDataGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var cell = FindAncestor<DataGridCell>(source);
        if (cell is null || cell.IsEditing || cell.IsReadOnly) return;

        if (sender is DataGrid grid)
            grid.BeginEdit(e);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null && current is not T)
            current = VisualTreeHelper.GetParent(current);
        return current as T;
    }
}
