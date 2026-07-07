use std::path::Path;

use anyhow::Context;
use rusqlite::Connection;

pub fn connect(database_path: &Path) -> anyhow::Result<Connection> {
    if let Some(parent) = database_path.parent() {
        std::fs::create_dir_all(parent)
            .with_context(|| format!("failed to create {}", parent.display()))?;
    }

    let conn = Connection::open(database_path)
        .with_context(|| format!("failed to open {}", database_path.display()))?;
    conn.pragma_update(None, "foreign_keys", "ON")?;
    conn.pragma_update(None, "journal_mode", "WAL")?;
    conn.busy_timeout(std::time::Duration::from_secs(5))?;

    Ok(conn)
}

pub fn migrate(conn: &Connection) -> Result<(), rusqlite::Error> {
    conn.execute_batch(
        r#"
        CREATE TABLE IF NOT EXISTS courses (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL UNIQUE
        );

        CREATE TABLE IF NOT EXISTS events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            event_type TEXT NOT NULL,
            start_time TEXT NOT NULL,
            end_time TEXT NOT NULL,
            importance INTEGER NOT NULL DEFAULT 3,
            related_course_id INTEGER,
            FOREIGN KEY (related_course_id) REFERENCES courses(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS topics (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            course_id INTEGER NOT NULL,
            name TEXT NOT NULL,
            mastery_level TEXT NOT NULL DEFAULT '未掌握',
            importance INTEGER NOT NULL DEFAULT 3,
            estimated_minutes INTEGER NOT NULL DEFAULT 30,
            exam_id INTEGER,
            status TEXT NOT NULL DEFAULT 'pending',
            FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE,
            FOREIGN KEY (exam_id) REFERENCES events(id) ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS study_records (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            topic_id INTEGER NOT NULL,
            date TEXT NOT NULL,
            minutes INTEGER NOT NULL,
            completion TEXT NOT NULL DEFAULT 'partial',
            note TEXT NOT NULL DEFAULT '',
            FOREIGN KEY (topic_id) REFERENCES topics(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS emotion_logs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            date TEXT NOT NULL,
            raw_text TEXT NOT NULL,
            normalized_text TEXT NOT NULL DEFAULT '',
            emotion TEXT NOT NULL,
            pressure_type TEXT NOT NULL,
            learning_state TEXT NOT NULL DEFAULT '常规学习',
            intensity_level TEXT NOT NULL DEFAULT 'medium',
            suggestion_tone TEXT NOT NULL DEFAULT 'balanced',
            score INTEGER NOT NULL,
            confidence REAL NOT NULL DEFAULT 0.5,
            matched_keywords TEXT NOT NULL DEFAULT '[]'
        );

        CREATE TABLE IF NOT EXISTS advice_logs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            date TEXT NOT NULL,
            input_snapshot TEXT NOT NULL,
            generated_advice TEXT NOT NULL,
            model_type TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS settings (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            advice_mode TEXT NOT NULL DEFAULT 'rules',
            ai_api_key TEXT,
            ai_base_url TEXT NOT NULL DEFAULT 'https://api.openai.com/v1',
            ai_model TEXT NOT NULL DEFAULT 'gpt-4o-mini',
            updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        INSERT OR IGNORE INTO settings (id, advice_mode, ai_api_key)
        VALUES (1, 'rules', NULL);
        "#,
    )?;

    ensure_column(
        conn,
        "emotion_logs",
        "normalized_text",
        "TEXT NOT NULL DEFAULT ''",
    )?;
    ensure_column(
        conn,
        "emotion_logs",
        "learning_state",
        "TEXT NOT NULL DEFAULT '常规学习'",
    )?;
    ensure_column(
        conn,
        "emotion_logs",
        "intensity_level",
        "TEXT NOT NULL DEFAULT 'medium'",
    )?;
    ensure_column(
        conn,
        "emotion_logs",
        "suggestion_tone",
        "TEXT NOT NULL DEFAULT 'balanced'",
    )?;
    ensure_column(
        conn,
        "emotion_logs",
        "confidence",
        "REAL NOT NULL DEFAULT 0.5",
    )?;
    ensure_column(
        conn,
        "emotion_logs",
        "matched_keywords",
        "TEXT NOT NULL DEFAULT '[]'",
    )?;
    ensure_column(
        conn,
        "settings",
        "ai_base_url",
        "TEXT NOT NULL DEFAULT 'https://api.openai.com/v1'",
    )?;
    ensure_column(
        conn,
        "settings",
        "ai_model",
        "TEXT NOT NULL DEFAULT 'gpt-4o-mini'",
    )?;

    Ok(())
}

fn ensure_column(
    conn: &Connection,
    table_name: &str,
    column_name: &str,
    column_definition: &str,
) -> Result<(), rusqlite::Error> {
    let mut stmt = conn.prepare(&format!("PRAGMA table_info({})", table_name))?;
    let columns = stmt
        .query_map([], |row| row.get::<_, String>(1))?
        .collect::<Result<Vec<_>, _>>()?;

    if columns.iter().any(|column| column == column_name) {
        return Ok(());
    }

    conn.execute(
        &format!(
            "ALTER TABLE {} ADD COLUMN {} {}",
            table_name, column_name, column_definition
        ),
        [],
    )?;

    Ok(())
}
