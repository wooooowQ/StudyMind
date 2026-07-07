using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System.Globalization;

namespace StudyMind.App.ViewModels;

public sealed class CyberNoteViewModel : ObservableObject
{
    private const int PageCount = 4;

    private readonly CyberNoteSettings _settings;
    private readonly Action _saveSettings;
    private int _currentPageIndex;

    public CyberNoteViewModel(MainViewModel study, CyberNoteSettings settings, Action saveSettings)
    {
        Study = study;
        _settings = settings;
        _saveSettings = saveSettings;
        _currentPageIndex = Math.Clamp(settings.CurrentPageIndex, 0, PageCount - 1);
        PreviousPageCommand = new RelayCommand(PreviousPage);
        NextPageCommand = new RelayCommand(NextPage);
    }

    public MainViewModel Study { get; }

    public IRelayCommand PreviousPageCommand { get; }

    public IRelayCommand NextPageCommand { get; }

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            var normalized = ((value % PageCount) + PageCount) % PageCount;
            if (SetProperty(ref _currentPageIndex, normalized))
            {
                _settings.CurrentPageIndex = normalized;
                _saveSettings();
                NotifyPageChanged();
            }
        }
    }

    public Visibility PlanInputPageVisibility => PageVisibility(0);

    public Visibility AdvicePageVisibility => PageVisibility(1);

    public Visibility SchedulePageVisibility => PageVisibility(2);

    public Visibility TopicsPageVisibility => PageVisibility(3);

    public string PageTitle => CurrentPageIndex switch
    {
        0 => "计划",
        1 => "建议",
        2 => "日程",
        _ => "知识点"
    };

    public string PageSubtitle => CurrentPageIndex switch
    {
        0 => "",
        1 => "",
        2 => "",
        _ => ""
    };

    public string PageNumberText => $"{CurrentPageIndex + 1:00} / {PageCount:00}";

    public string PageDotsText => string.Join(" ", Enumerable.Range(0, PageCount).Select(index => index == CurrentPageIndex ? "●" : "○"));

    public string TodayStampText => DateTime.Now.ToString("yyyy / MM / dd  dddd", CultureInfo.GetCultureInfo("zh-CN"));

    public string NavigationHintText => "← / → 切换便签页";

    public void PreviousPage()
    {
        CurrentPageIndex--;
    }

    public void NextPage()
    {
        CurrentPageIndex++;
    }

    private Visibility PageVisibility(int pageIndex) =>
        CurrentPageIndex == pageIndex ? Visibility.Visible : Visibility.Collapsed;

    private void NotifyPageChanged()
    {
        OnPropertyChanged(nameof(PlanInputPageVisibility));
        OnPropertyChanged(nameof(AdvicePageVisibility));
        OnPropertyChanged(nameof(SchedulePageVisibility));
        OnPropertyChanged(nameof(TopicsPageVisibility));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(PageNumberText));
        OnPropertyChanged(nameof(PageDotsText));
    }
}
