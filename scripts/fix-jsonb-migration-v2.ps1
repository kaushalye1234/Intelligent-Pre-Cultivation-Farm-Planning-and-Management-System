$path = "backend/AgriAssist.Api/Migrations/20260908130024_ApplyFullConstraints.cs"
$content = Get-Content -LiteralPath $path -Raw

function Replace-JsonAlter($content, $table, $column) {
    $pattern = '(?s)\s{12}migrationBuilder\.AlterColumn<string>\(\s*name: "' + [regex]::Escape($column) + '",\s*table: "' + [regex]::Escape($table) + '",\s*type: "jsonb",\s*nullable: false,\s*oldClrType: typeof\(string\),\s*oldType: "text"\);'
    $replacement = "`r`n            migrationBuilder.Sql(""ALTER TABLE """"$table"""" ALTER COLUMN """"$column"""" TYPE jsonb USING """"$column""""::jsonb;"");"
    return [regex]::Replace($content, $pattern, $replacement, 1)
}

$content = Replace-JsonAlter $content "AgentValidationResults" "ErrorsJson"
$content = Replace-JsonAlter $content "AgentToolExecutions" "OutputJson"
$content = Replace-JsonAlter $content "AgentToolExecutions" "InputJson"
$content = Replace-JsonAlter $content "AgentSteps" "OutputJson"
$content = Replace-JsonAlter $content "AgentSteps" "InputJson"

Set-Content -LiteralPath $path -Value $content -Encoding ASCII
