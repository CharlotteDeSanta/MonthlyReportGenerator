using System.Windows.Automation.Peers;

namespace MonthlyReportGenerator;

/// <summary>
/// 禁用自动化对等（AutomationPeer）的表格。
/// 根因：首次打开任何弹层（下拉框/菜单/ToolTip）后，WPF 的 Popup.ForceMsaaToUiaBridge
/// 会在无自动化客户端的情况下强制为整个可视树创建 AutomationPeer
/// （dotnet/wpf #5807），控件多的大表格因此卡顿（#9881）。
/// 官方尚未修复（"Future" 里程碑），社区验证有效的 workaround 即覆写
/// OnCreateAutomationPeer 返回 null（见 #9881 评论）。内部工具不需要屏幕阅读器支持。
/// </summary>
public class PerformanceDataGrid : Wpf.Ui.Controls.DataGrid
{
    protected override AutomationPeer OnCreateAutomationPeer() => null!;
}
