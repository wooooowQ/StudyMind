use std::collections::HashMap;

use axum::{
    Json, Router,
    extract::{Path, Query, State},
    http::StatusCode,
    routing::{get, put},
};
use chrono::{Duration, Local, NaiveDate, NaiveDateTime};
use rusqlite::{Connection, OptionalExtension, Row, params};
use serde::Serialize;
use tower_http::{cors::CorsLayer, trace::TraceLayer};

use crate::{
    advice, ai,
    errors::{AppError, AppResult},
    models::{
        AdviceLog, AnalyzeEmotionRequest, AppSettings, BackupResponse, Course, CourseInput,
        CourseMinutes, DailyMinutes, DashboardStatsResponse, EmotionLog, EmotionTrendPoint,
        EventInput, EventItem, ExportResponse, HealthResponse, SettingsInput, SettingsResponse,
        StatsQuery, StudyRecord, StudyRecordInput, TodayAdviceRequest, TodayAdviceResponse, Topic,
        TopicInput, TopicProgress, UpcomingEventSummary, WeeklyStatsResponse,
    },
    state::AppState,
};

pub fn router(state: AppState) -> Router {
    Router::new()
        .route("/health", get(health))
        .route("/courses", get(list_courses).post(create_course))
        .route("/courses/{id}", put(update_course).delete(delete_course))
        .route("/topics", get(list_topics).post(create_topic))
        .route("/topics/{id}", put(update_topic).delete(delete_topic))
        .route("/events", get(list_events).post(create_event))
        .route("/events/{id}", put(update_event).delete(delete_event))
        .route(
            "/study-records",
            get(list_study_records).post(create_study_record),
        )
        .route(
            "/study-records/{id}",
            put(update_study_record).delete(delete_study_record),
        )
        .route(
            "/emotion/analyze",
            get(|| async { StatusCode::METHOD_NOT_ALLOWED }).post(analyze_emotion),
        )
        .route(
            "/advice/today",
            get(|| async { StatusCode::METHOD_NOT_ALLOWED }).post(today_advice),
        )
        .route("/stats/weekly", get(weekly_stats))
        .route("/stats/dashboard", get(dashboard_stats))
        .route("/settings", get(get_settings).put(update_settings))
        .route(
            "/export",
            get(|| async { StatusCode::METHOD_NOT_ALLOWED }).post(export_data),
        )
        .route(
            "/backup",
            get(|| async { StatusCode::METHOD_NOT_ALLOWED }).post(backup_database),
        )
        .layer(CorsLayer::permissive())
        .layer(TraceLayer::new_for_http())
        .with_state(state)
}

async fn health(State(state): State<AppState>) -> AppResult<Json<HealthResponse>> {
    let conn = state.connection()?;
    conn.query_row("SELECT 1", [], |_| Ok(()))?;

    Ok(Json(HealthResponse {
        status: "ok".to_string(),
        database: state.database_path.display().to_string(),
    }))
}

async fn list_courses(State(state): State<AppState>) -> AppResult<Json<Vec<Course>>> {
    let conn = state.connection()?;
    Ok(Json(fetch_courses(&conn)?))
}

async fn create_course(
    State(state): State<AppState>,
    Json(input): Json<CourseInput>,
) -> AppResult<Json<Course>> {
    let name = required_text(input.name, "课程名称")?;
    let conn = state.connection()?;

    conn.execute("INSERT INTO courses (name) VALUES (?1)", params![name])?;

    Ok(Json(fetch_course(&conn, conn.last_insert_rowid())?))
}

async fn update_course(
    State(state): State<AppState>,
    Path(id): Path<i64>,
    Json(input): Json<CourseInput>,
) -> AppResult<Json<Course>> {
    let name = required_text(input.name, "课程名称")?;
    let conn = state.connection()?;

    let rows = conn.execute(
        "UPDATE courses SET name = ?1 WHERE id = ?2",
        params![name, id],
    )?;

    ensure_changed(rows, "课程", id)?;
    Ok(Json(fetch_course(&conn, id)?))
}

async fn delete_course(
    State(state): State<AppState>,
    Path(id): Path<i64>,
) -> AppResult<StatusCode> {
    let conn = state.connection()?;
    let rows = conn.execute("DELETE FROM courses WHERE id = ?1", params![id])?;

    ensure_changed(rows, "课程", id)?;
    Ok(StatusCode::NO_CONTENT)
}

async fn list_topics(State(state): State<AppState>) -> AppResult<Json<Vec<Topic>>> {
    let conn = state.connection()?;
    Ok(Json(fetch_topics(&conn)?))
}

async fn create_topic(
    State(state): State<AppState>,
    Json(input): Json<TopicInput>,
) -> AppResult<Json<Topic>> {
    let name = required_text(input.name, "知识点名称")?;
    let mastery_level = optional_text(input.mastery_level, "未掌握")?;
    let importance = importance(input.importance.unwrap_or(3))?;
    let estimated_minutes = positive_minutes(input.estimated_minutes.unwrap_or(30))?;
    let status = optional_text(input.status, "pending")?;
    let conn = state.connection()?;

    ensure_course_exists(&conn, input.course_id)?;
    if let Some(exam_id) = input.exam_id {
        ensure_event_exists(&conn, exam_id)?;
    }

    conn.execute(
        r#"
        INSERT INTO topics
            (course_id, name, mastery_level, importance, estimated_minutes, exam_id, status)
        VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)
        "#,
        params![
            input.course_id,
            name,
            mastery_level,
            importance,
            estimated_minutes,
            input.exam_id,
            status
        ],
    )?;

    Ok(Json(fetch_topic(&conn, conn.last_insert_rowid())?))
}

