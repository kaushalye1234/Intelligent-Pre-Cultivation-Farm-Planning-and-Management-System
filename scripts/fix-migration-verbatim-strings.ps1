$path = "backend/AgriAssist.Api/Migrations/20260908130024_ApplyFullConstraints.cs"
$content = Get-Content -LiteralPath $path -Raw
$content = $content.Replace('migrationBuilder.Sql("ALTER TABLE', 'migrationBuilder.Sql(@"ALTER TABLE')
Set-Content -LiteralPath $path -Value $content -Encoding ASCII
