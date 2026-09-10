$path = "backend/AgriAssist.Api/Data/AppDbContext.cs"
$content = Get-Content -LiteralPath $path -Raw
$content = $content -replace 'using AgriAssist.Api.Models.Inspections;', "using AgriAssist.Api.Models.Inspections;`r`nusing AgriAssist.Api.Models.Resources;"
$content = $content -replace 'public DbSet<FollowUpRecommendation> FollowUpRecommendations => Set<FollowUpRecommendation>\(\);', @'
public DbSet<FollowUpRecommendation> FollowUpRecommendations => Set<FollowUpRecommendation>();
    public DbSet<ResourceCategory> ResourceCategories => Set<ResourceCategory>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<ResourceReservation> ResourceReservations => Set<ResourceReservation>();
'@
$resourceConfig = @'

        modelBuilder.Entity<ResourceCategory>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).IsRequired().HasMaxLength(120);
            entity.Property(category => category.Description).HasMaxLength(500);
            entity.HasIndex(category => category.Name).IsUnique();
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(supplier => supplier.Id);
            entity.Property(supplier => supplier.Name).IsRequired().HasMaxLength(160);
            entity.Property(supplier => supplier.ContactEmail).HasMaxLength(180);
            entity.Property(supplier => supplier.Phone).HasMaxLength(40);
            entity.HasIndex(supplier => supplier.Name);
        });

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.HasKey(resource => resource.Id);
            entity.Property(resource => resource.Name).IsRequired().HasMaxLength(160);
            entity.Property(resource => resource.Unit).IsRequired().HasMaxLength(40);
            entity.HasOne(resource => resource.ResourceCategory).WithMany().HasForeignKey(resource => resource.ResourceCategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(resource => resource.Supplier).WithMany().HasForeignKey(resource => resource.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(resource => resource.Name);
            entity.HasIndex(resource => resource.IsActive);
        });

        modelBuilder.Entity<InventoryStock>(entity =>
        {
            entity.HasKey(stock => stock.Id);
            entity.Property(stock => stock.QuantityOnHand).HasPrecision(12, 2);
            entity.Property(stock => stock.ReservedQuantity).HasPrecision(12, 2);
            entity.Property(stock => stock.LowStockThreshold).HasPrecision(12, 2);
            entity.Property(stock => stock.RowVersion).IsRowVersion();
            entity.HasOne(stock => stock.Resource).WithMany().HasForeignKey(stock => stock.ResourceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(stock => stock.ResourceId).IsUnique();
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(transaction => transaction.Quantity).HasPrecision(12, 2);
            entity.Property(transaction => transaction.Note).HasMaxLength(500);
            entity.HasOne(transaction => transaction.InventoryStock).WithMany().HasForeignKey(transaction => transaction.InventoryStockId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(transaction => transaction.InventoryStockId);
            entity.HasIndex(transaction => transaction.Type);
        });

        modelBuilder.Entity<ResourceReservation>(entity =>
        {
            entity.HasKey(reservation => reservation.Id);
            entity.Property(reservation => reservation.Quantity).HasPrecision(12, 2);
            entity.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(reservation => reservation.Purpose).IsRequired().HasMaxLength(500);
            entity.HasOne(reservation => reservation.InventoryStock).WithMany().HasForeignKey(reservation => reservation.InventoryStockId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(reservation => reservation.RequestedByUser).WithMany().HasForeignKey(reservation => reservation.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(reservation => reservation.Status);
            entity.HasIndex(reservation => reservation.InventoryStockId);
        });
'@
$content = $content -replace '    }\r?\n}$', "$resourceConfig`r`n    }`r`n}"
Set-Content -LiteralPath $path -Value $content -Encoding ASCII

$seedPath = "backend/AgriAssist.Api/Data/SeedData.cs"
$seed = Get-Content -LiteralPath $seedPath -Raw
$seed = $seed -replace 'new AppUser\r?\n                \{\r?\n                    FullName = "Demo Field Officer",', @'
new AppUser
                {
                    FullName = "Demo Resource Officer",
                    Email = "resourceofficer@agriassist.local",
                    Role = ApplicationRole.ResourceOfficer,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Resource@2026"),
                    IsActive = true
                },
                new AppUser
                {
                    FullName = "Demo Field Officer",
'@
Set-Content -LiteralPath $seedPath -Value $seed -Encoding ASCII

$programPath = "backend/AgriAssist.Api/Program.cs"
$program = Get-Content -LiteralPath $programPath -Raw
$program = $program -replace 'using AgriAssist.Api.Dtos.Inspections;', "using AgriAssist.Api.Dtos.Inspections;`r`nusing AgriAssist.Api.Dtos.Resources;"
$program = $program -replace 'using AgriAssist.Api.Services.Inspections;', "using AgriAssist.Api.Services.Inspections;`r`nusing AgriAssist.Api.Services.Resources;"
$program = $program -replace 'using AgriAssist.Api.Validators.Inspections;', "using AgriAssist.Api.Validators.Inspections;`r`nusing AgriAssist.Api.Validators.Resources;"
$registrations = @'
builder.Services.AddScoped<IRequestValidator<ResourceCategoryRequest>, ResourceCategoryRequestValidator>();
builder.Services.AddScoped<IRequestValidator<SupplierRequest>, SupplierRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ResourceRequest>, ResourceRequestValidator>();
builder.Services.AddScoped<IRequestValidator<InventoryStockRequest>, InventoryStockRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ResourceReservationRequest>, ResourceReservationRequestValidator>();
builder.Services.AddScoped<IRequestValidator<StockAdjustmentRequest>, StockAdjustmentRequestValidator>();
builder.Services.AddScoped<IResourceService, ResourceService>();
'@
$program = $program -replace 'builder.Services.AddScoped<IInspectionService, InspectionService>\(\);', "builder.Services.AddScoped<IInspectionService, InspectionService>();`r`n$registrations"
Set-Content -LiteralPath $programPath -Value $program -Encoding ASCII
