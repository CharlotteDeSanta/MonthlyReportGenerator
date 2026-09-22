using System.Windows;
using System.Windows.Threading;
using MonthlyReportGenerator.Services;
using MonthlyReportGenerator.ViewModels;

namespace MonthlyReportGenerator;

/// <summary>
/// 应用级生命周期：单实例互斥、全局异常兜底、退出前强制落盘。
///
/// 之所以需要"崩溃前落盘"：草稿是每 10 秒节流保存的，未落盘窗口最多 10 秒；
/// 一旦发生未捕获异常，进程直接终止会把这 10 秒内的填报内容一起带走。
/// 这里在任何未处理异常路径上先把草稿刷盘，再提示并退出。
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\MonthlyReportGenerator.SingleInstance";

    private Mutex? _singleInstance;
    private ShellViewModel? _shell;
    private bool _shuttingDown;

    /// <summary>启动入口：单实例检查 → 全局异常登记 → 手动创建主窗口。</summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // ---------- 单实例：两个进程同时编辑同一月份会互相覆盖草稿 ----------
        _singleInstance = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "月报生成工具已在运行。\r\n\r\n" +
                "为避免两个窗口同时编辑草稿互相覆盖，请不要重复启动；" +
                "请切换到已打开的窗口继续使用。",
                "程序已在运行", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // ---------- 全局异常兜底 ----------
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // StartupUri 已移除：这里手动创建，便于持有 ShellViewModel 以支持崩溃前落盘
        var window = new MainWindow();
        _shell = window.ViewModel;
        MainWindow = window;
        window.Show();
    }

    // ---------------- 未处理异常 ----------------

    /// <summary>UI 线程未处理异常：落盘 → 记日志 → 提示 → 退出（避免在不确定状态下继续运行）。</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true; // 阻止默认的进程崩溃，改由我们完成落盘与提示
        DraftService.WriteErrorLog("UI 线程未处理异常", e.Exception);

        var logPath = FlushDraftsAndReport(e.Exception, "程序遇到未预期的错误");

        TryShow(
            $"程序遇到未预期的错误，为避免数据不一致将立即退出。{Environment.NewLine}{Environment.NewLine}" +
            $"草稿已尽力保存。{Environment.NewLine}" +
            $"错误：{e.Exception.Message}{Environment.NewLine}{Environment.NewLine}" +
            $"详细信息已写入：{logPath}",
            "程序错误");

        ExitApplication();
    }

    /// <summary>非 UI 线程未处理异常：进程即将终止，至少把草稿刷盘并记日志。</summary>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            DraftService.WriteErrorLog("后台线程未处理异常", ex);
            try
            {
                _shell?.FlushDraftsAndProfile();
            }
            catch
            {
                // 已经在崩溃路径上，忽略二次异常
            }
        }
    }

    /// <summary>未观察的 Task 异常：不影响运行，仅记日志备查。</summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        DraftService.WriteErrorLog("未观察的异步任务异常", e.Exception);
        e.SetObserved();
    }

    // ---------------- 退出 ----------------

    /// <summary>窗口关闭：保存全部草稿。</summary>
    public void OnMainWindowClosed() => FlushDrafts();

    /// <summary>强制保存三个页面的草稿（不依赖 _isDirty 判断）并保存个人配置。</summary>
    public void FlushDrafts()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        try
        {
            _shell?.FlushDraftsAndProfile();
        }
        catch (Exception ex)
        {
            DraftService.WriteErrorLog("退出保存草稿失败", ex);
        }
    }

    /// <summary>崩溃路径：先刷盘，再返回日志路径。</summary>
    private string FlushDraftsAndReport(Exception ex, string context)
    {
        try
        {
            _shell?.FlushDraftsAndProfile();
        }
        catch (Exception saveEx)
        {
            DraftService.WriteErrorLog($"{context}（保存草稿时再次失败）", saveEx);
        }
        return DraftService.ErrorLogPath;
    }

    private void ExitApplication()
    {
        try
        {
            _singleInstance?.ReleaseMutex();
            _singleInstance?.Dispose();
            _singleInstance = null;
        }
        catch
        {
            // 忽略
        }
        Shutdown();
        Environment.Exit(0);
    }

    private static void TryShow(string message, string title)
    {
        try
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // 连提示都失败时静默退出，日志已写入
        }
    }
}
