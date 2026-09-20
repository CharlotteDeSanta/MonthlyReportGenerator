using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

public static class XlsxExportService
{
    /// <summary>
    /// 把报表行序列写成 XLSX。数值列写成真数值并套用与模板一致的显示格式，
    /// 行角色决定样式，MergeDownCols/MergeDownCount 决定向下合并的单元格。
    /// </summary>
    public static void Write(string path, IReadOnlyList<ReportLine> lines)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("报表");

        var row = 1;
        foreach (var line in lines)
        {
            WriteLine(sheet, row, line);
            row++;
        }

        sheet.Column(1).Width = 12;  // 日期/类别
        sheet.Column(2).Width = 14;  // 星期/完成计划
        sheet.Column(3).Width = 46;  // 工作内容
        sheet.Column(4).Width = 12;  // 阶段要点/负责人
        sheet.Column(5).Width = 12;  // 需要协调的内容/人员
        sheet.Column(6).Width = 11;  // 计划完成时间
        sheet.Column(7).Width = 11;  // 进度状态
        sheet.Column(8).Width = 10;  // 备注
        sheet.SheetView.FreezeRows(3); // 冻结标题/元信息/表头

        workbook.SaveAs(path);
    }

    private static void WriteLine(IXLWorksheet sheet, int row, ReportLine line)
    {
        var columnCount = Math.Min(line.Cells.Length, line.ColumnCount);
        for (var col = 0; col < columnCount; col++)
            WriteCell(sheet, row, col + 1, line.Cells[col], GetNumberFormat(line.Role, col + 1));

        ApplyStyle(sheet, row, line);

        // 休息日（周末/法定节假日，补班日除外）：仅日期格标红
        if (line.RestDay)
        {
            var style = sheet.Cell(row, 1).Style;
            if (style is not null)
                style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
        }

        // 向下合并（与模板一致）
        if (line.MergeDownCols is { Length: > 0 } mergeCols && line.MergeDownCount > 1)
        {
            foreach (var col in mergeCols)
            {
                if (col >= 1 && col <= line.ColumnCount)
                    sheet.Range(row, col, row + line.MergeDownCount - 1, col).Merge();
            }
        }
    }

    private static void WriteCell(IXLWorksheet sheet, int row, int col, string text, string? numberFormat)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (numberFormat is not null &&
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            var cell = sheet.Cell(row, col);
            cell.Value = value;

            // ClosedXML 0.105 在“未物化样式”的单元格上 Style.NumberFormat 可能为 null，
            // 对 null 赋 .Format 会 NRE；此时退化为按格式化文本写入，保证显示与模板一致。
            if (cell.Style.NumberFormat is { } numberFormatStyle)
            {
                numberFormatStyle.Format = numberFormat;
            }
            else
            {
                cell.Value = text;
            }
        }
        else
        {
            sheet.Cell(row, col).Value = text;
        }
    }

    /// <summary>各列显示格式，与模板里的文本格式保持一致。</summary>
    private static string? GetNumberFormat(LineRole role, int col) => role switch
    {
        LineRole.Meta when col is 5 or 7 => "0",
        LineRole.Detail when col is 6 or 7 or 8 => "0.00",
        LineRole.SummaryValues when col == 3 => "0.0",
        LineRole.SummaryValues when col is 4 or 5 or 6 or 7 => "0.00",
        LineRole.SummaryValues when col == 8 => "0.0",
        LineRole.LocationRow when col == 2 => "0.0",
        LineRole.LocationRow when col == 3 => "0.00",
        _ => null,
    };

    private static void ApplyStyle(IXLWorksheet sheet, int row, ReportLine line)
    {
        var columnCount = line.ColumnCount;

        switch (line.Role)
        {
            case LineRole.Title:
                sheet.Range(row, 1, row, columnCount).Merge();
                var title = sheet.Cell(row, 1);
                if (title.Style is { } titleStyle)
                {
                    titleStyle.Font.SetBold();
                    titleStyle.Font.FontSize = 14;
                    titleStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    titleStyle.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
                sheet.Row(row).Height = 26;
                break;

            case LineRole.DetailHeader:
            case LineRole.LocationHeader:
                StyleRow(sheet, row, columnCount, bold: true, fill: "#D9E1F2", center: true, border: true);
                break;

            case LineRole.Detail:
                StyleRow(sheet, row, columnCount, bold: false, fill: null, center: false, border: true);
                foreach (var col in new[] { 1, 4, 5, 6, 7, 8 })
                    Center(sheet, row, col);
                // 工作内容支持 Enter 换行（多行文本自动换行显示）
                Wrap(sheet, row, 3);
                break;

            case LineRole.DailyDone:
            case LineRole.DailyPlan:
                StyleRow(sheet, row, columnCount, bold: false, fill: null, center: false, border: true);
                Center(sheet, row, 1);   // 日期居中
                Wrap(sheet, row, 3);     // 每日完成内容换行
                break;

            case LineRole.WeeklyRow:
                StyleRow(sheet, row, columnCount, bold: false, fill: null, center: false, border: true);
                Wrap(sheet, row, 3);     // 工作内容换行
                break;

            case LineRole.WeekThis:
            case LineRole.WeekNext:
                StyleRow(sheet, row, columnCount, bold: false, fill: null, center: false, border: true);
                var label = sheet.Cell(row, 1);
                if (label.Style is { } labelStyle)
                {
                    labelStyle.Font.SetBold();
                    labelStyle.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
                    labelStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    labelStyle.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
                break;

            case LineRole.SummaryTitle:
            case LineRole.SummaryValues:
            case LineRole.LocationTitle:
                StyleRow(sheet, row, columnCount, bold: true, fill: null, center: false, border: false);
                break;

            // Separator / Meta / LocationRow：默认样式即可
        }
    }

    /// <summary>逐单元格套用样式（避免对整行 Range 的样式操作，稳定性更高）。</summary>
    private static void StyleRow(IXLWorksheet sheet, int row, int columnCount, bool bold, string? fill, bool center, bool border)
    {
        for (var col = 1; col <= columnCount; col++)
        {
            var style = sheet.Cell(row, col).Style;
            if (style is null) continue; // 样式对象不可用时跳过（保险，避免 NRE）
            if (bold) style.Font.SetBold();
            if (fill is not null) style.Fill.BackgroundColor = XLColor.FromHtml(fill);
            if (center) style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (border) style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
    }

    private static void Center(IXLWorksheet sheet, int row, int col) =>
        sheet.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    private static void Wrap(IXLWorksheet sheet, int row, int col) =>
        sheet.Cell(row, col).Style.Alignment.WrapText = true;
}
