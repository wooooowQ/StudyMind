# StudyMind.App 前端项目说明文档

本文档说明 StudyMind 前端桌面程序的功能、模块操作与技术架构。文档对象是 `frontend/StudyMind.App` 下的 WinUI 3 客户端，它通过 HTTP/JSON 调用本地 Rust 后端，并围绕课程、知识点、日程、学习记录、今日建议和学习复盘组织用户工作流。

前端由两个窗体组成：

- 主窗体：承载完整的学习规划、数据维护、复盘和设置工作流。
- 桌面便签窗体：作为主窗体的伴随小窗，以贴纸式纸张界面常驻桌面上方，支持通过左右方向键或页内箭头切换四个小页面，并通过托盘菜单显示、隐藏、返回主窗口和退出程序。

## 1. 程序实现的功能

StudyMind.App 是一个面向大学生学习场景的本地桌面学习规划客户端。它不是单纯的待办事项列表，而是将学习任务管理、考试/DDL 管理、学习记录、情绪状态输入、智能建议生成和数据复盘整合到一个侧边栏工作流中。

程序主要实现以下能力：

- 管理课程：创建、编辑、删除课程，并作为知识点、日程和统计分析的基础分类。
- 管理知识点：为课程维护知识点，记录掌握程度、重要性、预计学习分钟数、任务状态，并可关联考试或 DDL。
- 管理日程：记录考试、作业 DDL 和活动，维护起止日期、重要性和关联课程。
- 管理学习记录：按日期记录某个知识点的学习时长、完成情况和备注。
- 生成今日计划：用户输入当天状态后，系统结合情绪识别、课程知识点、日程紧急度和学习记录生成今日建议与推荐任务。
- 推荐任务闭环：用户可以将推荐任务带入学习记录表单，也可以一键“完成并记录”。
- 可视化复盘：展示学习时长趋势、情绪趋势、课程学习占比、课程完成率、临近日程、今日重点和近期风险。
- 设置 AI 建议：配置建议模式、本地规则/AI/混合模式、AI Base URL、模型名和 API Key，并提供隐私确认。
- 本地数据管理：支持导出 JSON 和备份 SQLite 数据库。
- 交互保护：支持表单校验、删除确认、关联数据自动清理说明、后端连接失败提示、加载状态和空状态提示。
- 桌面便签：提供可置顶、可拖动、固定尺寸的纸质伴随窗体，从主窗口共享同一份学习数据，便于在不打开完整主界面的情况下生成今日计划、查看建议任务、检查 DDL 和浏览知识点。

前端默认连接本地后端：

```text
http://127.0.0.1:7878
```

前端使用的接口封装集中在 `Services/StudyMindApiClient.cs`，界面状态和业务命令集中在 `ViewModels/MainViewModel.cs`。

## 2. 操作与模块说明

### 2.1 今日计划

#### 模块介绍

“今日计划”是程序的主入口。用户在这里选择日期、输入当天学习状态，例如“考试快到了，有点焦虑”或“今天状态不错，想多复习一点”。前端将这些信息提交给后端，后端返回情绪/压力识别结果、今日建议文本和推荐任务列表。

#### 模块意义

该模块将分散的课程、知识点、考试/DDL 和学习记录转化为可执行的今日学习安排。它解决的是“今天该先学什么”“当前状态适合做多重任务”“临近 DDL 是否需要优先处理”等问题。

#### 支持的操作

- 选择今日计划日期。
- 输入今日学习状态文本。
- 生成今日计划。
- 查看情绪/压力、学习状态/强度、分析依据。
- 查看今日重点、临近日程提醒和今日建议。
- 查看推荐任务列表。
- 将推荐任务带入学习记录表单。
- 一键完成推荐任务并写入学习记录。

#### 如何进行

用户进入“今日计划”页后：

1. 在日期选择器中选择统计/建议日期。
2. 在“今日状态”文本框中输入当前学习状态。
3. 点击“生成今日计划”。
4. 查看页面右侧返回的今日建议和推荐任务。
5. 对推荐任务执行“记录学习”或“完成并记录”。

“记录学习”会跳转到“学习记录”页，并自动填入知识点、日期、预计分钟数和备注；用户可以继续调整后保存。

“完成并记录”会直接写入一条完成状态的学习记录。

#### 后端数据修改方式

生成今日计划时，前端调用：

```text
POST /advice/today
```

请求数据来自 `TodayAdviceRequest`：

```json
{
  "date": "2026-07-05",
  "state_text": "考试快到了，有点焦虑"
}
```

后端会完成以下数据变化：

- 如果传入了 `state_text`，后端会进行情绪/压力/学习状态识别，并保存到 `emotion_logs`。
- 后端会读取课程、知识点、日程和学习记录，计算推荐任务优先级。
- 后端会生成建议文本，并保存到 `advice_logs`。
- 返回 `TodayAdviceResponseDto`，包括 `advice`、`emotion`、`recommended_tasks`、`model_type` 和 `fallback_reason`。

