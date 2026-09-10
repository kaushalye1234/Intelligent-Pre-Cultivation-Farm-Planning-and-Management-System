$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$logDir = Join-Path $root "artifacts\dev-servers"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Test-PortOpen($Port) {
    $connection = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    return $null -ne $connection
}

function Find-Port($Preferred) {
    $port = $Preferred
    while (Test-PortOpen $port) {
        $port++
    }
    return $port
}

$apiPort = Find-Port 5000
$reactPort = Find-Port 5173

$apiOut = Join-Path $logDir "api.out.log"
$apiErr = Join-Path $logDir "api.err.log"
$reactOut = Join-Path $logDir "react.out.log"
$reactErr = Join-Path $logDir "react.err.log"

$apiArgs = @("run", "--project", "backend\AgriAssist.Api\AgriAssist.Api.csproj", "--urls", "http://localhost:$apiPort")
Start-Process -WindowStyle Hidden -FilePath "dotnet" -ArgumentList $apiArgs -WorkingDirectory $root -RedirectStandardOutput $apiOut -RedirectStandardError $apiErr

$reactArgs = @("run", "dev", "--", "--host", "127.0.0.1", "--port", "$reactPort")
Start-Process -WindowStyle Hidden -FilePath "npm.cmd" -ArgumentList $reactArgs -WorkingDirectory (Join-Path $root "frontend\react-app") -RedirectStandardOutput $reactOut -RedirectStandardError $reactErr

$apiUrl = "http://localhost:$apiPort"
$reactUrl = "http://127.0.0.1:$reactPort"

$apiHealthy = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "$apiUrl/health" -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            $apiHealthy = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 1
    }
}

$reactHealthy = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $response = Invoke-WebRequest -Uri $reactUrl -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            $reactHealthy = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 1
    }
}

[PSCustomObject]@{
    ApiUrl = $apiUrl
    ApiHealth = $apiHealthy
    ReactUrl = $reactUrl
    ReactHealth = $reactHealthy
    LogDirectory = $logDir
} | ConvertTo-Json
