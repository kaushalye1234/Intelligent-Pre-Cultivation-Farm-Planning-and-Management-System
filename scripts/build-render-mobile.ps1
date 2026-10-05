param(
    [ValidateSet('apk', 'appbundle', 'web')]
    [string]$Target = 'apk',
    [string]$ApiBaseUrl = 'https://agriassist-api-sl97.onrender.com/api'
)

$ErrorActionPreference = 'Stop'
$apiUri = $null
if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$apiUri) -or
    $apiUri.Scheme -ne 'https' -or
    $apiUri.AbsolutePath.TrimEnd('/') -ne '/api' -or
    $apiUri.Query -or $apiUri.Fragment -or $apiUri.UserInfo) {
    throw 'ApiBaseUrl must be an HTTPS API URL ending in /api, without credentials, a query, or a fragment.'
}

$flutter = Get-Command flutter -ErrorAction Stop
$mobilePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'mobile/flutter_app'
Push-Location $mobilePath
try {
    & $flutter.Source pub get
    if ($LASTEXITCODE -ne 0) { throw 'Flutter dependency resolution failed.' }

    & $flutter.Source build $Target --release --no-pub "--dart-define=AGRIASSIST_API_BASE_URL=$($ApiBaseUrl.TrimEnd('/'))"
    if ($LASTEXITCODE -ne 0) { throw "Flutter $Target build failed." }
    Write-Host "Built $Target using $ApiBaseUrl"
}
finally {
    Pop-Location
}
