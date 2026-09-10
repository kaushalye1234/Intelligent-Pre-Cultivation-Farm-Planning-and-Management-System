$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$root = Join-Path $PSScriptRoot "..\frontend\react-app"
$patterns = @("*.json", "*.ts", "*.tsx", "*.css", "*.html", "*.example")

foreach ($pattern in $patterns) {
    Get-ChildItem -Path $root -Filter $pattern -Recurse -File | ForEach-Object {
        if ($_.FullName -like "*\node_modules\*") {
            return
        }

        $content = [System.IO.File]::ReadAllText($_.FullName)
        [System.IO.File]::WriteAllText($_.FullName, $content, $encoding)
    }
}