点击“完成并记录”时，前端调用：

```text
POST /study-records
```

写入一条 `completion = "completed"` 的学习记录。后端保存到 `study_records` 后，会同步更新对应知识点状态，将已完成记录对应的知识点标记为完成/已掌握。

### 2.2 课程与知识点管理

#### 模块介绍

“课程知识点”页用于维护课程和知识点。课程是学习内容的一级分类；知识点是实际被学习、被推荐、被记录和被统计的基本单位。

页面包含两个核心区：

- 课程表单和课程列表。
- 知识点表单和知识点列表。

#### 模块意义

课程和知识点是整个系统的基础数据。没有课程和知识点，今日计划无法生成有针对性的任务，学习记录也无法绑定具体学习对象，复盘统计也无法按课程或完成率分析。

#### 支持的操作

课程支持：

- 新建课程。
- 编辑课程名称。
- 删除课程及关联数据。
- 选择课程作为知识点或日程的默认关联项。

知识点支持：

- 新建知识点。
- 编辑知识点名称。
- 设置所属课程。
- 设置掌握程度：未掌握、学习中、已掌握。
- 设置重要性，范围 1 到 5。
- 设置预计学习分钟数。
- 关联考试或 DDL。
- 设置任务状态：待学习、已完成。
- 删除知识点及关联学习记录。

#### 如何进行

课程操作：

1. 在课程输入框中填写课程名称。
2. 点击“保存”。如果未选中已有课程，前端会新建课程。
3. 从课程列表选择已有课程后，表单进入编辑状态。
4. 修改名称后再次点击“保存”，前端会更新当前选中的课程。
5. 点击删除按钮时，前端会弹出确认框，说明将自动删除的知识点和学习记录数量，以及将解除关联的日程数量。

知识点操作：

1. 选择所属课程。
2. 填写知识点名称。
3. 选择掌握程度、任务状态、关联考试/DDL。
4. 设置重要性和预计分钟数。
5. 点击“保存”。如果未选中已有知识点，前端会新建知识点；如果已经选中列表项，则更新该知识点。
6. 删除知识点时，前端会提示将自动删除多少条关联学习记录，并从今日推荐中移除相关任务。

#### 后端数据修改方式

课程创建：

```text
POST /courses
```

请求体为 `CourseInput`：

```json
{
  "name": "数据库系统"
}
```

课程编辑：

```text
PUT /courses/{id}
```

课程删除：

```text
DELETE /courses/{id}
```

当前前端在删除课程前会主动执行以下清理步骤：

1. 找出该课程下所有知识点。
2. 找出这些知识点下所有学习记录。
3. 对直接关联该课程的日程调用 `PUT /events/{id}`，将 `related_course_id` 置空，保留日程本身。
4. 对关联学习记录逐条调用 `DELETE /study-records/{id}`。
5. 对关联知识点逐条调用 `DELETE /topics/{id}`。
6. 最后调用 `DELETE /courses/{id}` 删除课程。

这样做的目的是让删除行为在前端提示和实际操作之间保持一致，避免用户手动逐项清理。

知识点创建：

```text
POST /topics
```

请求体为 `TopicInput`：

```json
{
  "course_id": 1,
  "name": "事务隔离级别",
  "mastery_level": "学习中",
  "importance": 4,
  "estimated_minutes": 45,
  "exam_id": 2,
  "status": "pending"
}
```

知识点编辑：

```text
PUT /topics/{id}
```

知识点删除：

```text
DELETE /topics/{id}
```

当前前端在删除知识点前会先删除该知识点关联的学习记录：

```text
DELETE /study-records/{id}
DELETE /topics/{id}
```

删除后，前端会刷新课程、知识点、日程、学习记录和复盘统计。

### 2.3 日程管理

#### 模块介绍

“日程”页用于记录考试、作业 DDL 和活动。日程可以关联课程，也可以作为知识点的考试/DDL 目标。它影响今日推荐中的紧急度计算，也会在复盘页展示倒计时和未完成知识点数量。

#### 模块意义

学习计划不只取决于知识点本身，还取决于截止时间。日程管理让系统知道哪些考试或 DDL 临近，从而在今日计划中提升相关知识点的优先级。

#### 支持的操作

- 新建考试、作业或活动。
- 编辑日程标题。
- 设置日程类型：考试、作业、活动。
- 选择关联课程。
- 选择时间模式：仅设置结束时间点，或使用开始/结束日期范围。
- 设置结束时间点；在日期范围模式下还可以设置开始日期。
- 设置重要性，范围 1 到 5。
- 删除日程。

#### 如何进行

