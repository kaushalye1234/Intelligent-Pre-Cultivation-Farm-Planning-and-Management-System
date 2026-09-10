$ErrorActionPreference = "Stop"

$loginBody = @{ email = "admin@agriassist.local"; password = "Admin@2026" } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$summary = Invoke-RestMethod -Uri "http://localhost:5000/api/dashboard/summary" -Method Get -Headers $headers

Write-Output "DASHBOARD_USERS_BY_ROLE=$($summary.usersByRole.Count)"
Write-Output "DASHBOARD_ACTIVE_FARMS=$($summary.activeFarms)"
Write-Output "DASHBOARD_ACTIVE_CROP_PLANS=$($summary.activeCropPlans)"
Write-Output "DASHBOARD_OPEN_CROP_ISSUES=$($summary.openCropIssues)"
Write-Output "DASHBOARD_LOW_STOCK_RESOURCES=$($summary.lowStockResources)"
Write-Output "DASHBOARD_PENDING_TASKS=$($summary.pendingTasks)"
Write-Output "DASHBOARD_PENDING_APPROVALS=$($summary.pendingApprovals)"
