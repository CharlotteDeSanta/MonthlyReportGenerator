using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

/// <summary>个人配置（跨月记忆）。</summary>
public class ProfileData
{
    public string EmployeeName { get; set; } = "";
    public EngineerLevel Level { get; set; } = EngineerLevel.新进工程师;
}

/// <summary>某年某月的草稿。</summary>
public class DraftData
{
    public int SchemaVersion { get; set; } = 1;
    public int Year { get; set; }
    public int Month { get; set; }
    public List<DailyEntry> Days { get; set; } = new();
}

/// <summary>某年某月的日报草稿。</summary>
public class DailyDraftData
{
    public int SchemaVersion { get; set; } = 1;
    public int Year { get; set; }
    public int Month { get; set; }
    public List<DailyDayEntry> Days { get; set; } = new();
}

/// <summary>某年某周的周报草稿。</summary>
public class WeeklyDraftData
{
    public int SchemaVersion { get; set; } = 1;
    public int Year { get; set; }
    public int Week { get; set; }
    public WeeklyReportMeta Meta { get; set; } = new();
    public List<WeekDayEntry> ThisWeek { get; set; } = new();
    public List<WeekDayEntry> NextWeek { get; set; } = new();
}

/// <summary>
/// 草稿与个人配置的本地 JSON 存取，位于 %LocalAppData%\MonthlyReportGenerator。
/// 草稿按“年-月”独立存档（draft-2026-09.json）。
///
/// 写入采用“先写临时文件 → 原子替换”策略：崩溃/断电不会留下被截断的 JSON，
/// 从而避免草稿静默损坏。写入失败不再被完全吞掉——<see cref="StorageFailure"/> 会记录
/// 最近一次失败信息，由 ViewModel 显示到界面并写入错误日志。
/// </summary>
public static class DraftService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>最近一次本地写入失败的信息（null = 无失败）。用于在界面上提示，而不是静默丢数据。</summary>
    public static string? StorageFailure { get; private set; }

    /// <summary>清除失败状态（成功写入后调用）。</summary>
    public static void ClearStorageFailure() => StorageFailure = null;

    private static string _folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonthlyReportGenerator");

    /// <summary>
    /// 本地数据目录。setter 仅用于自动化测试把数据目录重定向到沙箱，
    /// 从而在不触碰用户真实数据的前提下验证读写与失败路径。
    /// </summary>
    public static string Folder
    {
        get => _folder;
        set => _folder = value;
    }

    /// <summary>错误日志路径（导出异常与本地存储异常共用）。</summary>
    public static string ErrorLogPath => Path.Combine(Folder, "export-error.log");

    private static string ProfilePath => Path.Combine(Folder, "profile.json");

    private static string DraftPath(int year, int month) =>
        Path.Combine(Folder, $"draft-{year:D4}-{month:D2}.json");

    // ---------------- 个人配置 ----------------

    public static ProfileData? LoadProfile()
    {
        try
        {
            return File.Exists(ProfilePath)
                ? JsonSerializer.Deserialize<ProfileData>(File.ReadAllText(ProfilePath), JsonOptions)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveProfile(ProfileData profile)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            AtomicWrite(ProfilePath, JsonSerializer.Serialize(profile, JsonOptions));
            ClearStorageFailure();
        }
        catch (Exception ex)
        {
            StorageFailure = $"个人配置保存失败：{ex.Message}";
            WriteErrorLog("保存 profile.json 失败", ex);
        }
    }

    // ---------------- 月报草稿 ----------------

    public static DraftData? LoadDraft(int year, int month)
    {
        try
        {
            var path = DraftPath(year, month);
            if (!File.Exists(path)) return null;
            var draft = JsonSerializer.Deserialize<DraftData>(File.ReadAllText(path), JsonOptions);
            if (draft is null || draft.Year != year || draft.Month != month) return null;
            return draft;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveDraft(DraftData draft)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            AtomicWrite(DraftPath(draft.Year, draft.Month), JsonSerializer.Serialize(draft, JsonOptions));
            ClearStorageFailure();
        }
        catch (Exception ex)
        {
            StorageFailure = $"月报草稿保存失败（{draft.Year}-{draft.Month:D2}）：{ex.Message}";
            WriteErrorLog("保存月报草稿失败", ex);
        }
    }

    public static void DeleteDraft(int year, int month)
    {
        try
        {
            File.Delete(DraftPath(year, month));
        }
        catch (Exception ex)
        {
            StorageFailure = $"草稿删除失败：{ex.Message}";
            WriteErrorLog("删除月报草稿失败", ex);
        }
    }

    // ---------------- 日报 / 周报草稿 ----------------

    public static DailyDraftData? LoadDailyDraft(int year, int month) =>
        LoadJson<DailyDraftData>($"draft-daily-{year:D4}-{month:D2}.json");

    public static void SaveDailyDraft(DailyDraftData draft) =>
        SaveJson($"draft-daily-{draft.Year:D4}-{draft.Month:D2}.json", draft,
            $"日报草稿保存失败（{draft.Year}-{draft.Month:D2}）");

    public static WeeklyDraftData? LoadWeeklyDraft(int year, int week) =>
        LoadJson<WeeklyDraftData>($"draft-weekly-{year:D4}-W{week:D2}.json");

    public static void SaveWeeklyDraft(WeeklyDraftData draft) =>
        SaveJson($"draft-weekly-{draft.Year:D4}-W{draft.Week:D2}.json", draft,
            $"周报草稿保存失败（{draft.Year}-W{draft.Week:D2}）");

    private static T? LoadJson<T>(string fileName)
    {
        try
        {
            var path = Path.Combine(Folder, fileName);
            if (!File.Exists(path)) return default;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private static void SaveJson<T>(string fileName, T data, string failureLabel)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            AtomicWrite(Path.Combine(Folder, fileName), JsonSerializer.Serialize(data, JsonOptions));
            ClearStorageFailure();
        }
        catch (Exception ex)
        {
            StorageFailure = $"{failureLabel}：{ex.Message}";
            WriteErrorLog(failureLabel, ex);
        }
    }

    // ---------------- 原子写入与错误日志 ----------------

    /// <summary>
    /// 原子写入：先写同目录临时文件，再以覆盖方式移动到目标。
    /// 目标要么是上一版的完整内容，要么是新版的完整内容，不会出现被截断的 JSON。
    ///
    /// 刻意不使用 <c>File.Replace</c>：它要求目标文件具备 DELETE 权限并在失败时可能
    /// 把目标一并删除（实测在受控目录下会触发 UnauthorizedAccessException 且目标消失），
    /// 对“宁可保留旧草稿也不能丢”的场景风险更高。同卷的 Move(overwrite: true) 已足够原子。
    /// </summary>
    private static void AtomicWrite(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);

        try
        {
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // 替换失败时清理临时文件，避免残留 .tmp 干扰下次写入
            try { File.Delete(tmp); } catch { /* 忽略 */ }
            throw;
        }
    }

    /// <summary>把异常完整写入错误日志（界面弹窗文本不可复制，故保留全量堆栈）。</summary>
    public static void WriteErrorLog(string context, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(ErrorLogPath,
                $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} [{context}] ===={Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 日志写入失败无计可施，忽略
        }
    }
}