async fn update_topic(
    State(state): State<AppState>,
    Path(id): Path<i64>,
    Json(input): Json<TopicInput>,
) -> AppResult<Json<Topic>> {
    let name = required_text(input.name, "知识点名称")?;
    let mastery_level = optional_text(input.mastery_level, "未掌握")?;
    let importance = importance(input.importance.unwrap_or(3))?;
    let estimated_minutes = positive_minutes(input.estimated_minutes.unwrap_or(30))?;
    let status = optional_text(input.status, "pending")?;
    let conn = state.connection()?;

    ensure_topic_exists(&conn, id)?;
    ensure_course_exists(&conn, input.course_id)?;
    if let Some(exam_id) = input.exam_id {
        ensure_event_exists(&conn, exam_id)?;
    }

    conn.execute(
        r#"
        UPDATE topics
        SET course_id = ?1, name = ?2, mastery_level = ?3, importance = ?4,
            estimated_minutes = ?5, exam_id = ?6, status = ?7
        WHERE id = ?8
        "#,
        params![
            input.course_id,
            name,
            mastery_level,
            importance,
            estimated_minutes,
            input.exam_id,
            status,
            id
        ],
    )?;

    Ok(Json(fetch_topic(&conn, id)?))
}

async fn delete_topic(State(state): State<AppState>, Path(id): Path<i64>) -> AppResult<StatusCode> {
    let conn = state.connection()?;
    let rows = conn.execute("DELETE FROM topics WHERE id = ?1", params![id])?;

    ensure_changed(rows, "知识点", id)?;
    Ok(StatusCode::NO_CONTENT)
}

async fn list_events(State(state): State<AppState>) -> AppResult<Json<Vec<EventItem>>> {
    let conn = state.connection()?;
    Ok(Json(fetch_events(&conn)?))
}

async fn create_event(
    State(state): State<AppState>,
    Json(input): Json<EventInput>,
) -> AppResult<Json<EventItem>> {
    let title = required_text(input.title, "日程标题")?;
    let event_type = optional_text(Some(input.event_type), "考试")?;
    let start_time = required_text(input.start_time, "开始时间")?;
    let end_time = required_text(input.end_time, "结束时间")?;
    validate_event_range(&start_time, &end_time)?;
    let importance = importance(input.importance.unwrap_or(3))?;
    let conn = state.connection()?;

    if let Some(course_id) = input.related_course_id {
        ensure_course_exists(&conn, course_id)?;
    }

    conn.execute(
        r#"
        INSERT INTO events
            (title, event_type, start_time, end_time, importance, related_course_id)
        VALUES (?1, ?2, ?3, ?4, ?5, ?6)
        "#,
        params![
            title,
            event_type,
            start_time,
            end_time,
            importance,
            input.related_course_id
        ],
    )?;

    Ok(Json(fetch_event(&conn, conn.last_insert_rowid())?))
}

async fn update_event(
    State(state): State<AppState>,
    Path(id): Path<i64>,
    Json(input): Json<EventInput>,
) -> AppResult<Json<EventItem>> {
    let title = required_text(input.title, "日程标题")?;
    let event_type = optional_text(Some(input.event_type), "考试")?;
    let start_time = required_text(input.start_time, "开始时间")?;
    let end_time = required_text(input.end_time, "结束时间")?;
    validate_event_range(&start_time, &end_time)?;
    let importance = importance(input.importance.unwrap_or(3))?;
    let conn = state.connection()?;

    ensure_event_exists(&conn, id)?;
    if let Some(course_id) = input.related_course_id {
        ensure_course_exists(&conn, course_id)?;
    }

    conn.execute(
        r#"
        UPDATE events
        SET title = ?1, event_type = ?2, start_time = ?3, end_time = ?4,
            importance = ?5, related_course_id = ?6
        WHERE id = ?7
        "#,
        params![
            title,
            event_type,
            start_time,
            end_time,
            importance,
            input.related_course_id,
            id
        ],
    )?;

    Ok(Json(fetch_event(&conn, id)?))
}

async fn delete_event(State(state): State<AppState>, Path(id): Path<i64>) -> AppResult<StatusCode> {
    let conn = state.connection()?;
    let rows = conn.execute("DELETE FROM events WHERE id = ?1", params![id])?;

    ensure_changed(rows, "日程", id)?;
    Ok(StatusCode::NO_CONTENT)
}

async fn list_study_records(State(state): State<AppState>) -> AppResult<Json<Vec<StudyRecord>>> {
    let conn = state.connection()?;
    Ok(Json(fetch_study_records(&conn)?))
}

