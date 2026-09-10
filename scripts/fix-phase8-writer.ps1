$ErrorActionPreference = "Stop"

$path = Join-Path $PSScriptRoot "write-phase8-react-app.ps1"
$content = Get-Content $path -Raw
$content = $content.Replace('$packageJson.scripts.test = "vitest --run"', '$packageJson.scripts | Add-Member -NotePropertyName "test" -NotePropertyValue "vitest --run" -Force')
$content = $content.Replace('$packageJson.scripts."test:watch" = "vitest"', '$packageJson.scripts | Add-Member -NotePropertyName "test:watch" -NotePropertyValue "vitest" -Force')
Set-Content -Path $path -Value $content -Encoding UTF8
