using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudyMind.App.Models;

namespace StudyMind.App.Services;

public sealed class StudyMindApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;

    public StudyMindApiClient(string baseAddress = "http://127.0.0.1:7878")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public Task<List<CourseDto>> GetCoursesAsync() =>
        GetAsync<List<CourseDto>>("/courses");

    public Task<CourseDto> CreateCourseAsync(CourseInput input) =>
        PostAsync<CourseInput, CourseDto>("/courses", input);

    public Task<CourseDto> UpdateCourseAsync(long id, CourseInput input) =>
        PutAsync<CourseInput, CourseDto>($"/courses/{id}", input);

    public Task DeleteCourseAsync(long id) =>
        DeleteAsync($"/courses/{id}");

    public Task<List<TopicDto>> GetTopicsAsync() =>
        GetAsync<List<TopicDto>>("/topics");

    public Task<TopicDto> CreateTopicAsync(TopicInput input) =>
        PostAsync<TopicInput, TopicDto>("/topics", input);

    public Task<TopicDto> UpdateTopicAsync(long id, TopicInput input) =>
        PutAsync<TopicInput, TopicDto>($"/topics/{id}", input);

    public Task DeleteTopicAsync(long id) =>
        DeleteAsync($"/topics/{id}");

    public Task<List<EventDto>> GetEventsAsync() =>
        GetAsync<List<EventDto>>("/events");

    public Task<EventDto> CreateEventAsync(EventInput input) =>
        PostAsync<EventInput, EventDto>("/events", input);

    public Task<EventDto> UpdateEventAsync(long id, EventInput input) =>
        PutAsync<EventInput, EventDto>($"/events/{id}", input);

    public Task DeleteEventAsync(long id) =>
        DeleteAsync($"/events/{id}");

    public Task<List<StudyRecordDto>> GetStudyRecordsAsync() =>
        GetAsync<List<StudyRecordDto>>("/study-records");

    public Task<StudyRecordDto> CreateStudyRecordAsync(StudyRecordInput input) =>
        PostAsync<StudyRecordInput, StudyRecordDto>("/study-records", input);

    public Task<StudyRecordDto> UpdateStudyRecordAsync(long id, StudyRecordInput input) =>
        PutAsync<StudyRecordInput, StudyRecordDto>($"/study-records/{id}", input);

    public Task DeleteStudyRecordAsync(long id) =>
        DeleteAsync($"/study-records/{id}");

    public Task<TodayAdviceResponseDto> GenerateTodayAdviceAsync(TodayAdviceRequest input) =>
        PostAsync<TodayAdviceRequest, TodayAdviceResponseDto>("/advice/today", input);

    public Task<WeeklyStatsResponseDto> GetWeeklyStatsAsync(string date) =>
        GetAsync<WeeklyStatsResponseDto>($"/stats/weekly?date={Uri.EscapeDataString(date)}");

    public Task<DashboardStatsResponseDto> GetDashboardStatsAsync(string date, int days = 14) =>
        GetAsync<DashboardStatsResponseDto>(
            $"/stats/dashboard?date={Uri.EscapeDataString(date)}&days={days}");

    public Task<SettingsDto> GetSettingsAsync() =>
        GetAsync<SettingsDto>("/settings");

    public Task<SettingsDto> UpdateSettingsAsync(SettingsInput input) =>
        PutAsync<SettingsInput, SettingsDto>("/settings", input);

    public Task<ExportResponseDto> ExportAsync() =>
        PostAsync<object, ExportResponseDto>("/export", new { });

    public Task<BackupResponseDto> BackupAsync() =>
        PostAsync<object, BackupResponseDto>("/backup", new { });

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await _httpClient.GetAsync(path);
        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<T>(response);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest payload)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, payload, JsonOptions);
        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<TResponse>(response);
    }

    private async Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest payload)
    {
        using var response = await _httpClient.PutAsJsonAsync(path, payload, JsonOptions);
        await EnsureSuccessAsync(response);
        return await ReadJsonAsync<TResponse>(response);
    }

    private async Task DeleteAsync(string path)
    {
        using var response = await _httpClient.DeleteAsync(path);
        await EnsureSuccessAsync(response);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return value ?? throw new InvalidOperationException("后端返回了空响应。");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"后端请求失败：{(int)response.StatusCode} {body}");
    }

    public static JsonSerializerOptions SerializerOptions => JsonOptions;
}
