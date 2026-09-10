$path = "backend/AgriAssist.Api/Data/AppDbContext.cs"
$content = Get-Content -LiteralPath $path -Raw
$content = $content -replace 'using AgriAssist.Api.Models.CropPlanning;', "using AgriAssist.Api.Models.CropPlanning;`r`nusing AgriAssist.Api.Models.Inspections;"
$content = $content -replace 'public DbSet<CropPlanRequestHistory> CropPlanRequestHistories => Set<CropPlanRequestHistory>\(\);', @'
public DbSet<CropPlanRequestHistory> CropPlanRequestHistories => Set<CropPlanRequestHistory>();
    public DbSet<FieldInspection> FieldInspections => Set<FieldInspection>();
    public DbSet<InspectionObservation> InspectionObservations => Set<InspectionObservation>();
    public DbSet<CropIssue> CropIssues => Set<CropIssue>();
    public DbSet<InspectionImage> InspectionImages => Set<InspectionImage>();
    public DbSet<FollowUpRecommendation> FollowUpRecommendations => Set<FollowUpRecommendation>();
'@
$inspectionConfig = @'

        modelBuilder.Entity<FieldInspection>(entity =>
        {
            entity.HasKey(inspection => inspection.Id);
            entity.Property(inspection => inspection.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(inspection => inspection.Summary).HasMaxLength(1000);
            entity.HasOne(inspection => inspection.Field).WithMany().HasForeignKey(inspection => inspection.FieldId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(inspection => inspection.InspectorUser).WithMany().HasForeignKey(inspection => inspection.InspectorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(inspection => inspection.FieldId);
            entity.HasIndex(inspection => inspection.Status);
            entity.HasIndex(inspection => inspection.ScheduledAt);
        });

        modelBuilder.Entity<InspectionObservation>(entity =>
        {
            entity.HasKey(observation => observation.Id);
            entity.Property(observation => observation.ObservationType).IsRequired().HasMaxLength(120);
            entity.Property(observation => observation.Notes).IsRequired().HasMaxLength(1000);
            entity.HasOne(observation => observation.FieldInspection).WithMany().HasForeignKey(observation => observation.FieldInspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(observation => observation.FieldInspectionId);
        });

        modelBuilder.Entity<CropIssue>(entity =>
        {
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.Title).IsRequired().HasMaxLength(160);
            entity.Property(issue => issue.Description).IsRequired().HasMaxLength(1500);
            entity.Property(issue => issue.Severity).HasConversion<string>().HasMaxLength(40);
            entity.Property(issue => issue.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(issue => issue.FieldInspection).WithMany().HasForeignKey(issue => issue.FieldInspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(issue => issue.Status);
            entity.HasIndex(issue => issue.Severity);
        });

        modelBuilder.Entity<InspectionImage>(entity =>
        {
            entity.HasKey(image => image.Id);
            entity.Property(image => image.Url).IsRequired().HasMaxLength(1000);
            entity.Property(image => image.PublicId).IsRequired().HasMaxLength(240);
            entity.Property(image => image.ContentType).IsRequired().HasMaxLength(80);
            entity.HasOne(image => image.FieldInspection).WithMany().HasForeignKey(image => image.FieldInspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(image => image.FieldInspectionId);
        });

        modelBuilder.Entity<FollowUpRecommendation>(entity =>
        {
            entity.HasKey(recommendation => recommendation.Id);
            entity.Property(recommendation => recommendation.Recommendation).IsRequired().HasMaxLength(1500);
            entity.HasOne(recommendation => recommendation.CropIssue).WithMany().HasForeignKey(recommendation => recommendation.CropIssueId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(recommendation => recommendation.CropIssueId);
            entity.HasIndex(recommendation => recommendation.IsCompleted);
        });
'@
$content = $content -replace '    }\r?\n}$', "$inspectionConfig`r`n    }`r`n}"
Set-Content -LiteralPath $path -Value $content -Encoding ASCII

$seedPath = "backend/AgriAssist.Api/Data/SeedData.cs"
$seed = Get-Content -LiteralPath $seedPath -Raw
$seed = $seed -replace 'new AppUser\r?\n                \{\r?\n                    FullName = "Demo Farmer",', @'
new AppUser
                {
                    FullName = "Demo Field Officer",
                    Email = "fieldofficer@agriassist.local",
                    Role = ApplicationRole.FieldOfficer,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Field@2026"),
                    IsActive = true
                },
                new AppUser
                {
                    FullName = "Demo Agricultural Officer",
                    Email = "agofficer@agriassist.local",
                    Role = ApplicationRole.AgriculturalOfficer,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Agri@2026"),
                    IsActive = true
                },
                new AppUser
                {
                    FullName = "Demo Farmer",
'@
Set-Content -LiteralPath $seedPath -Value $seed -Encoding ASCII

$programPath = "backend/AgriAssist.Api/Program.cs"
$program = Get-Content -LiteralPath $programPath -Raw
$program = $program -replace 'using AgriAssist.Api.Dtos.CropPlanning;', "using AgriAssist.Api.Dtos.CropPlanning;`r`nusing AgriAssist.Api.Dtos.Inspections;"
$program = $program -replace 'using AgriAssist.Api.Services.CropPlanning;', "using AgriAssist.Api.Services.CropPlanning;`r`nusing AgriAssist.Api.Services.Inspections;"
$program = $program -replace 'using AgriAssist.Api.Validators.CropPlanning;', "using AgriAssist.Api.Validators.CropPlanning;`r`nusing AgriAssist.Api.Validators.Inspections;`r`nusing AgriAssist.Api.ExternalServices.Cloudinary;"
$program = $program -replace 'builder.Services.AddHealthChecks\(\);', "builder.Services.AddHealthChecks();`r`nbuilder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection(""Cloudinary""));"
$registrations = @'
builder.Services.AddScoped<IRequestValidator<FieldInspectionRequest>, FieldInspectionRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ObservationRequest>, ObservationRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropIssueRequest>, CropIssueRequestValidator>();
builder.Services.AddScoped<IRequestValidator<FollowUpRecommendationRequest>, FollowUpRecommendationRequestValidator>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<IInspectionService, InspectionService>();
'@
$program = $program -replace 'builder.Services.AddScoped<ICropPlanningService, CropPlanningService>\(\);', "builder.Services.AddScoped<ICropPlanningService, CropPlanningService>();`r`n$registrations"
Set-Content -LiteralPath $programPath -Value $program -Encoding ASCII
