$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$path = Join-Path $PSScriptRoot "test-real-cloudinary-upload.ps1"
$content = [System.IO.File]::ReadAllText($path)
$start = $content.IndexOf('[byte[]]$pngBytes = @(')
$end = $content.IndexOf('[System.IO.File]::WriteAllBytes($tmpPng, $pngBytes)')
if ($start -lt 0 -or $end -lt 0) {
    throw "PNG block not found."
}

$replacement = '$pngBytes = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=")' + "`r`n"
$content = $content.Substring(0, $start) + $replacement + $content.Substring($end)
[System.IO.File]::WriteAllText($path, $content, $encoding)
