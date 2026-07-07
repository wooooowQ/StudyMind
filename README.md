# StudyMind

StudyMind 是一个基于 WinUI 3 + Rust + SQLite 的情绪感知型智能学习规划系统。本仓库已实现需求文档中的四个阶段基础版：基础桌面应用、本地后端、SQLite 本地存储、课程/知识点/日程/学习记录 CRUD、规则建议生成、轻量情绪/压力/学习状态识别、可选 AI 大模型建议生成，以及学习数据可视化复盘。

当前前端已从单页功能堆叠改进为侧边栏分区工作流，包含“今日计划、课程知识点、日程、学习记录、复盘、设置”等页面，并补充了推荐任务快捷记录、删除风险拦截、表单内错误提示、日期选择器、图表空状态和 AI 模式隐私确认等交互能力，更贴近“学习助手”定位。

## 项目结构

```text
backend/                  Rust axum 本地 API 服务
frontend/StudyMind.App/   WinUI 3 桌面客户端
scripts/                  启动与演示数据脚本
```

## 后端运行

日常使用时不需要手动启动后端；WinUI 前端启动时会自动检查 `http://127.0.0.1:7878/health`，如果本地后端未运行，会自动拉起后端进程。

开发或排查后端时，可以单独启动本地 API 服务。需要先安装 Rust 工具链：

```powershell
.\scripts\start-backend.ps1
```

默认监听 `http://127.0.0.1:7878`，数据库文件为 `backend\data\studymind.db`。

可用环境变量：

```powershell
$env:STUDYMIND_BIND_ADDR = "127.0.0.1:7878"
$env:STUDYMIND_DATABASE_PATH = "data\studymind.db"
```

## 写入演示数据

先打开前端应用，或手动启动后端，然后在另一个 PowerShell 里运行：

```powershell
.\scripts\seed-sample.ps1
```

脚本会创建一门课程、一个考试日程、两个知识点、一条学习记录，并调用 `/advice/today` 生成规则建议。

## 前端运行

前端会自动启动并管理本地后端。命令行启动前端：

```powershell
dotnet restore .\StudyMind.sln
dotnet run --project .\frontend\StudyMind.App\StudyMind.App.csproj
```

如果只想检查能否编译，可以运行：

```powershell
dotnet build .\frontend\StudyMind.App\StudyMind.App.csproj
```

WinUI 项目也可以直接用 Visual Studio 打开 `StudyMind.sln` 运行。若 VS 提示未设置正确的启动项目，请在解决方案资源管理器中右键 `StudyMind.App`，选择“设为启动项目”，再按 F5。前端会复用已有 StudyMind 后端；如果没有检测到后端，会自动启动源码目录下的 Rust 后端。

当前前端目标框架为 `.NET 10` + WinUI 3。若本机缺少 .NET 10 SDK、Windows App SDK 或 Windows SDK 桌面开发组件，需要在 Visual Studio Installer 中补齐相关组件。从源码目录直接运行前端时，若后端可执行文件尚未生成，自动启动机制会调用 `cargo run`，因此仍需要 Rust 工具链；发布包已经包含后端可执行文件，不需要额外安装 Rust。

## 本地打包

可以使用脚本生成本地发布目录：

```powershell
.\scripts\package-release.ps1
```

默认输出到 `dist\StudyMind`，内容包括 Rust 后端 release 可执行文件、WinUI 前端发布文件和 `start-studymind.ps1` 一键启动脚本。发布版运行数据默认保存到 `%LOCALAPPDATA%\StudyMind\studymind.db`，避免重新打包时覆盖用户数据。

发布目录中可以直接运行 `frontend\StudyMind.App.exe`，前端会自动启动 `backend\studymind-backend.exe`。`start-studymind.ps1` 仍保留为兼容的一键启动入口。

## 已实现接口

- `GET /health`
- `GET/POST /courses`
- `PUT/DELETE /courses/{id}`
- `GET/POST /topics`
- `PUT/DELETE /topics/{id}`
- `GET/POST /events`
- `PUT/DELETE /events/{id}`
- `GET/POST /study-records`
- `PUT/DELETE /study-records/{id}`
- `POST /emotion/analyze`
- `POST /advice/today`
- `GET /stats/weekly`
- `GET /stats/dashboard`
- `GET/PUT /settings`
- `POST /export`
- `POST /backup`

## 前端体验

WinUI 前端采用 `NavigationView` 分区组织主要工作流：

- 今日计划：输入今日状态，查看情绪/压力识别、今日重点、自然语言建议和推荐任务。
- 课程知识点：维护课程、知识点、掌握程度、重要性、预计学习时间和关联考试/DDL。
- 日程：维护考试、作业 DDL 和活动安排。
- 学习记录：记录学习时长、完成情况和备注，也可从推荐任务直接带入记录。
- 复盘：查看学习时长趋势、情绪趋势、课程投入、知识点完成率和近期风险。
- 设置：配置 AI 建议模式、Base URL、模型、API Key，并执行导出和备份。