1. 进入“日程”页。
2. 在表单中填写标题，选择类型、关联课程、时间模式、结束时间点和重要性。
3. 如果只关心考试或交付截止时间，保持“仅设置结束时间点”开启；前端会把开始日期和结束日期都写成该结束时间点。
4. 如果需要记录活动或持续性安排，关闭“仅设置结束时间点”，再分别选择开始日期和结束日期。
5. 点击“保存”。如果右侧没有选中任何日程，前端会新建日程，并在表单底部用绿色 InfoBar 提示已新建。
6. 从日程列表选择已有日程后，表单进入编辑状态；再次点击“保存”会修改该日程。
7. 编辑已有日程时，如果标题与其它日程重复，前端会弹出确认框。用户可以取消保存，也可以将标题保存为“原标题-2”。
8. 点击删除按钮后确认删除。

日期通过 `DatePicker` 选择，前端会减少手动输入格式错误；后端仍会验证日期格式和起止顺序。

#### 后端数据修改方式

日程创建：

```text
POST /events
```

请求体为 `EventInput`：

```json
{
  "title": "数据库期末考试",
  "event_type": "考试",
  "start_time": "2026-07-15",
  "end_time": "2026-07-15",
  "importance": 5,
  "related_course_id": 1
}
```

日程编辑：

```text
PUT /events/{id}
```

前端的“保存”按钮会根据当前是否选中了右侧日程列表项决定调用创建或编辑接口：

- 未选中日程：调用 `POST /events` 创建新日程。
- 已选中日程：调用 `PUT /events/{id}` 修改该日程。

在“仅设置结束时间点”模式下，前端仍使用同一个 `EventInput` 请求结构，但会将 `start_time` 和 `end_time` 都设置为用户选择的结束时间点，从而兼容现有后端接口。

日程删除：

```text
DELETE /events/{id}
```

后端数据库中，知识点通过 `exam_id` 关联日程。当删除日程时，数据库外键规则会将知识点的 `exam_id` 置空，避免知识点本身被删除。

当删除课程时，前端会对关联日程调用 `PUT /events/{id}`，将 `related_course_id` 置空，以保留原有考试/DDL 信息。

### 2.4 学习记录管理

#### 模块介绍

“学习记录”页用于记录用户每天学习了哪个知识点、学习了多少分钟、完成情况和备注。学习记录是复盘图表、课程投入统计、推荐任务排序和知识点自动完成判断的重要来源。

#### 模块意义

学习记录把“计划”转化为可回溯的数据。没有学习记录，系统只能给出静态建议；有了学习记录后，系统可以判断近期投入、学习趋势、知识点是否长期未复习、课程投入是否失衡。

#### 支持的操作

- 新建学习记录。
- 编辑学习记录。
- 删除学习记录。
- 选择知识点。
- 选择学习日期。
- 填写学习分钟数。
- 设置完成情况：部分完成、已完成。
- 填写备注。
- 从推荐任务自动带入记录表单。
- 从推荐任务一键完成并写入记录。

#### 如何进行

普通记录流程：

1. 进入“学习记录”页。
2. 选择知识点。
3. 选择日期。
4. 输入分钟数。
5. 选择完成情况。
6. 填写备注。
7. 点击“保存”。如果未选中已有记录，前端会新建学习记录；如果已经选中列表项，则更新该记录。

推荐任务带入流程：

1. 在“今日计划”页生成推荐任务。
2. 点击某个任务的“记录学习”。
3. 前端跳转到“学习记录”页，并自动填入知识点、日期、预计分钟数和备注。
4. 用户确认或调整后保存。

推荐任务一键完成流程：

1. 在“今日计划”页点击“完成并记录”。
2. 前端直接调用后端创建学习记录。
3. 完成情况写为 `completed`。

#### 后端数据修改方式

学习记录创建：

```text
POST /study-records
```

请求体为 `StudyRecordInput`：

```json
{
  "topic_id": 10,
  "date": "2026-07-05",
  "minutes": 30,
  "completion": "completed",
  "note": "从今日推荐完成：临近考试，优先复习"
}
```

学习记录编辑：

```text
PUT /study-records/{id}
```

学习记录删除：

```text
DELETE /study-records/{id}
```

后端创建或编辑学习记录后，如果 `completion` 是 `completed`、`done`、`已完成` 或 `完成`，会同步更新对应知识点，将 `status` 设为 `completed`，并将 `mastery_level` 更新为 `已掌握`。

### 2.5 可视化复盘

#### 模块介绍

“复盘”页通过图表和摘要卡片展示最近一段时间的学习状态。用户可以设置统计天数，范围 7 到 30 天，并查看学习投入、完成进度、近期风险、学习时长趋势、课程学习占比、情绪趋势、课程完成率和临近日程。

#### 模块意义

复盘模块帮助用户从数据中看到学习节奏是否稳定、课程投入是否均衡、临近考试是否存在风险，以及情绪记录是否足够。它让系统不只是生成计划，也能持续反馈计划执行效果。

#### 支持的操作

