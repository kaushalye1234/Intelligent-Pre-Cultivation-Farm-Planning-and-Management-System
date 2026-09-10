$ErrorActionPreference = "Stop"

function Login($email, $password) {
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method Post -ContentType "application/json" -Body $body
}

$resourceLogin = Login "resourceofficer@agriassist.local" "Resource@2026"
$headers = @{ Authorization = "Bearer $($resourceLogin.accessToken)" }

$categoryBody = @{ name = "Seed"; description = "Development resource category." } | ConvertTo-Json
$category = Invoke-RestMethod -Uri "http://localhost:5000/api/resources/categories" -Method Post -ContentType "application/json" -Body $categoryBody -Headers $headers

$supplierBody = @{ name = "Demo Supplier"; contactEmail = "supplier@example.test"; phone = "0770000000" } | ConvertTo-Json
$supplier = Invoke-RestMethod -Uri "http://localhost:5000/api/resources/suppliers" -Method Post -ContentType "application/json" -Body $supplierBody -Headers $headers

$resourceBody = @{ resourceCategoryId = $category.id; supplierId = $supplier.id; name = "Demo Seed Pack"; unit = "kg"; isActive = $true } | ConvertTo-Json
$resource = Invoke-RestMethod -Uri "http://localhost:5000/api/resources" -Method Post -ContentType "application/json" -Body $resourceBody -Headers $headers

$stockBody = @{ resourceId = $resource.id; quantityOnHand = 10; lowStockThreshold = 3 } | ConvertTo-Json
$stock = Invoke-RestMethod -Uri "http://localhost:5000/api/resources/stocks" -Method Post -ContentType "application/json" -Body $stockBody -Headers $headers

$reservationBody = @{ inventoryStockId = $stock.id; quantity = 4; purpose = "Reserve for demo field." } | ConvertTo-Json
$reservation = Invoke-RestMethod -Uri "http://localhost:5000/api/resources/reservations" -Method Post -ContentType "application/json" -Body $reservationBody -Headers $headers

$overReserveStatus = "not-tested"
try {
    $overBody = @{ inventoryStockId = $stock.id; quantity = 20; purpose = "Too much." } | ConvertTo-Json
    Invoke-RestMethod -Uri "http://localhost:5000/api/resources/reservations" -Method Post -ContentType "application/json" -Body $overBody -Headers $headers | Out-Null
    $overReserveStatus = "unexpected-success"
} catch {
    $overReserveStatus = $_.Exception.Response.StatusCode.value__
}

$released = Invoke-RestMethod -Uri "http://localhost:5000/api/resources/reservations/$($reservation.id)/release" -Method Post -Headers $headers
$history = Invoke-RestMethod -Uri "http://localhost:5000/api/resources/stocks/$($stock.id)/history" -Method Get -Headers $headers
$lowStocks = Invoke-RestMethod -Uri "http://localhost:5000/api/resources/stocks?lowStockOnly=true" -Method Get -Headers $headers

Write-Output "RESOURCE_OFFICER_LOGIN=$($resourceLogin.user.email)"
Write-Output "CATEGORY_CREATED=$($category.name)"
Write-Output "SUPPLIER_CREATED=$($supplier.name)"
Write-Output "RESOURCE_CREATED=$($resource.name)"
Write-Output "STOCK_AVAILABLE_INITIAL=$($stock.availableQuantity)"
Write-Output "RESERVATION_STATUS=$($reservation.status)"
Write-Output "OVER_RESERVE_STATUS=$overReserveStatus"
Write-Output "RELEASED_STATUS=$($released.status)"
Write-Output "HISTORY_COUNT=$($history.Count)"
Write-Output "LOW_STOCK_COUNT=$($lowStocks.totalCount)"
