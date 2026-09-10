$ErrorActionPreference = "Stop"

$encoding = New-Object System.Text.UTF8Encoding($false)
$root = Join-Path $PSScriptRoot ".."

function Write-NoBom($Path, $Content) {
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$dbContextPath = Join-Path $root "backend\AgriAssist.Api\Data\AppDbContext.cs"
$content = [System.IO.File]::ReadAllText($dbContextPath)
$content = $content.Replace(
    '            entity.Property(stock => stock.RowVersion).IsRowVersion();',
    '            entity.Property(stock => stock.RowVersion).IsConcurrencyToken().IsRequired();')
Write-NoBom $dbContextPath $content

$servicePath = Join-Path $root "backend\AgriAssist.Api\Services\Resources\ResourceService.cs"
$content = [System.IO.File]::ReadAllText($servicePath)
$content = $content.Replace(
    'stock = new InventoryStock { ResourceId = request.ResourceId, QuantityOnHand = request.QuantityOnHand, LowStockThreshold = request.LowStockThreshold, CreatedByUserId = currentUser.UserId };',
    'stock = new InventoryStock { ResourceId = request.ResourceId, QuantityOnHand = request.QuantityOnHand, LowStockThreshold = request.LowStockThreshold, RowVersion = NewRowVersion(), CreatedByUserId = currentUser.UserId };')
$content = $content.Replace(
    'stock.LowStockThreshold = request.LowStockThreshold;
            stock.UpdatedAt = DateTime.UtcNow;',
    'stock.LowStockThreshold = request.LowStockThreshold;
            stock.RowVersion = NewRowVersion();
            stock.UpdatedAt = DateTime.UtcNow;')
$content = $content.Replace(
    'stock.ReservedQuantity += request.Quantity;
        if (stock.ReservedQuantity > stock.QuantityOnHand) throw new ApiException',
    'stock.ReservedQuantity += request.Quantity;
        stock.RowVersion = NewRowVersion();
        if (stock.ReservedQuantity > stock.QuantityOnHand) throw new ApiException')
$content = $content.Replace(
    'stock.ReservedQuantity -= reservation.Quantity;
        if (stock.ReservedQuantity < 0) throw new ApiException',
    'stock.ReservedQuantity -= reservation.Quantity;
        stock.RowVersion = NewRowVersion();
        if (stock.ReservedQuantity < 0) throw new ApiException')
$content = $content.Replace(
    'stock.ReservedQuantity -= reservation.Quantity;
        if (stock.ReservedQuantity < 0) throw new ApiException',
    'stock.ReservedQuantity -= reservation.Quantity;
        stock.RowVersion = NewRowVersion();
        if (stock.ReservedQuantity < 0) throw new ApiException')
$content = $content.Replace(
    '    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)',
    '    private static byte[] NewRowVersion() => Guid.NewGuid().ToByteArray();

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)')
Write-NoBom $servicePath $content
