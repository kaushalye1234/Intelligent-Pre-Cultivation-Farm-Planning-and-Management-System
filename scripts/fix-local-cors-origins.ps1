$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$path = Join-Path $PSScriptRoot "..\backend\AgriAssist.Api\Program.cs"
$content = [System.IO.File]::ReadAllText($path)

$old = @'
var reactUrl = builder.Configuration["App:ReactUrl"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApps", policy =>
    {
        policy.WithOrigins(reactUrl)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
'@

$new = @'
var configuredReactUrl = builder.Configuration["App:ReactUrl"] ?? "http://localhost:5173";
var allowedClientOrigins = new[] { configuredReactUrl, "http://localhost:5173", "http://127.0.0.1:5173" }
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApps", policy =>
    {
        policy.WithOrigins(allowedClientOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
'@

if (-not $content.Contains($old)) {
    throw "Expected CORS block not found."
}

$content = $content.Replace($old, $new)
[System.IO.File]::WriteAllText($path, $content, $encoding)
