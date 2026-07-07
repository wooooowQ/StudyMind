use std::collections::{BTreeSet, HashMap};

use chrono::{Duration, Local, NaiveDate, NaiveDateTime};
use rusqlite::{Connection, OptionalExtension, Row};
use serde::Serialize;

use crate::{
    errors::AppResult,
    models::{EmotionAnalysis, RecommendedTask, TodayAdviceResponse},
};

#[derive(Debug)]
struct TopicForAdvice {
    id: i64,
    name: String,
    course_name: Option<String>,
    mastery_level: String,
    importance: i64,
    estimated_minutes: i64,
    status: String,
    exam_title: Option<String>,
    exam_start_time: Option<String>,
}

#[derive(Debug)]
struct LatestEmotion {
    raw_text: String,
    normalized_text: String,
    emotion: String,
    pressure_type: String,
    learning_state: String,
    intensity_level: String,
    suggestion_tone: String,
    score: i64,
    confidence: f64,
    matched_keywords: String,
}

#[derive(Debug)]
struct CategoryScore {
    label: &'static str,
    points: f64,
    matched_keywords: Vec<String>,
}

#[derive(Debug, Serialize)]
struct AdviceSnapshot {
    date: String,
    emotion: Option<EmotionAnalysis>,
    recommended_tasks: Vec<RecommendedTask>,
}

#[derive(Debug, Clone)]
pub struct AdviceDraft {
    pub date: String,
    pub model_type: String,
    pub emotion: Option<EmotionAnalysis>,
    pub advice: String,
    pub recommended_tasks: Vec<RecommendedTask>,
    pub input_snapshot: String,
    pub fallback_reason: Option<String>,
}

impl AdviceDraft {
    pub fn with_ai_advice(
        &self,
        model_type: String,
        advice: String,
        fallback_reason: Option<String>,
    ) -> Self {
        Self {
            date: self.date.clone(),
            model_type,
            emotion: self.emotion.clone(),
            advice,
            recommended_tasks: self.recommended_tasks.clone(),
            input_snapshot: self.input_snapshot.clone(),
            fallback_reason,
        }
    }
}

pub fn today_string() -> String {
    Local::now().date_naive().to_string()
}

pub fn classify_emotion(raw_text: &str, date: String) -> EmotionAnalysis {
    let normalized_text = normalize_text(raw_text);
    let emotion_score = best_category(&normalized_text, emotion_rules());
    let pressure_score = best_category(&normalized_text, pressure_rules());

    let emotion = emotion_score
        .as_ref()
        .map(|item| item.label)
        .unwrap_or("中性");
    let pressure_type = pressure_score
        .as_ref()
        .map(|item| item.label)
        .unwrap_or("未明确");

    let learning_state = infer_learning_state(emotion, pressure_type);
    let intensity_level = infer_intensity_level(emotion, &learning_state);
    let suggestion_tone = infer_suggestion_tone(emotion, pressure_type);
    let points = emotion_score
        .as_ref()
        .map(|item| item.points)
        .unwrap_or(0.0);
    let pressure_points = pressure_score
        .as_ref()
        .map(|item| item.points)
        .unwrap_or(0.0);

    let mut matched = BTreeSet::new();
    if let Some(score) = &emotion_score {
        matched.extend(score.matched_keywords.iter().cloned());
    }
    if let Some(score) = &pressure_score {
        matched.extend(score.matched_keywords.iter().cloned());
    }

    EmotionAnalysis {
        date,
        raw_text: raw_text.trim().to_string(),
        normalized_text,
        emotion: emotion.to_string(),
        pressure_type: pressure_type.to_string(),
        learning_state,
        intensity_level,
        suggestion_tone,
        score: emotion_level(emotion, points),
        confidence: confidence(points, pressure_points),
        matched_keywords: matched.into_iter().collect(),
    }
}

pub fn latest_emotion_for_date(
    conn: &Connection,
    date: &str,
) -> AppResult<Option<EmotionAnalysis>> {
    let row = conn
        .query_row(
            r#"
            SELECT
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
            WHERE date = ?1
            ORDER BY id DESC
            LIMIT 1
            "#,
            [date],
            |row| {
                Ok(LatestEmotion {
                    raw_text: row.get(0)?,
                    normalized_text: row.get(1)?,
                    emotion: row.get(2)?,
                    pressure_type: row.get(3)?,
                    learning_state: row.get(4)?,
                    intensity_level: row.get(5)?,
                    suggestion_tone: row.get(6)?,
                    score: row.get(7)?,
                    confidence: row.get(8)?,
                    matched_keywords: row.get(9)?,
                })
            },
        )
        .optional()?;

    Ok(row.map(|row| EmotionAnalysis {
        date: date.to_string(),
        raw_text: row.raw_text,
        normalized_text: row.normalized_text,
        emotion: row.emotion,
        pressure_type: row.pressure_type,
        learning_state: row.learning_state,
        intensity_level: row.intensity_level,
        suggestion_tone: row.suggestion_tone,
        score: row.score,
        confidence: row.confidence,
        matched_keywords: parse_keywords(&row.matched_keywords),
    }))
}

