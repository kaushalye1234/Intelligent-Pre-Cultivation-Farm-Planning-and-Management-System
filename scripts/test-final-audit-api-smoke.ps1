$ErrorActionPreference = "Stop"

$apiRoot = "http://localhost:5000"
$apiBase = "$apiRoot/api"
$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 8)

function Invoke-Json($Method, $Path, $Token, $Body = $null, $ExpectedStatus = 200) {
    $headers = @{}
    if ($Token) {
        $headers.Authorization = "Bearer $Token"
    }

    $params = @{
        Uri = "$apiBase$Path"
        Method = $Method
        Headers = $headers
        ContentType = "application/json"
        TimeoutSec = 15
    }

    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 8)
    }

    try {
        $result = Invoke-RestMethod @params
        if ($ExpectedStatus -lt 200 -or $ExpectedStatus -ge 300) {
            throw "Expected HTTP $ExpectedStatus for $Method $Path but request succeeded."
        }
        return $result
    } catch [System.Net.WebException] {
        $status = [int]$_.Exception.Response.StatusCode
        if ($status -ne $ExpectedStatus) {
            throw "Expected HTTP $ExpectedStatus for $Method $Path but got $status."
        }
        return $null
    }
}

function Login($Email, $Password) {
    (Invoke-Json Post "/auth/login" $null @{ email = $Email; password = $Password }).accessToken
}

$health = Invoke-WebRequest -Uri "$apiRoot/health" -UseBasicParsing -TimeoutSec 10
if ($health.StatusCode -ne 200 -or $health.Content -notmatch "Healthy") {
    throw "Health check failed."
}

$swagger = Invoke-WebRequest -Uri "$apiRoot/swagger/v1/swagger.json" -UseBasicParsing -TimeoutSec 10
if ($swagger.StatusCode -ne 200 -or $swagger.Content -notmatch "Bearer") {
    throw "Swagger JWT metadata check failed."
}

$adminToken = Login "admin@agriassist.local" "Admin@2026"
$farmerToken = Login "farmer@agriassist.local" "Farmer@2026"
$fieldToken = Login "fieldofficer@agriassist.local" "Field@2026"
$resourceToken = Login "resourceofficer@agriassist.local" "Resource@2026"
$agToken = Login "agofficer@agriassist.local" "Agri@2026"

$profile = Invoke-Json Get "/auth/profile" $adminToken
Invoke-Json Get "/auth/profile" "not-a-valid-token" $null 401 | Out-Null
Invoke-Json Get "/users" $farmerToken $null 403 | Out-Null
$users = Invoke-Json Get "/users?pageSize=20" $adminToken

$newUserEmail = "audit.$suffix@example.test"
$registered = Invoke-Json Post "/auth/register" $null @{
    fullName = "Audit User $suffix"
    email = $newUserEmail
    password = "Audit@2026"
    role = 1
}
Invoke-Json Get "/users/$($registered.user.id)" $adminToken | Out-Null
Invoke-Json Patch "/users/$($registered.user.id)/role" $adminToken @{ role = 2 } | Out-Null
Invoke-Json Patch "/users/$($registered.user.id)/active" $adminToken @{ isActive = $false } | Out-Null
Invoke-Json Patch "/users/$($registered.user.id)/active" $adminToken @{ isActive = $true } | Out-Null

$farm = Invoke-Json Post "/crop-planning/farms" $farmerToken @{
    name = "Audit Farm $suffix"
    location = "Audit Location"
    totalArea = 10
    ownerUserId = $null
}
$field = Invoke-Json Post "/crop-planning/fields" $farmerToken @{
    farmId = $farm.id
    name = "Audit Field $suffix"
    area = 4
    soilType = "Loam"
    isActive = $true
}
Invoke-Json Post "/crop-planning/fields" $farmerToken @{
    farmId = $farm.id
    name = "Too Large Field $suffix"
    area = 20
    soilType = "Loam"
    isActive = $true
} 400 | Out-Null

$cropTypes = Invoke-Json Get "/crop-planning/crop-types?pageSize=1" $adminToken
$cropTypeId = $cropTypes.items[0].id
$plan = Invoke-Json Post "/crop-planning/requests/preliminary" $farmerToken @{
    farmId = $farm.id
    fieldId = $field.id
    cropTypeId = $cropTypeId
    preferredStartDate = "2026-10-01"
    preferredEndDate = "2026-12-01"
    budget = 1000
    objective = "Audit plan"
}
Invoke-Json Post "/crop-planning/requests/preliminary" $farmerToken @{
    farmId = $farm.id
    fieldId = $field.id
    cropTypeId = $cropTypeId
    preferredStartDate = "2026-10-01"
    preferredEndDate = "2026-12-01"
    budget = 1000
    objective = "Duplicate audit plan"
} 409 | Out-Null
Invoke-Json Get "/crop-planning/requests/$($plan.id)/history" $farmerToken | Out-Null

