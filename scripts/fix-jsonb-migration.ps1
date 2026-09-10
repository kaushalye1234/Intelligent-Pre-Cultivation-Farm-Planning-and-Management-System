$path = "backend/AgriAssist.Api/Migrations/20260908130024_ApplyFullConstraints.cs"
$content = Get-Content -LiteralPath $path -Raw

$replacements = @{
    '            migrationBuilder.AlterColumn<string>(
                name: "ErrorsJson",
                table: "AgentValidationResults",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");' = '            migrationBuilder.Sql("ALTER TABLE ""AgentValidationResults"" ALTER COLUMN ""ErrorsJson"" TYPE jsonb USING ""ErrorsJson""::jsonb;");'

    '            migrationBuilder.AlterColumn<string>(
                name: "OutputJson",
                table: "AgentToolExecutions",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");' = '            migrationBuilder.Sql("ALTER TABLE ""AgentToolExecutions"" ALTER COLUMN ""OutputJson"" TYPE jsonb USING ""OutputJson""::jsonb;");'

    '            migrationBuilder.AlterColumn<string>(
                name: "InputJson",
                table: "AgentToolExecutions",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");' = '            migrationBuilder.Sql("ALTER TABLE ""AgentToolExecutions"" ALTER COLUMN ""InputJson"" TYPE jsonb USING ""InputJson""::jsonb;");'

    '            migrationBuilder.AlterColumn<string>(
                name: "OutputJson",
                table: "AgentSteps",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");' = '            migrationBuilder.Sql("ALTER TABLE ""AgentSteps"" ALTER COLUMN ""OutputJson"" TYPE jsonb USING ""OutputJson""::jsonb;");'

    '            migrationBuilder.AlterColumn<string>(
                name: "InputJson",
                table: "AgentSteps",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");' = '            migrationBuilder.Sql("ALTER TABLE ""AgentSteps"" ALTER COLUMN ""InputJson"" TYPE jsonb USING ""InputJson""::jsonb;");'
}

foreach ($entry in $replacements.GetEnumerator()) {
    $content = $content.Replace($entry.Key, $entry.Value)
}

Set-Content -LiteralPath $path -Value $content -Encoding ASCII
