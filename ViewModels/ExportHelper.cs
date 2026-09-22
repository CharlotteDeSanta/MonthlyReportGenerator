using System.IO;
using System.Windows;
using Microsoft.Win32;
using MonthlyReportGenerator.Services;

namespace MonthlyReportGenerator.ViewModels;

/// <summary>
/// 导出公共流程：保存对话框 → 后台写 XLSX → 成功提示 / 失败写日志。
///
/// 渲染 XLSX 是纯 CPU 密集型工作（实测月报 30 行约 0.45 秒，且随样式/合并单元格增长），
/// 因此取到文件路径后放到线程池执行，避免 UI 线程假死。
/// </summary>
public static class ExportHelper
{
    public static async Task ExportWithDialogAsync(string defaultFileName, Action<string> writeTo)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出 XLSX",
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            FileName = defaultFileName,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await Task.Run(() => writeTo(dialog.FileName));
            MessageBox.Show($"已导出：{dialog.FileName}", "导出成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            // 已经完整上报给用户并写入日志：用一个哨兵异常终止本次流程，
            // 避免调用方再次弹窗重复提示。
            throw new ExportReportedException(ex);
        }
    }

    /// <summary>完整异常写入日志并提示（弹窗文本不可复制）。</summary>
    public static void ReportError(Exception ex)
    {
        DraftService.WriteErrorLog("导出失败", ex);
        MessageBox.Show(
            $"导出失败：{ex.Message}{Environment.NewLine}{Environment.NewLine}详细信息已写入：{DraftService.ErrorLogPath}",
            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

/// <summary>导出的失败已经向用户上报并记入日志，调用方无需重复处理。</summary>
public sealed class ExportReportedException : Exception
{
    public ExportReportedException(Exception inner) : base(inner.Message, inner) { }
}