- 设置统计天数。
- 更新复盘数据。
- 查看统计区间。
- 查看学习总时长和日均学习分钟数。
- 查看知识点整体完成进度。
- 查看近期风险提示。
- 查看学习时长折线图。
- 查看课程学习占比饼图。
- 查看情绪趋势折线图。
- 查看课程完成率列表。
- 查看最近考试/DDL 倒计时和未完成知识点数。

#### 如何进行

1. 进入“复盘”页。
2. 调整“统计天数”，可选择 7 到 30 天。
3. 点击“更新复盘”。
4. 查看摘要卡片和图表。
5. 如果暂无学习记录、课程投入或情绪记录，图表区会展示空状态提示。

#### 后端数据读取与前端展示方式

复盘页主要调用：

```text
GET /stats/dashboard?date={date}&days={days}
```

返回数据映射为 `DashboardStatsResponseDto`，包括：

- `daily_minutes`：每日学习分钟数，用于学习时长趋势图。
- `emotion_trend`：每日情绪强度，用于情绪趋势图。
- `course_minutes`：课程学习投入，用于课程占比饼图。
- `topic_progress`：课程知识点完成率，用于完成率列表。
- `upcoming_events`：临近日程和未完成知识点数。
- `total_minutes`、`average_daily_minutes`、`completed_topics`、`total_topics`、`overall_completion_rate` 等摘要指标。

该接口不直接修改数据，只读取并聚合后端 SQLite 中的课程、知识点、日程、学习记录和情绪日志。

前端收到数据后会在 `ApplyDashboardStats` 中：

- 更新 `WeeklyStats`、`CourseMinutes`、`TopicProgress`、`UpcomingEvents`、`EmotionTrend`。
- 生成 LiveCharts2 的折线图和饼图数据源。
- 计算“今日重点”“近期风险”和“趋势洞察”显示文案。

### 2.6 设置与本地数据管理

#### 模块介绍

“设置”页用于配置 AI 建议模式、AI 接口信息，以及执行本地数据导出和数据库备份。

#### 模块意义

StudyMind 是本地优先的学习规划工具。设置模块让用户可以选择完全本地规则建议，也可以配置兼容 OpenAI Chat Completions 的服务以增强建议文本表达。同时，数据导出和备份降低了误删、迁移和本地数据损坏的风险。

#### 支持的操作

- 查看当前数据库路径。
- 设置建议模式：本地规则、优先 AI、仅 AI。
- 设置 AI Base URL。
- 设置 AI 模型名称。
- 设置 API Key。
- 保存设置。
- 导出 JSON。
- 备份数据库。

#### 如何进行

AI 设置流程：

1. 进入“设置”页。
2. 选择建议模式。
3. 填写 Base URL、模型名称和 API Key。
4. 点击“保存设置”。
5. 如果从本地规则切换到 AI 或混合模式，前端会弹出隐私确认，说明今日状态文本和结构化学习摘要可能发送到用户配置的兼容接口。

导出流程：

1. 点击“导出 JSON”。
2. 前端调用后端导出接口获取完整数据。
3. 前端将 JSON 写入用户文档目录下的 `StudyMind` 文件夹。

备份流程：

1. 点击“备份数据库”。
2. 后端执行 SQLite WAL checkpoint。
3. 后端复制数据库文件到备份目录。
4. 前端显示备份路径。

#### 后端数据修改方式

读取设置：

```text
GET /settings
```

更新设置：

```text
PUT /settings
```

请求体为 `SettingsInput`：

```json
{
  "advice_mode": "hybrid",
  "ai_base_url": "https://api.openai.com/v1",
  "ai_model": "gpt-4o-mini",
  "ai_api_key": "sk-..."
}
```

后端更新 `settings` 表中的建议模式、API Key、Base URL、模型名称和更新时间。API Key 只写入后端，不会在前端回显；前端留空 API Key 表示不修改已保存的 Key。

导出数据：

```text
POST /export
```

该接口读取课程、知识点、日程、学习记录、情绪日志和建议日志，并返回给前端。前端再写入本地 JSON 文件。

备份数据库：

```text
POST /backup
```

该接口由后端复制 SQLite 数据库文件，返回备份路径。前端不直接复制数据库。

### 2.7 全局导航、刷新和状态反馈

#### 模块介绍

前端通过 `NavigationView` 提供侧边栏导航，页面包括：

- 今日计划
- 课程知识点
- 日程
- 学习记录
- 复盘
- 设置

页面标题区展示当前子页面标题，并在标题右侧提供“便签”和“刷新”操作。左侧导航栏底部展示当前操作状态，加载时显示不确定进度条。

#### 模块意义

全局导航让用户在“计划、维护数据、记录学习、复盘、设置”之间形成闭环。导航栏底部状态和错误提示帮助用户知道后端是否连接正常、当前操作是否成功、表单或请求为何失败。

#### 支持的操作

- 切换页面。
- 刷新全部数据。
- 查看后端连接状态。
- 查看表单错误和请求错误。
- 在后端未启动时得到启动脚本提示。