$inspection = Invoke-Json Post "/inspections" $fieldToken @{
    fieldId = $field.id
    scheduledAt = (Get-Date).ToUniversalTime().AddHours(1).ToString("o")
    status = 1
    summary = "Audit inspection"
}
Invoke-Json Post "/inspections/observations" $fieldToken @{
    fieldInspectionId = $inspection.id
    observationType = "Canopy"
    notes = "Audit observation"
} | Out-Null
$issue = Invoke-Json Post "/inspections/issues" $fieldToken @{
    fieldInspectionId = $inspection.id
    title = "Audit issue"
    description = "Serious crop issue"
    severity = 3
    status = 1
}
$escalated = Invoke-Json Post "/inspections/issues/$($issue.id)/escalate" $fieldToken

$category = Invoke-Json Post "/resources/categories" $resourceToken @{
    name = "Audit Category $suffix"
    description = "Audit"
}
$resource = Invoke-Json Post "/resources" $resourceToken @{
    resourceCategoryId = $category.id
    supplierId = $null
    name = "Audit Resource $suffix"
    unit = "kg"
    isActive = $true
}
$stock = Invoke-Json Post "/resources/stocks" $resourceToken @{
    resourceId = $resource.id
    quantityOnHand = 50
    lowStockThreshold = 5
}
$reservation = Invoke-Json Post "/resources/reservations" $resourceToken @{
    inventoryStockId = $stock.id
    quantity = 20
    purpose = "Audit reserve"
}
Invoke-Json Post "/resources/reservations" $resourceToken @{
    inventoryStockId = $stock.id
    quantity = 60
    purpose = "Audit over reserve"
} 409 | Out-Null
Invoke-Json Post "/resources/reservations/$($reservation.id)/release" $resourceToken | Out-Null
Invoke-Json Post "/resources/reservations/$($reservation.id)/release" $resourceToken $null 409 | Out-Null
$reservationToCancel = Invoke-Json Post "/resources/reservations" $resourceToken @{
    inventoryStockId = $stock.id
    quantity = 5
    purpose = "Audit cancel"
}
Invoke-Json Post "/resources/reservations/$($reservationToCancel.id)/cancel" $resourceToken | Out-Null
Invoke-Json Get "/resources/stocks/$($stock.id)/history" $resourceToken | Out-Null

$task = Invoke-Json Post "/task-approval/tasks" $fieldToken @{
    farmId = $farm.id
    title = "Audit task"
    description = "Audit"
    dueAt = (Get-Date).ToUniversalTime().AddDays(1).ToString("o")
    assignedToUserId = $registered.user.id
    status = 2
}
Invoke-Json Post "/task-approval/tasks/$($task.id)/approve" $farmerToken @{ comment = "No"; agentWorkflowId = $null } 403 | Out-Null
Invoke-Json Post "/task-approval/tasks/$($task.id)/approve" $agToken @{ comment = "Approved"; agentWorkflowId = $null } | Out-Null
Invoke-Json Post "/task-approval/tasks/$($task.id)/approve" $agToken @{ comment = "Again"; agentWorkflowId = $null } 409 | Out-Null

$schedule = Invoke-Json Post "/task-approval/schedules" $fieldToken @{
    fieldId = $field.id
    scheduledAt = (Get-Date).ToUniversalTime().AddDays(1).ToString("o")
    durationMinutes = 30
    notes = "Audit schedule"
    status = 1
}
Invoke-Json Post "/task-approval/schedules/$($schedule.id)/reject" $agToken @{ comment = "Rejected"; agentWorkflowId = $null } | Out-Null
Invoke-Json Get "/task-approval/approvals" $agToken | Out-Null
Invoke-Json Get "/dashboard/summary" $adminToken | Out-Null

[PSCustomObject]@{
    Health = "PASS"
    Swagger = "PASS"
    AuthProfile = $profile.email
    UsersReturned = $users.totalCount
    CropPlanStatus = $plan.status
    EscalatedIssueStatus = $escalated.status
    ReservationStatus = "release/cancel/over-reserve verified"
    TaskApproval = "farmer denied, approval succeeds, repeat blocked"
    ScheduleReject = "PASS"
} | ConvertTo-Json
