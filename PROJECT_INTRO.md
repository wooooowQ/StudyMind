# StudyMind 项目介绍文档

## 1. 项目概述

StudyMind 是一个面向大学生学习场景的情绪感知型智能学习规划系统。项目采用 WinUI 3 桌面前端、Rust 本地后端和 SQLite 本地数据库构建，围绕课程、知识点、考试/DDL、学习记录和用户当日情绪状态进行综合分析，生成个性化学习建议，并通过图表帮助用户复盘学习状态。

本项目不是单纯的待办事项工具，而是将学习任务管理、情绪识别、任务优先级计算、AI 建议生成和数据可视化组合在一起，用于解决任务分散、复习优先级不清晰、情绪影响学习效率、学习过程缺少反馈等问题。

## 2. 核心功能

### 2.1 今日建议

用户可以输入当天的学习状态，例如“考试快到了，有点焦虑”或“今天效率不错，想多复习一点”。系统会完成以下处理：

- 识别用户当前情绪，如积极、中性、焦虑、疲惫、拖延。
- 判断压力来源，如考试压力、作业压力、活动冲突、任务堆积。
- 结合知识点掌握情况、考试/DDL 日期和近期学习记录计算任务优先级。
- 生成今日学习建议和推荐任务列表。
- 根据临近 DDL、未完成知识点和课程完成率生成“今日重点”提示。
- 推荐任务支持“记录学习”和“完成并记录”，可以直接把建议转化为学习记录。
- 在启用 AI 模式时，调用 OpenAI-compatible Chat Completions 接口生成更自然的建议文本。
- 当 AI API 不可用或未配置 Key 时，自动回退到本地规则建议。

### 2.2 课程与知识点管理

系统支持维护课程和知识点数据：

- 添加课程。
- 添加知识点。
- 修改和删除课程、知识点。
- 设置知识点掌握程度，如未掌握、学习中、已掌握。
- 设置知识点重要性和预计学习时长。
- 将知识点绑定到考试或 DDL。
- 根据学习记录自动更新部分知识点状态。
- 删除课程或知识点前，前端会提示关联影响；当存在关联学习数据时，会阻止直接级联删除，要求用户先清理或迁移数据。

### 2.3 日程管理

系统支持记录考试、作业 DDL 和活动安排：

- 添加考试、作业、活动等日程。
- 修改和删除日程。
- 设置起止日期、重要性和关联课程。
- 前端使用日期选择器减少手动输入错误，后端继续校验日期格式和起止顺序。
- 在可视化复盘中展示最近考试/DDL 倒计时。
- 统计每个即将到来的日程下仍未完成的关联知识点数量。

### 2.4 学习记录

用户可以记录每天学习了哪个知识点、学习了多少分钟、完成情况和备注。学习记录用于：

- 添加、修改和删除学习记录。
- 从今日推荐任务直接带入记录表单，或一键按推荐预计分钟写入已完成记录。
- 计算近 7 天或近 14 天学习时长。
- 判断某个知识点近期是否学习不足。
- 辅助今日推荐任务排序。
- 生成学习时长趋势图和课程学习占比。

### 2.5 情绪识别与学习状态分析

系统当前使用轻量关键词规则进行情绪分析，支持中英文关键词识别。分析结果包括：

- `emotion`：情绪类别。
- `pressure_type`：压力来源。
- `learning_state`：学习状态标签，如减压复习、轻量复习、低启动学习、高能量学习。
- `intensity_level`：建议学习强度，如 light、medium、high。
- `suggestion_tone`：建议表达语气。
- `confidence`：规则识别置信度。
- `matched_keywords`：命中的关键词。

这部分后续可以替换为 TF-IDF + Logistic Regression、Naive Bayes 等轻量文本分类模型。

### 2.6 AI 建议增强

系统支持三种建议生成模式：

- `rules`：只使用本地规则建议。
- `hybrid`：优先调用 AI，失败时回退到规则建议。
- `ai`：调用 AI 生成自然语言建议，失败时仍回退到规则建议。

AI 调用由 Rust 后端完成，前端只负责配置：

- `advice_mode`
- `ai_base_url`
- `ai_model`
- `ai_api_key`

API Key 只写入后端设置，不会在前端回显。AI 建议不会直接替代本地决策，系统仍会先完成本地结构化分析和任务排序，再把分析结果交给 AI 进行自然语言表达。

当前前端在用户从 `rules` 切换到 `hybrid` 或 `ai` 模式时，会弹出隐私确认，提示今日状态文本和结构化学习摘要可能发送到用户配置的兼容 AI 接口。

### 2.7 可视化复盘

WinUI 主界面包含“可视化复盘”区域，展示：

- 学习时长趋势。
- 情绪趋势。
- 知识点完成率。
- 课程学习占比。
- 最近考试/DDL 倒计时。
- 今日重点和近期风险提示。
- 图表空状态提示，例如暂无学习记录、暂无情绪记录或暂无课程投入时给出下一步操作。

后端通过 `GET /stats/dashboard` 提供综合统计数据，支持 `date` 和 `days` 查询参数。

### 2.9 前端交互与易用性