#### 如何进行

用户点击左侧导航菜单切换页面。点击当前子页面标题右侧的“刷新”按钮会重新加载课程、日程、知识点、学习记录和复盘统计；点击“便签”会打开桌面便签。

#### 后端数据读取方式

初始化和刷新时，前端会调用：

```text
GET /courses
GET /events
GET /topics
GET /study-records
GET /stats/dashboard
GET /settings
```

这些接口主要读取数据，不直接修改数据。刷新完成后，前端会重建绑定集合和图表数据源。

### 2.8 桌面便签窗体

#### 模块介绍

桌面便签是主窗体之外的伴随窗体，对应 `CyberNoteWindow.xaml` 和 `CyberNoteWindow.xaml.cs`。它不是独立的数据维护页，而是一个贴纸式轻量面板：从主窗体的 `MainViewModel` 读取同一份课程、知识点、日程、今日建议、推荐任务和状态消息，把用户最需要随手看的内容压缩到一个置顶小窗中。

便签适合在学习过程中常驻桌面，用于快速生成今日计划、查看下一项推荐任务、检查最近 DDL、浏览总体知识点进度和当前后端/刷新状态。需要新增、编辑或删除课程、知识点、日程和学习记录时，用户仍回到主窗体完成。

#### 模块意义

主窗体适合完整规划和维护数据，便签窗体适合“执行中提醒”和轻量生成今日计划。两者共享同一个 `MainViewModel`，因此主窗体生成今日计划、刷新数据或修改学习记录后，便签会通过数据绑定展示最新状态；便签本身不复制业务数据。

#### 打开与关闭方式

- 在主窗体顶部点击“便签”按钮会打开桌面便签。
- 第一次打开便签时，主窗体会懒创建 `CyberNoteWindow`，同时创建系统托盘图标。
- 便签右上角的隐藏按钮只隐藏便签，不退出应用。
- 便签右上角的主窗口按钮会激活 StudyMind 主窗体。
- 关闭便签窗口时，除应用退出流程外，窗口关闭会被拦截并转为隐藏，避免误关。
- 主窗体真正关闭或托盘菜单选择退出时，会保存便签设置、关闭便签并移除托盘图标。

#### 托盘菜单

托盘图标由 `TrayIconController.cs` 通过 Windows Shell NotifyIcon API 创建，提示文本为“StudyMind 便签”。

托盘交互包括：

- 左键双击托盘图标：显示便签。
- 右键托盘图标：打开菜单。
- 菜单“显示便签”：显示或重新激活便签。
- 菜单“隐藏便签”：隐藏便签。
- 菜单“显示主窗口”：激活主窗体。
- 菜单“退出 StudyMind”：关闭应用，并清理便签窗体和托盘图标。

#### 便签显示内容

便签包含四个小页面，可通过页眉右侧的上一页/下一页按钮切换，也可以在非文本输入焦点下使用键盘左右方向键切换：

- 第 1 页“今日计划”：选择计划日期，输入“描述今日状态”，点击“生成计划”后复用 `Study.GenerateAdviceCommand` 调用今日计划生成逻辑；页面下方展示状态识别结果。
- 第 2 页“建议与任务”：展示 `Study.TodayAdvice`、建议来源和 `Study.RecommendedTasks` 推荐任务列表。推荐任务条目展示知识点名称、课程与预计分钟数、推荐理由和优先级。
- 第 3 页“DDL 日程”：只读展示 `Study.UpcomingEvents`，包含标题、日期、剩余天数、类型和未完成知识点数量；页面下方用醒目的提醒框展示当前最紧急的日程摘要。
- 第 4 页“知识点”：只读展示 `Study.Topics`，包含知识点名称、所属课程、预计分钟、重要性、掌握程度和完成状态，并在页眉显示总体完成进度。

便签底部会展示主 ViewModel 的 `StatusMessage`，例如后端连接状态、数据刷新结果、今日计划生成结果、记录保存结果等。

#### 便签设置

便签会保存窗口位置和最后停留的小页面。分页状态由 `CyberNoteViewModel` 写回 `CyberNoteSettings`，并保存到：

```text
%APPDATA%\StudyMind\cyber-note-settings.json
```

保存字段包括：

- `CurrentPageIndex`：便签最后停留的小页面，范围 0 到 3。
- `Width` / `Height`：便签固定显示尺寸，当前归一化为默认值。
- `X` / `Y`：便签上次所在屏幕坐标。

设置加载失败或保存失败不会阻止便签工作；前端会回退到默认设置。

#### 窗口行为

便签窗体使用无原生标题栏的自绘纸张外观，并设置为置顶窗口：

