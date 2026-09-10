$appDbContext = @'
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
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
    public DbSet<FieldInspection> FieldInspections => Set<FieldInspection>();
    public DbSet<InspectionObservation> InspectionObservations => Set<InspectionObservation>();
    public DbSet<CropIssue> CropIssues => Set<CropIssue>();
    public DbSet<InspectionImage> InspectionImages => Set<InspectionImage>();
    public DbSet<FollowUpRecommendation> FollowUpRecommendations => Set<FollowUpRecommendation>();
    public DbSet<ResourceCategory> ResourceCategories => Set<ResourceCategory>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<ResourceReservation> ResourceReservations => Set<ResourceReservation>();
    public DbSet<FarmTask> FarmTasks => Set<FarmTask>();
    public DbSet<IrrigationSchedule> IrrigationSchedules => Set<IrrigationSchedule>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<AgentWorkflow> AgentWorkflows => Set<AgentWorkflow>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    public DbSet<AgentToolExecution> AgentToolExecutions => Set<AgentToolExecution>();
    public DbSet<AgentValidationResult> AgentValidationResults => Set<AgentValidationResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
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
            entity.Property(farm => farm.Name).IsRequired().HasMaxLength(120);
            entity.Property(farm => farm.Location).IsRequired().HasMaxLength(240);
            entity.Property(farm => farm.TotalArea).HasPrecision(12, 2);
            entity.HasOne(farm => farm.OwnerUser).WithMany().HasForeignKey(farm => farm.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(farm => farm.OwnerUserId);
            entity.HasIndex(farm => farm.Name);
        });

        modelBuilder.Entity<Field>(entity =>
        {
            entity.Property(field => field.Name).IsRequired().HasMaxLength(120);
            entity.Property(field => field.SoilType).HasMaxLength(120);
            entity.Property(field => field.Area).HasPrecision(12, 2);
            entity.HasOne(field => field.Farm).WithMany(farm => farm.Fields).HasForeignKey(field => field.FarmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(field => field.FarmId);
            entity.HasIndex(field => field.IsActive);
        });

        modelBuilder.Entity<CropType>(entity =>
        {
            entity.Property(cropType => cropType.Name).IsRequired().HasMaxLength(120);
            entity.Property(cropType => cropType.Description).HasMaxLength(500);
            entity.HasIndex(cropType => cropType.Name).IsUnique();
            entity.HasIndex(cropType => cropType.IsActive);
        });

        modelBuilder.Entity<CropCycle>(entity =>
        {
            entity.Property(cycle => cycle.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(cycle => cycle.Field).WithMany().HasForeignKey(cycle => cycle.FieldId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(cycle => cycle.CropType).WithMany().HasForeignKey(cycle => cycle.CropTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(cycle => cycle.FieldId);
            entity.HasIndex(cycle => cycle.Status);
        });

        modelBuilder.Entity<CropPlanRequest>(entity =>
        {
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
            entity.Property(history => history.FromStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(history => history.ToStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(history => history.Note).HasMaxLength(500);
            entity.HasOne(history => history.CropPlanRequest).WithMany(request => request.History).HasForeignKey(history => history.CropPlanRequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(history => history.ChangedByUser).WithMany().HasForeignKey(history => history.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(history => history.CropPlanRequestId);
        });

        modelBuilder.Entity<FieldInspection>(entity =>
        {
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
            entity.Property(observation => observation.ObservationType).IsRequired().HasMaxLength(120);
            entity.Property(observation => observation.Notes).IsRequired().HasMaxLength(1000);
            entity.HasOne(observation => observation.FieldInspection).WithMany().HasForeignKey(observation => observation.FieldInspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(observation => observation.FieldInspectionId);
        });

        modelBuilder.Entity<CropIssue>(entity =>
        {
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
            entity.Property(image => image.Url).IsRequired().HasMaxLength(1000);
            entity.Property(image => image.PublicId).IsRequired().HasMaxLength(240);
            entity.Property(image => image.ContentType).IsRequired().HasMaxLength(80);
            entity.HasOne(image => image.FieldInspection).WithMany().HasForeignKey(image => image.FieldInspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(image => image.FieldInspectionId);
        });

        modelBuilder.Entity<FollowUpRecommendation>(entity =>
        {
            entity.Property(recommendation => recommendation.Recommendation).IsRequired().HasMaxLength(1500);
            entity.HasOne(recommendation => recommendation.CropIssue).WithMany().HasForeignKey(recommendation => recommendation.CropIssueId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(recommendation => recommendation.CropIssueId);
            entity.HasIndex(recommendation => recommendation.IsCompleted);
        });

        modelBuilder.Entity<ResourceCategory>(entity =>
        {
            entity.Property(category => category.Name).IsRequired().HasMaxLength(120);
            entity.Property(category => category.Description).HasMaxLength(500);
            entity.HasIndex(category => category.Name).IsUnique();
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(supplier => supplier.Name).IsRequired().HasMaxLength(160);
            entity.Property(supplier => supplier.ContactEmail).HasMaxLength(180);
            entity.Property(supplier => supplier.Phone).HasMaxLength(40);
            entity.HasIndex(supplier => supplier.Name);
        });

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.Property(resource => resource.Name).IsRequired().HasMaxLength(160);
            entity.Property(resource => resource.Unit).IsRequired().HasMaxLength(40);
            entity.HasOne(resource => resource.ResourceCategory).WithMany().HasForeignKey(resource => resource.ResourceCategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(resource => resource.Supplier).WithMany().HasForeignKey(resource => resource.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(resource => resource.Name);
            entity.HasIndex(resource => resource.IsActive);
        });

        modelBuilder.Entity<InventoryStock>(entity =>
        {
            entity.Property(stock => stock.QuantityOnHand).HasPrecision(12, 2);
            entity.Property(stock => stock.ReservedQuantity).HasPrecision(12, 2);
            entity.Property(stock => stock.LowStockThreshold).HasPrecision(12, 2);
            entity.Property(stock => stock.RowVersion).IsRowVersion();
            entity.HasOne(stock => stock.Resource).WithMany().HasForeignKey(stock => stock.ResourceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(stock => stock.ResourceId).IsUnique();
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(transaction => transaction.Quantity).HasPrecision(12, 2);
            entity.Property(transaction => transaction.Note).HasMaxLength(500);
            entity.HasOne(transaction => transaction.InventoryStock).WithMany().HasForeignKey(transaction => transaction.InventoryStockId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(transaction => transaction.InventoryStockId);
            entity.HasIndex(transaction => transaction.Type);
        });

        modelBuilder.Entity<ResourceReservation>(entity =>
        {
            entity.Property(reservation => reservation.Quantity).HasPrecision(12, 2);
            entity.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(reservation => reservation.Purpose).IsRequired().HasMaxLength(500);
            entity.HasOne(reservation => reservation.InventoryStock).WithMany().HasForeignKey(reservation => reservation.InventoryStockId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(reservation => reservation.RequestedByUser).WithMany().HasForeignKey(reservation => reservation.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(reservation => reservation.Status);
            entity.HasIndex(reservation => reservation.InventoryStockId);
        });

        modelBuilder.Entity<FarmTask>(entity =>
        {
            entity.Property(task => task.Title).IsRequired().HasMaxLength(160);
            entity.Property(task => task.Description).HasMaxLength(1500);
            entity.Property(task => task.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(task => task.Farm).WithMany().HasForeignKey(task => task.FarmId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(task => task.AssignedToUser).WithMany().HasForeignKey(task => task.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(task => task.Status);
            entity.HasIndex(task => task.DueAt);
        });

        modelBuilder.Entity<IrrigationSchedule>(entity =>
        {
            entity.Property(schedule => schedule.Notes).HasMaxLength(1000);
            entity.Property(schedule => schedule.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(schedule => schedule.Field).WithMany().HasForeignKey(schedule => schedule.FieldId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(schedule => schedule.Status);
            entity.HasIndex(schedule => schedule.ScheduledAt);
        });

        modelBuilder.Entity<ApprovalDecision>(entity =>
        {
            entity.Property(approval => approval.Decision).HasConversion<string>().HasMaxLength(40);
            entity.Property(approval => approval.Comment).HasMaxLength(1000);
            entity.HasOne(approval => approval.FarmTask).WithMany().HasForeignKey(approval => approval.FarmTaskId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(approval => approval.IrrigationSchedule).WithMany().HasForeignKey(approval => approval.IrrigationScheduleId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(approval => approval.DecidedByUser).WithMany().HasForeignKey(approval => approval.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(approval => approval.AgentWorkflow).WithMany().HasForeignKey(approval => approval.AgentWorkflowId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(approval => approval.Decision);
            entity.HasIndex(approval => approval.AgentWorkflowId);
        });

        modelBuilder.Entity<AgentWorkflow>(entity =>
        {
            entity.Property(workflow => workflow.Objective).IsRequired().HasMaxLength(1000);
            entity.Property(workflow => workflow.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(workflow => workflow.CurrentStep).HasMaxLength(160);
            entity.HasOne(workflow => workflow.CropPlanRequest).WithMany().HasForeignKey(workflow => workflow.CropPlanRequestId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(workflow => workflow.InitiatedByUser).WithMany().HasForeignKey(workflow => workflow.InitiatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(workflow => workflow.CropPlanRequestId);
            entity.HasIndex(workflow => workflow.Status);
        });

        modelBuilder.Entity<AgentStep>(entity =>
        {
            entity.Property(step => step.AgentName).IsRequired().HasMaxLength(120);
            entity.Property(step => step.StepName).IsRequired().HasMaxLength(160);
            entity.Property(step => step.InputJson).HasColumnType("jsonb");
            entity.Property(step => step.OutputJson).HasColumnType("jsonb");
            entity.Property(step => step.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(step => step.ErrorCode).HasMaxLength(120);
            entity.Property(step => step.ErrorMessageSafe).HasMaxLength(1000);
            entity.HasOne(step => step.AgentWorkflow).WithMany(workflow => workflow.Steps).HasForeignKey(step => step.AgentWorkflowId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(step => new { step.AgentWorkflowId, step.Sequence });
        });

        modelBuilder.Entity<AgentToolExecution>(entity =>
        {
            entity.Property(tool => tool.ToolName).IsRequired().HasMaxLength(160);
            entity.Property(tool => tool.InputJson).HasColumnType("jsonb");
            entity.Property(tool => tool.OutputJson).HasColumnType("jsonb");
            entity.Property(tool => tool.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(tool => tool.AgentStep).WithMany(step => step.ToolExecutions).HasForeignKey(tool => tool.AgentStepId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(tool => tool.AgentStepId);
        });

        modelBuilder.Entity<AgentValidationResult>(entity =>
        {
            entity.Property(result => result.ValidatorName).IsRequired().HasMaxLength(160);
            entity.Property(result => result.ErrorsJson).HasColumnType("jsonb");
            entity.HasOne(result => result.AgentWorkflow).WithMany(workflow => workflow.ValidationResults).HasForeignKey(result => result.AgentWorkflowId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(result => result.AgentWorkflowId);
        });
    }
}
'@

Set-Content -LiteralPath "backend/AgriAssist.Api/Data/AppDbContext.cs" -Value $appDbContext -Encoding ASCII