推荐任务支持两种闭环动作：

- “记录学习”：把推荐任务带入学习记录表单，用户可补充分钟数、完成情况和备注。
- “完成并记录”：按推荐预计分钟直接写入一条已完成学习记录，并刷新复盘统计。

为了降低误操作风险，前端会在删除课程、知识点、日程和学习记录前进行确认；当课程或知识点下仍有关联学习数据时，会阻止直接级联删除并提示先清理或迁移数据。表单校验错误会显示在对应页面内；如果本地后端自动启动失败，也会在页面内显示原因。

## 第一阶段业务逻辑

规则建议不会直接依赖大模型，而是按以下指标计算知识点优先级：

- 考试/DDL 紧急度
- 知识点重要性
- 掌握薄弱程度
- 情绪适配度
- 近 7 天学习不足程度

建议记录会写入 `advice_logs`，便于后续复盘和第二、三阶段继续扩展。

## 第二阶段增强

第二阶段已加入轻量情绪识别增强，当前采用规则词典实现，后续可替换为 Python 训练出的 TF-IDF + Logistic Regression / Naive Bayes 模型。

当前 `/emotion/analyze` 与 `/advice/today` 会返回并保存：

- `emotion`：积极、中性、焦虑、疲惫、拖延
- `pressure_type`：考试压力、作业压力、活动冲突、任务堆积、未明确
- `learning_state`：减压复习、轻量复习、低启动学习、高能量学习、任务拆解、常规学习
- `intensity_level`：light、medium、high
- `suggestion_tone`：安抚型、温和型、启动型、推进型、结构化、平衡型
- `confidence`：规则识别置信度
- `matched_keywords`：命中的关键词

今日建议会根据学习状态调整推荐任务数量和建议表达方式，例如焦虑时减少任务数量、拖延时优先推荐短时低启动任务。

## 第三阶段 AI 增强

第三阶段已加入 OpenAI-compatible Chat Completions 调用能力。系统仍然先进行本地结构化分析和任务优先级排序，再把结构化结果交给 AI 进行自然语言表达。

设置项位于前端“设置”页面的“AI 建议设置”区域，也可通过 `PUT /settings` 修改：

- `advice_mode`：`rules`、`hybrid` 或 `ai`
- `ai_base_url`：默认 `https://api.openai.com/v1`
- `ai_model`：默认 `gpt-4o-mini`，可改成兼容服务支持的模型名
- `ai_api_key`：只写入后端，不会在前端回显；留空表示不修改已保存的 Key

模式说明：

- `rules`：只使用本地规则建议。
- `hybrid`：优先调用 AI，失败或未配置 Key 时自动回退到规则建议。
- `ai`：调用 AI 生成自然语言建议；为了可靠性，AI 失败时仍会回退到规则建议。

`/advice/today` 会返回 `model_type` 与 `fallback_reason`。当 AI 不可用时，`model_type` 会显示为 `rules-v2-fallback`，并在 `fallback_reason` 中说明原因。

前端从 `rules` 切换到 `hybrid` 或 `ai` 时，会弹出隐私确认，提示今日状态文本和结构化学习摘要可能会发送到用户配置的兼容 AI 接口。

## 第四阶段可视化复盘

第四阶段已补充 `GET /stats/dashboard` 综合统计接口，并在 WinUI 前端加入独立“复盘”页面。当前可展示：

- 学习时长趋势：按日期聚合近 7-30 天学习分钟数。
- 情绪趋势：按日期展示最近一次情绪分析强度。
- 知识点完成率：按课程统计已完成/总知识点数量和完成率。
- 课程学习占比：统计不同课程在当前周期内的学习投入。
- 考试/DDL 倒计时：展示最近日程、剩余天数和关联未完成知识点数量。
- 今日重点：根据临近 DDL、未完成知识点和课程完成率给出当日优先方向。
- 近期风险：识别临近考试/DDL、整体完成率偏低、学习投入偏低或情绪记录不足等情况。
- 图表空状态：当学习记录、课程投入或情绪日志为空时，显示可执行的下一步提示。
- 本地发布打包：`scripts\package-release.ps1` 会整理前端、后端和启动脚本到 `dist\StudyMind`。
- 数据导出：`POST /export` 会导出课程、知识点、日程、学习记录、情绪日志和建议日志。

`/stats/dashboard` 支持查询参数：

- `date`：统计截止日期，例如 `2026-06-24`，默认今天。
- `days`：统计天数，范围 7-30，默认 14。