- 默认尺寸为 `430 x 620`。
- 使用固定窗口尺寸，避免原生可调整边框在纸张外侧形成浅色外沿。
- 使用 Win32 圆角窗口区域裁剪，让真实窗体外形贴合黄色便签外层圆角。
- 不显示最大化和最小化按钮。
- 不出现在任务切换器中。
- 首次打开时默认放在当前工作区右下角附近。
- 后续打开时优先恢复上次保存的位置，并保持默认显示尺寸。
- 如果屏幕工作区发生变化，便签会自动夹回可见区域。
- 顶部标题区域可拖动窗口。
- 非文本输入焦点下按键盘左/右方向键可切换小页面。

#### 刷新与数据关系

便签刷新按钮绑定的是 `Study.RefreshCommand`，也就是主窗体使用的同一个刷新命令。刷新时会重新读取课程、日程、知识点、学习记录、复盘统计和设置；刷新完成后，主窗体和便签共享的绑定数据一起更新。

便签直接触发的业务写入仅限“生成今日计划”：它复用主 `MainViewModel` 的 `GenerateAdviceCommand`，由后端按原有 `/advice/today` 流程保存情绪日志和建议日志。便签不直接新增、编辑或删除课程、知识点、日程和学习记录；这些维护操作仍在主窗体完成。便签自身还会保存本地分页、窗口位置和尺寸偏好。

## 3. 程序框架与架构

### 3.1 技术栈

前端技术栈：

- WinUI 3：Windows 桌面 UI 框架。
- C# / .NET：前端业务逻辑和数据绑定代码。
- CommunityToolkit.Mvvm：提供 `ObservableObject`、`RelayCommand`、属性通知和命令绑定。
- HttpClient：调用本地 Rust 后端 HTTP API。
- System.Text.Json：序列化和反序列化 JSON。
- LiveCharts2：绘制学习趋势、情绪趋势和课程占比图表。

后端交互对象：

- Rust axum 本地 HTTP 服务。
- SQLite 本地数据库。

### 3.2 目录结构

前端目录位于：

```text
frontend/StudyMind.App
```

主要文件：

```text
App.xaml
App.xaml.cs
MainWindow.xaml
MainWindow.xaml.cs
CyberNoteWindow.xaml
CyberNoteWindow.xaml.cs
CyberNoteSettings.cs
Models/ApiModels.cs
Services/StudyMindApiClient.cs
ViewModels/MainViewModel.cs
ViewModels/CyberNoteViewModel.cs
TrayIconController.cs
StudyMind.App.csproj
```

各文件职责：

- `App.xaml`：定义应用级资源，包括浅色/深色主题、页面背景、面板、强调色、成功/信息/警告/危险语义色。
- `MainWindow.xaml`：定义主界面布局，包括侧边栏导航、页面标题工具栏、各页面表单、列表、图表、空状态和设置页。
- `MainWindow.xaml.cs`：处理窗口初始化、导航选中同步、响应式布局、删除确认弹窗、AI 隐私确认弹窗，以及便签窗体和托盘图标的创建、显示、隐藏和释放。
- `CyberNoteWindow.xaml`：定义桌面便签窗体布局，包括纸张外观、拖动区域、刷新/主窗口/隐藏按钮、四个分页、页码圆点、纸面纹理和右下角卷页效果。
- `CyberNoteWindow.xaml.cs`：处理便签窗口初始化、无标题栏配置、置顶和固定尺寸、拖动行为、左右方向键分页、隐藏而非关闭、恢复/保存窗口位置，以及打开主窗体。
- `CyberNoteSettings.cs`：定义便签分页、窗口位置和固定显示尺寸等本地偏好，并将其持久化到 `%APPDATA%\StudyMind\cyber-note-settings.json`。
- `MainViewModel.cs`：承担页面状态、命令、表单校验、数据加载、接口调用、删除关联清理、推荐任务闭环、图表数据生成。
- `CyberNoteViewModel.cs`：包装主 `MainViewModel`，为便签提供四页分页状态、上一页/下一页命令、页面可见性、页标题、页码圆点和时间戳等轻量状态。
- `ApiModels.cs`：定义前后端 DTO 和部分前端展示辅助属性。
- `StudyMindApiClient.cs`：封装 HTTP GET/POST/PUT/DELETE 请求，统一 JSON 命名策略和错误处理。
- `TrayIconController.cs`：封装 Windows 托盘图标、托盘右键菜单、双击显示便签和退出应用等 Win32 交互。
- `StudyMind.App.csproj`：声明 WinUI、Windows App SDK、CommunityToolkit.Mvvm 和 LiveCharts2 依赖。

### 3.3 架构模式

前端整体采用 MVVM 风格：

```text
MainWindow.xaml
    |
    | 数据绑定 / 命令绑定
    v
MainViewModel.cs
    |
    | DTO / 服务调用
    v
StudyMindApiClient.cs
    |
    | HTTP / JSON
    v
Rust axum 后端
    |
    | rusqlite
    v
SQLite 本地数据库
```

View 层只负责呈现和触发命令。ViewModel 层保存状态并处理业务流程。Service 层负责 HTTP 调用。Model 层定义请求和响应数据结构。

