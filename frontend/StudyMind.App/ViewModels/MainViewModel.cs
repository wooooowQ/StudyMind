using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.UI.Xaml;
using StudyMind.App.Models;
using StudyMind.App.Services;

namespace StudyMind.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly StudyMindApiClient _apiClient;

    public ObservableCollection<CourseDto> Courses { get; } = [];
    public ObservableCollection<TopicDto> Topics { get; } = [];
    public ObservableCollection<EventDto> Events { get; } = [];
    public ObservableCollection<StudyRecordDto> StudyRecords { get; } = [];
    public ObservableCollection<RecommendedTaskDto> RecommendedTasks { get; } = [];
    public ObservableCollection<DailyMinutesDto> WeeklyStats { get; } = [];
    public ObservableCollection<CourseMinutesDto> CourseMinutes { get; } = [];
    public ObservableCollection<TopicProgressDto> TopicProgress { get; } = [];
    public ObservableCollection<UpcomingEventSummaryDto> UpcomingEvents { get; } = [];
    public ObservableCollection<EmotionTrendPointDto> EmotionTrend { get; } = [];

    private bool _isBusy;
    private string _currentPage = "today";
    private string _statusMessage = "正在连接后端…";
    private string _formMessage = "";
    private string _firstInvalidField = "";
    private string _courseNameError = "";
    private string _topicCourseError = "";
    private string _topicNameError = "";
    private string _eventTitleError = "";
    private string _eventDateError = "";
    private string _recordTopicError = "";
    private string _recordMinutesError = "";
    private string _todayDate = DateTime.Now.ToString("yyyy-MM-dd");
    private string _todayState = "";
    private string _todayEmotion = "暂无";
    private string _todayLearningState = "暂无";
    private string _todayEmotionDetails = "等待输入今日状态文本";
    private string _todayModelType = "rules-v2";
    private string _todayFallbackReason = "";
    private string _todayAdvice = "先录入课程、知识点和日程，再写下今天的学习状态。StudyMind 会把情绪、DDL 和近期学习记录合在一起，生成今天最适合启动的任务。";
    private string _newCourseName = "";
    private CourseDto? _selectedCourse;
    private CourseDto? _selectedTopicCourse;
    private CourseDto? _selectedEventCourse;
    private TopicDto? _selectedTopic;
    private EventDto? _selectedEvent;
    private EventDto? _selectedTopicEvent;
    private StudyRecordDto? _selectedStudyRecord;
    private string _newTopicName = "";
    private string _newTopicMastery = "未掌握";
    private string _newTopicStatus = "pending";
    private double _newTopicImportance = 3;
    private double _newTopicMinutes = 30;
    private string _newEventTitle = "";
    private string _newEventType = "考试";
    private string _newEventStart = DateTime.Now.AddDays(3).ToString("yyyy-MM-dd");
    private string _newEventEnd = DateTime.Now.AddDays(3).ToString("yyyy-MM-dd");
    private bool _newEventUseEndOnly = true;
    private string _eventSuccessMessage = "";
    private double _newEventImportance = 3;
    private string _newRecordDate = DateTime.Now.ToString("yyyy-MM-dd");
    private double _newRecordMinutes = 30;
    private string _newRecordCompletion = "partial";
    private string _newRecordNote = "";
    private string _databasePath = "";
    private string _adviceMode = "rules";
    private string _savedAdviceMode = "rules";
    private string _savedAiBaseUrl = "https://api.openai.com/v1";
    private string _savedAiModel = "gpt-4o-mini";
    private string _aiBaseUrl = "https://api.openai.com/v1";
    private string _aiModel = "gpt-4o-mini";
    private string _aiApiKey = "";
    private string _aiKeyStatus = "API Key 未配置";
    private double _statsDays = 14;
    private string _statsRange = "暂无统计区间";
    private string _statsTotalMinutes = "0 分钟";
    private string _statsAverageMinutes = "日均 0 分钟";
    private string _overallProgressText = "0 / 0 个知识点";
    private string _nextUpcomingEventText = "暂无临近考试或 DDL";
    private string _todayFocusText = "先建立课程、知识点和日程，今日重点会自动出现。";
    private string _reviewRiskText = "暂无明显风险。";
    private string _reviewInsightText = "先添加学习记录，复盘页会展示投入、进度和近期风险。";
    private double _overallCompletionRate;
    private ISeries[] _studyMinutesSeries = [];
    private Axis[] _studyMinutesXAxes = [new Axis()];
    private Axis[] _studyMinutesYAxes = [new Axis { Name = "分钟", MinLimit = 0 }];
    private ISeries[] _emotionScoreSeries = [];
    private Axis[] _emotionXAxes = [new Axis()];
    private Axis[] _emotionYAxes = [new Axis { Name = "强度", MinLimit = 0, MaxLimit = 5 }];
    private ISeries[] _courseMinutesSeries = [];

    public MainViewModel(StudyMindApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(BusyVisibility));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public string CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, NormalizePage(value)))
            {
                NotifyPagePropertiesChanged();
            }
        }
    }

    public string CurrentPageTitle => CurrentPage switch
    {
        "courses" => "课程与知识点",
        "schedule" => "考试 / DDL 日程",
        "records" => "学习记录",
        "review" => "可视化复盘",
        "settings" => "设置与本地数据",
        _ => "今日计划"
    };

    public Visibility TodayPageVisibility => PageVisibility("today");
    public Visibility CoursesPageVisibility => PageVisibility("courses");
    public Visibility SchedulePageVisibility => PageVisibility("schedule");
    public Visibility RecordsPageVisibility => PageVisibility("records");
    public Visibility ReviewPageVisibility => PageVisibility("review");
    public Visibility SettingsPageVisibility => PageVisibility("settings");

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string FormMessage
    {
        get => _formMessage;
        set
        {
            if (SetProperty(ref _formMessage, value))
            {
                OnPropertyChanged(nameof(FormMessageVisibility));
            }
        }
    }

    public Visibility FormMessageVisibility =>
        string.IsNullOrWhiteSpace(FormMessage) ? Visibility.Collapsed : Visibility.Visible;

    public string TodayDate
    {
        get => _todayDate;
        set
        {
            if (SetProperty(ref _todayDate, value))
            {
                OnPropertyChanged(nameof(TodayDateValue));
            }
        }
    }

    public DateTimeOffset TodayDateValue
    {
        get => DateFromString(TodayDate);
        set => TodayDate = FormatDate(value);
    }

    public string TodayState
    {
        get => _todayState;
        set => SetProperty(ref _todayState, value);
    }

    public string TodayEmotion
    {
        get => _todayEmotion;
        set => SetProperty(ref _todayEmotion, value);
    }

    public string TodayLearningState
    {
        get => _todayLearningState;
        set => SetProperty(ref _todayLearningState, value);
    }

    public string TodayEmotionDetails
    {
        get => _todayEmotionDetails;
        set => SetProperty(ref _todayEmotionDetails, value);
    }

    public string TodayModelType
    {
        get => _todayModelType;
        set
        {
            if (SetProperty(ref _todayModelType, value))
            {
                OnPropertyChanged(nameof(AdviceSourceText));
            }
        }
    }

    public string TodayFallbackReason
    {
        get => _todayFallbackReason;
        set
        {
            if (SetProperty(ref _todayFallbackReason, value))
            {
                OnPropertyChanged(nameof(FallbackNoticeVisibility));
                OnPropertyChanged(nameof(AdviceSourceText));
            }
        }
    }

    public string TodayAdvice
    {
        get => _todayAdvice;
        set => SetProperty(ref _todayAdvice, value);
    }

    public string AdviceSourceText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TodayFallbackReason))
            {
                return TodayFallbackReason;
            }

            if (TodayModelType.StartsWith("ai:", StringComparison.OrdinalIgnoreCase))
            {
                return $"已使用 AI 增强建议（{TodayModelType[3..]}）";
            }

            return "当前使用本地规则建议";
        }
    }

    public Visibility FallbackNoticeVisibility =>
        string.IsNullOrWhiteSpace(TodayFallbackReason) && !TodayModelType.StartsWith("ai:", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public string RecommendedTaskCountText => $"{RecommendedTasks.Count} 项";

    public Visibility EmptyRecommendedTasksVisibility =>
        RecommendedTasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FirstRunHintVisibility =>
        Courses.Count == 0 || Topics.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyCoursesVisibility =>
        Courses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyTopicsVisibility =>
        Topics.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyEventsVisibility =>
        Events.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStudyRecordsVisibility =>
        StudyRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyUpcomingEventsVisibility =>
        UpcomingEvents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStudyMinutesChartVisibility =>
        WeeklyStats.Sum(item => item.Minutes) == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StudyMinutesDetailsVisibility =>
        WeeklyStats.Sum(item => item.Minutes) == 0 ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EmptyCourseMinutesChartVisibility =>
        CourseMinutes.Count == 0 || CourseMinutes.Sum(item => item.Minutes) == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility CourseMinutesDetailsVisibility =>
        CourseMinutes.Count == 0 || CourseMinutes.Sum(item => item.Minutes) == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility EmptyEmotionChartVisibility =>
        EmotionTrend.All(item => !item.HasData) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmotionTrendDetailsVisibility =>
        EmotionTrend.All(item => !item.HasData) ? Visibility.Collapsed : Visibility.Visible;

    public string FirstInvalidField
    {
        get => _firstInvalidField;
        private set => SetProperty(ref _firstInvalidField, value);
    }

    public string CourseNameError
    {
        get => _courseNameError;
        set
        {
            if (SetProperty(ref _courseNameError, value))
            {
                OnPropertyChanged(nameof(CourseNameErrorVisibility));
            }
        }
    }

    public Visibility CourseNameErrorVisibility =>
        string.IsNullOrWhiteSpace(CourseNameError) ? Visibility.Collapsed : Visibility.Visible;

    public string TopicCourseError
    {
        get => _topicCourseError;
        set
        {
            if (SetProperty(ref _topicCourseError, value))
            {
                OnPropertyChanged(nameof(TopicCourseErrorVisibility));
            }
        }
    }

    public Visibility TopicCourseErrorVisibility =>
        string.IsNullOrWhiteSpace(TopicCourseError) ? Visibility.Collapsed : Visibility.Visible;

    public string TopicNameError
    {
        get => _topicNameError;
        set
        {
            if (SetProperty(ref _topicNameError, value))
            {
                OnPropertyChanged(nameof(TopicNameErrorVisibility));
            }
        }
    }

    public Visibility TopicNameErrorVisibility =>
        string.IsNullOrWhiteSpace(TopicNameError) ? Visibility.Collapsed : Visibility.Visible;

    public string EventTitleError
    {
        get => _eventTitleError;
        set
        {
            if (SetProperty(ref _eventTitleError, value))
            {
                OnPropertyChanged(nameof(EventTitleErrorVisibility));
            }
        }
    }

    public Visibility EventTitleErrorVisibility =>
        string.IsNullOrWhiteSpace(EventTitleError) ? Visibility.Collapsed : Visibility.Visible;

    public string EventDateError
    {
        get => _eventDateError;
        set
        {
            if (SetProperty(ref _eventDateError, value))
            {
                OnPropertyChanged(nameof(EventDateErrorVisibility));
            }
        }
    }

    public Visibility EventDateErrorVisibility =>
        string.IsNullOrWhiteSpace(EventDateError) ? Visibility.Collapsed : Visibility.Visible;

    public string RecordTopicError
    {
        get => _recordTopicError;
        set
        {
            if (SetProperty(ref _recordTopicError, value))
            {
                OnPropertyChanged(nameof(RecordTopicErrorVisibility));
            }
        }
    }

    public Visibility RecordTopicErrorVisibility =>
        string.IsNullOrWhiteSpace(RecordTopicError) ? Visibility.Collapsed : Visibility.Visible;

    public string RecordMinutesError
    {
        get => _recordMinutesError;
        set
        {
            if (SetProperty(ref _recordMinutesError, value))
            {
                OnPropertyChanged(nameof(RecordMinutesErrorVisibility));
            }
        }
    }

    public Visibility RecordMinutesErrorVisibility =>
        string.IsNullOrWhiteSpace(RecordMinutesError) ? Visibility.Collapsed : Visibility.Visible;

    public bool HasUnsavedChanges => CurrentPage switch
    {
        "courses" => IsCourseFormDirty() || IsTopicFormDirty(),
        "schedule" => IsEventFormDirty(),
        "records" => IsRecordFormDirty(),
        "settings" => IsSettingsFormDirty(),
        _ => false
    };

    public string NewCourseName
    {
        get => _newCourseName;
        set => SetProperty(ref _newCourseName, value);
    }

    public string CourseEditorModeText =>
        SelectedCourse is null ? "新建课程" : $"正在编辑：{SelectedCourse.Name}";

    public CourseDto? SelectedCourse
    {
        get => _selectedCourse;
        set
        {
            if (SetProperty(ref _selectedCourse, value) && value is not null)
            {
                NewCourseName = value.Name;
            }

            OnPropertyChanged(nameof(CourseEditorModeText));
        }
    }

    public CourseDto? SelectedTopicCourse
    {
        get => _selectedTopicCourse;
        set => SetProperty(ref _selectedTopicCourse, value);
    }

    public CourseDto? SelectedEventCourse
    {
        get => _selectedEventCourse;
        set => SetProperty(ref _selectedEventCourse, value);
    }

    public TopicDto? SelectedTopic
    {
        get => _selectedTopic;
        set
        {
            if (SetProperty(ref _selectedTopic, value))
            {
                ApplyTopicSelection(value);
                OnPropertyChanged(nameof(TopicEditorModeText));
            }
        }
    }

    public string TopicEditorModeText =>
        SelectedTopic is null ? "新建知识点" : $"正在编辑：{SelectedTopic.Name}";

    public EventDto? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (SetProperty(ref _selectedEvent, value))
            {
                ApplyEventSelection(value);
                OnPropertyChanged(nameof(EventEditorModeText));
            }
        }
    }

    public string EventEditorModeText =>
        SelectedEvent is null ? "新建日程" : $"正在编辑：{SelectedEvent.Title}";

    public EventDto? SelectedTopicEvent
    {
        get => _selectedTopicEvent;
        set => SetProperty(ref _selectedTopicEvent, value);
    }

    public StudyRecordDto? SelectedStudyRecord
    {
        get => _selectedStudyRecord;
        set
        {
            if (SetProperty(ref _selectedStudyRecord, value))
            {
                ApplyStudyRecordSelection(value);
                OnPropertyChanged(nameof(RecordEditorModeText));
            }
        }
    }

    public string RecordEditorModeText =>
        SelectedStudyRecord is null ? "新建学习记录" : $"正在编辑：{SelectedStudyRecord.TopicDisplay} · {SelectedStudyRecord.DateDisplay}";

    public string NewTopicName
    {
        get => _newTopicName;
        set => SetProperty(ref _newTopicName, value);
    }

    public string NewTopicMastery
    {
        get => _newTopicMastery;
        set => SetProperty(ref _newTopicMastery, value);
    }

    public string NewTopicStatus
    {
        get => _newTopicStatus;
        set => SetProperty(ref _newTopicStatus, value);
    }

    public double NewTopicImportance
    {
        get => _newTopicImportance;
        set => SetProperty(ref _newTopicImportance, value);
    }

    public double NewTopicMinutes
    {
        get => _newTopicMinutes;
        set => SetProperty(ref _newTopicMinutes, value);
    }

    public string NewEventTitle
    {
        get => _newEventTitle;
        set => SetProperty(ref _newEventTitle, value);
    }

    public string NewEventType
    {
        get => _newEventType;
        set => SetProperty(ref _newEventType, value);
    }

    public string NewEventStart
    {
        get => _newEventStart;
        set
        {
            if (SetProperty(ref _newEventStart, value))
            {
                OnPropertyChanged(nameof(NewEventStartDate));
            }
        }
    }

    public DateTimeOffset NewEventStartDate
    {
        get => DateFromString(NewEventStart);
        set => NewEventStart = FormatDate(value);
    }

    public string NewEventEnd
    {
        get => _newEventEnd;
        set
        {
            if (SetProperty(ref _newEventEnd, value))
            {
                OnPropertyChanged(nameof(NewEventEndDate));
                if (NewEventUseEndOnly && NewEventStart != value)
                {
                    NewEventStart = value;
                }
            }
        }
    }

    public DateTimeOffset NewEventEndDate
    {
        get => DateFromString(NewEventEnd);
        set => NewEventEnd = FormatDate(value);
    }

    public double NewEventImportance
    {
        get => _newEventImportance;
        set => SetProperty(ref _newEventImportance, value);
    }

    public bool NewEventUseEndOnly
    {
        get => _newEventUseEndOnly;
        set
        {
            if (SetProperty(ref _newEventUseEndOnly, value))
            {
                OnPropertyChanged(nameof(NewEventRangeStartEnabled));
                if (value)
                {
                    NewEventStart = NewEventEnd;
                }
            }
        }
    }

    public bool NewEventRangeStartEnabled => !NewEventUseEndOnly;

    public string EventSuccessMessage
    {
        get => _eventSuccessMessage;
        set
        {
            if (SetProperty(ref _eventSuccessMessage, value))
            {
                OnPropertyChanged(nameof(EventSuccessMessageVisibility));
            }
        }
    }

    public Visibility EventSuccessMessageVisibility =>
        string.IsNullOrWhiteSpace(EventSuccessMessage) ? Visibility.Collapsed : Visibility.Visible;

    public string NewRecordDate
    {
        get => _newRecordDate;
        set
        {
            if (SetProperty(ref _newRecordDate, value))
            {
                OnPropertyChanged(nameof(NewRecordDateValue));
            }
        }
    }

    public DateTimeOffset NewRecordDateValue
    {
        get => DateFromString(NewRecordDate);
        set => NewRecordDate = FormatDate(value);
    }

    public double NewRecordMinutes
    {
        get => _newRecordMinutes;
        set => SetProperty(ref _newRecordMinutes, value);
    }

    public string NewRecordCompletion
    {
        get => _newRecordCompletion;
        set => SetProperty(ref _newRecordCompletion, value);
    }

    public string NewRecordNote
    {
        get => _newRecordNote;
        set => SetProperty(ref _newRecordNote, value);
    }

    public string DatabasePath
    {
        get => _databasePath;
        set => SetProperty(ref _databasePath, value);
    }

    public string AdviceMode
    {
        get => _adviceMode;
        set
        {
            if (SetProperty(ref _adviceMode, value))
            {
                OnPropertyChanged(nameof(AdviceModeDescription));
                OnPropertyChanged(nameof(RequiresAiPrivacyConfirmation));
            }
        }
    }

    public bool RequiresAiPrivacyConfirmation =>
        _savedAdviceMode == "rules" && AdviceMode is "hybrid" or "ai";

    public string AdviceModeDescription => AdviceMode switch
    {
        "hybrid" => "优先使用 AI 生成自然语言建议，失败时自动回退到本地规则。",
        "ai" => "请求 AI 生成建议；如果接口不可用，后端仍会回退到本地规则。",
        _ => "只使用本地规则，不上传今日状态或学习摘要。"
    };

    public string AiBaseUrl
    {
        get => _aiBaseUrl;
        set => SetProperty(ref _aiBaseUrl, value);
    }

    public string AiModel
    {
        get => _aiModel;
        set => SetProperty(ref _aiModel, value);
    }

    public string AiApiKey
    {
        get => _aiApiKey;
        set => SetProperty(ref _aiApiKey, value);
    }

    public string AiKeyStatus
    {
        get => _aiKeyStatus;
        set => SetProperty(ref _aiKeyStatus, value);
    }

    public double StatsDays
    {
        get => _statsDays;
        set => SetProperty(ref _statsDays, value);
    }

    public string StatsRange
    {
        get => _statsRange;
        set => SetProperty(ref _statsRange, value);
    }

    public string StatsTotalMinutes
    {
        get => _statsTotalMinutes;
        set => SetProperty(ref _statsTotalMinutes, value);
    }

    public string StatsAverageMinutes
    {
        get => _statsAverageMinutes;
        set => SetProperty(ref _statsAverageMinutes, value);
    }

    public string OverallProgressText
    {
        get => _overallProgressText;
        set => SetProperty(ref _overallProgressText, value);
    }

    public string NextUpcomingEventText
    {
        get => _nextUpcomingEventText;
        set => SetProperty(ref _nextUpcomingEventText, value);
    }

    public string TodayFocusText
    {
        get => _todayFocusText;
        set => SetProperty(ref _todayFocusText, value);
    }

    public string ReviewRiskText
    {
        get => _reviewRiskText;
        set => SetProperty(ref _reviewRiskText, value);
    }

    public string ReviewInsightText
    {
        get => _reviewInsightText;
        set => SetProperty(ref _reviewInsightText, value);
    }

    public double OverallCompletionRate
    {
        get => _overallCompletionRate;
        set => SetProperty(ref _overallCompletionRate, value);
    }

    public ISeries[] StudyMinutesSeries
    {
        get => _studyMinutesSeries;
        set => SetProperty(ref _studyMinutesSeries, value);
    }

    public Axis[] StudyMinutesXAxes
    {
        get => _studyMinutesXAxes;
        set => SetProperty(ref _studyMinutesXAxes, value);
    }

    public Axis[] StudyMinutesYAxes
    {
        get => _studyMinutesYAxes;
        set => SetProperty(ref _studyMinutesYAxes, value);
    }

    public ISeries[] EmotionScoreSeries
    {
        get => _emotionScoreSeries;
        set => SetProperty(ref _emotionScoreSeries, value);
    }

    public Axis[] EmotionXAxes
    {
        get => _emotionXAxes;
        set => SetProperty(ref _emotionXAxes, value);
    }

    public Axis[] EmotionYAxes
    {
        get => _emotionYAxes;
        set => SetProperty(ref _emotionYAxes, value);
    }

    public ISeries[] CourseMinutesSeries
    {
        get => _courseMinutesSeries;
        set => SetProperty(ref _courseMinutesSeries, value);
    }

    [RelayCommand]
    public void Navigate(string? page)
    {
        FormMessage = "";
        EventSuccessMessage = "";
        ClearFormErrors();
        CurrentPage = NormalizePage(page);
    }

    public void DiscardCurrentPageChanges()
    {
        switch (CurrentPage)
        {
            case "courses":
                if (IsCourseFormDirty())
                {
                    ResetCourseForm();
                }

                if (IsTopicFormDirty())
                {
                    ResetTopicForm();
                }

                break;
            case "schedule":
                if (IsEventFormDirty())
                {
                    ResetEventForm();
                }

                break;
            case "records":
                if (IsRecordFormDirty())
                {
                    ResetRecordForm();
                }

                break;
            case "settings":
                if (IsSettingsFormDirty())
                {
                    ResetSettingsForm();
                }

                break;
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RunBusyAsync("正在加载数据…", async () =>
        {
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            var settings = await _apiClient.GetSettingsAsync();
            ApplySettings(settings);
            StatusMessage = "后端连接正常";
        });
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await RunBusyAsync("正在刷新数据…", async () =>
        {
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "数据已刷新";
        });
    }

    [RelayCommand]
    public async Task RefreshStatsAsync()
    {
        await RunBusyAsync("正在更新复盘…", async () =>
        {
            await LoadDashboardStatsAsync();
            StatusMessage = "复盘已更新";
        });
    }

    [RelayCommand]
    public void ResetCourseForm()
    {
        FormMessage = "";
        ClearFormErrors();
        SelectedCourse = null;
        NewCourseName = "";
        StatusMessage = "已切换到新建课程";
    }

    [RelayCommand]
    public void ResetTopicForm()
    {
        FormMessage = "";
        ClearFormErrors();
        SelectedTopic = null;
        SelectedTopicCourse = SelectedCourse ?? Courses.FirstOrDefault();
        SelectedTopicEvent = null;
        NewTopicName = "";
        NewTopicMastery = "未掌握";
        NewTopicStatus = "pending";
        NewTopicImportance = 3;
        NewTopicMinutes = 30;
        OnPropertyChanged(nameof(TopicEditorModeText));
        StatusMessage = "已切换到新建知识点";
    }

    [RelayCommand]
    public void ResetEventForm()
    {
        FormMessage = "";
        EventSuccessMessage = "";
        ClearFormErrors();
        SelectedEvent = null;
        SelectedEventCourse = SelectedCourse ?? Courses.FirstOrDefault();
        NewEventTitle = "";
        NewEventType = "考试";
        NewEventStart = DateTime.Now.AddDays(3).ToString("yyyy-MM-dd");
        NewEventEnd = DateTime.Now.AddDays(3).ToString("yyyy-MM-dd");
        NewEventUseEndOnly = true;
        NewEventImportance = 3;
        OnPropertyChanged(nameof(EventEditorModeText));
        StatusMessage = "已切换到新建日程";
    }

    [RelayCommand]
    public void ResetRecordForm()
    {
        FormMessage = "";
        ClearFormErrors();
        SelectedStudyRecord = null;
        NewRecordDate = TodayDate;
        NewRecordMinutes = 30;
        NewRecordCompletion = "partial";
        NewRecordNote = "";
        OnPropertyChanged(nameof(RecordEditorModeText));
        StatusMessage = "已切换到新建学习记录";
    }

    [RelayCommand]
    public async Task AddCourseAsync()
    {
        await RunBusyAsync("正在添加课程…", async () =>
        {
            ValidateCourseForm();
            await _apiClient.CreateCourseAsync(new CourseInput { Name = NewCourseName });
            NewCourseName = "";
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "课程已添加";
        });
    }

    [RelayCommand]
    public async Task SaveCourseAsync()
    {
        await RunBusyAsync(SelectedCourse is null ? "正在新建课程…" : "正在保存课程…", async () =>
        {
            ValidateCourseForm();
            if (SelectedCourse is null)
            {
                SelectedCourse = await _apiClient.CreateCourseAsync(new CourseInput { Name = NewCourseName });
                StatusMessage = "课程已新建";
            }
            else
            {
                await _apiClient.UpdateCourseAsync(SelectedCourse.Id, new CourseInput { Name = NewCourseName });
                StatusMessage = "课程已保存";
            }

            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
        });
    }

    [RelayCommand]
    public async Task UpdateCourseAsync()
    {
        await RunBusyAsync("正在保存课程…", async () =>
        {
            if (SelectedCourse is null)
            {
                throw new InvalidOperationException("请先选择课程。");
            }

            ValidateCourseForm();
            await _apiClient.UpdateCourseAsync(SelectedCourse.Id, new CourseInput { Name = NewCourseName });
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "课程已保存";
        });
    }

    [RelayCommand]
    public async Task DeleteCourseAsync()
    {
        await RunBusyAsync("正在删除课程及关联数据…", async () =>
        {
            if (SelectedCourse is null)
            {
                throw new InvalidOperationException("请先选择课程。");
            }

            var course = SelectedCourse;
            var topicIds = Topics
                .Where(topic => topic.CourseId == course.Id)
                .Select(topic => topic.Id)
                .ToHashSet();
            var recordIds = StudyRecords
                .Where(record => topicIds.Contains(record.TopicId))
                .Select(record => record.Id)
                .ToArray();
            var relatedEvents = Events
                .Where(item => item.RelatedCourseId == course.Id)
                .ToArray();

            foreach (var item in relatedEvents)
            {
                await _apiClient.UpdateEventAsync(item.Id, CreateEventInput(item, null));
            }

            foreach (var recordId in recordIds)
            {
                await _apiClient.DeleteStudyRecordAsync(recordId);
            }

            foreach (var topicId in topicIds)
            {
                await _apiClient.DeleteTopicAsync(topicId);
            }

            await _apiClient.DeleteCourseAsync(course.Id);
            NewCourseName = "";
            SelectedCourse = null;
            RemoveRecommendedTasks(topicIds);
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = $"课程已删除，同时清理 {topicIds.Count} 个知识点、{recordIds.Length} 条学习记录，并解除 {relatedEvents.Length} 个日程关联";
        });
    }

    [RelayCommand]
    public async Task AddTopicAsync()
    {
        await RunBusyAsync("正在添加知识点…", async () =>
        {
            if (SelectedTopicCourse is null)
            {
                throw new InvalidOperationException("请先选择所属课程。");
            }

            ValidateTopicForm();
            await _apiClient.CreateTopicAsync(CreateTopicInput(SelectedTopicCourse.Id));

            NewTopicName = "";
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "知识点已添加";
        });
    }

    [RelayCommand]
    public async Task SaveTopicAsync()
    {
        await RunBusyAsync(SelectedTopic is null ? "正在新建知识点…" : "正在保存知识点…", async () =>
        {
            ValidateTopicForm();
            if (SelectedTopicCourse is null)
            {
                throw new InvalidOperationException("请先选择所属课程。");
            }

            if (SelectedTopic is null)
            {
                SelectedTopic = await _apiClient.CreateTopicAsync(CreateTopicInput(SelectedTopicCourse.Id));
                StatusMessage = "知识点已新建";
            }
            else
            {
                await _apiClient.UpdateTopicAsync(SelectedTopic.Id, CreateTopicInput(SelectedTopicCourse.Id));
                StatusMessage = "知识点已保存";
            }

            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
        });
    }

    [RelayCommand]
    public async Task UpdateTopicAsync()
    {
        await RunBusyAsync("正在保存知识点…", async () =>
        {
            if (SelectedTopic is null)
            {
                throw new InvalidOperationException("请先选择知识点。");
            }

            if (SelectedTopicCourse is null)
            {
                throw new InvalidOperationException("请先选择所属课程。");
            }

            ValidateTopicForm();
            await _apiClient.UpdateTopicAsync(SelectedTopic.Id, CreateTopicInput(SelectedTopicCourse.Id));
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "知识点已保存";
        });
    }

    [RelayCommand]
    public async Task DeleteTopicAsync()
    {
        await RunBusyAsync("正在删除知识点及关联记录…", async () =>
        {
            if (SelectedTopic is null)
            {
                throw new InvalidOperationException("请先选择知识点。");
            }

            var topic = SelectedTopic;
            var recordIds = StudyRecords
                .Where(record => record.TopicId == topic.Id)
                .Select(record => record.Id)
                .ToArray();

            foreach (var recordId in recordIds)
            {
                await _apiClient.DeleteStudyRecordAsync(recordId);
            }

            await _apiClient.DeleteTopicAsync(topic.Id);
            SelectedTopic = null;
            NewTopicName = "";
            RemoveRecommendedTasks([topic.Id]);
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = $"知识点已删除，同时清理 {recordIds.Length} 条学习记录";
        });
    }

    [RelayCommand]
    public async Task AddEventAsync()
    {
        await RunBusyAsync("正在添加日程…", async () =>
        {
            ValidateEventForm();
            await _apiClient.CreateEventAsync(CreateEventInput());

            NewEventTitle = "";
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "日程已添加";
        });
    }

    [RelayCommand]
    public async Task UpdateEventAsync()
    {
        await RunBusyAsync("正在保存日程…", async () =>
        {
            if (SelectedEvent is null)
            {
                throw new InvalidOperationException("请先选择日程。");
            }

            ValidateEventForm();
            await _apiClient.UpdateEventAsync(SelectedEvent.Id, CreateEventInput());
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "日程已保存";
        });
    }

    [RelayCommand]
    public async Task SaveEventAsync()
    {
        await RunBusyAsync(SelectedEvent is null ? "正在新建日程…" : "正在保存日程…", async () =>
        {
            ValidateEventForm();
            if (SelectedEvent is null)
            {
                SelectedEvent = await _apiClient.CreateEventAsync(CreateEventInput());
                EventSuccessMessage = "已新建日程表单";
                StatusMessage = "日程已新建";
            }
            else
            {
                await _apiClient.UpdateEventAsync(SelectedEvent.Id, CreateEventInput());
                StatusMessage = "日程已保存";
            }

            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
        });
    }

    [RelayCommand]
    public async Task DeleteEventAsync()
    {
        await RunBusyAsync("正在删除日程…", async () =>
        {
            if (SelectedEvent is null)
            {
                throw new InvalidOperationException("请先选择日程。");
            }

            await _apiClient.DeleteEventAsync(SelectedEvent.Id);
            SelectedEvent = null;
            NewEventTitle = "";
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "日程已删除";
        });
    }

    [RelayCommand]
    public async Task AddStudyRecordAsync()
    {
        await RunBusyAsync("正在添加学习记录…", async () =>
        {
            if (SelectedTopic is null)
            {
                throw new InvalidOperationException("请先选择知识点。");
            }

            ValidateStudyRecordForm();
            await _apiClient.CreateStudyRecordAsync(CreateStudyRecordInput(SelectedTopic.Id));

            NewRecordNote = "";
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "学习记录已添加";
        });
    }

    [RelayCommand]
    public async Task SaveStudyRecordAsync()
    {
        await RunBusyAsync(SelectedStudyRecord is null ? "正在新建学习记录…" : "正在保存学习记录…", async () =>
        {
            ValidateStudyRecordForm();
            if (SelectedTopic is null)
            {
                throw new InvalidOperationException("请先选择知识点。");
            }

            if (SelectedStudyRecord is null)
            {
                SelectedStudyRecord = await _apiClient.CreateStudyRecordAsync(CreateStudyRecordInput(SelectedTopic.Id));
                StatusMessage = "学习记录已新建";
            }
            else
            {
                await _apiClient.UpdateStudyRecordAsync(
                    SelectedStudyRecord.Id,
                    CreateStudyRecordInput(SelectedTopic.Id));
                StatusMessage = "学习记录已保存";
            }

            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
        });
    }

    [RelayCommand]
    public async Task UpdateStudyRecordAsync()
    {
        await RunBusyAsync("正在保存学习记录…", async () =>
        {
            if (SelectedStudyRecord is null)
            {
                throw new InvalidOperationException("请先选择学习记录。");
            }

            if (SelectedTopic is null)
            {
                throw new InvalidOperationException("请先选择知识点。");
            }

            ValidateStudyRecordForm();
            await _apiClient.UpdateStudyRecordAsync(
                SelectedStudyRecord.Id,
                CreateStudyRecordInput(SelectedTopic.Id));
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "学习记录已保存";
        });
    }

    [RelayCommand]
    public async Task DeleteStudyRecordAsync()
    {
        await RunBusyAsync("正在删除学习记录…", async () =>
        {
            if (SelectedStudyRecord is null)
            {
                throw new InvalidOperationException("请先选择学习记录。");
            }

            await _apiClient.DeleteStudyRecordAsync(SelectedStudyRecord.Id);
            SelectedStudyRecord = null;
            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = "学习记录已删除";
        });
    }

    [RelayCommand]
    public async Task GenerateAdviceAsync()
    {
        await RunBusyAsync("正在生成今日计划…", async () =>
        {
            var response = await _apiClient.GenerateTodayAdviceAsync(new TodayAdviceRequest
            {
                Date = TodayDate,
                StateText = TodayState
            });

            TodayAdvice = response.Advice;
            TodayEmotion = response.Emotion is null
                ? "暂无"
                : $"{response.Emotion.Emotion} / {response.Emotion.PressureType}";
            TodayLearningState = response.Emotion is null
                ? "暂无"
                : $"{response.Emotion.LearningState} / {response.Emotion.IntensityLevel}";
            TodayEmotionDetails = response.Emotion is null
                ? "未读取到情绪分析记录"
                : BuildEmotionDetails(response.Emotion);
            TodayModelType = response.ModelType;
            TodayFallbackReason = response.FallbackReason ?? "";

            Replace(RecommendedTasks, response.RecommendedTasks);
            NotifyRecommendedTaskPropertiesChanged();
            await LoadDashboardStatsAsync();
            StatusMessage = $"今日计划已生成，记录编号 {response.SavedAdviceId}";
        });
    }

    [RelayCommand]
    public void UseTaskForRecord(RecommendedTaskDto? task)
    {
        if (task is null)
        {
            return;
        }

        var topic = Topics.FirstOrDefault(item => item.Id == task.TopicId);
        if (topic is not null)
        {
            SelectedTopic = topic;
        }

        NewRecordDate = TodayDate;
        NewRecordMinutes = Math.Max(5, task.EstimatedMinutes);
        NewRecordCompletion = "partial";
        NewRecordNote = $"来自今日推荐：{task.Reason}";
        Navigate("records");
        StatusMessage = $"已准备记录：{task.TopicName}";
    }

    [RelayCommand]
    public async Task CompleteRecommendedTaskAsync(RecommendedTaskDto? task)
    {
        await RunBusyAsync("正在记录推荐任务…", async () =>
        {
            if (task is null)
            {
                throw new InvalidOperationException("请先选择推荐任务。");
            }

            if (Topics.All(topic => topic.Id != task.TopicId))
            {
                throw new InvalidOperationException("推荐任务关联的知识点不存在，请刷新后重试。");
            }

            await _apiClient.CreateStudyRecordAsync(new StudyRecordInput
            {
                TopicId = task.TopicId,
                Date = TodayDate,
                Minutes = Math.Max(1, task.EstimatedMinutes),
                Completion = "completed",
                Note = $"从今日推荐完成：{task.Reason}"
            });

            await LoadCollectionsAsync();
            await LoadDashboardStatsAsync();
            StatusMessage = $"已记录完成：{task.TopicName}";
        });
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        await RunBusyAsync("正在保存设置…", async () =>
        {
            var settings = await _apiClient.UpdateSettingsAsync(new SettingsInput
            {
                AdviceMode = AdviceMode,
                AiBaseUrl = AiBaseUrl,
                AiModel = AiModel,
                AiApiKey = string.IsNullOrWhiteSpace(AiApiKey) ? null : AiApiKey
            });

            AiApiKey = "";
            ApplySettings(settings);
            StatusMessage = "设置已保存";
        });
    }

    [RelayCommand]
    public async Task BackupAsync()
    {
        await RunBusyAsync("正在备份数据库…", async () =>
        {
            var response = await _apiClient.BackupAsync();
            StatusMessage = $"数据库已备份：{response.BackupPath}";
        });
    }

    [RelayCommand]
    public async Task ExportAsync()
    {
        await RunBusyAsync("正在导出数据…", async () =>
        {
            var response = await _apiClient.ExportAsync();
            var exportDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "StudyMind");
            Directory.CreateDirectory(exportDir);

            var exportPath = Path.Combine(
                exportDir,
                $"studymind-export-{DateTime.Now:yyyyMMddHHmmss}.json");
            var json = JsonSerializer.Serialize(response, StudyMindApiClient.SerializerOptions);
            await File.WriteAllTextAsync(exportPath, json);

            StatusMessage = $"数据已导出：{exportPath}";
        });
    }

    public string BuildCourseDeleteImpact()
    {
        if (SelectedCourse is null)
        {
            return "请先选择课程。";
        }

        var topicIds = Topics
            .Where(topic => topic.CourseId == SelectedCourse.Id)
            .Select(topic => topic.Id)
            .ToHashSet();
        var recordCount = StudyRecords.Count(record => topicIds.Contains(record.TopicId));
        var eventCount = Events.Count(item => item.RelatedCourseId == SelectedCourse.Id);

        return
            $"确定删除“{SelectedCourse.Name}”吗？\n\n" +
            $"将自动删除：{topicIds.Count} 个知识点、{recordCount} 条学习记录。\n" +
            $"将保留但解除课程关联：{eventCount} 个日程。\n\n" +
            "此操作不可撤销；如果需要保留历史数据，请先在设置页执行导出或备份。";
    }

    public bool CanDeleteSelectedCourseSafely()
    {
        if (SelectedCourse is null)
        {
            return false;
        }

        var topicIds = Topics
            .Where(topic => topic.CourseId == SelectedCourse.Id)
            .Select(topic => topic.Id)
            .ToHashSet();
        var recordCount = StudyRecords.Count(record => topicIds.Contains(record.TopicId));
        return topicIds.Count == 0 && recordCount == 0;
    }

    public string BuildCourseDeleteBlockReason()
    {
        if (SelectedCourse is null)
        {
            return "请先选择课程。";
        }

        var topicIds = Topics
            .Where(topic => topic.CourseId == SelectedCourse.Id)
            .Select(topic => topic.Id)
            .ToHashSet();
        var recordCount = StudyRecords.Count(record => topicIds.Contains(record.TopicId));
        return $"“{SelectedCourse.Name}”下还有 {topicIds.Count} 个知识点、{recordCount} 条学习记录。删除课程时可以选择一次性清理这些关联数据。";
    }

    public string BuildTopicDeleteImpact()
    {
        if (SelectedTopic is null)
        {
            return "请先选择知识点。";
        }

        var recordCount = StudyRecords.Count(record => record.TopicId == SelectedTopic.Id);
        var recommendationCount = RecommendedTasks.Count(task => task.TopicId == SelectedTopic.Id);

        return
            $"确定删除“{SelectedTopic.Name}”吗？\n\n" +
            $"将自动删除：{recordCount} 条学习记录。\n" +
            $"将从今日推荐中移除：{recommendationCount} 条相关任务。\n\n" +
            "此操作不可撤销；如果需要保留历史数据，请先在设置页执行导出或备份。";
    }

    public bool CanDeleteSelectedTopicSafely()
    {
        if (SelectedTopic is null)
        {
            return false;
        }

        return StudyRecords.All(record => record.TopicId != SelectedTopic.Id);
    }

    public string BuildTopicDeleteBlockReason()
    {
        if (SelectedTopic is null)
        {
            return "请先选择知识点。";
        }

        var recordCount = StudyRecords.Count(record => record.TopicId == SelectedTopic.Id);
        return $"“{SelectedTopic.Name}”下还有 {recordCount} 条学习记录。删除知识点时可以选择一次性清理这些学习记录。";
    }

    public string BuildEventDeleteImpact()
    {
        if (SelectedEvent is null)
        {
            return "请先选择日程。";
        }

        var linkedTopicCount = Topics.Count(topic => topic.ExamId == SelectedEvent.Id);
        return $"确定删除“{SelectedEvent.Title}”吗？当前有 {linkedTopicCount} 个知识点绑定到这个考试或 DDL。";
    }

    public bool EditingEventTitleConflicts()
    {
        if (SelectedEvent is null || string.IsNullOrWhiteSpace(NewEventTitle))
        {
            return false;
        }

        var title = NewEventTitle.Trim();
        return Events.Any(item =>
            item.Id != SelectedEvent.Id &&
            string.Equals(item.Title.Trim(), title, StringComparison.OrdinalIgnoreCase));
    }

    public string EventDuplicateTitleSuggestion =>
        string.IsNullOrWhiteSpace(NewEventTitle) ? "-2" : $"{NewEventTitle.Trim()}-2";

    public string BuildEventTitleConflictMessage() =>
        $"已经存在标题为“{NewEventTitle.Trim()}”的其它日程。可以取消保存，或将当前日程标题保存为“{EventDuplicateTitleSuggestion}”。";

    public void ApplyEventDuplicateTitleSuggestion()
    {
        NewEventTitle = EventDuplicateTitleSuggestion;
    }

    public string BuildStudyRecordDeleteImpact()
    {
        if (SelectedStudyRecord is null)
        {
            return "请先选择学习记录。";
        }

        return $"确定删除“{SelectedStudyRecord.TopicDisplay} · {SelectedStudyRecord.DateDisplay}”这条 {SelectedStudyRecord.Minutes} 分钟记录吗？";
    }

    private async Task LoadCollectionsAsync()
    {
        var selectedCourseId = SelectedCourse?.Id;
        var selectedTopicCourseId = SelectedTopicCourse?.Id;
        var selectedEventCourseId = SelectedEventCourse?.Id;
        var selectedTopicId = SelectedTopic?.Id;
        var selectedEventId = SelectedEvent?.Id;
        var selectedTopicEventId = SelectedTopicEvent?.Id;
        var selectedRecordId = SelectedStudyRecord?.Id;

        Replace(Courses, await _apiClient.GetCoursesAsync());
        Replace(Events, await _apiClient.GetEventsAsync());
        Replace(Topics, await _apiClient.GetTopicsAsync());
        Replace(StudyRecords, await _apiClient.GetStudyRecordsAsync());

        SelectedCourse = FindById(Courses, selectedCourseId) ?? Courses.FirstOrDefault();
        SelectedTopicCourse = FindById(Courses, selectedTopicCourseId) ?? SelectedCourse ?? Courses.FirstOrDefault();
        SelectedEventCourse = FindById(Courses, selectedEventCourseId) ?? SelectedCourse ?? Courses.FirstOrDefault();
        SelectedTopicEvent = FindById(Events, selectedTopicEventId) ?? Events.FirstOrDefault();
        SelectedTopic = FindById(Topics, selectedTopicId) ?? Topics.FirstOrDefault();
        SelectedEvent = FindById(Events, selectedEventId) ?? Events.FirstOrDefault();
        SelectedStudyRecord = FindById(StudyRecords, selectedRecordId) ?? StudyRecords.FirstOrDefault();
        NotifyDataStatePropertiesChanged();
    }

    private async Task LoadDashboardStatsAsync()
    {
        var days = Math.Clamp(ToLong(StatsDays), 7, 30);
        var stats = await _apiClient.GetDashboardStatsAsync(TodayDate, (int)days);
        ApplyDashboardStats(stats);
    }

    private async Task RunBusyAsync(string workingMessage, Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            FormMessage = "";
            EventSuccessMessage = "";
            ClearFormErrors();
            StatusMessage = workingMessage;
            await action();
        }
        catch (Exception ex)
        {
            var message = FriendlyErrorMessage(ex);
            StatusMessage = message;
            FormMessage = message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private TopicInput CreateTopicInput(long courseId) => new()
    {
        CourseId = courseId,
        Name = NewTopicName,
        MasteryLevel = NewTopicMastery,
        Importance = ToLong(NewTopicImportance),
        EstimatedMinutes = ToLong(NewTopicMinutes),
        ExamId = SelectedTopicEvent?.Id,
        Status = NewTopicStatus
    };

    private EventInput CreateEventInput() => new()
    {
        Title = NewEventTitle,
        EventType = NewEventType,
        StartTime = EffectiveEventStart,
        EndTime = NewEventEnd,
        Importance = ToLong(NewEventImportance),
        RelatedCourseId = SelectedEventCourse?.Id
    };

    private static EventInput CreateEventInput(EventDto item, long? relatedCourseId) => new()
    {
        Title = item.Title,
        EventType = item.EventType,
        StartTime = item.StartTime,
        EndTime = item.EndTime,
        Importance = item.Importance,
        RelatedCourseId = relatedCourseId
    };

    private StudyRecordInput CreateStudyRecordInput(long topicId) => new()
    {
        TopicId = topicId,
        Date = NewRecordDate,
        Minutes = ToLong(NewRecordMinutes),
        Completion = NewRecordCompletion,
        Note = NewRecordNote
    };

    private void ValidateCourseForm()
    {
        if (string.IsNullOrWhiteSpace(NewCourseName))
        {
            SetValidationError("CourseName", value => CourseNameError = value, "请输入课程名称。");
            throw new InvalidOperationException("请修正课程表单中的问题。");
        }
    }

    private void ValidateTopicForm()
    {
        var hasError = false;
        if (SelectedTopicCourse is null)
        {
            SetValidationError("TopicCourse", value => TopicCourseError = value, "请选择所属课程。");
            hasError = true;
        }

        if (string.IsNullOrWhiteSpace(NewTopicName))
        {
            SetValidationError("TopicName", value => TopicNameError = value, "请输入知识点名称。");
            hasError = true;
        }

        if (hasError)
        {
            throw new InvalidOperationException("请修正知识点表单中的问题。");
        }
    }

    private void ValidateEventForm()
    {
        var hasError = false;
        if (string.IsNullOrWhiteSpace(NewEventTitle))
        {
            SetValidationError("EventTitle", value => EventTitleError = value, "请输入日程标题。");
            hasError = true;
        }

        DateTimeOffset startDate = default;
        DateTimeOffset endDate = default;
        if (!TryParseIsoDate(EffectiveEventStart, out startDate))
        {
            SetValidationError("EventStart", value => EventDateError = value, "请选择有效的开始日期。");
            hasError = true;
        }

        if (!TryParseIsoDate(NewEventEnd, out endDate))
        {
            SetValidationError("EventEnd", value => EventDateError = value, "请选择有效的结束日期。");
            hasError = true;
        }

        if (!hasError && endDate < startDate)
        {
            SetValidationError("EventEnd", value => EventDateError = value, "结束日期不能早于开始日期。");
            hasError = true;
        }

        if (hasError)
        {
            throw new InvalidOperationException("请修正日程表单中的问题。");
        }
    }

    private string EffectiveEventStart => NewEventUseEndOnly ? NewEventEnd : NewEventStart;

    private void ValidateStudyRecordForm()
    {
        var hasError = false;
        if (SelectedTopic is null)
        {
            SetValidationError("RecordTopic", value => RecordTopicError = value, "请选择知识点。");
            hasError = true;
        }

        if (NewRecordMinutes < 1)
        {
            SetValidationError("RecordMinutes", value => RecordMinutesError = value, "学习分钟数必须大于 0。");
            hasError = true;
        }

        if (hasError)
        {
            throw new InvalidOperationException("请修正学习记录表单中的问题。");
        }
    }

    private void ApplyTopicSelection(TopicDto? topic)
    {
        if (topic is null)
        {
            return;
        }

        NewTopicName = topic.Name;
        NewTopicMastery = topic.MasteryLevel;
        NewTopicImportance = topic.Importance;
        NewTopicMinutes = topic.EstimatedMinutes;
        NewTopicStatus = topic.Status;
        SelectedTopicCourse = Courses.FirstOrDefault(course => course.Id == topic.CourseId);
        SelectedTopicEvent = topic.ExamId is null
            ? null
            : Events.FirstOrDefault(item => item.Id == topic.ExamId.Value);
    }

    private void ApplyEventSelection(EventDto? item)
    {
        if (item is null)
        {
            return;
        }

        NewEventUseEndOnly = false;
        NewEventTitle = item.Title;
        NewEventType = item.EventType;
        NewEventStart = item.StartTime;
        NewEventEnd = item.EndTime;
        NewEventImportance = item.Importance;
        NewEventUseEndOnly = SameDate(item.StartTime, item.EndTime);
        SelectedEventCourse = item.RelatedCourseId is null
            ? null
            : Courses.FirstOrDefault(course => course.Id == item.RelatedCourseId.Value);
    }

    private void ApplyStudyRecordSelection(StudyRecordDto? record)
    {
        if (record is null)
        {
            return;
        }

        SelectedTopic = Topics.FirstOrDefault(topic => topic.Id == record.TopicId) ?? SelectedTopic;
        NewRecordDate = record.Date;
        NewRecordMinutes = record.Minutes;
        NewRecordCompletion = record.Completion;
        NewRecordNote = record.Note;
    }

    private void ApplySettings(SettingsDto settings)
    {
        AdviceMode = settings.AdviceMode;
        _savedAdviceMode = settings.AdviceMode;
        AiBaseUrl = settings.AiBaseUrl;
        _savedAiBaseUrl = settings.AiBaseUrl;
        AiModel = settings.AiModel;
        _savedAiModel = settings.AiModel;
        DatabasePath = settings.DatabasePath;
        AiKeyStatus = settings.AiApiKeyConfigured ? "API Key 已配置" : "API Key 未配置";
        OnPropertyChanged(nameof(RequiresAiPrivacyConfirmation));
    }

    private void ApplyDashboardStats(DashboardStatsResponseDto stats)
    {
        Replace(WeeklyStats, stats.DailyMinutes);
        Replace(CourseMinutes, stats.CourseMinutes);
        Replace(TopicProgress, stats.TopicProgress);
        Replace(UpcomingEvents, stats.UpcomingEvents);
        Replace(EmotionTrend, stats.EmotionTrend);
        NotifyDataStatePropertiesChanged();

        StatsRange = $"{stats.From} 至 {stats.To}";
        StatsTotalMinutes = $"{stats.TotalMinutes} 分钟";
        StatsAverageMinutes = $"日均 {stats.AverageDailyMinutes:0.#} 分钟";
        OverallCompletionRate = stats.OverallCompletionRate;
        OverallProgressText = $"{stats.CompletedTopics} / {stats.TotalTopics} 个知识点";
        NextUpcomingEventText = BuildNextEventText(stats.UpcomingEvents);
        TodayFocusText = BuildTodayFocus(stats);
        ReviewRiskText = BuildReviewRisk(stats);
        ReviewInsightText = BuildReviewInsight(stats);

        var dateLabels = stats.DailyMinutes.Select(item => ShortDate(item.Date)).ToArray();
        StudyMinutesSeries =
        [
            new LineSeries<double>
            {
                Name = "学习分钟",
                Values = stats.DailyMinutes.Select(item => (double)item.Minutes).ToArray(),
                GeometrySize = 8,
                Fill = null
            }
        ];
        StudyMinutesXAxes = [new Axis { Labels = dateLabels }];
        StudyMinutesYAxes = [new Axis { Name = "分钟", MinLimit = 0 }];

        EmotionScoreSeries =
        [
            new LineSeries<double>
            {
                Name = "情绪强度",
                Values = stats.EmotionTrend.Select(item => (double)(item.Score ?? 0)).ToArray(),
                GeometrySize = 8,
                Fill = null
            }
        ];
        EmotionXAxes = [new Axis { Labels = stats.EmotionTrend.Select(item => ShortDate(item.Date)).ToArray() }];
        EmotionYAxes = [new Axis { Name = "强度", MinLimit = 0, MaxLimit = 5 }];

        CourseMinutesSeries = stats.CourseMinutes.Count == 0
            ? []
            : stats.CourseMinutes
                .Select(item => new PieSeries<double>
                {
                    Name = item.CourseName,
                    Values = new double[] { item.Minutes }
                })
                .Cast<ISeries>()
                .ToArray();
    }

    private void NotifyPagePropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(TodayPageVisibility));
        OnPropertyChanged(nameof(CoursesPageVisibility));
        OnPropertyChanged(nameof(SchedulePageVisibility));
        OnPropertyChanged(nameof(RecordsPageVisibility));
        OnPropertyChanged(nameof(ReviewPageVisibility));
        OnPropertyChanged(nameof(SettingsPageVisibility));
    }

    private void NotifyRecommendedTaskPropertiesChanged()
    {
        OnPropertyChanged(nameof(RecommendedTaskCountText));
        OnPropertyChanged(nameof(EmptyRecommendedTasksVisibility));
    }

    private void RemoveRecommendedTasks(IEnumerable<long> topicIds)
    {
        var topicIdSet = topicIds.ToHashSet();
        foreach (var task in RecommendedTasks.Where(task => topicIdSet.Contains(task.TopicId)).ToArray())
        {
            RecommendedTasks.Remove(task);
        }

        NotifyRecommendedTaskPropertiesChanged();
    }

    private void NotifyDataStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(FirstRunHintVisibility));
        OnPropertyChanged(nameof(EmptyCoursesVisibility));
        OnPropertyChanged(nameof(EmptyTopicsVisibility));
        OnPropertyChanged(nameof(EmptyEventsVisibility));
        OnPropertyChanged(nameof(EmptyStudyRecordsVisibility));
        OnPropertyChanged(nameof(EmptyUpcomingEventsVisibility));
        OnPropertyChanged(nameof(EmptyStudyMinutesChartVisibility));
        OnPropertyChanged(nameof(StudyMinutesDetailsVisibility));
        OnPropertyChanged(nameof(EmptyCourseMinutesChartVisibility));
        OnPropertyChanged(nameof(CourseMinutesDetailsVisibility));
        OnPropertyChanged(nameof(EmptyEmotionChartVisibility));
        OnPropertyChanged(nameof(EmotionTrendDetailsVisibility));
    }

    private void ClearFormErrors()
    {
        FirstInvalidField = "";
        CourseNameError = "";
        TopicCourseError = "";
        TopicNameError = "";
        EventTitleError = "";
        EventDateError = "";
        RecordTopicError = "";
        RecordMinutesError = "";
    }

    private void SetValidationError(string field, Action<string> setError, string message)
    {
        setError(message);
        if (string.IsNullOrWhiteSpace(FirstInvalidField))
        {
            FirstInvalidField = field;
        }
    }

    private bool IsCourseFormDirty() =>
        SelectedCourse is null
            ? !string.IsNullOrWhiteSpace(NewCourseName)
            : !string.Equals(NewCourseName.Trim(), SelectedCourse.Name.Trim(), StringComparison.Ordinal);

    private bool IsTopicFormDirty()
    {
        if (SelectedTopic is null)
        {
            return !string.IsNullOrWhiteSpace(NewTopicName);
        }

        return
            !string.Equals(NewTopicName.Trim(), SelectedTopic.Name.Trim(), StringComparison.Ordinal) ||
            SelectedTopicCourse?.Id != SelectedTopic.CourseId ||
            SelectedTopicEvent?.Id != SelectedTopic.ExamId ||
            !string.Equals(NewTopicMastery, SelectedTopic.MasteryLevel, StringComparison.Ordinal) ||
            !string.Equals(NewTopicStatus, SelectedTopic.Status, StringComparison.Ordinal) ||
            ToLong(NewTopicImportance) != SelectedTopic.Importance ||
            ToLong(NewTopicMinutes) != SelectedTopic.EstimatedMinutes;
    }

    private bool IsEventFormDirty()
    {
        if (SelectedEvent is null)
        {
            return !string.IsNullOrWhiteSpace(NewEventTitle);
        }

        return
            !string.Equals(NewEventTitle.Trim(), SelectedEvent.Title.Trim(), StringComparison.Ordinal) ||
            !string.Equals(NewEventType, SelectedEvent.EventType, StringComparison.Ordinal) ||
            !string.Equals(EffectiveEventStart, SelectedEvent.StartTime, StringComparison.Ordinal) ||
            !string.Equals(NewEventEnd, SelectedEvent.EndTime, StringComparison.Ordinal) ||
            ToLong(NewEventImportance) != SelectedEvent.Importance ||
            SelectedEventCourse?.Id != SelectedEvent.RelatedCourseId;
    }

    private bool IsRecordFormDirty()
    {
        if (SelectedStudyRecord is null)
        {
            return !string.IsNullOrWhiteSpace(NewRecordNote) ||
                NewRecordMinutes != 30 ||
                !string.Equals(NewRecordCompletion, "partial", StringComparison.Ordinal);
        }

        return
            SelectedTopic?.Id != SelectedStudyRecord.TopicId ||
            !string.Equals(NewRecordDate, SelectedStudyRecord.Date, StringComparison.Ordinal) ||
            ToLong(NewRecordMinutes) != SelectedStudyRecord.Minutes ||
            !string.Equals(NewRecordCompletion, SelectedStudyRecord.Completion, StringComparison.Ordinal) ||
            !string.Equals(NewRecordNote, SelectedStudyRecord.Note, StringComparison.Ordinal);
    }

    private bool IsSettingsFormDirty() =>
        !string.Equals(AdviceMode, _savedAdviceMode, StringComparison.Ordinal) ||
        !string.Equals(AiBaseUrl.Trim(), _savedAiBaseUrl.Trim(), StringComparison.Ordinal) ||
        !string.Equals(AiModel.Trim(), _savedAiModel.Trim(), StringComparison.Ordinal);

    private void ResetSettingsForm()
    {
        AdviceMode = _savedAdviceMode;
        AiBaseUrl = _savedAiBaseUrl;
        AiModel = _savedAiModel;
        AiApiKey = "";
        FormMessage = "";
        ClearFormErrors();
        StatusMessage = "已放弃未保存的设置修改";
    }

    private Visibility PageVisibility(string page) =>
        CurrentPage == page ? Visibility.Visible : Visibility.Collapsed;

    private static string NormalizePage(string? page) =>
        page is "courses" or "schedule" or "records" or "review" or "settings"
            ? page
            : "today";

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private static T? FindById<T>(IEnumerable<T> values, long? id)
    {
        if (id is null)
        {
            return default;
        }

        return values.FirstOrDefault(value =>
        {
            var property = typeof(T).GetProperty("Id");
            return property?.GetValue(value) is long valueId && valueId == id.Value;
        });
    }

    private static long ToLong(double value) => Math.Max(1, (long)Math.Round(value));

    private static void EnsureRequired(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{field}不能为空。");
        }
    }

    private static void EnsureDateRange(string start, string end)
    {
        var startDate = ParseDateStrict(start, "开始日期");
        var endDate = ParseDateStrict(end, "结束日期");
        if (endDate < startDate)
        {
            throw new InvalidOperationException("结束日期不能早于开始日期。");
        }
    }

    private static DateTimeOffset DateFromString(string value) =>
        TryParseIsoDate(value, out var parsed)
            ? parsed
            : DateTimeOffset.Now;

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDateStrict(string value, string field) =>
        TryParseIsoDate(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"{field}格式应为 YYYY-MM-DD。");

    private static bool TryParseIsoDate(string value, out DateTimeOffset parsed)
    {
        if (DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date))
        {
            parsed = new DateTimeOffset(date);
            return true;
        }

        parsed = default;
        return false;
    }

    private static bool SameDate(string left, string right) =>
        DateTime.TryParse(left, out var leftDate) &&
        DateTime.TryParse(right, out var rightDate) &&
        leftDate.Date == rightDate.Date;

    private static string FriendlyErrorMessage(Exception ex) => ex switch
    {
        HttpRequestException => "无法连接本地后端。StudyMind 会在启动时自动拉起后端；请重新打开应用，或用 .\\scripts\\start-backend.ps1 单独检查后端。",
        TaskCanceledException => "后端请求超时。请确认本地服务正在运行后重试。",
        _ => ex.Message
    };

    private static string BuildEmotionDetails(EmotionAnalysisDto emotion)
    {
        var keywords = emotion.MatchedKeywords.Count == 0
            ? "无明显关键词"
            : string.Join("、", emotion.MatchedKeywords);

        return $"学习状态：{emotion.LearningState}；建议语气：{emotion.SuggestionTone}；置信度：{emotion.Confidence:P0}；命中词：{keywords}";
    }

    private static string BuildNextEventText(IReadOnlyList<UpcomingEventSummaryDto> events)
    {
        var next = events.FirstOrDefault();
        return next is null
            ? "暂无临近考试或 DDL"
            : $"{next.Title}：{next.DaysLeftText}，{next.RemainingTopicsText}";
    }

    private static string BuildTodayFocus(DashboardStatsResponseDto stats)
    {
        var urgent = stats.UpcomingEvents
            .Where(item => item.DaysLeft <= 7 && item.RemainingTopics > 0)
            .OrderBy(item => item.DaysLeft)
            .ThenByDescending(item => item.Importance)
            .FirstOrDefault();
        if (urgent is not null)
        {
            return $"今日重点：优先处理“{urgent.Title}”相关内容，仍有 {urgent.RemainingTopics} 个知识点未完成。";
        }

        var weakestCourse = stats.TopicProgress
            .Where(item => item.TotalTopics > 0 && item.CompletionRate < 100)
            .OrderBy(item => item.CompletionRate)
            .FirstOrDefault();
        if (weakestCourse is not null)
        {
            return $"今日重点：补齐“{weakestCourse.CourseName}”，当前完成率 {weakestCourse.CompletionRate:0.#}%。";
        }

        if (stats.TotalMinutes == 0)
        {
            return "今日重点：先完成一段 25-30 分钟学习并记录下来，后续推荐会更准。";
        }

        return "今日重点：保持已有节奏，优先完成推荐列表里的短任务。";
    }

    private static string BuildReviewRisk(DashboardStatsResponseDto stats)
    {
        var urgent = stats.UpcomingEvents
            .Where(item => item.DaysLeft <= 3 && item.RemainingTopics > 0)
            .OrderBy(item => item.DaysLeft)
            .FirstOrDefault();
        if (urgent is not null)
        {
            return $"高优先级：{urgent.Title} {urgent.DaysLeftText}，还有 {urgent.RemainingTopics} 个知识点未完成。";
        }

        if (stats.TotalTopics > 0 && stats.OverallCompletionRate < 50)
        {
            return $"进度偏低：整体完成率 {stats.OverallCompletionRate:0.#}%，建议先处理低完成率课程。";
        }

        if (stats.TotalTopics > 0 && stats.AverageDailyMinutes < 20)
        {
            return $"投入偏低：统计区间日均 {stats.AverageDailyMinutes:0.#} 分钟，可以先从短时任务恢复节奏。";
        }

        if (stats.EmotionTrend.All(item => !item.HasData))
        {
            return "情绪记录较少：在今日计划里写下状态后，情绪趋势会更有参考价值。";
        }

        return "暂无明显风险：可以按今日推荐任务继续推进。";
    }

    private static string BuildReviewInsight(DashboardStatsResponseDto stats)
    {
        if (stats.TotalTopics == 0)
        {
            return "还没有知识点数据。先建立课程和知识点，复盘会更有方向。";
        }

        if (stats.TotalMinutes == 0)
        {
            return "当前统计区间还没有学习记录。记录一次学习后，这里会显示投入和进度变化。";
        }

        var count = stats.DailyMinutes.Count;
        var recent = stats.DailyMinutes.Skip(Math.Max(0, count - 3)).Sum(item => item.Minutes);
        var previous = stats.DailyMinutes.Skip(Math.Max(0, count - 6)).Take(Math.Min(3, Math.Max(0, count - 3))).Sum(item => item.Minutes);
        var trend = previous switch
        {
            > 0 when recent < previous * 0.7 => $"最近三天学习时长下降（{recent} / {previous} 分钟）。",
            > 0 when recent > previous * 1.3 => $"最近三天学习投入上升（{recent} / {previous} 分钟）。",
            > 0 => $"最近三天学习投入基本稳定（{recent} / {previous} 分钟）。",
            _ => $"最近三天累计学习 {recent} 分钟。"
        };

        return $"{trend} 统计区间日均 {stats.AverageDailyMinutes:0.#} 分钟，整体完成率 {stats.OverallCompletionRate:0.#}%。";
    }

    private static string ShortDate(string date) =>
        TryParseIsoDate(date, out var parsed) ? parsed.ToString("M", CultureInfo.CurrentCulture) : date;
}