前端已从早期单页堆叠式界面改进为侧边栏分区工作流，主要页面包括：

- 今日计划：输入今日状态、查看情绪识别结果、生成建议并处理推荐任务。
- 课程知识点：维护课程与知识点数据。
- 日程：维护考试、作业 DDL 和活动安排。
- 学习记录：记录学习时长和完成情况。
- 复盘：查看图表、风险提示和近期趋势。
- 设置：配置 AI 建议模式、API Key，并执行数据导出和备份。

为提升真实使用体验，前端还补充了表单内错误提示、首次使用引导、后端未启动提示、删除确认、关联数据删除拦截、日期选择器、图表空状态和推荐任务完成闭环。

### 2.8 数据导出与备份

系统提供本地数据管理能力：

- `POST /export`：导出课程、知识点、日程、学习记录、情绪日志和建议日志。
- `POST /backup`：备份当前 SQLite 数据库。
- 发布版数据库默认存储在 `%LOCALAPPDATA%\StudyMind\studymind.db`，避免重新打包覆盖用户数据。

## 3. 使用方法

### 3.1 开发环境要求

推荐环境：

- Windows 10/11。
- Visual Studio，安装 Windows App SDK / Windows 桌面应用开发相关组件。
- .NET SDK，项目当前使用 `net10.0-windows10.0.19041.0`。
- Rust 工具链。
- PowerShell。

### 3.2 启动后端

在项目根目录运行：

```powershell
.\scripts\start-backend.ps1
```

默认监听地址：

```text
http://127.0.0.1:7878
```

默认数据库路径：

```text
backend\data\studymind.db
```

可通过环境变量修改：

```powershell
$env:STUDYMIND_BIND_ADDR = "127.0.0.1:7878"
$env:STUDYMIND_DATABASE_PATH = "data\studymind.db"
```

### 3.3 启动前端

确保后端已启动，然后可以使用 Visual Studio 打开：

```text
StudyMind.sln
```

如果 Visual Studio 提示“未将正确的项目设置为启动项目”，在解决方案资源管理器中右键 `StudyMind.App`，选择“设为启动项目”，再按 F5。

也可以先执行构建命令：

```powershell
dotnet restore .\StudyMind.sln
dotnet build .\frontend\StudyMind.App\StudyMind.App.csproj
```

### 3.4 写入演示数据

先启动后端，再运行：

```powershell
.\scripts\seed-sample.ps1
```

脚本会创建一门课程、一个考试日程、两个知识点、一条学习记录，并调用 `/advice/today` 生成一次建议，便于快速查看界面效果。

### 3.5 配置 AI 建议

在前端“设置”页面的“AI 建议设置”区域填写：

- 建议模式：`rules`、`hybrid` 或 `ai`。
- Base URL：默认 `https://api.openai.com/v1`。
- 模型名称：默认 `gpt-4o-mini`。
- API Key：留空表示不修改已保存的 Key。

保存后，点击“生成建议”即可按所选模式生成今日建议。如果 AI 不可用，系统会显示回退原因并使用本地规则建议。

如果从本地规则模式切换到 `hybrid` 或 `ai`，前端会先显示隐私确认，提醒用户相关学习状态文本和结构化摘要可能会发送到所配置的 AI 兼容接口。

### 3.6 本地打包发布

运行：

```powershell
.\scripts\package-release.ps1
```

默认输出目录：

```text
dist\StudyMind
```

发布目录包含：

- `backend\studymind-backend.exe`
- `frontend\StudyMind.App.exe`
- `start-studymind.ps1`
- `README-RELEASE.txt`

运行发布版时，执行：

```powershell
.\dist\StudyMind\start-studymind.ps1
```

启动脚本会自动启动后端、等待健康检查通过，再启动前端。它会检查 `7878` 端口是否已被其他 StudyMind 后端占用，并在前端退出后停止本次启动的后端。

## 4. 系统架构说明

### 4.1 总体架构

StudyMind 采用本地前后端分离架构：

```text
WinUI 3 前端
    |
    | HTTP / JSON
    v
Rust axum 本地后端
    |
    | rusqlite
    v
SQLite 本地数据库

Rust 后端还可通过 reqwest 调用 OpenAI-compatible AI API。
```

### 4.2 前端架构

前端位于：

```text
frontend\StudyMind.App
```

主要技术：

- WinUI 3：构建 Windows 桌面界面。
- C#：编写前端逻辑。
- CommunityToolkit.Mvvm：实现 MVVM 结构。
- NavigationView：组织今日计划、课程知识点、日程、学习记录、复盘和设置等页面。
- HttpClient：调用 Rust 后端接口。
- LiveCharts2：绘制学习趋势、情绪趋势和课程占比图。

主要模块：

- `MainWindow.xaml`：主界面布局、侧边栏导航、表单、图表和空状态展示。
- `MainWindow.xaml.cs`：窗口初始化、导航事件、删除确认和 AI 隐私确认。
- `ViewModels\MainViewModel.cs`：界面状态、命令、数据绑定、表单校验、风险提示和推荐任务闭环逻辑。
- `Services\StudyMindApiClient.cs`：HTTP API 客户端。
- `Models\ApiModels.cs`：前后端 JSON DTO 以及前端展示辅助属性。

