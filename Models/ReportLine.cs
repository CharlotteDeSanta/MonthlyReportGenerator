namespace MonthlyReportGenerator.Models;

/// <summary>报表行的角色，决定 XLSX 渲染时的样式。</summary>
public enum LineRole
{
    Title,          // 标题
    Meta,           // 姓名/等级/年份/月份 键值行
    DetailHeader,   // 明细表头
    Detail,         // 明细行（当月每天一行）
    Separator,      // 空行
    SummaryTitle,   // “月度汇总统计”标题行
    SummaryValues,  // 月度汇总数值行
    LocationTitle,  // “项目地点汇总”标题行
    LocationHeader, // 地点汇总表头
    LocationRow,    // 地点汇总数据行
}

/// <summary>
/// 报表的一行。CSV 与 XLSX 两个渲染器消费同一份行序列，
/// 从结构上保证两者内容完全一致。
/// RestDay：该行是否为休息日（周末/法定节假日，补班日除外），XLSX 渲染时用于红底高亮。
/// </summary>
public sealed record ReportLine(LineRole Role, string[] Cells, bool RestDay = false);
