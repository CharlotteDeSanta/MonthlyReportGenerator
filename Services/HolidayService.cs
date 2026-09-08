using System.IO;
using System.Net.Http;
using System.Text.Json;
using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

/// <summary>
/// 节假日数据加载（与原网页版行为一致）：
/// 1. 内置表（2025/2026）→ 2. 本地缓存（%LocalAppData%\MonthlyReportGenerator\holidays-{年}.json）
/// → 3. 网络获取 https://timor.tech/api/holiday/year/{year} 并写入缓存。
/// 获取失败时保持“仅周末”回退，不影响使用。
/// </summary>
public static class HolidayService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task EnsureYearAsync(int year)
    {
        // 内置年份直接使用内置表；其余年份每次启动尝试网络刷新（与原网页版一致），失败回退本地缓存
        if (WorkCalendar.IsBuiltIn(year)) return;

        string[] holidays = Array.Empty<string>();
        string[] workdays = Array.Empty<string>();

        try
        {
            var json = await Client.GetStringAsync($"https://timor.tech/api/holiday/year/{year}");
            Parse(json, out holidays, out workdays);
            SaveCache(year, json);
        }
        catch
        {
            // 网络失败：回退本地缓存（等价于网页版的 localStorage）
            if (!TryLoadCache(year, out holidays, out workdays))
                return; // 无缓存：保持“仅周末”回退
        }

        WorkCalendar.Register(year, holidays, workdays);
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
            // 缓存写入失败不致命
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
