use std::{env, net::SocketAddr, path::PathBuf};

#[derive(Debug, Clone)]
pub struct Config {
    pub bind_addr: SocketAddr,
    pub database_path: PathBuf,
}

impl Config {
    pub fn from_env() -> Self {
        let bind_addr = env::var("STUDYMIND_BIND_ADDR")
            .ok()
            .and_then(|value| value.parse().ok())
            .unwrap_or_else(|| SocketAddr::from(([127, 0, 0, 1], 7878)));

        let database_path = env::var("STUDYMIND_DATABASE_PATH")
            .map(PathBuf::from)
            .unwrap_or_else(|_| PathBuf::from("data").join("studymind.db"));

        Self {
            bind_addr,
            database_path,
        }
    }
}
