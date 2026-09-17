[CmdletBinding()]
param(
    [string]$Container = 'agriassist-postgres-phase2',
    [string]$Database = 'agriassist',
    [string]$User = 'postgres',
    [string]$Password = 'AgriAssistPhase2Local!'
)

$ErrorActionPreference = 'Stop'

function Invoke-Psql([string]$Sql) {
    $result = docker exec -e "PGPASSWORD=$Password" $Container psql -X -v ON_ERROR_STOP=1 -U $User -d $Database -At -c $Sql
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL command failed: $Sql" }
    return ($result -join "`n").Trim()
}

if (-not (docker ps --format '{{.Names}}' | Select-String -SimpleMatch $Container)) {
    throw "Container '$Container' is not running. Start it before running this check."
}

$migrationCount = [int](Invoke-Psql 'select count(*) from "__EFMigrationsHistory";')
if ($migrationCount -lt 5) { throw "Expected at least 5 applied migrations; found $migrationCount." }

$requiredTables = @('FarmTasks', 'IrrigationSchedules', 'ApprovalDecisions', 'AgentWorkflows', 'ResourceReservations')
foreach ($table in $requiredTables) {
    $exists = Invoke-Psql "select count(*) from information_schema.tables where table_schema='public' and table_name='$table';"
    if ([int]$exists -ne 1) { throw "Required table '$table' is missing." }
}

$rollbackMarker = "member4_rollback_$([guid]::NewGuid().ToString('N'))"
$rollbackOutput = Invoke-Psql "begin; create temporary table member4_rollback_probe (marker text); insert into member4_rollback_probe values ('$rollbackMarker'); savepoint member4_probe; update member4_rollback_probe set marker='changed'; rollback to savepoint member4_probe; select count(*) from member4_rollback_probe where marker='$rollbackMarker'; rollback;"
$rollbackCount = [int](($rollbackOutput -split "`n" | Where-Object { $_ -match '^\d+$' } | Select-Object -Last 1))
if ($rollbackCount -ne 1) { throw 'Rollback-to-savepoint probe did not restore the original row.' }

$lockResult = Invoke-Psql 'create temporary table member4_lock_probe (id integer primary key, value integer); insert into member4_lock_probe values (1, 0); select value from member4_lock_probe where id=1 for update;'
if ($lockResult -notmatch '0') { throw 'Row-lock probe did not return the expected row.' }

Write-Output "PostgreSQL Member 4 checks passed: migrations=$migrationCount, requiredTables=$($requiredTables.Count), rollback=passed, rowLock=passed"