### 4.3 后端架构

后端位于：

```text
backend
```

主要技术：

- Rust。
- axum：提供本地 HTTP API。
- tokio：异步运行时。
- rusqlite：访问 SQLite。
- serde / serde_json：JSON 序列化。
- reqwest：调用 AI API。
- chrono：日期处理。
- thiserror / anyhow：错误处理。

主要模块：

- `main.rs`：后端入口。
- `handlers.rs`：HTTP 路由和接口处理。
- `db.rs`：数据库连接、建表和迁移。
- `models.rs`：请求/响应模型。
- `advice.rs`：情绪分析、任务优先级计算和规则建议生成。
- `ai.rs`：OpenAI-compatible Chat Completions 调用。
- `config.rs`：运行配置。
- `state.rs`：应用共享状态。
- `errors.rs`：错误类型和统一响应。

### 4.4 数据库设计

SQLite 主要数据表：

- `courses`：课程信息。
- `topics`：知识点信息。
- `events`：考试、作业 DDL、活动等日程。
- `study_records`：学习记录。
- `emotion_logs`：情绪分析日志。
- `advice_logs`：建议生成日志。
- `settings`：系统设置，包括 AI 模式、Base URL、模型名和 API Key。

数据库迁移在后端启动时自动执行，旧数据库会通过 `ALTER TABLE` 补齐新增字段。

### 4.5 智能建议生成流程

今日建议生成流程：

```text
用户输入今日状态
    |
    v
情绪/压力/学习状态识别
    |
    v
读取课程、知识点、日程、学习记录
    |
    v
计算知识点优先级
    |
    v
生成推荐任务列表
    |
    +--> rules 模式：使用规则模板生成建议
    |
    +--> hybrid / ai 模式：调用 AI 生成自然语言建议
              |
              +--> 失败时回退到规则建议
    |
    v
保存 advice_logs 并返回前端展示
```

任务优先级主要考虑：

- 考试/DDL 紧急度。
- 知识点重要性。
- 掌握薄弱程度。
- 情绪适配度。
- 近 7 天学习不足程度。

### 4.6 可视化统计流程

可视化数据由 `GET /stats/dashboard` 提供。后端聚合 SQLite 中的学习记录、情绪日志、知识点和日程数据，返回：

- `daily_minutes`
- `emotion_trend`
- `course_minutes`
- `topic_progress`
- `upcoming_events`
- 总学习时长、日均学习时长和整体完成率

前端接收后由 LiveCharts2 绘制图表，并用列表和进度条展示课程进度与倒计时。

当前前端还会基于统计数据生成今日重点、近期风险和趋势洞察。例如临近 DDL 且仍有未完成知识点时，会在今日计划和复盘页突出提醒；当学习记录、课程投入或情绪记录为空时，图表区域会显示空状态提示。

## 5. 接口概览

当前已实现接口：

```text
GET    /health
GET    /courses
POST   /courses
PUT    /courses/{id}
DELETE /courses/{id}
GET    /topics
POST   /topics
PUT    /topics/{id}
DELETE /topics/{id}
GET    /events
POST   /events
PUT    /events/{id}
DELETE /events/{id}
GET    /study-records
POST   /study-records
PUT    /study-records/{id}
DELETE /study-records/{id}
POST   /emotion/analyze
POST   /advice/today
GET    /stats/weekly
GET    /stats/dashboard
GET    /settings
PUT    /settings
POST   /export
POST   /backup
```

## 6. 项目特点

- 本地优先：学习记录、情绪日志和建议日志默认保存在本地 SQLite。
- 情绪感知：建议生成会根据焦虑、疲惫、拖延等状态调整任务强度和表达方式。
- AI 可选：AI 只负责增强自然语言表达，本地规则仍负责结构化分析和兜底。
- 学习闭环：推荐任务可以直接转为学习记录，或一键完成并写入已完成记录。
- 易用性增强：前端包含分区导航、日期选择器、空状态提示、表单内错误提示和删除风险拦截。
- 隐私提示：从本地规则切换到 AI 模式时，会提示可能上传到兼容 AI 接口的数据范围。
- 易演示：支持演示数据脚本、可视化图表和本地打包脚本。
- 可扩展：后续可以替换情绪识别模型、扩展多用户、加入云同步或更完整的学习计划排程。

## 7. 后续改进方向

- 使用训练后的轻量文本分类模型替换关键词规则。
- 增加更多图表维度，例如按月统计、学习连续天数、课程目标完成率。
- 增加数据库清空、恢复备份和数据导入能力。
- 增加更自然的响应式布局，在窄窗口下自动切换为单列页面。
- 为 ViewModel 中的表单校验、风险提示、推荐任务完成和图表空状态增加单元测试。
- 启动前后端进行真实窗口视觉验收，检查导航、弹窗、图表、DatePicker 和窄窗口滚动效果。
- 增加系统托盘、开机自启或后端进程托管能力。
- 增加更细粒度的学习计划，如番茄钟、任务拆分和提醒。
