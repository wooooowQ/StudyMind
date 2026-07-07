use std::{
    path::PathBuf,
    sync::{Arc, Mutex, MutexGuard},
};

use rusqlite::Connection;

use crate::errors::{AppError, AppResult};

#[derive(Clone)]
pub struct AppState {
    pub db: Arc<Mutex<Connection>>,
    pub database_path: Arc<PathBuf>,
}

impl AppState {
    pub fn new(db: Connection, database_path: PathBuf) -> Self {
        Self {
            db: Arc::new(Mutex::new(db)),
            database_path: Arc::new(database_path),
        }
    }

    pub fn connection(&self) -> AppResult<MutexGuard<'_, Connection>> {
        self.db
            .lock()
            .map_err(|_| AppError::State("数据库连接锁已损坏".to_string()))
    }
}