pub fn save_emotion(conn: &Connection, analysis: &EmotionAnalysis) -> AppResult<i64> {
    let matched_keywords =
        serde_json::to_string(&analysis.matched_keywords).unwrap_or_else(|_| "[]".to_string());

    conn.execute(
        r#"
        INSERT INTO emotion_logs (
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
        )
        VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11)
        "#,
        (
            &analysis.date,
            &analysis.raw_text,
            &analysis.normalized_text,
            &analysis.emotion,
            &analysis.pressure_type,
            &analysis.learning_state,
            &analysis.intensity_level,
            &analysis.suggestion_tone,
            analysis.score,
            analysis.confidence,
            matched_keywords,
        ),
    )?;

    Ok(conn.last_insert_rowid())
}

pub fn generate_today_advice(
    conn: &Connection,
    date: String,
    emotion: Option<EmotionAnalysis>,
) -> AppResult<TodayAdviceResponse> {
    let draft = build_today_advice_draft(conn, date, emotion, "rules-v2".to_string(), None)?;
    let saved_advice_id = save_advice(conn, &draft)?;

    Ok(draft.into_response(saved_advice_id))
}

pub fn build_today_advice_draft(
    conn: &Connection,
    date: String,
    emotion: Option<EmotionAnalysis>,
    model_type: String,
    fallback_reason: Option<String>,
) -> AppResult<AdviceDraft> {
    let target_date = parse_date(&date).unwrap_or_else(|| Local::now().date_naive());
    let since = (target_date - Duration::days(6)).to_string();

    let mut stmt = conn.prepare(
        r#"
        SELECT
            t.id,
            t.name,
            c.name AS course_name,
            t.mastery_level,
            t.importance,
            t.estimated_minutes,
            t.status,
            e.title AS exam_title,
            e.start_time AS exam_start_time
        FROM topics t
        LEFT JOIN courses c ON c.id = t.course_id
        LEFT JOIN events e ON e.id = t.exam_id
        WHERE t.status NOT IN ('completed', 'done', '已完成')
          AND t.mastery_level NOT IN ('已掌握', '掌握', 'mastered')
        ORDER BY t.importance DESC, t.id DESC
        "#,
    )?;

    let topics = stmt
        .query_map([], map_topic_for_advice)?
        .collect::<Result<Vec<_>, _>>()?;

    let mut stmt = conn.prepare(
        r#"
        SELECT topic_id, COALESCE(SUM(minutes), 0) AS minutes
        FROM study_records
        WHERE date >= ?1 AND date <= ?2
        GROUP BY topic_id
        "#,
    )?;

    let recent_minutes = stmt
        .query_map((&since, &date), |row| {
            Ok((row.get::<_, i64>(0)?, row.get::<_, i64>(1)?))
        })?
        .collect::<Result<HashMap<i64, i64>, _>>()?;

    let mut recommended_tasks = topics
        .into_iter()
        .map(|topic| score_topic(topic, target_date, emotion.as_ref(), &recent_minutes))
        .collect::<Vec<_>>();

    recommended_tasks.sort_by(|a, b| {
        b.priority_score
            .partial_cmp(&a.priority_score)
            .unwrap_or(std::cmp::Ordering::Equal)
    });
    recommended_tasks.truncate(recommended_limit(emotion.as_ref()));

    let advice = build_advice_text(&date, emotion.as_ref(), &recommended_tasks);
    let snapshot = AdviceSnapshot {
        date: date.clone(),
        emotion: emotion.clone(),
        recommended_tasks: recommended_tasks.clone(),
    };
    let input_snapshot = serde_json::to_string(&snapshot).unwrap_or_else(|_| "{}".to_string());

    Ok(AdviceDraft {
        date,
        model_type,
        emotion,
        advice,
        recommended_tasks,
        input_snapshot,
        fallback_reason,
    })
}

