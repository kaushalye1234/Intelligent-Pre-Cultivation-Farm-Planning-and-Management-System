$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$path = Join-Path $PSScriptRoot "test-final-audit-api-smoke.ps1"
$content = [System.IO.File]::ReadAllText($path)
$content = $content.Replace(
    'Invoke-Json Patch "/users/$($registered.user.id)/active" $adminToken @{ isActive = $false } | Out-Null',
    'Invoke-Json Patch "/users/$($registered.user.id)/active" $adminToken @{ isActive = $false } | Out-Null
Invoke-Json Patch "/users/$($registered.user.id)/active" $adminToken @{ isActive = $true } | Out-Null')
[System.IO.File]::WriteAllText($path, $content, $encoding)
