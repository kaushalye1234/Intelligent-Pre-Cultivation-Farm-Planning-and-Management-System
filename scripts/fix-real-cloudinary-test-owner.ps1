$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$path = Join-Path $PSScriptRoot "test-real-cloudinary-upload.ps1"
$content = [System.IO.File]::ReadAllText($path)

$content = $content.Replace(
    '$admin = Login "admin@agriassist.local" "Admin@2026"' + "`r`n" + '$fieldOfficer = Login "fieldofficer@agriassist.local" "Field@2026"',
    '$admin = Login "admin@agriassist.local" "Admin@2026"' + "`r`n" + '$farmer = Login "farmer@agriassist.local" "Farmer@2026"' + "`r`n" + '$fieldOfficer = Login "fieldofficer@agriassist.local" "Field@2026"')
$content = $content.Replace(
    '$adminHeaders = @{ Authorization = "Bearer $($admin.accessToken)" }' + "`r`n" + '$fieldHeaders = @{ Authorization = "Bearer $($fieldOfficer.accessToken)" }',
    '$adminHeaders = @{ Authorization = "Bearer $($admin.accessToken)" }' + "`r`n" + '$farmerHeaders = @{ Authorization = "Bearer $($farmer.accessToken)" }' + "`r`n" + '$fieldHeaders = @{ Authorization = "Bearer $($fieldOfficer.accessToken)" }')
$content = $content.Replace(
    '$farm = Invoke-RestMethod -Uri "$apiBase/crop-planning/farms" -Method Post -Headers $adminHeaders',
    '$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 8)' + "`r`n" + '$farm = Invoke-RestMethod -Uri "$apiBase/crop-planning/farms" -Method Post -Headers $farmerHeaders')
$content = $content.Replace(
    'name = "Cloudinary Verification Farm"',
    'name = "Cloudinary Verification Farm $suffix"')
$content = $content.Replace(
    '$field = Invoke-RestMethod -Uri "$apiBase/crop-planning/fields" -Method Post -Headers $adminHeaders',
    '$field = Invoke-RestMethod -Uri "$apiBase/crop-planning/fields" -Method Post -Headers $farmerHeaders')
$content = $content.Replace(
    'name = "Upload Test Field"',
    'name = "Upload Test Field $suffix"')

[System.IO.File]::WriteAllText($path, $content, $encoding)
