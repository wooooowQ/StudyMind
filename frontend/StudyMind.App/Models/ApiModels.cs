using System.Globalization;
using System.Text.Json.Serialization;

namespace StudyMind.App.Models;

public sealed class CourseDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class TopicDto
{
    public long Id { get; set; }
    public long CourseId { get; set; }
    public string? CourseName { get; set; }
    public string Name { get; set; } = "";
    public string MasteryLevel { get; set; } = "";
    public long Importance { get; set; }
    public long EstimatedMinutes { get; set; }
    public long? ExamId { get; set; }
    public string Status { get; set; } = "";

    [JsonIgnore]
    public string CourseDisplay => string.IsNullOrWhiteSpace(CourseName) ? "未关联课程" : CourseName!;

    [JsonIgnore]
    public string ImportanceText => $"重要性 {Importance}";

    [JsonIgnore]
    public string EstimatedText => $"{EstimatedMinutes} 分钟";

    [JsonIgnore]
    public string StatusDisplay => Status switch
    {
        "completed" or "done" or "已完成" => "已完成",
        _ => "待学习"
    };
}

public sealed class EventDto
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string EventType { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public long Importance { get; set; }
    public long? RelatedCourseId { get; set; }
    public string? RelatedCourseName { get; set; }

    [JsonIgnore]
    public string RelatedCourseDisplay =>
        string.IsNullOrWhiteSpace(RelatedCourseName) ? "未关联课程" : RelatedCourseName!;

    [JsonIgnore]
    public string ImportanceText => $"重要性 {Importance}";

    [JsonIgnore]
    public string StartTimeDisplay => DisplayFormat.Date(StartTime);

    [JsonIgnore]
    public string EndTimeDisplay => DisplayFormat.Date(EndTime);

    [JsonIgnore]
    public string TimeDisplay =>
        string.Equals(StartTime, EndTime, StringComparison.OrdinalIgnoreCase)
            ? $"结束：{EndTimeDisplay}"
            : $"{StartTimeDisplay} - {EndTimeDisplay}";
}

public sealed class StudyRecordDto
{
    public long Id { get; set; }
    public long TopicId { get; set; }
    public string? TopicName { get; set; }
    public string Date { get; set; } = "";
    public long Minutes { get; set; }
    public string Completion { get; set; } = "";
    public string Note { get; set; } = "";

    [JsonIgnore]
    public string DateDisplay => DisplayFormat.Date(Date);

    [JsonIgnore]
    public string TopicDisplay => string.IsNullOrWhiteSpace(TopicName) ? "未关联知识点" : TopicName!;

    [JsonIgnore]
    public string MinutesText => $"{Minutes} 分钟";

    [JsonIgnore]
    public string CompletionDisplay => Completion switch
    {
        "completed" or "done" or "已完成" or "完成" => "已完成",
        _ => "部分完成"
    };
}

public sealed class EmotionAnalysisDto
{
    public string Date { get; set; } = "";
    public string RawText { get; set; } = "";
    public string NormalizedText { get; set; } = "";
    public string Emotion { get; set; } = "";
    public string PressureType { get; set; } = "";
    public string LearningState { get; set; } = "";
    public string IntensityLevel { get; set; } = "";
    public string SuggestionTone { get; set; } = "";
    public long Score { get; set; }
    public double Confidence { get; set; }
    public List<string> MatchedKeywords { get; set; } = [];
}

public sealed class EmotionLogDto
{
    public long Id { get; set; }
    public string Date { get; set; } = "";
    public string RawText { get; set; } = "";
    public string NormalizedText { get; set; } = "";
    public string Emotion { get; set; } = "";
    public string PressureType { get; set; } = "";
    public string LearningState { get; set; } = "";
    public string IntensityLevel { get; set; } = "";
    public string SuggestionTone { get; set; } = "";
    public long Score { get; set; }
    public double Confidence { get; set; }
    public List<string> MatchedKeywords { get; set; } = [];
}

public sealed class AdviceLogDto
{
    public long Id { get; set; }
    public string Date { get; set; } = "";
    public string InputSnapshot { get; set; } = "";
    public string GeneratedAdvice { get; set; } = "";
    public string ModelType { get; set; } = "";
}

public sealed class RecommendedTaskDto
{
    public long TopicId { get; set; }
    public string TopicName { get; set; } = "";
    public string? CourseName { get; set; }
    public long EstimatedMinutes { get; set; }
    public double PriorityScore { get; set; }
    public string Reason { get; set; } = "";

    [JsonIgnore]
    public string PriorityText => $"优先级 {PriorityScore:0.0}";

    [JsonIgnore]
    public string CourseDisplay => string.IsNullOrWhiteSpace(CourseName) ? "未关联课程" : CourseName!;

    [JsonIgnore]
    public string CourseAndMinutesText => $"{CourseDisplay} · {EstimatedMinutes} 分钟";
}

public sealed class TodayAdviceResponseDto
{
    public string Date { get; set; } = "";
    public string ModelType { get; set; } = "";
    public EmotionAnalysisDto? Emotion { get; set; }
    public string Advice { get; set; } = "";
    public List<RecommendedTaskDto> RecommendedTasks { get; set; } = [];
    public long SavedAdviceId { get; set; }
    public string? FallbackReason { get; set; }
}

