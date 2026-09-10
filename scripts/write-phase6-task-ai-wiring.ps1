$path = "backend/AgriAssist.Api/Data/AppDbContext.cs"
$content = Get-Content -LiteralPath $path -Raw
$content = $content -replace 'using AgriAssist.Api.Models.Resources;', "using AgriAssist.Api.Models.Resources;`r`nusing AgriAssist.Api.Models.TaskApproval;"
$content = $content -replace 'public DbSet<ResourceReservation> ResourceReservations => Set<ResourceReservation>\(\);', @'
public DbSet<ResourceReservation> ResourceReservations => Set<ResourceReservation>();
    public DbSet<FarmTask> FarmTasks => Set<FarmTask>();
    public DbSet<IrrigationSchedule> IrrigationSchedules => Set<IrrigationSchedule>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<AgentWorkflow> AgentWorkflows => Set<AgentWorkflow>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    public DbSet<AgentToolExecution> AgentToolExecutions => Set<AgentToolExecution>();
    public DbSet<AgentValidationResult> AgentValidationResults => Set<AgentValidationResult>();
'@
$taskAiConfig = @'

        modelBuilder.Entity<AgentWorkflow>(entity =>
        {
            entity.HasKey(workflow => workflow.Id);
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
            entity.HasKey(step => step.Id);
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
            entity.HasKey(tool => tool.Id);
            entity.Property(tool => tool.ToolName).IsRequired().HasMaxLength(160);
            entity.Property(tool => tool.InputJson).HasColumnType("jsonb");
            entity.Property(tool => tool.OutputJson).HasColumnType("jsonb");
            entity.Property(tool => tool.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(tool => tool.AgentStep).WithMany(step => step.ToolExecutions).HasForeignKey(tool => tool.AgentStepId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(tool => tool.AgentStepId);
        });

        modelBuilder.Entity<AgentValidationResult>(entity =>
        {
            entity.HasKey(result => result.Id);
            entity.Property(result => result.ValidatorName).IsRequired().HasMaxLength(160);
            entity.Property(result => result.ErrorsJson).HasColumnType("jsonb");
            entity.HasOne(result => result.AgentWorkflow).WithMany(workflow => workflow.ValidationResults).HasForeignKey(result => result.AgentWorkflowId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(result => result.AgentWorkflowId);
        });

        modelBuilder.Entity<FarmTask>(entity =>
        {
            entity.HasKey(task => task.Id);
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
            entity.HasKey(schedule => schedule.Id);
            entity.Property(schedule => schedule.Notes).HasMaxLength(1000);
            entity.Property(schedule => schedule.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(schedule => schedule.Field).WithMany().HasForeignKey(schedule => schedule.FieldId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(schedule => schedule.Status);
            entity.HasIndex(schedule => schedule.ScheduledAt);
        });

        modelBuilder.Entity<ApprovalDecision>(entity =>
        {
            entity.HasKey(approval => approval.Id);
            entity.Property(approval => approval.Decision).HasConversion<string>().HasMaxLength(40);
            entity.Property(approval => approval.Comment).HasMaxLength(1000);
            entity.HasOne(approval => approval.FarmTask).WithMany().HasForeignKey(approval => approval.FarmTaskId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(approval => approval.IrrigationSchedule).WithMany().HasForeignKey(approval => approval.IrrigationScheduleId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(approval => approval.DecidedByUser).WithMany().HasForeignKey(approval => approval.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(approval => approval.AgentWorkflow).WithMany().HasForeignKey(approval => approval.AgentWorkflowId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(approval => approval.Decision);
            entity.HasIndex(approval => approval.AgentWorkflowId);
        });
'@
$content = $content -replace '    }\r?\n}$', "$taskAiConfig`r`n    }`r`n}"
Set-Content -LiteralPath $path -Value $content -Encoding ASCII

$programPath = "backend/AgriAssist.Api/Program.cs"
$program = Get-Content -LiteralPath $programPath -Raw
$program = $program -replace 'using AgriAssist.Api.Dtos.Resources;', "using AgriAssist.Api.Dtos.Resources;`r`nusing AgriAssist.Api.Dtos.TaskApproval;"
$program = $program -replace 'using AgriAssist.Api.Services.Resources;', "using AgriAssist.Api.Services.Resources;`r`nusing AgriAssist.Api.Services.TaskApproval;`r`nusing AgriAssist.Api.ExternalServices.AgenticAI;"
$program = $program -replace 'using AgriAssist.Api.Validators.Resources;', "using AgriAssist.Api.Validators.Resources;`r`nusing AgriAssist.Api.Validators.TaskApproval;"
$registrations = @'
builder.Services.AddScoped<IRequestValidator<FarmTaskRequest>, FarmTaskRequestValidator>();
builder.Services.AddScoped<IRequestValidator<IrrigationScheduleRequest>, IrrigationScheduleRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ApprovalActionRequest>, ApprovalActionRequestValidator>();
builder.Services.AddScoped<ITaskApprovalService, TaskApprovalService>();
builder.Services.AddScoped<IAgenticAIClient, AgenticAIClient>();
'@
$program = $program -replace 'builder.Services.AddScoped<IResourceService, ResourceService>\(\);', "builder.Services.AddScoped<IResourceService, ResourceService>();`r`n$registrations"
Set-Content -LiteralPath $programPath -Value $program -Encoding ASCII
