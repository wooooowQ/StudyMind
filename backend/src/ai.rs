use std::time::Duration;

use reqwest::StatusCode;
use serde::{Deserialize, Serialize};
use serde_json::json;

use crate::{advice::AdviceDraft, models::AppSettings};

#[derive(Debug, Serialize)]
struct ChatCompletionRequest {
    model: String,
    messages: Vec<ChatMessage>,
    temperature: f32,
    max_tokens: u16,
}

#[derive(Debug, Serialize, Deserialize)]
struct ChatMessage {
    role: String,
    content: String,
}

#[derive(Debug, Deserialize)]
struct ChatCompletionResponse {
    choices: Vec<ChatChoice>,
}

#[derive(Debug, Deserialize)]
struct ChatChoice {
    message: ChatMessage,
}

#[derive(Debug, Deserialize)]
struct ErrorResponse {
    error: Option<ApiError>,
}

#[derive(Debug, Deserialize)]
struct ApiError {
    message: Option<String>,
}

pub async fn try_generate_ai_advice(
    settings: &AppSettings,
    draft: &AdviceDraft,
) -> Result<String, String> {
    let api_key = settings
        .ai_api_key
        .as_deref()
        .map(str::trim)
        .filter(|value| !value.is_empty())
        .ok_or_else(|| "未配置 AI API Key".to_string())?;

    let endpoint = chat_completions_url(&settings.ai_base_url);
    let request = ChatCompletionRequest {
        model: settings.ai_model.clone(),
        messages: build_messages(draft),
        temperature: 0.35,
        max_tokens: 700,
    };

    let client = reqwest::Client::builder()
        .timeout(Duration::from_secs(25))
        .build()
        .map_err(|err| format!("AI 客户端初始化失败：{}", err))?;

    let response = client
        .post(endpoint)
        .bearer_auth(api_key)
        .json(&request)
        .send()
        .await
        .map_err(|err| format!("AI API 调用失败：{}", err))?;

    let status = response.status();
    let body = response
        .text()
        .await
        .map_err(|err| format!("读取 AI 响应失败：{}", err))?;

    if status != StatusCode::OK {
        return Err(format!(
            "AI API 返回错误 {}：{}",
            status,
            readable_error(&body)
        ));
    }

    let parsed: ChatCompletionResponse =
        serde_json::from_str(&body).map_err(|err| format!("解析 AI 响应失败：{}", err))?;

    let content = parsed
        .choices
        .into_iter()
        .next()
        .map(|choice| choice.message.content.trim().to_string())
        .filter(|content| !content.is_empty())
        .ok_or_else(|| "AI 响应为空".to_string())?;

    validate_ai_advice(&content)?;
    Ok(content)
}

fn build_messages(draft: &AdviceDraft) -> Vec<ChatMessage> {
    let system = r#"你是 StudyMind 的学习规划助手。你只负责把本地结构化分析结果表达成自然、友好的学习建议，不要替代本地优先级决策，不要虚构课程、考试、时间或知识点。

输出要求：
1. 使用中文。
2. 保持 2 到 4 个短段落。
3. 先回应用户当前状态，再给出今日学习安排。
4. 若用户焦虑或疲惫，语气要安抚、轻量；若用户拖延，语气要帮助启动；若状态积极，可以更推进。
5. 不要输出 JSON，不要使用 Markdown 表格。"#;

    let context = json!({
        "emotion": draft.emotion,
        "recommended_tasks": draft.recommended_tasks,
        "local_rule_advice": draft.advice,
        "guardrails": [
            "必须尊重 recommended_tasks 的排序",
            "不能添加本地数据中不存在的任务",
            "不能建议过高学习强度",
            "建议必须能在今天执行"
        ]
    });

    vec![
        ChatMessage {
            role: "system".to_string(),
            content: system.to_string(),
        },
        ChatMessage {
            role: "user".to_string(),
            content: format!(
                "请根据以下 StudyMind 本地分析结果生成今日学习建议：\n{}",
                serde_json::to_string_pretty(&context).unwrap_or_else(|_| "{}".to_string())
            ),
        },
    ]
}

fn validate_ai_advice(content: &str) -> Result<(), String> {
    let char_count = content.chars().count();

    if char_count < 30 {
        return Err("AI 建议过短".to_string());
    }

    if char_count > 1800 {
        return Err("AI 建议过长".to_string());
    }

    Ok(())
}

fn readable_error(body: &str) -> String {
    serde_json::from_str::<ErrorResponse>(body)
        .ok()
        .and_then(|value| value.error)
        .and_then(|error| error.message)
        .filter(|message| !message.trim().is_empty())
        .unwrap_or_else(|| body.chars().take(240).collect())
}

fn chat_completions_url(base_url: &str) -> String {
    let trimmed = base_url.trim().trim_end_matches('/');
    if trimmed.ends_with("/chat/completions") {
        trimmed.to_string()
    } else {
        format!("{}/chat/completions", trimmed)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn appends_chat_completions_to_base_url() {
        assert_eq!(
            chat_completions_url("https://api.openai.com/v1/"),
            "https://api.openai.com/v1/chat/completions"
        );
    }

    #[test]
    fn accepts_full_chat_completions_url() {
        assert_eq!(
            chat_completions_url("https://example.com/v1/chat/completions"),
            "https://example.com/v1/chat/completions"
        );
    }
}
