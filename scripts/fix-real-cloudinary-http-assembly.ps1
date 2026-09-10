$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$path = Join-Path $PSScriptRoot "test-real-cloudinary-upload.ps1"
$content = [System.IO.File]::ReadAllText($path)
if ($content -notmatch "Add-Type -AssemblyName System.Net.Http") {
    $content = $content.Replace('$ErrorActionPreference = "Stop"', '$ErrorActionPreference = "Stop"' + "`r`nAdd-Type -AssemblyName System.Net.Http")
}
[System.IO.File]::WriteAllText($path, $content, $encoding)
