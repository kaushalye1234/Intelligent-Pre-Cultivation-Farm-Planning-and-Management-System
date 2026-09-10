$programPath = "backend/AgriAssist.Api/Program.cs"
$program = Get-Content -LiteralPath $programPath -Raw
$program = $program -replace 'builder.Services.AddScoped<IUserService, UserService>\(\);', "builder.Services.AddScoped<IUserService, UserService>();`r`nbuilder.Services.AddScoped<IDashboardService, DashboardService>();"
Set-Content -LiteralPath $programPath -Value $program -Encoding ASCII