pub fn save_advice(conn: &Connection, draft: &AdviceDraft) -> AppResult<i64> {
    conn.execute(
        r#"
        INSERT INTO advice_logs (date, input_snapshot, generated_advice, model_type)
        VALUES (?1, ?2, ?3, ?4)
        "#,
        (
            &draft.date,
            &draft.input_snapshot,
            &draft.advice,
            &draft.model_type,
        ),
    )?;

    Ok(conn.last_insert_rowid())
}

impl AdviceDraft {
    pub fn into_response(self, saved_advice_id: i64) -> TodayAdviceResponse {
        TodayAdviceResponse {
            date: self.date,
            model_type: self.model_type,
            emotion: self.emotion,
            advice: self.advice,
            recommended_tasks: self.recommended_tasks,
            saved_advice_id,
            fallback_reason: self.fallback_reason,
        }
    }
}

fn normalize_text(input: &str) -> String {
    let mut normalized = String::with_capacity(input.len());
    let mut previous_space = false;

    for ch in input.trim().chars().flat_map(char::to_lowercase) {
        let replacement = if ch.is_control() || ch.is_ascii_punctuation() {
            ' '
        } else {
            ch
        };

        if replacement.is_whitespace() {
            if !previous_space {
                normalized.push(' ');
            }
            previous_space = true;
        } else {
            normalized.push(replacement);
            previous_space = false;
        }
    }

    normalized.trim().to_string()
}

fn best_category(
    normalized_text: &str,
    rules: &[(&'static str, &'static [&'static str])],
) -> Option<CategoryScore> {
    rules
        .iter()
        .filter_map(|(label, keywords)| {
            let mut points = 0.0;
            let mut matched_keywords = Vec::new();

            for keyword in *keywords {
                let needle = keyword.to_lowercase();
                if normalized_text.contains(&needle) {
                    points += keyword_weight(keyword);
                    matched_keywords.push((*keyword).to_string());
                }
            }

            (points > 0.0).then_some(CategoryScore {
                label,
                points,
                matched_keywords,
            })
        })
        .max_by(|a, b| {
            a.points
                .partial_cmp(&b.points)
                .unwrap_or(std::cmp::Ordering::Equal)
        })
}

fn keyword_weight(keyword: &str) -> f64 {
    match keyword.chars().count() {
        0..=1 => 1.0,
        2..=3 => 1.25,
        4..=6 => 1.6,
        _ => 2.0,
    }
}

fn emotion_rules() -> &'static [(&'static str, &'static [&'static str])] {
    &[
        (
            "焦虑",
            &[
                "焦虑",
                "紧张",
                "担心",
                "压力",
                "来不及",
                "崩溃",
                "害怕",
                "慌",
                "anxious",
                "anxiety",
                "stress",
                "worried",
                "panic",
                "nervous",
            ],
        ),
        (
            "拖延",
            &[
                "拖延",
                "不想学",
                "刷手机",
                "没动力",
                "摆烂",
                "提不起劲",
                "procrastinate",
                "phone",
                "low motivation",
                "avoid",
            ],
        ),
        (
            "疲惫",
            &[
                "累",
                "疲惫",
                "困",
                "熬夜",
                "头疼",
                "没精神",
                "tired",
                "sleepy",
                "exhausted",
                "fatigue",
            ],
        ),
        (
            "积极",
            &[
                "状态好",
                "想学习",
                "效率高",
                "开心",
                "顺利",
                "有动力",
                "专注",
                "focused",
                "motivated",
                "productive",
                "good mood",
            ],
        ),
    ]
}

fn pressure_rules() -> &'static [(&'static str, &'static [&'static str])] {
    &[
        (
            "考试压力",
            &[
                "考试", "复习", "挂科", "期末", "考核", "测验", "exam", "final", "quiz", "test",
            ],
        ),
        (
            "作业压力",
            &[
                "作业",
                "ddl",
                "截止",
                "论文",
                "实验报告",
                "提交",
                "homework",
                "assignment",
                "deadline",
                "paper",
            ],
        ),
        (
            "活动冲突",
            &[
                "社团",
                "活动",
                "开会",
                "冲突",
                "兼职",
                "排不开",
                "club",
                "meeting",
                "activity",
                "conflict",
            ],
        ),
        (
            "任务堆积",
            &[
                "太多",
                "堆积",
                "安排不过来",
                "来不及",
                "很多课",
                "好多",
                "too much",
                "overload",
                "pile",
                "too many",
            ],
        ),
    ]
}

