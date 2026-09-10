$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$path = Join-Path $PSScriptRoot "test-real-cloudinary-upload.ps1"

[System.IO.File]::WriteAllText($path, @'
$ErrorActionPreference = "Stop"

$apiBase = "http://localhost:5000/api"
$tmpPng = Join-Path $env:TEMP "agriassist-upload-test.png"

[byte[]]$pngBytes = @(
    0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,
    0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
    0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,
    0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
    0x89,0x00,0x00,0x00,0x0A,0x49,0x44,0x41,
    0x54,0x78,0x9C,0x63,0x00,0x01,0x00,0x00,
    0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,
    0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
    0x42,0x60,0x82
)
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
'@, $encoding)