async fn create_study_record(
    State(state): State<AppState>,
    Json(input): Json<StudyRecordInput>,
) -> AppResult<Json<StudyRecord>> {
    let date = required_text(input.date, "学习日期")?;
    let minutes = positive_minutes(input.minutes)?;
    let completion = optional_text(input.completion, "partial")?;
    let note = input.note.unwrap_or_default();
    let conn = state.connection()?;

    ensure_topic_exists(&conn, input.topic_id)?;

    conn.execute(
        r#"
        INSERT INTO study_records (topic_id, date, minutes, completion, note)
        VALUES (?1, ?2, ?3, ?4, ?5)
        "#,
        params![input.topic_id, date, minutes, completion, note],
    )?;

    sync_topic_status_after_record(&conn, input.topic_id, &completion)?;

    Ok(Json(fetch_study_record(&conn, conn.last_insert_rowid())?))
}

async fn update_study_record(
    State(state): State<AppState>,
    Path(id): Path<i64>,
    Json(input): Json<StudyRecordInput>,
) -> AppResult<Json<StudyRecord>> {
    let date = required_text(input.date, "学习日期")?;
    let minutes = positive_minutes(input.minutes)?;
    let completion = optional_text(input.completion, "partial")?;
    let note = input.note.unwrap_or_default();
    let conn = state.connection()?;

    ensure_study_record_exists(&conn, id)?;
    ensure_topic_exists(&conn, input.topic_id)?;

    conn.execute(
        r#"
        UPDATE study_records
        SET topic_id = ?1, date = ?2, minutes = ?3, completion = ?4, note = ?5
        WHERE id = ?6
        "#,
        params![input.topic_id, date, minutes, completion, note, id],
    )?;

    sync_topic_status_after_record(&conn, input.topic_id, &completion)?;

    Ok(Json(fetch_study_record(&conn, id)?))
}

async fn delete_study_record(
    State(state): State<AppState>,
    Path(id): Path<i64>,
) -> AppResult<StatusCode> {
    let conn = state.connection()?;
    let rows = conn.execute("DELETE FROM study_records WHERE id = ?1", params![id])?;

    ensure_changed(rows, "学习记录", id)?;
    Ok(StatusCode::NO_CONTENT)
}

async fn analyze_emotion(
    State(state): State<AppState>,
    Json(input): Json<AnalyzeEmotionRequest>,
) -> AppResult<Json<crate::models::EmotionAnalysis>> {
    let raw_text = required_text(input.raw_text, "今日状态")?;
    let date = input.date.unwrap_or_else(advice::today_string);
    let analysis = advice::classify_emotion(&raw_text, date);
    let conn = state.connection()?;

    advice::save_emotion(&conn, &analysis)?;

    Ok(Json(analysis))
}

async fn today_advice(
    State(state): State<AppState>,
    Json(input): Json<TodayAdviceRequest>,
) -> AppResult<Json<TodayAdviceResponse>> {
    let date = input.date.unwrap_or_else(advice::today_string);
    let (settings, rule_draft) = {
        let conn = state.connection()?;
        let emotion = match input.state_text {
            Some(text) if !text.trim().is_empty() => {
                let analysis = advice::classify_emotion(&text, date.clone());
                advice::save_emotion(&conn, &analysis)?;
                Some(analysis)
            }
            _ => advice::latest_emotion_for_date(&conn, &date)?,
        };
        let settings = fetch_app_settings(&conn)?;
        let rule_draft =
            advice::build_today_advice_draft(&conn, date, emotion, "rules-v2".to_string(), None)?;
        (settings, rule_draft)
    };

    let final_draft = if settings.advice_mode == "rules" {
        rule_draft
    } else {
        match ai::try_generate_ai_advice(&settings, &rule_draft).await {
            Ok(ai_advice) => {
                rule_draft.with_ai_advice(format!("ai:{}", settings.ai_model), ai_advice, None)
            }
            Err(reason) => rule_draft.with_ai_advice(
                "rules-v2-fallback".to_string(),
                rule_draft.advice.clone(),
                Some(format!("AI 不可用，已回退到规则建议：{}", reason)),
            ),
        }
    };

    let conn = state.connection()?;
    let saved_advice_id = advice::save_advice(&conn, &final_draft)?;

    Ok(Json(final_draft.into_response(saved_advice_id)))
}

async fn weekly_stats(
    State(state): State<AppState>,
    Query(query): Query<StatsQuery>,
) -> AppResult<Json<WeeklyStatsResponse>> {
    let to_date = query
        .date
        .as_deref()
        .and_then(parse_date)
        .unwrap_or_else(|| Local::now().date_naive());
    let from_date = to_date - Duration::days(6);
    let conn = state.connection()?;
    let daily_minutes = fetch_daily_minutes(&conn, from_date, to_date, 7)?;

    Ok(Json(WeeklyStatsResponse {
        from: from_date.to_string(),
        to: to_date.to_string(),
        daily_minutes,
    }))
}

