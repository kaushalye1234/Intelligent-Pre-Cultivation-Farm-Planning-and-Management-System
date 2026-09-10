$ErrorActionPreference = "Stop"

function Login($email, $password) {
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -ContentType "application/json" -Body $body
}

$farmerLogin = Login "farmer@agriassist.local" "Farmer@2026"
$fieldLogin = Login "fieldofficer@agriassist.local" "Field@2026"
$agLogin = Login "agofficer@agriassist.local" "Agri@2026"
$farmerHeaders = @{ Authorization = "Bearer $($farmerLogin.accessToken)" }
$fieldHeaders = @{ Authorization = "Bearer $($fieldLogin.accessToken)" }
$agHeaders = @{ Authorization = "Bearer $($agLogin.accessToken)" }

$farmBody = @{ name = "Task Demo Farm"; location = "Demo Location"; totalArea = 8; ownerUserId = $null } | ConvertTo-Json
$farm = Invoke-RestMethod -Uri "http://localhost:5000/api/crop-planning/farms" -Method Post -ContentType "application/json" -Body $farmBody -Headers $farmerHeaders
$fieldBody = @{ farmId = $farm.id; name = "Task Field"; area = 2; soilType = "Loam"; isActive = $true } | ConvertTo-Json
$field = Invoke-RestMethod -Uri "http://localhost:5000/api/crop-planning/fields" -Method Post -ContentType "application/json" -Body $fieldBody -Headers $farmerHeaders

$taskBody = @{
    farmId = $farm.id
    title = "Review irrigation setup"
    description = "Manual workflow task."
    dueAt = "2026-10-05T08:00:00Z"
    assignedToUserId = $farmerLogin.user.id
    status = 2
} | ConvertTo-Json
$task = Invoke-RestMethod -Uri "http://localhost:5000/api/task-approval/tasks" -Method Post -ContentType "application/json" -Body $taskBody -Headers $fieldHeaders

$approvalBody = @{ comment = "Approved manually."; agentWorkflowId = $null } | ConvertTo-Json
$approval = Invoke-RestMethod -Uri "http://localhost:5000/api/task-approval/tasks/$($task.id)/approve" -Method Post -ContentType "application/json" -Body $approvalBody -Headers $agHeaders
$approvals = Invoke-RestMethod -Uri "http://localhost:5000/api/task-approval/approvals" -Method Get -Headers $agHeaders

$scheduleBody = @{ fieldId = $field.id; scheduledAt = "2026-10-06T06:00:00Z"; durationMinutes = 45; notes = "Manual schedule."; status = 1 } | ConvertTo-Json
$schedule = Invoke-RestMethod -Uri "http://localhost:5000/api/task-approval/schedules" -Method Post -ContentType "application/json" -Body $scheduleBody -Headers $fieldHeaders
$scheduleApproval = Invoke-RestMethod -Uri "http://localhost:5000/api/task-approval/schedules/$($schedule.id)/approve" -Method Post -ContentType "application/json" -Body $approvalBody -Headers $agHeaders

Write-Output "FIELD_LOGIN=$($fieldLogin.user.email)"
Write-Output "AG_LOGIN=$($agLogin.user.email)"
Write-Output "TASK_STATUS_INITIAL=$($task.status)"
Write-Output "TASK_APPROVAL_DECISION=$($approval.decision)"
Write-Output "APPROVAL_COUNT=$($approvals.totalCount)"
Write-Output "SCHEDULE_STATUS_INITIAL=$($schedule.status)"
Write-Output "SCHEDULE_APPROVAL_DECISION=$($scheduleApproval.decision)"
