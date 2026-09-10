$appDbContext = @'
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Field> Fields => Set<Field>();
    public DbSet<CropType> CropTypes => Set<CropType>();
    public DbSet<CropCycle> CropCycles => Set<CropCycle>();
    public DbSet<CropPlanRequest> CropPlanRequests => Set<CropPlanRequest>();
    public DbSet<CropPlanRequestHistory> CropPlanRequestHistories => Set<CropPlanRequestHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.FullName).IsRequired().HasMaxLength(120);
            entity.Property(user => user.Email).IsRequired().HasMaxLength(180);
            entity.Property(user => user.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.Role);
            entity.HasIndex(user => user.IsActive);
        });

        modelBuilder.Entity<Farm>(entity =>
        {
            entity.HasKey(farm => farm.Id);
            entity.Property(farm => farm.Name).IsRequired().HasMaxLength(120);
            entity.Property(farm => farm.Location).IsRequired().HasMaxLength(240);
            entity.Property(farm => farm.TotalArea).HasPrecision(12, 2);
            entity.HasOne(farm => farm.OwnerUser).WithMany().HasForeignKey(farm => farm.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(farm => farm.OwnerUserId);
            entity.HasIndex(farm => farm.Name);
        });

        modelBuilder.Entity<Field>(entity =>
        {
            entity.HasKey(field => field.Id);
            entity.Property(field => field.Name).IsRequired().HasMaxLength(120);
            entity.Property(field => field.SoilType).HasMaxLength(120);
            entity.Property(field => field.Area).HasPrecision(12, 2);
            entity.HasOne(field => field.Farm).WithMany(farm => farm.Fields).HasForeignKey(field => field.FarmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(field => field.FarmId);
            entity.HasIndex(field => field.IsActive);
        });

        modelBuilder.Entity<CropType>(entity =>
        {
            entity.HasKey(cropType => cropType.Id);
            entity.Property(cropType => cropType.Name).IsRequired().HasMaxLength(120);
            entity.Property(cropType => cropType.Description).HasMaxLength(500);
            entity.HasIndex(cropType => cropType.Name).IsUnique();
            entity.HasIndex(cropType => cropType.IsActive);
        });

        modelBuilder.Entity<CropCycle>(entity =>
        {
            entity.HasKey(cycle => cycle.Id);
            entity.Property(cycle => cycle.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(cycle => cycle.Field).WithMany().HasForeignKey(cycle => cycle.FieldId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(cycle => cycle.CropType).WithMany().HasForeignKey(cycle => cycle.CropTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(cycle => cycle.FieldId);
            entity.HasIndex(cycle => cycle.Status);
        });

        modelBuilder.Entity<CropPlanRequest>(entity =>
        {
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Objective).IsRequired().HasMaxLength(500);
            entity.Property(request => request.Budget).HasPrecision(12, 2);
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(request => request.Farm).WithMany().HasForeignKey(request => request.FarmId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.Field).WithMany().HasForeignKey(request => request.FieldId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.CropType).WithMany().HasForeignKey(request => request.CropTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.RequestedByUser).WithMany().HasForeignKey(request => request.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(request => request.FarmId);
            entity.HasIndex(request => request.FieldId);
            entity.HasIndex(request => request.Status);
        });

        modelBuilder.Entity<CropPlanRequestHistory>(entity =>
        {
            entity.HasKey(history => history.Id);
            entity.Property(history => history.FromStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(history => history.ToStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(history => history.Note).HasMaxLength(500);
            entity.HasOne(history => history.CropPlanRequest).WithMany(request => request.History).HasForeignKey(history => history.CropPlanRequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(history => history.ChangedByUser).WithMany().HasForeignKey(history => history.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(history => history.CropPlanRequestId);
        });
    }
}
'@

$seedData = @'
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext dbContext)
    {
        if (!await dbContext.Users.AnyAsync())
        {
            dbContext.Users.AddRange(
                new AppUser
                {
                    FullName = "Development Admin",
                    Email = "admin@agriassist.local",
                    Role = ApplicationRole.Admin,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@2026"),
                    IsActive = true
                },
                new AppUser
                {
                    FullName = "Demo Farmer",
                    Email = "farmer@agriassist.local",
                    Role = ApplicationRole.Farmer,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Farmer@2026"),
                    IsActive = true
                });
        }

        if (!await dbContext.CropTypes.AnyAsync())
        {
            dbContext.CropTypes.AddRange(
                new CropType { Name = "Rice", Description = "Demo crop name for development.", IsActive = true },
                new CropType { Name = "Maize", Description = "Demo crop name for development.", IsActive = true },
                new CropType { Name = "Vegetables", Description = "Demo crop category for development.", IsActive = true });
        }

        await dbContext.SaveChangesAsync();
    }
}
'@

$programPath = "backend/AgriAssist.Api/Program.cs"
$program = Get-Content -LiteralPath $programPath -Raw
$program = $program -replace 'using AgriAssist.Api.Dtos.Shared;', "using AgriAssist.Api.Dtos.Shared;`r`nusing AgriAssist.Api.Dtos.CropPlanning;"
$program = $program -replace 'using AgriAssist.Api.Services.Shared;', "using AgriAssist.Api.Services.Shared;`r`nusing AgriAssist.Api.Services.CropPlanning;"
$program = $program -replace 'using AgriAssist.Api.Validators.Shared;', "using AgriAssist.Api.Validators.Shared;`r`nusing AgriAssist.Api.Validators.CropPlanning;"
$registrations = @'
builder.Services.AddScoped<IRequestValidator<FarmRequest>, FarmRequestValidator>();
builder.Services.AddScoped<IRequestValidator<FieldRequest>, FieldRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropTypeRequest>, CropTypeRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropCycleRequest>, CropCycleRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropPlanRequestCreate>, CropPlanRequestCreateValidator>();
builder.Services.AddScoped<IRequestValidator<CropPlanRequestUpdate>, CropPlanRequestUpdateValidator>();
builder.Services.AddScoped<ICropPlanningService, CropPlanningService>();
'@
$program = $program -replace 'builder.Services.AddScoped<IUserService, UserService>\(\);', "builder.Services.AddScoped<IUserService, UserService>();`r`n$registrations"

Set-Content -LiteralPath "backend/AgriAssist.Api/Data/AppDbContext.cs" -Value $appDbContext -Encoding ASCII
Set-Content -LiteralPath "backend/AgriAssist.Api/Data/SeedData.cs" -Value $seedData -Encoding ASCII
Set-Content -LiteralPath $programPath -Value $program -Encoding ASCII