async fn dashboard_stats(
    State(state): State<AppState>,
    Query(query): Query<StatsQuery>,
) -> AppResult<Json<DashboardStatsResponse>> {
    let to_date = query
        .date
        .as_deref()
        .and_then(parse_date)
        .unwrap_or_else(|| Local::now().date_naive());
    let days = query.days.unwrap_or(14).clamp(7, 30);
    let from_date = to_date - Duration::days(days - 1);
    let conn = state.connection()?;

    let daily_minutes = fetch_daily_minutes(&conn, from_date, to_date, days)?;
    let total_minutes = daily_minutes.iter().map(|item| item.minutes).sum::<i64>();
    let average_daily_minutes = round2(total_minutes as f64 / days as f64);
    let course_minutes = fetch_course_minutes(&conn, from_date, to_date, total_minutes)?;
    let topic_progress = fetch_topic_progress(&conn)?;
    let completed_topics = topic_progress
        .iter()
        .map(|item| item.completed_topics)
        .sum::<i64>();
    let total_topics = topic_progress
        .iter()
        .map(|item| item.total_topics)
        .sum::<i64>();
    let overall_completion_rate = percentage(completed_topics, total_topics);
    let emotion_trend = fetch_emotion_trend(&conn, from_date, to_date, days)?;
    let upcoming_events = fetch_upcoming_events(&conn, to_date)?;

    Ok(Json(DashboardStatsResponse {
        from: from_date.to_string(),
        to: to_date.to_string(),
        total_minutes,
        average_daily_minutes,
        total_topics,
        completed_topics,
        overall_completion_rate,
        daily_minutes,
        emotion_trend,
        course_minutes,
        topic_progress,
        upcoming_events,
    }))
}

async fn get_settings(State(state): State<AppState>) -> AppResult<Json<SettingsResponse>> {
    let conn = state.connection()?;
    Ok(Json(fetch_settings(&conn, &state)?))
}

async fn update_settings(
    State(state): State<AppState>,
    Json(input): Json<SettingsInput>,
) -> AppResult<Json<SettingsResponse>> {
    let conn = state.connection()?;
    let current = fetch_app_settings(&conn)?;

    let advice_mode = input.advice_mode.unwrap_or(current.advice_mode);
    if !matches!(advice_mode.as_str(), "rules" | "ai" | "hybrid") {
        return Err(AppError::Validation(
            "advice_mode 只能是 rules、ai 或 hybrid".to_string(),
        ));
    }

    let ai_api_key = match input.ai_api_key {
        Some(value) if value.trim().is_empty() => current.ai_api_key,
        Some(value) => Some(value.trim().to_string()),
        None => current.ai_api_key,
    };
    let ai_base_url = input
        .ai_base_url
        .map(|value| value.trim().trim_end_matches('/').to_string())
        .filter(|value| !value.is_empty())
        .unwrap_or(current.ai_base_url);
    let ai_model = input
        .ai_model
        .map(|value| value.trim().to_string())
        .filter(|value| !value.is_empty())
        .unwrap_or(current.ai_model);

    conn.execute(
        r#"
        UPDATE settings
        SET advice_mode = ?1,
            ai_api_key = ?2,
            ai_base_url = ?3,
            ai_model = ?4,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = 1
        "#,
        params![advice_mode, ai_api_key, ai_base_url, ai_model],
    )?;

    Ok(Json(fetch_settings(&conn, &state)?))
}

fn fetch_app_settings(conn: &Connection) -> AppResult<AppSettings> {
    conn.query_row(
        r#"
        SELECT advice_mode, ai_api_key, ai_base_url, ai_model
        FROM settings
        WHERE id = 1
        "#,
        [],
        |row| {
            Ok(AppSettings {
                advice_mode: row.get(0)?,
                ai_api_key: row.get(1)?,
                ai_base_url: row.get(2)?,
                ai_model: row.get(3)?,
            })
        },
    )
    .map_err(AppError::from)
}

async fn export_data(State(state): State<AppState>) -> AppResult<Json<ExportResponse>> {
    let conn = state.connection()?;

    let mut stmt = conn.prepare(
        r#"
        SELECT
            id,
            date,
            raw_text,
            normalized_text,
            emotion,
            pressure_type,
            learning_state,
            intensity_level,
            suggestion_tone,
            score,
            confidence,
            matched_keywords
        FROM emotion_logs
        ORDER BY date DESC, id DESC
        "#,
    )?;

    let emotion_logs = stmt
        .query_map([], map_emotion_log)?
        .collect::<Result<Vec<_>, _>>()?;

    let mut stmt = conn.prepare(
        r#"
        SELECT id, date, input_snapshot, generated_advice, model_type
        FROM advice_logs
        ORDER BY date DESC, id DESC
        "#,
    )?;

    let advice_logs = stmt
        .query_map([], map_advice_log)?
        .collect::<Result<Vec<_>, _>>()?;

    Ok(Json(ExportResponse {
        courses: fetch_courses(&conn)?,
        topics: fetch_topics(&conn)?,
        events: fetch_events(&conn)?,
        study_records: fetch_study_records(&conn)?,
        emotion_logs,
        advice_logs,
    }))
}

async fn backup_database(State(state): State<AppState>) -> AppResult<Json<BackupResponse>> {
    {
        let conn = state.connection()?;
        conn.execute_batch("PRAGMA wal_checkpoint(TRUNCATE);")?;
    }

    let source = state.database_path.as_ref();
    let backup_dir = source
        .parent()
        .map(|path| path.join("backups"))
        .unwrap_or_else(|| std::path::PathBuf::from("backups"));
    tokio::fs::create_dir_all(&backup_dir).await?;

    let backup_path = backup_dir.join(format!(
        "studymind-{}.db",
        Local::now().format("%Y%m%d%H%M%S")
    ));
    tokio::fs::copy(source, &backup_path).await?;

    Ok(Json(BackupResponse {
        backup_path: backup_path.display().to_string(),
    }))
}

