$ErrorActionPreference = Stop

function Login($email, $password) {
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    Invoke-RestMethod -Uri http://localhost:5000/api/auth/login -Method Post -ContentType application/json -Body $body
}

$farmerLogin = Login farmer@agriassist.local Farmer@2026
$fieldLogin = Login fieldofficer@agriassist.local Field@2026
$farmerHeaders = @{ Authorization = Bearer $($farmerLogin.accessToken) }
$fieldHeaders = @{ Authorization = Bearer $($fieldLogin.accessToken) }

$farmBody = @{ name = Inspection Demo Farm $([Guid]::NewGuid()); location = Demo Location; totalArea = 9; ownerUserId = $null } | ConvertTo-Json
$farm = Invoke-RestMethod -Uri http://localhost:5000/api/crop-planning/farms -Method Post -ContentType application/json -Body $farmBody -Headers $farmerHeaders
$fieldBody = @{ farmId = $farm.id; name = Inspection Field; area = 3; soilType = Loam; isActive = $true } | ConvertTo-Json
$field = Invoke-RestMethod -Uri http://localhost:5000/api/crop-planning/fields -Method Post -ContentType application/json -Body $fieldBody -Headers $farmerHeaders

$inspectionBody = @{ fieldId = $field.id; scheduledAt = 2026-10-02T08:00:00Z; status = 1; summary = Scheduled field inspection. } | ConvertTo-Json
$inspection = Invoke-RestMethod -Uri http://localhost:5000/api/inspections -Method Post -ContentType application/json -Body $inspectionBody -Headers $fieldHeaders

$observationBody = @{ fieldInspectionId = $inspection.id; observationType = Crop condition; notes = Leaves show visible stress in sample area. } | ConvertTo-Json
$observation = Invoke-RestMethod -Uri http://localhost:5000/api/inspections/observations -Method Post -ContentType application/json -Body $observationBody -Headers $fieldHeaders

$issueBody = @{ fieldInspectionId = $inspection.id; title = Serious crop issue; description = High severity issue for escalation workflow.; severity = 3; status = 1 } | ConvertTo-Json
$issue = Invoke-RestMethod -Uri http://localhost:5000/api/inspections/issues -Method Post -ContentType application/json -Body $issueBody -Headers $fieldHeaders
$escalated = Invoke-RestMethod -Uri http://localhost:5000/api/inspections/issues/$($issue.id)/escalate -Method Post -Headers $fieldHeaders

$temp = New-TemporaryFile
Set-Content -LiteralPath $temp -Value not an image -Encoding ASCII
$invalidUploadStatus = & curl.exe -s -o NUL -w %{http_code} -H Authorization: Bearer $($fieldLogin.accessToken) -F file=@$temp;type=text/plain http://localhost:5000/api/inspections/$($inspection.id)/images
Remove-Item -LiteralPath $temp -Force

Write-Output FIELD_OFFICER_LOGIN=$($fieldLogin.user.email)
Write-Output INSPECTION_STATUS=$($inspection.status)
Write-Output OBSERVATION_TYPE=$($observation.observationType)
Write-Output ISSUE_SEVERITY=$($issue.severity)
Write-Output ESCALATED_STATUS=$($escalated.status)
Write-Output INVALID_UPLOAD_STATUS=$invalidUploadStatus
