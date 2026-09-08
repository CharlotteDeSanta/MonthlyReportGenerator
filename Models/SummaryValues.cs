namespace MonthlyReportGenerator.Models;

/// <summary>“月度汇总统计”的六个指标。</summary>
public readonly record struct SummaryValues(
    decimal ActualWorkDays,     // 实际上班天数(天)：≥8h 记 1、4~8h 记 0.5、<4h 记 0（排除纯调休/休息）
    decimal ProjectAttendanceDays, // 项目出勤天数(天)：Σ项目出勤
    decimal TotalWorkHours,     // 总工作时长(h)
    decimal TotalOvertimeHours, // 总加班时长(h)
    decimal AccruedRestDays,    // 当月累计可调休天数(天)：总加班÷8，两位小数（银行家舍入）
    decimal UsedRestDays);      // 当月已调休天数(天)：工作日纯调休 +1、混合调休 +0.5

/// <summary>“项目地点汇总”的一行。</summary>
public sealed record LocationSummaryRow(string Location, decimal AttendanceDays, decimal OvertimeHours);