fn fetch_courses(conn: &Connection) -> AppResult<Vec<Course>> {
    let mut stmt = conn.prepare("SELECT id, name FROM courses ORDER BY name COLLATE NOCASE")?;
    Ok(stmt
        .query_map([], map_course)?
        .collect::<Result<Vec<_>, _>>()?)
}

fn fetch_course(conn: &Connection, id: i64) -> AppResult<Course> {
    conn.query_row(
        "SELECT id, name FROM courses WHERE id = ?1",
        params![id],
        map_course,
    )
    .optional()?
    .ok_or_else(|| AppError::NotFound(format!("课程 {} 不存在", id)))
}

fn fetch_topics(conn: &Connection) -> AppResult<Vec<Topic>> {
    let mut stmt = conn.prepare(
        r#"
        SELECT
            t.id,
            t.course_id,
            c.name AS course_name,
            t.name,
            t.mastery_level,
            t.importance,
            t.estimated_minutes,
            t.exam_id,
            t.status
        FROM topics t
        LEFT JOIN courses c ON c.id = t.course_id
        ORDER BY t.id DESC
        "#,
    )?;

    Ok(stmt
        .query_map([], map_topic)?
        .collect::<Result<Vec<_>, _>>()?)
}

fn fetch_topic(conn: &Connection, id: i64) -> AppResult<Topic> {
    conn.query_row(
        r#"
        SELECT
            t.id,
            t.course_id,
            c.name AS course_name,
            t.name,
            t.mastery_level,
            t.importance,
            t.estimated_minutes,
            t.exam_id,
            t.status
        FROM topics t
        LEFT JOIN courses c ON c.id = t.course_id
        WHERE t.id = ?1
        "#,
        params![id],
        map_topic,
    )
    .optional()?
    .ok_or_else(|| AppError::NotFound(format!("知识点 {} 不存在", id)))
}

fn fetch_events(conn: &Connection) -> AppResult<Vec<EventItem>> {
    let mut stmt = conn.prepare(
        r#"
        SELECT
            e.id,
            e.title,
            e.event_type,
            e.start_time,
            e.end_time,
            e.importance,
            e.related_course_id,
            c.name AS related_course_name
        FROM events e
        LEFT JOIN courses c ON c.id = e.related_course_id
        ORDER BY e.start_time ASC, e.id DESC
        "#,
    )?;

    Ok(stmt
        .query_map([], map_event)?
        .collect::<Result<Vec<_>, _>>()?)
}

fn fetch_event(conn: &Connection, id: i64) -> AppResult<EventItem> {
    conn.query_row(
        r#"
        SELECT
            e.id,
            e.title,
            e.event_type,
            e.start_time,
            e.end_time,
            e.importance,
            e.related_course_id,
            c.name AS related_course_name
        FROM events e
        LEFT JOIN courses c ON c.id = e.related_course_id
        WHERE e.id = ?1
        "#,
        params![id],
        map_event,
    )
    .optional()?
    .ok_or_else(|| AppError::NotFound(format!("日程 {} 不存在", id)))
}

fn fetch_study_records(conn: &Connection) -> AppResult<Vec<StudyRecord>> {
    let mut stmt = conn.prepare(
        r#"
        SELECT
            r.id,
            r.topic_id,
            t.name AS topic_name,
            r.date,
            r.minutes,
            r.completion,
            r.note
        FROM study_records r
        LEFT JOIN topics t ON t.id = r.topic_id
        ORDER BY r.date DESC, r.id DESC
        "#,
    )?;

    Ok(stmt
        .query_map([], map_study_record)?
        .collect::<Result<Vec<_>, _>>()?)
}

fn fetch_study_record(conn: &Connection, id: i64) -> AppResult<StudyRecord> {
    conn.query_row(
        r#"
        SELECT
            r.id,
            r.topic_id,
            t.name AS topic_name,
            r.date,
            r.minutes,
            r.completion,
            r.note
        FROM study_records r
        LEFT JOIN topics t ON t.id = r.topic_id
        WHERE r.id = ?1
        "#,
        params![id],
        map_study_record,
    )
    .optional()?
    .ok_or_else(|| AppError::NotFound(format!("学习记录 {} 不存在", id)))
}

fn fetch_daily_minutes(
    conn: &Connection,
    from_date: NaiveDate,
    to_date: NaiveDate,
    days: i64,
) -> AppResult<Vec<DailyMinutes>> {
    let mut stmt = conn.prepare(
        r#"
        SELECT date, COALESCE(SUM(minutes), 0) AS minutes
        FROM study_records
        WHERE date >= ?1 AND date <= ?2
        GROUP BY date
        ORDER BY date
        "#,
    )?;

    let mut by_date = stmt
        .query_map(params![from_date.to_string(), to_date.to_string()], |row| {
            Ok((row.get::<_, String>(0)?, row.get::<_, i64>(1)?))
        })?
        .collect::<Result<HashMap<String, i64>, _>>()?;

    Ok((0..days)
        .map(|offset| {
            let date = (from_date + Duration::days(offset)).to_string();
            DailyMinutes {
                minutes: by_date.remove(&date).unwrap_or_default(),
                date,
            }
        })
        .collect())
}

