[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][guid]$WorkflowId,
    [Parameter(Mandatory = $true)][int]$CandidateRevision,
    [Parameter(Mandatory = $true)][int]$ExpectedWorkflowVersion,
    [Parameter(Mandatory = $true)][string]$BearerToken,
    [string]$ApiBaseUrl = 'http://127.0.0.1:5087/api'
)

$ErrorActionPreference = 'Stop'
$uri = "$($ApiBaseUrl.TrimEnd('/'))/task-approval/workflows/$WorkflowId/approve"
$headers = @{ Authorization = "Bearer $BearerToken"; 'Content-Type' = 'application/json' }

function Send-Decision([string]$key, [string]$targetUri, [hashtable]$targetHeaders, [int]$revision, [int]$version) {
    $body = @{ candidateRevision = $revision; expectedWorkflowVersion = $version; idempotencyKey = $key; comment = "Concurrent approval probe $key" } | ConvertTo-Json
    try {
        $response = Invoke-WebRequest -Uri $targetUri -Method Post -Headers $targetHeaders -Body $body -UseBasicParsing
        return [pscustomobject]@{ Key = $key; Status = [int]$response.StatusCode; Body = $response.Content }
    } catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        return [pscustomobject]@{ Key = $key; Status = $status; Body = $_.Exception.Message }
    }
}

$jobs = @(
    Start-Job -ScriptBlock { param($key, $targetUri, $targetHeaders, $revision, $version); $body=@{candidateRevision=$revision;expectedWorkflowVersion=$version;idempotencyKey=$key;comment="Concurrent approval probe $key"}|ConvertTo-Json; try{$r=Invoke-WebRequest -Uri $targetUri -Method Post -Headers $targetHeaders -Body $body -UseBasicParsing; [pscustomobject]@{Key=$key;Status=[int]$r.StatusCode;Body=$r.Content}}catch{$s=if($_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{0}; [pscustomobject]@{Key=$key;Status=$s;Body=$_.Exception.Message}} } -ArgumentList 'concurrent-a', $uri, $headers, $CandidateRevision, $ExpectedWorkflowVersion,
    Start-Job -ScriptBlock { param($key, $targetUri, $targetHeaders, $revision, $version); $body=@{candidateRevision=$revision;expectedWorkflowVersion=$version;idempotencyKey=$key;comment="Concurrent approval probe $key"}|ConvertTo-Json; try{$r=Invoke-WebRequest -Uri $targetUri -Method Post -Headers $targetHeaders -Body $body -UseBasicParsing; [pscustomobject]@{Key=$key;Status=[int]$r.StatusCode;Body=$r.Content}}catch{$s=if($_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{0}; [pscustomobject]@{Key=$key;Status=$s;Body=$_.Exception.Message}} } -ArgumentList 'concurrent-b', $uri, $headers, $CandidateRevision, $ExpectedWorkflowVersion
)
Wait-Job $jobs | Out-Null
$results = Receive-Job $jobs
Remove-Job $jobs -Force
$results | Format-Table -AutoSize

$successes = @($results | Where-Object Status -ge 200 | Where-Object Status -lt 300).Count
$conflicts = @($results | Where-Object Status -eq 409).Count
if ($successes -ne 1 -or $conflicts -ne 1) {
    throw "Expected one successful decision and one HTTP 409 conflict; got successes=$successes conflicts=$conflicts."
}
Write-Output 'Concurrent approval probe passed: one decision succeeded and the competing decision conflicted.'