fn infer_learning_state(emotion: &str, pressure_type: &str) -> String {
    match (emotion, pressure_type) {
        ("焦虑", "考试压力") => "减压复习",
        ("焦虑", _) => "减压调整",
        ("疲惫", _) => "轻量复习",
        ("拖延", _) => "低启动学习",
        ("积极", _) => "高能量学习",
        (_, "任务堆积") => "任务拆解",
        _ => "常规学习",
    }
    .to_string()
}

fn infer_intensity_level(emotion: &str, learning_state: &str) -> String {
    match (emotion, learning_state) {
        ("积极", _) => "high",
        ("焦虑", _) | ("疲惫", _) | ("拖延", _) => "light",
        (_, "任务拆解") => "medium",
        _ => "medium",
    }
    .to_string()
}

fn infer_suggestion_tone(emotion: &str, pressure_type: &str) -> String {
    match (emotion, pressure_type) {
        ("焦虑", _) => "安抚型",
        ("疲惫", _) => "温和型",
        ("拖延", _) => "启动型",
        ("积极", _) => "推进型",
        (_, "任务堆积") => "结构化",
        _ => "平衡型",
    }
    .to_string()
}

fn emotion_level(emotion: &str, points: f64) -> i64 {
    match emotion {
        "积极" => 5,
        "中性" => 2,
        _ => (2 + points.round() as i64).clamp(2, 5),
    }
}

fn confidence(emotion_points: f64, pressure_points: f64) -> f64 {
    let value = 0.45 + emotion_points.min(3.0) * 0.1 + pressure_points.min(2.0) * 0.08;
    round2(value.clamp(0.5, 0.96))
}

fn recommended_limit(emotion: Option<&EmotionAnalysis>) -> usize {
    match emotion.map(|item| item.intensity_level.as_str()) {
        Some("light") => 2,
        Some("high") => 4,
        _ => 3,
    }
}

fn map_topic_for_advice(row: &Row<'_>) -> rusqlite::Result<TopicForAdvice> {
    Ok(TopicForAdvice {
        id: row.get(0)?,
        name: row.get(1)?,
        course_name: row.get(2)?,
        mastery_level: row.get(3)?,
        importance: row.get(4)?,
        estimated_minutes: row.get(5)?,
        status: row.get(6)?,
        exam_title: row.get(7)?,
        exam_start_time: row.get(8)?,
    })
}

fn score_topic(
    topic: TopicForAdvice,
    target_date: NaiveDate,
    emotion: Option<&EmotionAnalysis>,
    recent_minutes: &HashMap<i64, i64>,
) -> RecommendedTask {
    let urgency = topic
        .exam_start_time
        .as_deref()
        .and_then(parse_event_date)
        .map(|exam_date| {
            let days_left = (exam_date - target_date).num_days();
            match days_left {
                i64::MIN..=-1 => 1.0,
                0..=1 => 5.0,
                2..=3 => 4.5,
                4..=7 => 3.5,
                8..=14 => 2.5,
                _ => 1.5,
            }
        })
        .unwrap_or(1.5);

    let importance = topic.importance.clamp(1, 5) as f64;

    let weakness = match topic.mastery_level.as_str() {
        "未掌握" | "不会" | "薄弱" | "weak" | "not_started" => 5.0,
        "学习中" | "部分掌握" | "learning" | "partial" => 3.5,
        "已掌握" | "掌握" | "mastered" => 1.0,
        _ => 3.0,
    };

    let emotion_fit = match emotion {
        Some(item) if item.learning_state == "减压复习" && topic.estimated_minutes <= 45 => 5.0,
        Some(item) if item.learning_state == "轻量复习" && topic.estimated_minutes <= 30 => 4.8,
        Some(item) if item.learning_state == "低启动学习" && topic.estimated_minutes <= 30 => {
            5.0
        }
        Some(item) if item.learning_state == "高能量学习" && topic.importance >= 4 => 4.5,
        Some(item) if item.intensity_level == "light" && topic.estimated_minutes > 90 => 2.0,
        _ => 3.0,
    };

    let learned_minutes = recent_minutes.get(&topic.id).copied().unwrap_or_default();
    let history_deficit = match learned_minutes {
        0 => 5.0,
        1..=29 => 4.0,
        30..=59 => 3.0,
        _ => 1.5,
    };

    let priority_score = urgency * 0.30
        + importance * 0.25
        + weakness * 0.25
        + emotion_fit * 0.10
        + history_deficit * 0.10;

    let mut reasons = vec![
        format!("重要性 {}", topic.importance.clamp(1, 5)),
        format!("掌握程度：{}", topic.mastery_level),
    ];

    if let (Some(title), Some(start_time)) = (&topic.exam_title, &topic.exam_start_time) {
        if let Some(event_date) = parse_event_date(start_time) {
            let days_left = (event_date - target_date).num_days();
            if days_left >= 0 {
                reasons.push(format!("距离“{}”还有 {} 天", title, days_left));
            }
        }
    }

    if learned_minutes == 0 {
        reasons.push("近 7 天暂无学习记录".to_string());
    }

    if topic.status != "pending" {
        reasons.push(format!("当前状态：{}", topic.status));
    }

    RecommendedTask {
        topic_id: topic.id,
        topic_name: topic.name,
        course_name: topic.course_name,
        estimated_minutes: topic.estimated_minutes,
        priority_score: round2(priority_score),
        reason: reasons.join("；"),
    }
}

