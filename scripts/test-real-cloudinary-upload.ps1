$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$apiBase = "http://localhost:5000/api"
$tmpPng = Join-Path $env:TEMP "agriassist-upload-test.png"

$pngBytes = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=")
[System.IO.File]::WriteAllBytes($tmpPng, $pngBytes)

function Login($Email, $Password) {
    Invoke-RestMethod -Uri "$apiBase/auth/login" -Method Post -ContentType "application/json" -Body (@{
        email = $Email
        password = $Password
    } | ConvertTo-Json)
}

$farmer = Login "farmer@agriassist.local" "Farmer@2026"
$fieldOfficer = Login "fieldofficer@agriassist.local" "Field@2026"

$farmerHeaders = @{ Authorization = "Bearer $($farmer.accessToken)" }
$fieldHeaders = @{ Authorization = "Bearer $($fieldOfficer.accessToken)" }

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$farm = Invoke-RestMethod -Uri "$apiBase/crop-planning/farms" -Method Post -Headers $farmerHeaders -ContentType "application/json" -Body (@{
    name = "Cloudinary Verification Farm $suffix"
    location = "Upload Test"
    totalArea = 1.25
    ownerUserId = $null
} | ConvertTo-Json)

$field = Invoke-RestMethod -Uri "$apiBase/crop-planning/fields" -Method Post -Headers $farmerHeaders -ContentType "application/json" -Body (@{
    farmId = $farm.id
    name = "Upload Test Field $suffix"
    area = 1.0
    soilType = "Loam"
    isActive = $true
} | ConvertTo-Json)

$inspection = Invoke-RestMethod -Uri "$apiBase/inspections" -Method Post -Headers $fieldHeaders -ContentType "application/json" -Body (@{
    fieldId = $field.id
    scheduledAt = (Get-Date).ToUniversalTime().ToString("o")
    status = 1
    summary = "Cloudinary upload verification"
} | ConvertTo-Json)

$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $fieldOfficer.accessToken)
$form = [System.Net.Http.MultipartFormDataContent]::new()
$bytes = [System.IO.File]::ReadAllBytes($tmpPng)
$fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
$fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("image/png")
$form.Add($fileContent, "file", "agriassist-upload-test.png")
$response = $httpClient.PostAsync("$apiBase/inspections/$($inspection.id)/images", $form).GetAwaiter().GetResult()
$body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

$httpClient.Dispose()
$form.Dispose()
$fileContent.Dispose()

if (-not $response.IsSuccessStatusCode) {
    throw "Upload failed with HTTP $([int]$response.StatusCode): $body"
}

$upload = $body | ConvertFrom-Json
[PSCustomObject]@{
    InspectionId = $inspection.id
    UploadId = $upload.id
    ContentType = $upload.contentType
    SizeBytes = $upload.sizeBytes
    HasSecureUrl = -not [string]::IsNullOrWhiteSpace($upload.url)
    PublicIdPrefix = if ($upload.publicId.Length -gt 23) { $upload.publicId.Substring(0, 23) } else { $upload.publicId }
} | ConvertTo-Json