fn fetch_course_minutes(
    conn: &Connection,
    from_date: NaiveDate,
    to_date: NaiveDate,
    total_minutes: i64,
) -> AppResult<Vec<CourseMinutes>> {
    let mut stmt = conn.prepare(
        r#"
        SELECT
            c.id,
            c.name,
            COALESCE(SUM(r.minutes), 0) AS minutes
        FROM study_records r
        INNER JOIN topics t ON t.id = r.topic_id
        INNER JOIN courses c ON c.id = t.course_id
        WHERE r.date >= ?1 AND r.date <= ?2
        GROUP BY c.id, c.name
        ORDER BY minutes DESC, c.name COLLATE NOCASE
        "#,
    )?;

    Ok(stmt
        .query_map(params![from_date.to_string(), to_date.to_string()], |row| {
            let minutes = row.get::<_, i64>(2)?;
            Ok(CourseMinutes {
                course_id: row.get(0)?,
                course_name: row.get(1)?,
                minutes,
                percentage: percentage(minutes, total_minutes),
            })
        })?
        .collect::<Result<Vec<_>, _>>()?)
}

fn fetch_topic_progress(conn: &Connection) -> AppResult<Vec<TopicProgress>> {
    let mut stmt = conn.prepare(
        r#"
        SELECT
            c.id,
            c.name,
            COUNT(t.id) AS total_topics,
            COALESCE(SUM(
                CASE
                    WHEN t.status IN ('completed', 'done', '已完成')
                      OR t.mastery_level IN ('已掌握', '掌握', 'mastered')
                    THEN 1
                    ELSE 0
                END
            ), 0) AS completed_topics
        FROM courses c
        LEFT JOIN topics t ON t.course_id = c.id
        GROUP BY c.id, c.name
        ORDER BY c.name COLLATE NOCASE
        "#,
    )?;

    Ok(stmt
        .query_map([], |row| {
            let total_topics = row.get::<_, i64>(2)?;
            let completed_topics = row.get::<_, i64>(3)?;
            Ok(TopicProgress {
                course_id: row.get(0)?,
                course_name: row.get(1)?,
                total_topics,
                completed_topics,
                completion_rate: percentage(completed_topics, total_topics),
            })
        })?
        .collect::<Result<Vec<_>, _>>()?)
}

fn fetch_emotion_trend(
    conn: &Connection,
    from_date: NaiveDate,
    to_date: NaiveDate,
    days: i64,
) -> AppResult<Vec<EmotionTrendPoint>> {
    let mut stmt = conn.prepare(
        r#"
        SELECT
            e.date,
            e.emotion,
            e.pressure_type,
            e.learning_state,
            e.intensity_level,
            e.score
        FROM emotion_logs e
        INNER JOIN (
            SELECT date, MAX(id) AS latest_id
            FROM emotion_logs
            WHERE date >= ?1 AND date <= ?2
            GROUP BY date
        ) latest ON latest.latest_id = e.id
        ORDER BY e.date
        "#,
    )?;

    let mut by_date = stmt
        .query_map(params![from_date.to_string(), to_date.to_string()], |row| {
            let date = row.get::<_, String>(0)?;
            Ok((
                date.clone(),
                EmotionTrendPoint {
                    date,
                    emotion: Some(row.get(1)?),
                    pressure_type: Some(row.get(2)?),
                    learning_state: Some(row.get(3)?),
                    intensity_level: Some(row.get(4)?),
                    score: Some(row.get(5)?),
                    has_data: true,
                },
            ))
        })?
        .collect::<Result<HashMap<String, EmotionTrendPoint>, _>>()?;

    Ok((0..days)
        .map(|offset| {
            let date = (from_date + Duration::days(offset)).to_string();
            by_date.remove(&date).unwrap_or(EmotionTrendPoint {
                date,
                emotion: None,
                pressure_type: None,
                learning_state: None,
                intensity_level: None,
                score: None,
                has_data: false,
            })
        })
        .collect())
}

fn fetch_upcoming_events(
    conn: &Connection,
    target_date: NaiveDate,
) -> AppResult<Vec<UpcomingEventSummary>> {
    let mut stmt = conn.prepare(
        r#"
        SELECT
            e.id,
            e.title,
            e.event_type,
            e.start_time,
            e.importance,
            c.name AS related_course_name,
            (
                SELECT COUNT(*)
                FROM topics t
                WHERE t.exam_id = e.id
                  AND t.status NOT IN ('completed', 'done', '已完成')
                  AND t.mastery_level NOT IN ('已掌握', '掌握', 'mastered')
            ) AS remaining_topics
        FROM events e
        LEFT JOIN courses c ON c.id = e.related_course_id
        ORDER BY e.id DESC
        "#,
    )?;

    let mut events = stmt
        .query_map([], |row| {
            let start_time = row.get::<_, String>(3)?;
            let event_date = parse_event_date(&start_time);
            let days_left = event_date
                .map(|date| (date - target_date).num_days())
                .unwrap_or(i64::MAX);

            Ok((
                event_date,
                UpcomingEventSummary {
                    id: row.get(0)?,
                    title: row.get(1)?,
                    event_type: row.get(2)?,
                    start_time,
                    days_left,
                    importance: row.get(4)?,
                    related_course_name: row.get(5)?,
                    remaining_topics: row.get(6)?,
                },
            ))
        })?
        .collect::<Result<Vec<_>, _>>()?;

    events.retain(|(event_date, _)| event_date.is_some_and(|date| date >= target_date));
    events.sort_by(|(left_date, left), (right_date, right)| {
        left_date
            .cmp(right_date)
            .then_with(|| right.importance.cmp(&left.importance))
            .then_with(|| right.id.cmp(&left.id))
    });

    Ok(events.into_iter().take(6).map(|(_, event)| event).collect())
}

