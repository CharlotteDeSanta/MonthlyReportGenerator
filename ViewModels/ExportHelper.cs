using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace MonthlyReportGenerator.ViewModels;

/// <summary>导出公共流程：保存对话框 → 写 XLSX → 成功提示 / 失败写日志。</summary>
public static class ExportHelper
{
    public static void ExportWithDialog(string defaultFileName, Action<string> writeTo)
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
            writeTo(dialog.FileName);
            MessageBox.Show($"已导出：{dialog.FileName}", "导出成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    /// <summary>完整异常写入日志并提示（弹窗文本不可复制）。</summary>
    public static void ReportError(Exception ex)
    {
        try
        {
            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonthlyReportGenerator");
            Directory.CreateDirectory(logFolder);
            var logPath = Path.Combine(logFolder, "export-error.log");
            File.AppendAllText(logPath,
                $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===={Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");

            MessageBox.Show(
                $"导出失败：{ex.Message}{Environment.NewLine}{Environment.NewLine}详细信息已写入：{logPath}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
