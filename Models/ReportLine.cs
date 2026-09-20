namespace MonthlyReportGenerator.Models;

/// <summary>报表行的角色，决定 XLSX 渲染时的样式。</summary>
public enum LineRole
{
    Title,          // 标题
    Meta,           // 键值元信息行
    DetailHeader,   // 明细表头
    Detail,         // 月报明细行
    DailyDone,      // 日报“今日完成”行（与下一行成对，日期/人员列向下合并）
    DailyPlan,      // 日报“明日计划”行
    WeeklyRow,      // 周报内容行（周一~周日）
    WeekThis,       // 周报“本周工作总结”区块首行（A 列向下合并 7 行）
    WeekNext,       // 周报“下周工作计划”区块首行
    Separator,      // 空行
    SummaryTitle,   // “月度汇总统计”标题行
    SummaryValues,  // 月度汇总数值行
    LocationTitle,  // “项目地点汇总”标题行
    LocationHeader, // 地点汇总表头
    LocationRow,    // 地点汇总数据行
}

/// <summary>
/// 报表的一行。XLSX 渲染器消费统一行序列，从结构上保证输出一致。
/// RestDay：休息日（周末/法定节假日，补班日除外），XLSX 渲染时用于日期格红底；
/// MergeDownCols / MergeDownCount：需要向下合并的列（1-based）与合并行数，由 XLSX 执行；
/// ColumnCount：本行实际列数（日报 5 列，月报/周报 8 列）。
/// </summary>
public sealed record ReportLine(
    LineRole Role,
    string[] Cells,
    bool RestDay = false,
    int[]? MergeDownCols = null,
    int MergeDownCount = 1,
    int ColumnCount = 8);
