namespace MonthlyReportGenerator.Models;

/// <summary>周报的某一天（本周/下周各 7 行，DayIndex：0=周一 … 6=周日）。</summary>
public class WeekDayEntry : BindableBase
{
    public int DayIndex { get; init; }
    public DateOnly Date { get; init; }

    public string WeekdayText => DayIndex switch
    {
        0 => "周一",
        1 => "周二",
        2 => "周三",
        3 => "周四",
        4 => "周五",
        5 => "周六",
        _ => "周日",
    };

    public string DateDisplay => $"{WeekdayText} {Date:MM-dd}";

    private string _category = "";
    public string Category
    {
        get => _category;
        set => SetField(ref _category, value ?? "");
    }

    private string _content = "";
    public string Content
    {
        get => _content;
        set => SetField(ref _content, value ?? "");
    }

    private string _stagePoints = "";
    public string StagePoints
    {
        get => _stagePoints;
        set => SetField(ref _stagePoints, value ?? "");
    }

    private string _coordination = "";
    public string Coordination
    {
        get => _coordination;
        set => SetField(ref _coordination, value ?? "");
    }

    private string _planFinishDate = "";
    public string PlanFinishDate
    {
        get => _planFinishDate;
        set => SetField(ref _planFinishDate, value ?? "");
    }

    private string _progress = "";
    public string Progress
    {
        get => _progress;
        set => SetField(ref _progress, value ?? "");
    }

    private string _remark = "";
    public string Remark
    {
        get => _remark;
        set => SetField(ref _remark, value ?? "");
    }
}

/// <summary>周报元信息。</summary>
public class WeeklyReportMeta : BindableBase
{
    private string _projectName = "";
    public string ProjectName
    {
        get => _projectName;
        set => SetField(ref _projectName, value ?? "");
    }

    /// <summary>填表日期（默认今天）。</summary>
    public DateOnly ReportDate { get; set; }

    private int _weekNumber;
    public int WeekNumber
    {
        get => _weekNumber;
        set => SetField(ref _weekNumber, value);
    }

    private string _writer = "";
    public string Writer
    {
        get => _writer;
        set => SetField(ref _writer, value ?? "");
    }

    private bool _followedLastPlan;
    public bool FollowedLastPlan
    {
        get => _followedLastPlan;
        set => SetField(ref _followedLastPlan, value);
    }

    private string _unfinished = "";
    public string Unfinished
    {
        get => _unfinished;
        set => SetField(ref _unfinished, value ?? "");
    }
}
