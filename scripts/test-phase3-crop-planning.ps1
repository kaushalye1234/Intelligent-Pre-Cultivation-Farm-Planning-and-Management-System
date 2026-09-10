$ErrorActionPreference = "Stop"

function Login($email, $password) {
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -ContentType "application/json" -Body $body
}

$adminLogin = Login "admin@agriassist.local" "Admin@2026"
$farmerLogin = Login "farmer@agriassist.local" "Farmer@2026"

$adminHeaders = @{ Authorization = "Bearer $($adminLogin.accessToken)" }
$farmerHeaders = @{ Authorization = "Bearer $($farmerLogin.accessToken)" }

$cropTypes = Invoke-RestMethod -Uri "http://localhost:5000/api/crop-planning/crop-types?page=1&pageSize=10" -Method Get -Headers $adminHeaders
$cropTypeId = $cropTypes.items[0].id

$farmBody = @{ name = "North Demo Farm"; location = "Demo Location"; totalArea = 10; ownerUserId = $null } | ConvertTo-Json
$farm = Invoke-RestMethod -Uri "http://localhost:5000/api/crop-planning/farms" -Method Post -ContentType "application/json" -Body $farmBody -Headers $farmerHeaders

$fieldBody = @{ farmId = $farm.id; name = "Field A"; area = 4; soilType = "Loam"; isActive = $true } | ConvertTo-Json
$field = Invoke-RestMethod -Uri "http://localhost:5000/api/crop-planning/fields" -Method Post -ContentType "application/json" -Body $fieldBody -Headers $farmerHeaders

$requestBody = @{
    farmId = $farm.id
    fieldId = $field.id
    cropTypeId = $cropTypeId
    preferredStartDate = "2026-10-01"
    preferredEndDate = "2027-01-15"
    budget = 5000
    objective = "Create a basic seasonal crop plan request."
} | ConvertTo-Json

$plan = Invoke-RestMethod -Uri "http://localhost:5000/api/crop-planning/requests/preliminary" -Method Post -ContentType "application/json" -Body $requestBody -Headers $farmerHeaders
$history = Invoke-RestMethod -Uri "http://localhost:5000/api/crop-planning/requests/$($plan.id)/history" -Method Get -Headers $farmerHeaders

$duplicateStatus = "not-tested"
try {
    Invoke-RestMethod -Uri "http://localhost:5000/api/crop-planning/requests/preliminary" -Method Post -ContentType "application/json" -Body $requestBody -Headers $farmerHeaders | Out-Null
    $duplicateStatus = "unexpected-success"
} catch {
    $duplicateStatus = $_.Exception.Response.StatusCode.value__
}

Write-Output "ADMIN_LOGIN=$($adminLogin.user.email)"
Write-Output "FARMER_LOGIN=$($farmerLogin.user.email)"
Write-Output "CROP_TYPES=$($cropTypes.totalCount)"
Write-Output "FARM_CREATED=$($farm.name)"
Write-Output "FIELD_CREATED=$($field.name)"
Write-Output "PLAN_STATUS=$($plan.status)"
Write-Output "HISTORY_COUNT=$($history.Count)"
Write-Output "DUPLICATE_STATUS=$duplicateStatus"