fn fetch_settings(conn: &Connection, state: &AppState) -> AppResult<SettingsResponse> {
    let (advice_mode, ai_api_key_configured, ai_base_url, ai_model): (String, i64, String, String) = conn.query_row(
        r#"
        SELECT
            advice_mode,
            CASE WHEN ai_api_key IS NULL OR ai_api_key = '' THEN 0 ELSE 1 END AS ai_api_key_configured,
            ai_base_url,
            ai_model
        FROM settings
        WHERE id = 1
        "#,
        [],
        |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?, row.get(3)?)),
    )?;

    Ok(SettingsResponse {
        advice_mode,
        ai_api_key_configured: ai_api_key_configured == 1,
        ai_base_url,
        ai_model,
        database_path: state.database_path.display().to_string(),
    })
}

fn ensure_course_exists(conn: &Connection, id: i64) -> AppResult<()> {
    fetch_course(conn, id).map(|_| ())
}

fn ensure_event_exists(conn: &Connection, id: i64) -> AppResult<()> {
    fetch_event(conn, id).map(|_| ())
}

fn ensure_topic_exists(conn: &Connection, id: i64) -> AppResult<()> {
    fetch_topic(conn, id).map(|_| ())
}

fn ensure_study_record_exists(conn: &Connection, id: i64) -> AppResult<()> {
    fetch_study_record(conn, id).map(|_| ())
}

fn sync_topic_status_after_record(
    conn: &Connection,
    topic_id: i64,
    completion: &str,
) -> AppResult<()> {
    if matches!(completion, "completed" | "done" | "已完成" | "完成") {
        conn.execute(
            "UPDATE topics SET status = 'completed', mastery_level = '已掌握' WHERE id = ?1",
            params![topic_id],
        )?;
    }

    Ok(())
}

fn map_course(row: &Row<'_>) -> rusqlite::Result<Course> {
    Ok(Course {
        id: row.get(0)?,
        name: row.get(1)?,
    })
}

fn map_topic(row: &Row<'_>) -> rusqlite::Result<Topic> {
    Ok(Topic {
        id: row.get(0)?,
        course_id: row.get(1)?,
        course_name: row.get(2)?,
        name: row.get(3)?,
        mastery_level: row.get(4)?,
        importance: row.get(5)?,
        estimated_minutes: row.get(6)?,
        exam_id: row.get(7)?,
        status: row.get(8)?,
    })
}

fn map_event(row: &Row<'_>) -> rusqlite::Result<EventItem> {
    Ok(EventItem {
        id: row.get(0)?,
        title: row.get(1)?,
        event_type: row.get(2)?,
        start_time: row.get(3)?,
        end_time: row.get(4)?,
        importance: row.get(5)?,
        related_course_id: row.get(6)?,
        related_course_name: row.get(7)?,
    })
}

fn map_study_record(row: &Row<'_>) -> rusqlite::Result<StudyRecord> {
    Ok(StudyRecord {
        id: row.get(0)?,
        topic_id: row.get(1)?,
        topic_name: row.get(2)?,
        date: row.get(3)?,
        minutes: row.get(4)?,
        completion: row.get(5)?,
        note: row.get(6)?,
    })
}

fn map_emotion_log(row: &Row<'_>) -> rusqlite::Result<EmotionLog> {
    let matched_keywords: String = row.get(11)?;

    Ok(EmotionLog {
        id: row.get(0)?,
        date: row.get(1)?,
        raw_text: row.get(2)?,
        normalized_text: row.get(3)?,
        emotion: row.get(4)?,
        pressure_type: row.get(5)?,
        learning_state: row.get(6)?,
        intensity_level: row.get(7)?,
        suggestion_tone: row.get(8)?,
        score: row.get(9)?,
        confidence: row.get(10)?,
        matched_keywords: serde_json::from_str(&matched_keywords).unwrap_or_default(),
    })
}

fn map_advice_log(row: &Row<'_>) -> rusqlite::Result<AdviceLog> {
    Ok(AdviceLog {
        id: row.get(0)?,
        date: row.get(1)?,
        input_snapshot: row.get(2)?,
        generated_advice: row.get(3)?,
        model_type: row.get(4)?,
    })
}

fn required_text(value: String, field: &str) -> AppResult<String> {
    let value = value.trim().to_string();
    if value.is_empty() {
        Err(AppError::Validation(format!("{}不能为空", field)))
    } else {
        Ok(value)
    }
}