fn build_advice_text(
    date: &str,
    emotion: Option<&EmotionAnalysis>,
    tasks: &[RecommendedTask],
) -> String {
    if tasks.is_empty() {
        return format!(
            "{} 暂无未完成知识点。可以先补充课程、知识点和考试日程，或记录一次今天的学习情况。",
            date
        );
    }

    let mut lines = Vec::new();

    match emotion {
        Some(item) if item.learning_state == "减压复习" => lines.push(format!(
            "识别到你今天偏{}，压力来源可能是{}。建议采用“短时段 + 明确收尾”的复习方式，先完成一个能带来确定感的小任务。",
            item.emotion, item.pressure_type
        )),
        Some(item) if item.learning_state == "轻量复习" => lines.push(
            "你今天更适合轻量复习。建议选择时间短、边界清楚的知识点，不把目标设得太满。".to_string(),
        ),
        Some(item) if item.learning_state == "低启动学习" => lines.push(format!(
            "你今天的学习状态标签是“{}”。先用 {} 分钟启动任务，完成后再决定是否加量。",
            item.learning_state,
            tasks[0].estimated_minutes.min(25)
        )),
        Some(item) if item.learning_state == "高能量学习" => {
            lines.push("你今天状态较好，可以优先推进重要且薄弱的知识点。".to_string())
        }
        Some(item) if item.learning_state == "任务拆解" => lines.push(
            "今天的任务压力更像是内容堆积。建议先按优先级拆成 2-3 个小块，避免在多个任务之间来回切换。".to_string(),
        ),
        _ => lines.push("今天建议按紧急程度、重要性和掌握薄弱程度安排学习任务。".to_string()),
    }

    let first = &tasks[0];
    lines.push(format!(
        "优先学习“{}”，预计 {} 分钟。推荐理由：{}。",
        first.topic_name, first.estimated_minutes, first.reason
    ));

    if let Some(second) = tasks.get(1) {
        lines.push(format!(
            "如果时间充足，再安排“{}”约 {} 分钟，作为第二优先级任务。",
            second.topic_name, second.estimated_minutes
        ));
    }

    if let Some(third) = tasks.get(2) {
        lines.push(format!(
            "最后可以把“{}”作为弹性任务，完成核心概念整理即可。",
            third.topic_name
        ));
    }

    lines.join("\n")
}

fn parse_keywords(value: &str) -> Vec<String> {
    serde_json::from_str(value).unwrap_or_default()
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
            chrono::DateTime::parse_from_rfc3339(value)
                .ok()
                .map(|dt| dt.date_naive())
        })
}

fn round2(value: f64) -> f64 {
    (value * 100.0).round() / 100.0
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn classifies_anxiety_exam_pressure() {
        let result = classify_emotion("考试快到了，感觉很焦虑来不及复习", "2026-06-23".into());

        assert_eq!(result.emotion, "焦虑");
        assert_eq!(result.pressure_type, "考试压力");
        assert_eq!(result.learning_state, "减压复习");
        assert!(result.confidence >= 0.6);
        assert!(result.matched_keywords.contains(&"考试".to_string()));
    }

    #[test]
    fn classifies_procrastination() {
        let result = classify_emotion("今天一直刷手机，不想学", "2026-06-23".into());

        assert_eq!(result.emotion, "拖延");
        assert_eq!(result.learning_state, "低启动学习");
    }

    #[test]
    fn normalizes_text_for_english_keywords() {
        let result = classify_emotion("Exam is close, I feel anxious!", "2026-06-23".into());

        assert_eq!(result.emotion, "焦虑");
        assert_eq!(result.pressure_type, "考试压力");
    }
}
