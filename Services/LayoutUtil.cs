namespace MonthlyReportGenerator.Services;

/// <summary>布局行构建辅助（供日报/周报构建器复用）。</summary>
internal static class LayoutUtil
{
    /// <summary>填充指定列数的单元格数组（未提供的位置填空串，绝不为 null）。</summary>
    public static string[] Row(int columnCount, params string[] values)
    {
        var cells = new string[columnCount];
        Array.Fill(cells, "");
        for (var i = 0; i < values.Length && i < columnCount; i++)
            cells[i] = values[i];
        return cells;
    }
}
