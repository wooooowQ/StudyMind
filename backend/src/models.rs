use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize)]
pub struct HealthResponse {
    pub status: String,
    pub database: String,
}

#[derive(Debug, Serialize)]
pub struct Course {
    pub id: i64,
    pub name: String,
}

#[derive(Debug, Deserialize)]
pub struct CourseInput {
    pub name: String,
}

#[derive(Debug, Serialize)]
pub struct Topic {
    pub id: i64,
    pub course_id: i64,
    pub course_name: Option<String>,
    pub name: String,
    pub mastery_level: String,
    pub importance: i64,
    pub estimated_minutes: i64,
    pub exam_id: Option<i64>,
    pub status: String,
}

#[derive(Debug, Deserialize)]
pub struct TopicInput {
    pub course_id: i64,
    pub name: String,
    pub mastery_level: Option<String>,
    pub importance: Option<i64>,
    pub estimated_minutes: Option<i64>,
    pub exam_id: Option<i64>,
    pub status: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct EventItem {
    pub id: i64,
    pub title: String,
    pub event_type: String,
    pub start_time: String,
    pub end_time: String,
    pub importance: i64,
    pub related_course_id: Option<i64>,
    pub related_course_name: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct EventInput {
    pub title: String,
    pub event_type: String,
    pub start_time: String,
    pub end_time: String,
    pub importance: Option<i64>,
    pub related_course_id: Option<i64>,
}

#[derive(Debug, Serialize)]
pub struct StudyRecord {
    pub id: i64,
    pub topic_id: i64,
    pub topic_name: Option<String>,
    pub date: String,
    pub minutes: i64,
    pub completion: String,
    pub note: String,
}

#[derive(Debug, Deserialize)]
pub struct StudyRecordInput {
    pub topic_id: i64,
    pub date: String,
    pub minutes: i64,
    pub completion: Option<String>,
    pub note: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct EmotionLog {
    pub id: i64,
    pub date: String,
    pub raw_text: String,
    pub normalized_text: String,
    pub emotion: String,
    pub pressure_type: String,
    pub learning_state: String,
    pub intensity_level: String,
    pub suggestion_tone: String,
    pub score: i64,
    pub confidence: f64,
    pub matched_keywords: Vec<String>,
}

#[derive(Debug, Serialize)]
pub struct AdviceLog {
    pub id: i64,
    pub date: String,
    pub input_snapshot: String,
    pub generated_advice: String,
    pub model_type: String,
}

#[derive(Debug, Deserialize)]
pub struct AnalyzeEmotionRequest {
    pub raw_text: String,
    pub date: Option<String>,
}

#[derive(Debug, Serialize, Clone)]
pub struct EmotionAnalysis {
    pub date: String,
    pub raw_text: String,
    pub normalized_text: String,
    pub emotion: String,
    pub pressure_type: String,
    pub learning_state: String,
    pub intensity_level: String,
    pub suggestion_tone: String,
    pub score: i64,
    pub confidence: f64,
    pub matched_keywords: Vec<String>,
}

#[derive(Debug, Deserialize)]
pub struct TodayAdviceRequest {
    pub date: Option<String>,
    pub state_text: Option<String>,
}

#[derive(Debug, Serialize, Clone)]
pub struct RecommendedTask {
    pub topic_id: i64,
    pub topic_name: String,
    pub course_name: Option<String>,
    pub estimated_minutes: i64,
    pub priority_score: f64,
    pub reason: String,
}

#[derive(Debug, Serialize)]
pub struct TodayAdviceResponse {
    pub date: String,
    pub model_type: String,
    pub emotion: Option<EmotionAnalysis>,
    pub advice: String,
    pub recommended_tasks: Vec<RecommendedTask>,
    pub saved_advice_id: i64,
    pub fallback_reason: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct WeeklyStatsResponse {
    pub from: String,
    pub to: String,
    pub daily_minutes: Vec<DailyMinutes>,
}

#[derive(Debug, Serialize)]
pub struct DailyMinutes {
    pub date: String,
    pub minutes: i64,
}

#[derive(Debug, Deserialize)]
pub struct StatsQuery {
    pub date: Option<String>,
    pub days: Option<i64>,
}

#[derive(Debug, Serialize)]
pub struct DashboardStatsResponse {
    pub from: String,
    pub to: String,
    pub total_minutes: i64,
    pub average_daily_minutes: f64,
    pub total_topics: i64,
    pub completed_topics: i64,
    pub overall_completion_rate: f64,
    pub daily_minutes: Vec<DailyMinutes>,
    pub emotion_trend: Vec<EmotionTrendPoint>,
    pub course_minutes: Vec<CourseMinutes>,
    pub topic_progress: Vec<TopicProgress>,
    pub upcoming_events: Vec<UpcomingEventSummary>,
}

#[derive(Debug, Serialize)]
pub struct EmotionTrendPoint {
    pub date: String,
    pub emotion: Option<String>,
    pub pressure_type: Option<String>,
    pub learning_state: Option<String>,
    pub intensity_level: Option<String>,
    pub score: Option<i64>,
    pub has_data: bool,
}

#[derive(Debug, Serialize)]
pub struct CourseMinutes {
    pub course_id: i64,
    pub course_name: String,
    pub minutes: i64,
    pub percentage: f64,
}

#[derive(Debug, Serialize)]
pub struct TopicProgress {
    pub course_id: i64,
    pub course_name: String,
    pub total_topics: i64,
    pub completed_topics: i64,
    pub completion_rate: f64,
}

#[derive(Debug, Serialize)]
pub struct UpcomingEventSummary {
    pub id: i64,
    pub title: String,
    pub event_type: String,
    pub start_time: String,
    pub days_left: i64,
    pub importance: i64,
    pub related_course_name: Option<String>,
    pub remaining_topics: i64,
}

#[derive(Debug, Serialize)]
pub struct SettingsResponse {
    pub advice_mode: String,
    pub ai_api_key_configured: bool,
    pub ai_base_url: String,
    pub ai_model: String,
    pub database_path: String,
}

#[derive(Debug, Deserialize)]
pub struct SettingsInput {
    pub advice_mode: Option<String>,
    pub ai_api_key: Option<String>,
    pub ai_base_url: Option<String>,
    pub ai_model: Option<String>,
}

#[derive(Debug, Clone)]
pub struct AppSettings {
    pub advice_mode: String,
    pub ai_api_key: Option<String>,
    pub ai_base_url: String,
    pub ai_model: String,
}

#[derive(Debug, Serialize)]
pub struct BackupResponse {
    pub backup_path: String,
}

#[derive(Debug, Serialize)]
pub struct ExportResponse {
    pub courses: Vec<Course>,
    pub topics: Vec<Topic>,
    pub events: Vec<EventItem>,
    pub study_records: Vec<StudyRecord>,
    pub emotion_logs: Vec<EmotionLog>,
    pub advice_logs: Vec<AdviceLog>,
}
