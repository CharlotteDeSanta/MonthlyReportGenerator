using MonthlyReportGenerator.Models;
using MonthlyReportGenerator.Services;

namespace MonthlyReportGenerator.ViewModels;

/// <summary>共享个人配置（姓名/工程师等级），窗口级，三个页面共用；变更即持久化。</summary>
public class ProfileViewModel : ObservableObject
{
    private string _employeeName = "";
    public string EmployeeName
    {
        get => _employeeName;
        set { if (Set(ref _employeeName, value)) Save(); }
    }

    private EngineerLevel _level = EngineerLevel.新进工程师;
    public EngineerLevel Level
    {
        get => _level;
        set { if (Set(ref _level, value)) Save(); }
    }

    public EngineerLevel[] Levels { get; } = Enum.GetValues<EngineerLevel>();

    public void Load()
    {
        var profile = DraftService.LoadProfile();
        if (profile is null) return;
        _employeeName = profile.EmployeeName;
        _level = profile.Level;
        OnPropertyChanged(nameof(EmployeeName));
        OnPropertyChanged(nameof(Level));
    }

    public void Save() =>
        DraftService.SaveProfile(new ProfileData { EmployeeName = EmployeeName, Level = Level });
}