fn optional_text(value: Option<String>, default_value: &str) -> AppResult<String> {
    match value {
        Some(value) => {
            let value = value.trim().to_string();
            if value.is_empty() {
                Err(AppError::Validation("文本字段不能为空".to_string()))
            } else {
                Ok(value)
            }
        }
        None => Ok(default_value.to_string()),
    }
}

fn importance(value: i64) -> AppResult<i64> {
    if (1..=5).contains(&value) {
        Ok(value)
    } else {
        Err(AppError::Validation(
            "重要程度必须在 1 到 5 之间".to_string(),
        ))
    }
}

fn positive_minutes(value: i64) -> AppResult<i64> {
    if value > 0 {
        Ok(value)
    } else {
        Err(AppError::Validation("学习时间必须大于 0".to_string()))
    }
}

fn validate_event_range(start_time: &str, end_time: &str) -> AppResult<()> {
    let start_date = parse_event_date(start_time).ok_or_else(|| {
        AppError::Validation("开始时间需使用 YYYY-MM-DD 或 ISO 日期时间格式".to_string())
    })?;
    let end_date = parse_event_date(end_time).ok_or_else(|| {
        AppError::Validation("结束时间需使用 YYYY-MM-DD 或 ISO 日期时间格式".to_string())
    })?;

    if end_date < start_date {
        Err(AppError::Validation("结束时间不能早于开始时间".to_string()))
    } else {
        Ok(())
    }
}

fn ensure_changed(rows_affected: usize, name: &str, id: i64) -> AppResult<()> {
    if rows_affected == 0 {
        Err(AppError::NotFound(format!("{} {} 不存在", name, id)))
    } else {
        Ok(())
    }
}

fn parse_date(value: &str) -> Option<NaiveDate> {
    NaiveDate::parse_from_str(value, "%Y-%m-%d").ok()
}

fn parse_event_date(value: &str) -> Option<NaiveDate> {
    parse_date(value)
        .or_else(|| {
            NaiveDateTime::parse_from_str(value, "%Y-%m-%dT%H:%M:%S")
                .ok()
                .map(|dt| dt.date())
        })
        .or_else(|| {
            NaiveDateTime::parse_from_str(value, "%Y-%m-%d %H:%M:%S")
                .ok()
                .map(|dt| dt.date())
        })
        .or_else(|| {
            chrono::DateTime::parse_from_rfc3339(value)
                .ok()
                .map(|dt| dt.date_naive())
        })
        .or_else(|| value.get(..10).and_then(parse_date))
}

fn percentage(numerator: i64, denominator: i64) -> f64 {
    if denominator <= 0 {
        0.0
    } else {
        round2(numerator as f64 / denominator as f64 * 100.0)
    }
}

fn round2(value: f64) -> f64 {
    (value * 100.0).round() / 100.0
}

#[allow(dead_code)]
fn json<T: Serialize>(value: T) -> Json<T> {
    Json(value)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::db;

    fn in_memory_connection() -> Connection {
        let conn = Connection::open_in_memory().expect("open in-memory database");
        db::migrate(&conn).expect("migrate database");
        conn
    }

    #[test]
    fn daily_minutes_fill_missing_dates() {
        let conn = in_memory_connection();
        conn.execute("INSERT INTO courses (name) VALUES ('机器学习')", [])
            .unwrap();
        conn.execute(
            "INSERT INTO topics (course_id, name) VALUES (1, '模型评估')",
            [],
        )
        .unwrap();
        conn.execute(
            "INSERT INTO study_records (topic_id, date, minutes) VALUES (1, '2026-06-22', 25)",
            [],
        )
        .unwrap();
        conn.execute(
            "INSERT INTO study_records (topic_id, date, minutes) VALUES (1, '2026-06-24', 35)",
            [],
        )
        .unwrap();

        let from_date = NaiveDate::from_ymd_opt(2026, 6, 22).unwrap();
        let to_date = NaiveDate::from_ymd_opt(2026, 6, 24).unwrap();
        let result = fetch_daily_minutes(&conn, from_date, to_date, 3).unwrap();

        assert_eq!(result.len(), 3);
        assert_eq!(result[0].minutes, 25);
        assert_eq!(result[1].minutes, 0);
        assert_eq!(result[2].minutes, 35);
    }

    #[test]
    fn topic_progress_counts_completed_topics() {
        let conn = in_memory_connection();
        conn.execute("INSERT INTO courses (name) VALUES ('数据库')", [])
            .unwrap();
        conn.execute(
            r#"
            INSERT INTO topics (course_id, name, mastery_level, status)
            VALUES
                (1, 'SQL 查询', '已掌握', 'completed'),
                (1, '事务隔离', '学习中', 'pending')
            "#,
            [],
        )
        .unwrap();

        let result = fetch_topic_progress(&conn).unwrap();
        assert_eq!(result.len(), 1);
        assert_eq!(result[0].total_topics, 2);
        assert_eq!(result[0].completed_topics, 1);
        assert_eq!(result[0].completion_rate, 50.0);
    }

    #[test]
    fn event_range_rejects_invalid_or_reversed_dates() {
        assert!(validate_event_range("2026/06/24", "2026-06-25").is_err());
        assert!(validate_event_range("2026-06-25", "2026-06-24").is_err());
        assert!(validate_event_range("2026-06-24", "2026-06-25").is_ok());
    }
}
