[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5087/api')

$ErrorActionPreference = 'Stop'
$login = Invoke-RestMethod "$($ApiBaseUrl.TrimEnd('/'))/auth/login" -Method Post -ContentType 'application/json' -Body (@{ email = 'agofficer@agriassist.local'; password = 'Agri@2026' } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$health = Invoke-WebRequest "$($ApiBaseUrl.TrimEnd('/').Replace('/api',''))/health" -UseBasicParsing
$workflow = Invoke-RestMethod "$ApiBaseUrl/task-approval/workflows?pageSize=50" -Headers $headers
$tasks = Invoke-RestMethod "$ApiBaseUrl/task-approval/tasks?pageSize=50" -Headers $headers
$schedules = Invoke-RestMethod "$ApiBaseUrl/task-approval/schedules?pageSize=50" -Headers $headers
$approvals = Invoke-RestMethod "$ApiBaseUrl/task-approval/approvals?pageSize=50" -Headers $headers

try { Invoke-RestMethod "$ApiBaseUrl/task-approval/workflows?pageSize=1" | Out-Null; throw 'Unauthenticated request unexpectedly succeeded.' }
catch { if (-not $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 401) { throw } }

Write-Output "Member 4 API smoke passed: health=$($health.StatusCode), workflows=$($workflow.items.Count), tasks=$($tasks.items.Count), schedules=$($schedules.items.Count), approvals=$($approvals.items.Count), unauthenticated=401"
