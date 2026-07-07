$ErrorActionPreference = "Stop"

$baseUrl = "http://127.0.0.1:7878"

function Post-Json($path, $body) {
    Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUrl$path" `
        -ContentType "application/json; charset=utf-8" `
        -Body ($body | ConvertTo-Json -Depth 10)
}

Invoke-RestMethod -Method Get -Uri "$baseUrl/health" | Out-Null

$course = Post-Json "/courses" @{
    name = "Machine Learning"
}

$examDate = (Get-Date).AddDays(3).ToString("yyyy-MM-dd")
$event = Post-Json "/events" @{
    title = "Machine Learning Exam"
    event_type = "exam"
    start_time = $examDate
    end_time = $examDate
    importance = 5
    related_course_id = $course.id
}

$topic1 = Post-Json "/topics" @{
    course_id = $course.id
    name = "Model evaluation metrics"
    mastery_level = "learning"
    importance = 4
    estimated_minutes = 30
    exam_id = $event.id
    status = "pending"
}

Post-Json "/topics" @{
    course_id = $course.id
    name = "Support vector machine"
    mastery_level = "weak"
    importance = 5
    estimated_minutes = 45
    exam_id = $event.id
    status = "pending"
} | Out-Null

Post-Json "/study-records" @{
    topic_id = $topic1.id
    date = (Get-Date).ToString("yyyy-MM-dd")
    minutes = 20
    completion = "partial"
    note = "Reviewed accuracy, recall, and F1."
} | Out-Null

$advice = Post-Json "/advice/today" @{
    date = (Get-Date).ToString("yyyy-MM-dd")
    state_text = "exam is close and I feel anxious"
}

$advice | ConvertTo-Json -Depth 10
