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
/// </summary>
public static class DraftService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonthlyReportGenerator");

    private static string ProfilePath => Path.Combine(Folder, "profile.json");

    private static string DraftPath(int year, int month) =>
        Path.Combine(Folder, $"draft-{year:D4}-{month:D2}.json");

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
            File.WriteAllText(ProfilePath, JsonSerializer.Serialize(profile, JsonOptions));
        }
        catch
        {
            // 本地保存失败不致命，忽略
        }
    }

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
            File.WriteAllText(DraftPath(draft.Year, draft.Month), JsonSerializer.Serialize(draft, JsonOptions));
        }
        catch
        {
        }
    }

    public static void DeleteDraft(int year, int month)
    {
        try
        {
            File.Delete(DraftPath(year, month));
        }
        catch
        {
        }
    }

    // ---------------- 日报 / 周报草稿 ----------------

    public static DailyDraftData? LoadDailyDraft(int year, int month) =>
        LoadJson<DailyDraftData>($"draft-daily-{year:D4}-{month:D2}.json");

    public static void SaveDailyDraft(DailyDraftData draft) =>
        SaveJson($"draft-daily-{draft.Year:D4}-{draft.Month:D2}.json", draft);

    public static WeeklyDraftData? LoadWeeklyDraft(int year, int week) =>
        LoadJson<WeeklyDraftData>($"draft-weekly-{year:D4}-W{week:D2}.json");

    public static void SaveWeeklyDraft(WeeklyDraftData draft) =>
        SaveJson($"draft-weekly-{draft.Year:D4}-W{draft.Week:D2}.json", draft);

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

    private static void SaveJson<T>(string fileName, T data)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, fileName), JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
        }
    }
}