public sealed class DailyMinutesDto
{
    public string Date { get; set; } = "";
    public long Minutes { get; set; }

    [JsonIgnore]
    public string DateDisplay => DisplayFormat.Date(Date);

    [JsonIgnore]
    public string MinutesText => $"{Minutes} 分钟";
}

public sealed class WeeklyStatsResponseDto
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public List<DailyMinutesDto> DailyMinutes { get; set; } = [];
}

public sealed class DashboardStatsResponseDto
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public long TotalMinutes { get; set; }
    public double AverageDailyMinutes { get; set; }
    public long TotalTopics { get; set; }
    public long CompletedTopics { get; set; }
    public double OverallCompletionRate { get; set; }
    public List<DailyMinutesDto> DailyMinutes { get; set; } = [];
    public List<EmotionTrendPointDto> EmotionTrend { get; set; } = [];
    public List<CourseMinutesDto> CourseMinutes { get; set; } = [];
    public List<TopicProgressDto> TopicProgress { get; set; } = [];
    public List<UpcomingEventSummaryDto> UpcomingEvents { get; set; } = [];
}

public sealed class EmotionTrendPointDto
{
    public string Date { get; set; } = "";
    public string? Emotion { get; set; }
    public string? PressureType { get; set; }
    public string? LearningState { get; set; }
    public string? IntensityLevel { get; set; }
    public long? Score { get; set; }
    public bool HasData { get; set; }

    [JsonIgnore]
    public string DateDisplay => DisplayFormat.Date(Date);

    [JsonIgnore]
    public string ScoreText => HasData && Score is not null ? $"强度 {Score}" : "暂无记录";

    [JsonIgnore]
    public string SummaryText =>
        HasData
            ? $"{DateDisplay} · {Emotion ?? "情绪未记录"} · {PressureType ?? "压力未记录"} · {ScoreText}"
            : $"{DateDisplay} · 暂无情绪记录";
}

public sealed class CourseMinutesDto
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public long Minutes { get; set; }
    public double Percentage { get; set; }

    [JsonIgnore]
    public string MinutesText => $"{Minutes} 分钟";

    [JsonIgnore]
    public string PercentageText => $"{Percentage:0.#}%";
}

public sealed class TopicProgressDto
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public long TotalTopics { get; set; }
    public long CompletedTopics { get; set; }
    public double CompletionRate { get; set; }

    [JsonIgnore]
    public string CompletionRateText => $"{CompletionRate:0.#}%";
}

public sealed class UpcomingEventSummaryDto
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string EventType { get; set; } = "";
    public string StartTime { get; set; } = "";
    public long DaysLeft { get; set; }
    public long Importance { get; set; }
    public string? RelatedCourseName { get; set; }
    public long RemainingTopics { get; set; }

    [JsonIgnore]
    public string StartTimeDisplay => DisplayFormat.Date(StartTime);

    [JsonIgnore]
    public string DaysLeftText => DaysLeft switch
    {
        < 0 => "已过期",
        0 => "今天",
        _ => $"{DaysLeft} 天后"
    };

    [JsonIgnore]
    public string RemainingTopicsText => $"{RemainingTopics} 个未完成";
}

internal static class DisplayFormat
{
    public static string Date(string value)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.ToString("d", CultureInfo.CurrentCulture)
            : value;
    }
}

public sealed class SettingsDto
{
    public string AdviceMode { get; set; } = "";
    public bool AiApiKeyConfigured { get; set; }
    public string AiBaseUrl { get; set; } = "";
    public string AiModel { get; set; } = "";
    public string DatabasePath { get; set; } = "";
}

public sealed class SettingsInput
{
    public string? AdviceMode { get; set; }
    public string? AiApiKey { get; set; }
    public string? AiBaseUrl { get; set; }
    public string? AiModel { get; set; }
}

public sealed class BackupResponseDto
{
    public string BackupPath { get; set; } = "";
}

public sealed class ExportResponseDto
{
    public List<CourseDto> Courses { get; set; } = [];
    public List<TopicDto> Topics { get; set; } = [];
    public List<EventDto> Events { get; set; } = [];
    public List<StudyRecordDto> StudyRecords { get; set; } = [];
    public List<EmotionLogDto> EmotionLogs { get; set; } = [];
    public List<AdviceLogDto> AdviceLogs { get; set; } = [];
}

public sealed class CourseInput
{
    public string Name { get; set; } = "";
}

public sealed class TopicInput
{
    public long CourseId { get; set; }
    public string Name { get; set; } = "";
    public string? MasteryLevel { get; set; }
    public long? Importance { get; set; }
    public long? EstimatedMinutes { get; set; }
    public long? ExamId { get; set; }
    public string? Status { get; set; }
}

public sealed class EventInput
{
    public string Title { get; set; } = "";
    public string EventType { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public long? Importance { get; set; }
    public long? RelatedCourseId { get; set; }
}

public sealed class StudyRecordInput
{
    public long TopicId { get; set; }
    public string Date { get; set; } = "";
    public long Minutes { get; set; }
    public string? Completion { get; set; }
    public string? Note { get; set; }
}

public sealed class TodayAdviceRequest
{
    public string? Date { get; set; }
    public string? StateText { get; set; }
}
