using System.IO;
using System.Net.Http;
using System.Text.Json;
using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

/// <summary>节假日数据来源/状态。用于判断“当年的加班与出勤是否可信”。</summary>
public enum HolidayLoadStatus
{
    /// <summary>内置表（已随程序发布）。</summary>
    BuiltIn,

    /// <summary>网络获取成功（已写入本地缓存）。</summary>
    Network,

    /// <summary>网络失败，回退本地缓存。</summary>
    Cache,

    /// <summary>既非内置、网络也失败且无缓存 —— 该年只能按“仅周末”计算，加班/出勤可能不准。</summary>
    Unavailable,
}

/// <summary>某年节假日数据的加载结果。</summary>
public readonly record struct HolidayLoadResult(int Year, HolidayLoadStatus Status)
{
    /// <summary>数据的可信度是否足以导出报表。</summary>
    public bool IsReliable => Status != HolidayLoadStatus.Unavailable;

    /// <summary>面向用户的说明文本（Unavailable 时给出明确警告）。</summary>
    public string Describe() => Status switch
    {
        HolidayLoadStatus.BuiltIn => $"{Year} 年节假日：程序内置数据",
        HolidayLoadStatus.Network => $"{Year} 年节假日：已联网获取",
        HolidayLoadStatus.Cache => $"{Year} 年节假日：网络不可用，使用本地缓存",
        _ => $"{Year} 年法定节假日数据缺失（联网获取失败且无本地缓存），"
           + $"加班时长与项目出勤将按“仅周末”计算，结果可能不准",
    };
}

/// <summary>
/// 节假日数据加载（与原网页版行为一致）：
/// 1. 内置表（2025/2026）→ 2. 本地缓存（%LocalAppData%\MonthlyReportGenerator\holidays-{年}.json）
/// → 3. 网络获取 https://timor.tech/api/holiday/year/{year} 并写入缓存。
/// 三级全部落空时返回 <see cref="HolidayLoadStatus.Unavailable"/>，
/// 由调用方（月报页）显式告警并阻止导出，避免把错算的加班数据静默交付。
/// </summary>
public static class HolidayService
{
    /// <summary>
    /// 节假日接口客户端。internal 且非 readonly：便于自动化测试注入必定失败的客户端，
    /// 以确定性覆盖“离线且无缓存”这条最危险的降级路径。
    /// </summary>
    internal static HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };

    /// <summary>
    /// 确保某年节假日数据可用。失败不抛异常，而是通过返回值暴露状态，交由 UI 决策。
    /// </summary>
    public static async Task<HolidayLoadResult> EnsureYearAsync(int year)
    {
        // 内置年份直接使用内置表
        if (WorkCalendar.IsBuiltIn(year))
            return new HolidayLoadResult(year, HolidayLoadStatus.BuiltIn);

        // 本进程内已成功加载过（避免重复联网；失败过的年份不会被记住，下次仍会重试）
        if (WorkCalendar.HasData(year))
            return new HolidayLoadResult(year, HolidayLoadStatus.Cache);

        string[] holidays;
        string[] workdays;

        try
        {
            var json = await Client.GetStringAsync($"https://timor.tech/api/holiday/year/{year}");
            Parse(json, out holidays, out workdays);
            SaveCache(year, json);
            WorkCalendar.Register(year, holidays, workdays);
            return new HolidayLoadResult(year, HolidayLoadStatus.Network);
        }
        catch
        {
            // 网络失败：回退本地缓存（等价于网页版的 localStorage）
            if (TryLoadCache(year, out holidays, out workdays))
            {
                WorkCalendar.Register(year, holidays, workdays);
                return new HolidayLoadResult(year, HolidayLoadStatus.Cache);
            }
        }

        // 无内置、无缓存、无网络：明确告知不可信
        return new HolidayLoadResult(year, HolidayLoadStatus.Unavailable);
    }

    private static string CachePath(int year) =>
        Path.Combine(DraftService.Folder, $"holidays-{year}.json");

    private static bool TryLoadCache(int year, out string[] holidays, out string[] workdays)
    {
        holidays = Array.Empty<string>();
        workdays = Array.Empty<string>();
        try
        {
            var path = CachePath(year);
            if (!File.Exists(path)) return false;
            Parse(File.ReadAllText(path), out holidays, out workdays);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveCache(int year, string json)
    {
        try
        {
            Directory.CreateDirectory(DraftService.Folder);
            File.WriteAllText(CachePath(year), json);
        }
        catch
        {
            // 缓存写入失败不致命（本次运行已 Register，下次仍可重新联网获取）
        }
    }

    /// <summary>解析 timor.tech 返回的 JSON：holiday=true → 节假日，false → 补班日。</summary>
    private static void Parse(string json, out string[] holidays, out string[] workdays)
    {
        var h = new List<string>();
        var w = new List<string>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("holiday", out var map))
        {
            foreach (var prop in map.EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("date", out var dateEl) ||
                    !prop.Value.TryGetProperty("holiday", out var isHolidayEl))
                    continue;

                var date = dateEl.GetString();
                if (string.IsNullOrEmpty(date)) continue;

                if (isHolidayEl.GetBoolean()) h.Add(date);
                else w.Add(date);
            }
        }

        holidays = h.ToArray();
        workdays = w.ToArray();
    }
}
