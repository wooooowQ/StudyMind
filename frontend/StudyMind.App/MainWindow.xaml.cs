using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StudyMind.App.Services;
using StudyMind.App.ViewModels;
using WinRT.Interop;

namespace StudyMind.App;

public sealed partial class MainWindow : Window
{
    private readonly AppWindow? _appWindow;
    private readonly LocalBackendService _backendService;
    private CyberNoteWindow? _cyberNoteWindow;
    private TrayIconController? _trayIcon;
    private bool _isRestoringNavigationSelection;
    private bool _isConfirmedClose;
    private bool _isCloseConfirmationOpen;
    private bool _hasPendingAiApiKey;

    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();

        Title = "StudyMind";
        _backendService = new LocalBackendService();
        ViewModel = new MainViewModel(new StudyMindApiClient(_backendService.BaseAddress));
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Root.DataContext = ViewModel;
        ShellNav.SelectedItem = TodayNav;
        _appWindow = GetAppWindow();
        if (_appWindow is not null)
        {
            _appWindow.Closing += AppWindow_Closing;
        }
        Closed += MainWindow_Closed;

        _ = StartBackendAndInitializeAsync();
    }

    private async Task StartBackendAndInitializeAsync()
    {
        ViewModel.IsBusy = true;
        ViewModel.StatusMessage = "正在启动本地后端…";
        ViewModel.FormMessage = "";

        try
        {
            await _backendService.EnsureReadyAsync();
        }
        catch (Exception ex)
        {
            var message = $"本地后端启动失败：{ex.Message}";
            ViewModel.StatusMessage = message;
            ViewModel.FormMessage = message;
            return;
        }
        finally
        {
            ViewModel.IsBusy = false;
        }

        await ViewModel.InitializeCommand.ExecuteAsync(null);
    }

    private AppWindow? GetAppWindow()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        DisposeCompanionSurfaces();
        _backendService.Dispose();
    }

    private void ShowCyberNote_Click(object sender, RoutedEventArgs e)
    {
        ShowCyberNote();
    }

    private void ShowCyberNote()
    {
        EnsureTrayIcon();

        if (_cyberNoteWindow is null)
        {
            _cyberNoteWindow = new CyberNoteWindow(ViewModel, ShowMainWindow);
        }

        _cyberNoteWindow.ShowNote();
    }

    private void HideCyberNote()
    {
        _cyberNoteWindow?.HideNote();
    }

    private void ShowMainWindow()
    {
        Activate();
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        _trayIcon = new TrayIconController(
            WindowNative.GetWindowHandle(this),
            DispatcherQueue,
            ShowCyberNote,
            HideCyberNote,
            ShowMainWindow,
            Close);
    }

    private void DisposeCompanionSurfaces()
    {
        if (_cyberNoteWindow is not null)
        {
            _cyberNoteWindow.CloseForAppExit();
            _cyberNoteWindow = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayouts(e.NewSize.Width);
    }

    private void UpdateResponsiveLayouts(double width)
    {
        var useWideLayout = width >= 900;

        SetResponsiveColumns(TodayLayout, TodayPrimaryColumn, TodaySecondaryColumn, TodayOutputPanel, useWideLayout, 0.86, 1.34);
        SetResponsiveColumns(CoursesLayout, CoursesPrimaryColumn, CoursesSecondaryColumn, CoursesListPanel, useWideLayout, 0.8, 1.25);
        SetResponsiveColumns(ScheduleLayout, SchedulePrimaryColumn, ScheduleSecondaryColumn, ScheduleListPanel, useWideLayout, 1.15, 0.9);
        SetResponsiveColumns(RecordsLayout, RecordsPrimaryColumn, RecordsSecondaryColumn, RecordsListPanel, useWideLayout, 1.1, 0.95);
        SetResponsiveColumns(ReviewLearningChartsLayout, ReviewLearningPrimaryColumn, ReviewLearningSecondaryColumn, CourseMinutesPanel, useWideLayout, 1.35, 1);
        SetResponsiveColumns(ReviewProgressLayout, ReviewProgressPrimaryColumn, ReviewProgressSecondaryColumn, CourseProgressPanel, useWideLayout, 1.35, 1);
        SetResponsiveColumns(SettingsLayout, SettingsPrimaryColumn, SettingsSecondaryColumn, SettingsSidePanel, useWideLayout, 1.05, 0.95);
        UpdateReviewToolbarLayout(width >= 760);
        UpdateReviewSummaryLayout(width >= 760);
    }

    private static void SetResponsiveColumns(
        Grid layout,
        ColumnDefinition primaryColumn,
        ColumnDefinition secondaryColumn,
        FrameworkElement secondaryPanel,
        bool useWideLayout,
        double primaryWeight,
        double secondaryWeight)
    {
        if (useWideLayout)
        {
            layout.ColumnSpacing = 18;
            primaryColumn.Width = new GridLength(primaryWeight, GridUnitType.Star);
            secondaryColumn.Width = new GridLength(secondaryWeight, GridUnitType.Star);
            Grid.SetRow(secondaryPanel, 0);
            Grid.SetColumn(secondaryPanel, 1);
            return;
        }

        layout.ColumnSpacing = 0;
        primaryColumn.Width = new GridLength(1, GridUnitType.Star);
        secondaryColumn.Width = new GridLength(0);
        Grid.SetRow(secondaryPanel, 1);
        Grid.SetColumn(secondaryPanel, 0);
    }

    private void UpdateReviewToolbarLayout(bool useWideLayout)
    {
        if (useWideLayout)
        {
            ReviewToolbarLayout.ColumnSpacing = 12;
            ReviewToolbarPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
            ReviewToolbarDaysColumn.Width = new GridLength(180);
            ReviewToolbarActionColumn.Width = GridLength.Auto;

            Grid.SetRow(ReviewRangeText, 0);
            Grid.SetColumn(ReviewRangeText, 0);
            Grid.SetColumnSpan(ReviewRangeText, 1);
            Grid.SetRow(ReviewDaysBox, 0);
            Grid.SetColumn(ReviewDaysBox, 1);
            Grid.SetColumnSpan(ReviewDaysBox, 1);
            Grid.SetRow(ReviewRefreshButton, 0);
            Grid.SetColumn(ReviewRefreshButton, 2);
            Grid.SetColumnSpan(ReviewRefreshButton, 1);
            return;
        }

        ReviewToolbarLayout.ColumnSpacing = 8;
        ReviewToolbarPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
        ReviewToolbarDaysColumn.Width = GridLength.Auto;
        ReviewToolbarActionColumn.Width = new GridLength(0);

        Grid.SetRow(ReviewRangeText, 0);
        Grid.SetColumn(ReviewRangeText, 0);
        Grid.SetColumnSpan(ReviewRangeText, 3);
        Grid.SetRow(ReviewDaysBox, 1);
        Grid.SetColumn(ReviewDaysBox, 0);
        Grid.SetColumnSpan(ReviewDaysBox, 1);
        Grid.SetRow(ReviewRefreshButton, 1);
        Grid.SetColumn(ReviewRefreshButton, 1);
        Grid.SetColumnSpan(ReviewRefreshButton, 2);
    }

    private void UpdateReviewSummaryLayout(bool useWideLayout)
    {
        if (useWideLayout)
        {
            ReviewSummaryLayout.ColumnSpacing = 14;
            ReviewSummaryPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
            ReviewSummarySecondaryColumn.Width = new GridLength(1, GridUnitType.Star);
            ReviewSummaryTertiaryColumn.Width = new GridLength(1, GridUnitType.Star);

            SetGridPosition(ReviewMinutesTile, 0, 0);
            SetGridPosition(ReviewProgressTile, 0, 1);
            SetGridPosition(ReviewRiskTile, 0, 2);
            return;
        }

        ReviewSummaryLayout.ColumnSpacing = 0;
        ReviewSummaryPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
        ReviewSummarySecondaryColumn.Width = new GridLength(0);
        ReviewSummaryTertiaryColumn.Width = new GridLength(0);

        SetGridPosition(ReviewMinutesTile, 0, 0);
        SetGridPosition(ReviewProgressTile, 1, 0);
        SetGridPosition(ReviewRiskTile, 2, 0);
    }

    private static void SetGridPosition(FrameworkElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
    }

    private async void ShellNav_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (_isRestoringNavigationSelection)
        {
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string page)
        {
            if (page == ViewModel.CurrentPage)
            {
                return;
            }

            if (HasPendingUnsavedChanges() && !await ConfirmDiscardChangesAsync())
            {
                SelectNavigationItem(ViewModel.CurrentPage);
                return;
            }

            DiscardCurrentPageChanges();
            ViewModel.Navigate(page);
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isConfirmedClose || !HasPendingUnsavedChanges())
        {
            return;
        }

        args.Cancel = true;
        if (_isCloseConfirmationOpen)
        {
            return;
        }

        _isCloseConfirmationOpen = true;
        try
        {
            if (await ConfirmDiscardChangesAsync("当前表单还有未保存的修改。关闭窗口会丢弃这些修改。"))
            {
                _isConfirmedClose = true;
                DiscardCurrentPageChanges();
                Close();
            }
        }
        finally
        {
            _isCloseConfirmationOpen = false;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            SelectNavigationItem(ViewModel.CurrentPage);
        }

        if (e.PropertyName == nameof(MainViewModel.FirstInvalidField) &&
            !string.IsNullOrWhiteSpace(ViewModel.FirstInvalidField))
        {
            FocusInvalidField(ViewModel.FirstInvalidField);
        }
    }

    private void SelectNavigationItem(string page)
    {
        _isRestoringNavigationSelection = true;
        try
        {
            ShellNav.SelectedItem = page switch
            {
                "courses" => CoursesNav,
                "schedule" => ScheduleNav,
                "records" => RecordsNav,
                "review" => ReviewNav,
                "settings" => SettingsNav,
                _ => TodayNav
            };
        }
        finally
        {
            _isRestoringNavigationSelection = false;
        }
    }

    private void FocusInvalidField(string field)
    {
        Control? target = field switch
        {
            "CourseName" => CourseNameBox,
            "TopicCourse" => TopicCourseBox,
            "TopicName" => TopicNameBox,
            "EventTitle" => EventTitleBox,
            "EventStart" => EventStartPicker,
            "EventEnd" => EventEndPicker,
            "RecordTopic" => RecordTopicBox,
            "RecordMinutes" => RecordMinutesBox,
            _ => null
        };

        target?.Focus(FocusState.Programmatic);
    }

    private async void DeleteCourse_Click(object sender, RoutedEventArgs e)
    {
        var name = ViewModel.SelectedCourse?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            ViewModel.StatusMessage = "请先选择课程。";
            ViewModel.FormMessage = "请先选择课程。";
            return;
        }

        if (await ConfirmDeleteAsync("删除课程", ViewModel.BuildCourseDeleteImpact()))
        {
            await ViewModel.DeleteCourseCommand.ExecuteAsync(null);
        }
    }

    private async void DeleteTopic_Click(object sender, RoutedEventArgs e)
    {
        var name = ViewModel.SelectedTopic?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            ViewModel.StatusMessage = "请先选择知识点。";
            ViewModel.FormMessage = "请先选择知识点。";
            return;
        }

        if (await ConfirmDeleteAsync("删除知识点", ViewModel.BuildTopicDeleteImpact()))
        {
            await ViewModel.DeleteTopicCommand.ExecuteAsync(null);
        }
    }

    private async void DeleteEvent_Click(object sender, RoutedEventArgs e)
    {
        var title = ViewModel.SelectedEvent?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            ViewModel.StatusMessage = "请先选择日程。";
            ViewModel.FormMessage = "请先选择日程。";
            return;
        }

        if (await ConfirmDeleteAsync("删除日程", ViewModel.BuildEventDeleteImpact()))
        {
            await ViewModel.DeleteEventCommand.ExecuteAsync(null);
        }
    }

    private async void SaveEvent_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EditingEventTitleConflicts())
        {
            if (!await ConfirmEventDuplicateTitleAsync())
            {
                return;
            }

            ViewModel.ApplyEventDuplicateTitleSuggestion();
        }

        await ViewModel.SaveEventCommand.ExecuteAsync(null);
    }

    private async void DeleteStudyRecord_Click(object sender, RoutedEventArgs e)
    {
        var record = ViewModel.SelectedStudyRecord;
        if (record is null)
        {
            ViewModel.StatusMessage = "请先选择学习记录。";
            ViewModel.FormMessage = "请先选择学习记录。";
            return;
        }

        if (await ConfirmDeleteAsync("删除学习记录", ViewModel.BuildStudyRecordDeleteImpact()))
        {
            await ViewModel.DeleteStudyRecordCommand.ExecuteAsync(null);
        }
    }

    private async Task<bool> ConfirmDeleteAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        return await ConfirmDiscardChangesAsync("当前表单还有未保存的修改。离开此页面会丢弃这些修改。");
    }

    private async Task<bool> ConfirmDiscardChangesAsync(string content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "放弃未保存的更改？",
            Content = content,
            PrimaryButtonText = "放弃更改",
            CloseButtonText = "继续编辑",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private bool HasPendingUnsavedChanges() =>
        ViewModel.HasUnsavedChanges || _hasPendingAiApiKey;

    private void DiscardCurrentPageChanges()
    {
        ViewModel.DiscardCurrentPageChanges();
        ClearPendingAiApiKey();
    }

    private void ClearPendingAiApiKey()
    {
        _hasPendingAiApiKey = false;
        ViewModel.AiApiKey = "";
        if (!string.IsNullOrEmpty(AiApiKeyBox.Password))
        {
            AiApiKeyBox.Password = "";
        }
    }

    private async Task ShowNoticeAsync(string title, string message)
    {
        ViewModel.StatusMessage = message;
        ViewModel.FormMessage = message;
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "知道了",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private async Task<bool> ConfirmEventDuplicateTitleAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "日程标题重复",
            Content = ViewModel.BuildEventTitleConflictMessage(),
            PrimaryButtonText = $"保存为“{ViewModel.EventDuplicateTitleSuggestion}”",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AiApiKey = AiApiKeyBox.Password;
        if (ViewModel.RequiresAiPrivacyConfirmation && !await ConfirmAiPrivacyAsync())
        {
            ClearPendingAiApiKey();
            return;
        }

        await ViewModel.SaveSettingsCommand.ExecuteAsync(null);
        if (string.IsNullOrWhiteSpace(ViewModel.AiApiKey))
        {
            ClearPendingAiApiKey();
        }
    }

    private void AiApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _hasPendingAiApiKey = !string.IsNullOrWhiteSpace(AiApiKeyBox.Password);
    }

    private async Task<bool> ConfirmAiPrivacyAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "启用 AI 建议？",
            Content = "启用后，今日状态文本和结构化学习摘要可能会发送到你配置的兼容 AI 接口。请确认你信任该接口和当前网络环境。",
            PrimaryButtonText = "确认启用",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
