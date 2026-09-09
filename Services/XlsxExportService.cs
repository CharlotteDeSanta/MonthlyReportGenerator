using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using MonthlyReportGenerator.Models;

namespace MonthlyReportGenerator.Services;

public static class XlsxExportService
{
    /// <summary>
    /// 把报表行序列写成 XLSX。内容与 CSV 完全一致（同一份行序列），
    /// 数值列写成真数值并套用与 CSV 相同的显示格式，行角色决定样式。
    /// </summary>
    public static void Write(string path, IReadOnlyList<ReportLine> lines)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("月报");

        var row = 1;
        foreach (var line in lines)
        {
            WriteLine(sheet, row, line);
            row++;
        }

        sheet.Column(1).Width = 12;  // 日期
        sheet.Column(2).Width = 14;  // 上班地点
        sheet.Column(3).Width = 46;  // 工作内容
        sheet.Column(4).Width = 10;  // 上班时间
        sheet.Column(5).Width = 10;  // 下班时间
        sheet.Column(6).Width = 11;  // 工作时长
        sheet.Column(7).Width = 11;  // 加班时长
        sheet.Column(8).Width = 10;  // 项目出勤
        sheet.SheetView.FreezeRows(3); // 冻结标题/元信息/表头

        workbook.SaveAs(path);
    }

    private static void WriteLine(IXLWorksheet sheet, int row, ReportLine line)
    {
        for (var col = 0; col < line.Cells.Length; col++)
            WriteCell(sheet, row, col + 1, line.Cells[col], GetNumberFormat(line.Role, col + 1));

        ApplyStyle(sheet, row, line.Role);
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
            // 对 null 赋 .Format 会 NRE；此时退化为按格式化文本写入，保证显示与 CSV 一致。
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

    /// <summary>各列显示格式，与 CSV 里的文本格式保持一致。</summary>
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

    private static void ApplyStyle(IXLWorksheet sheet, int row, LineRole role)
    {
        switch (role)
        {
            case LineRole.Title:
                sheet.Range(row, 1, row, ReportLayoutBuilder.ColumnCount).Merge();
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
                StyleRow(sheet, row, bold: true, fill: "#D9E1F2", center: true, border: true);
                break;

            case LineRole.Detail:
                StyleRow(sheet, row, bold: false, fill: null, center: false, border: true);
                foreach (var col in new[] { 1, 4, 5, 6, 7, 8 })
                    Center(sheet, row, col);
                // 工作内容支持 Enter 换行（多行文本自动换行显示）
                sheet.Cell(row, 3).Style.Alignment.WrapText = true;
                break;

            case LineRole.SummaryTitle:
            case LineRole.SummaryValues:
            case LineRole.LocationTitle:
                StyleRow(sheet, row, bold: true, fill: null, center: false, border: false);
                break;

            // Separator / Meta / LocationRow：默认样式即可
        }
    }

    /// <summary>逐单元格套用样式（避免对整行 Range 的样式操作，稳定性更高）。</summary>
    private static void StyleRow(IXLWorksheet sheet, int row, bool bold, string? fill, bool center, bool border)
    {
        for (var col = 1; col <= ReportLayoutBuilder.ColumnCount; col++)
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
}
