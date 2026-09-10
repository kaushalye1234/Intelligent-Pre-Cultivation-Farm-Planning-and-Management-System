$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$root = Join-Path $PSScriptRoot ".."

$paths = @(
    (Join-Path $root "scripts\write-phase2-config-fix.ps1")
)

foreach ($path in $paths) {
    if (-not (Test-Path $path)) {
        continue
    }

    $content = [System.IO.File]::ReadAllText($path)
    $content = [Regex]::Replace($content, 'ConnectionStrings__DefaultConnection=.*', 'ConnectionStrings__DefaultConnection=')
    [System.IO.File]::WriteAllText($path, $content, $encoding)
}