便签窗体沿用同一套业务状态：

```text
CyberNoteWindow.xaml
    |
    | 数据绑定 / 本地显示偏好
    v
CyberNoteViewModel.cs
    |
    | Study 属性引用
    v
MainViewModel.cs
```

因此，主窗体和便签窗体不是两套独立业务流程。主窗体负责完整编辑和配置，便签负责从 `MainViewModel` 读取摘要信息并触发刷新、隐藏、返回主窗体等轻量操作。

### 3.4 数据流

典型新增课程流程：

```text
用户在 MainWindow.xaml 输入课程名称
    |
点击保存按钮
    |
SaveCourseCommand
    |
MainViewModel.ValidateCourseForm()
    |
未选中课程时调用 StudyMindApiClient.CreateCourseAsync()
    |
POST /courses
    |
后端写入 courses 表
    |
前端重新 LoadCollectionsAsync()
    |
界面列表刷新
```

典型生成今日计划流程：

```text
用户输入今日状态
    |
GenerateAdviceCommand
    |
POST /advice/today
    |
后端保存情绪日志和建议日志
    |
后端返回建议文本与推荐任务
    |
前端更新 TodayAdvice、TodayEmotion、RecommendedTasks
    |
前端刷新复盘统计
```

典型删除课程流程：

```text
用户点击删除课程
    |
MainWindow.xaml.cs 弹出确认对话框
    |
DeleteCourseCommand
    |
前端解除相关日程课程关联
    |
前端删除课程下学习记录
    |
前端删除课程下知识点
    |
前端删除课程
    |
刷新集合和复盘统计
```

典型打开便签流程：

```text
用户点击主窗体顶部“便签”
    |
ShowCyberNote_Click
    |
MainWindow.EnsureTrayIcon()
    |
首次打开时创建 TrayIconController
    |
首次打开时创建 CyberNoteWindow(ViewModel, ShowMainWindow)
    |
CyberNoteWindow 创建 CyberNoteViewModel
    |
读取 cyber-note-settings.json
    |
恢复上次位置尺寸，或放到工作区右下角
    |
显示置顶便签窗体
```

### 3.5 状态管理

`MainViewModel` 使用以下方式管理状态：

- `ObservableCollection<T>` 保存课程、知识点、日程、学习记录、推荐任务和统计集合。
- 普通属性保存表单输入、当前页面、选中项、状态消息和图表数据。
- `SetProperty` 和 `OnPropertyChanged` 通知 UI 更新。
- `RelayCommand` 将按钮操作映射为 ViewModel 命令。
- `RunBusyAsync` 统一处理加载状态、状态消息、异常提示和防止重复操作。

主要集合：

- `Courses`
- `Topics`
- `Events`
- `StudyRecords`
- `RecommendedTasks`
- `WeeklyStats`
- `CourseMinutes`
- `TopicProgress`
- `UpcomingEvents`
- `EmotionTrend`

便签相关状态分为两层：

- `CyberNoteViewModel`：保存 `CurrentPageIndex`，提供上一页/下一页循环切换命令，并根据当前页计算四个页面的可见性、标题、副标题、页码和页码圆点。
- `CyberNoteSettings`：保存当前页、窗口宽高和窗口坐标。`CyberNoteWindow` 在窗口移动、调整尺寸、切换页面、隐藏和退出时捕获并保存这些设置。

便签不维护课程、知识点、日程、学习记录或推荐任务的副本。相关列表、今日重点、复盘进度和状态消息都通过 `CyberNoteViewModel.Study` 引用主 `MainViewModel`。

### 3.6 接口封装

`StudyMindApiClient` 统一封装后端接口：

- `GetCoursesAsync` / `CreateCourseAsync` / `UpdateCourseAsync` / `DeleteCourseAsync`
- `GetTopicsAsync` / `CreateTopicAsync` / `UpdateTopicAsync` / `DeleteTopicAsync`
- `GetEventsAsync` / `CreateEventAsync` / `UpdateEventAsync` / `DeleteEventAsync`
- `GetStudyRecordsAsync` / `CreateStudyRecordAsync` / `UpdateStudyRecordAsync` / `DeleteStudyRecordAsync`
- `GenerateTodayAdviceAsync`
- `GetDashboardStatsAsync`
- `GetSettingsAsync` / `UpdateSettingsAsync`
- `ExportAsync`
- `BackupAsync`

JSON 序列化配置：

- 使用 snake_case 命名策略，与 Rust 后端字段风格一致。
- 忽略空值字段。
- 反序列化时不区分大小写。

### 3.7 响应式与视觉架构

主窗口使用 `NavigationView` 作为应用骨架。页面标题、便签入口和刷新按钮位于 `NavigationView.Header`，后端连接状态位于 `NavigationView.PaneFooter`。内容区域使用多个 `ScrollViewer` 和 Grid/StackPanel 组合页面布局。

前端已经实现以下响应式处理：

- 今日计划页在宽窗口下使用双列，在窄窗口下变为单列。
- 课程、日程、学习记录、复盘、设置等页面在宽窗口下使用主从两列，在窄窗口下切换为上下排列。
- 复盘页顶部工具栏和摘要卡片在窄窗口下自动分行。

视觉资源集中在 `App.xaml` 和 `MainWindow.xaml`：

- 面板卡片：`PanelBorderStyle`
- 重点提示：`FocusTileBorderStyle`
- 风险提示：`RiskTileBorderStyle`
- 成功/进度提示：`SuccessTileBorderStyle`
- 主要按钮：`PrimaryActionButtonStyle`
- 标签胶囊：`PillBorderStyle`

浅色和深色主题都提供了背景色、面板色、强调色、信息色、成功色、警告色和危险色。

便签窗体使用独立的纸张视觉资源，集中定义在 `CyberNoteWindow.xaml` 的 `Grid.Resources` 中：

- 墨色与弱化文字：`NoteInkBrush`、`NoteMutedBrush`
- 纸张与顶部栏：`NotePaperBrush`、`NotePaperSoftBrush`、`NoteHeaderBrush`
- 纸张边缘、横线和强调色：`NoteEdgeBrush`、`NoteLineBrush`、`NoteAccentBrush`
- 紧急提醒：`NoteWarningBrush`
- 纸张按钮、主按钮、纸面面板、列表行、标签和说明文字样式。

便签以小窗为主要形态，不使用主窗体的 `NavigationView`。它通过固定尺寸边界、页面 `ScrollViewer` 和四页可见性切换保证内容在较小窗口中仍可查看。纸张质感通过浅色径向光影、低透明度纹理线、暖黄色标题栏、阴影层和右下角卷页形状组合实现。便签顶部区域同时承担标题展示和拖动手柄职责，右侧提供刷新、打开主窗口和隐藏三个图标按钮。

### 3.8 错误处理与交互保护

前端提供以下保护：

- 表单必填校验：课程名称、知识点名称、日程标题、学习记录知识点等。
- 日期顺序校验：结束日期不能早于开始日期。
- 学习分钟数校验：必须大于 0。
- 删除确认：课程、知识点、日程、学习记录删除前均弹出确认框。
- 关联删除说明：删除课程/知识点时明确说明自动清理范围。
- 忙碌态保护：主要按钮在请求执行期间禁用，左侧导航栏底部进度条显示当前请求状态。
- 无障碍标签：纯图标按钮提供 `AutomationProperties.Name`，便于屏幕阅读器识别。
- AI 隐私确认：从本地规则切换到 AI 或混合模式时弹窗确认。
- 后端连接失败提示：如果后端不可达，提示运行启动脚本。
- 请求超时提示：提示确认本地服务状态。
- 便签误关闭保护：用户关闭便签时默认转为隐藏，只有应用退出流程会真正关闭便签窗体。
- 便签位置保护：恢复上次坐标后会检查当前屏幕工作区，若便签落在可见区域外，会自动移动回工作区内。
- 便签偏好容错：便签设置读取或保存失败时回退默认值，不影响主窗体和便签继续运行。
- 托盘资源清理：主窗体关闭时会同时关闭便签并删除托盘图标，避免残留无效图标。

### 3.9 与后端数据表的关系

前端模块与后端数据表大致对应如下：

| 前端模块 | 主要接口 | 后端数据 |
| --- | --- | --- |
| 课程管理 | `/courses` | `courses` |
| 知识点管理 | `/topics` | `topics` |
| 日程管理 | `/events` | `events` |
| 学习记录管理 | `/study-records` | `study_records` |
| 今日计划 | `/advice/today` | `emotion_logs`、`advice_logs`，并读取课程/知识点/日程/学习记录 |
| 复盘 | `/stats/dashboard` | 聚合 `study_records`、`topics`、`events`、`emotion_logs` |
| 设置 | `/settings` | `settings` |
| 导出 | `/export` | 读取主要业务表 |
| 备份 | `/backup` | 复制 SQLite 数据库文件 |
| 桌面便签 | 复用主窗体 `RefreshCommand`、`GenerateAdviceCommand` 和已加载集合 | 生成今日计划时写入 `emotion_logs`、`advice_logs`；本地分页和窗口偏好保存到 `%APPDATA%\StudyMind\cyber-note-settings.json` |

### 3.10 构建与运行

前端项目文件：

```text
frontend/StudyMind.App/StudyMind.App.csproj
```

构建命令：

```powershell
dotnet build .\frontend\StudyMind.App\StudyMind.App.csproj
```

运行前需要先启动本地后端：

```powershell
.\scripts\start-backend.ps1
```

如果使用 Visual Studio，可打开根目录下的 `StudyMind.sln`，将 `StudyMind.App` 设置为启动项目后运行。
