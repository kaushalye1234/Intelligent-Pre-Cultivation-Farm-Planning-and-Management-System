using System.Text;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Middleware;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Inspections;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.TaskApproval;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Validators.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using AgriAssist.Api.Validators.Inspections;
using AgriAssist.Api.Validators.Resources;
using AgriAssist.Api.Validators.TaskApproval;
using AgriAssist.Api.ExternalServices.Cloudinary;
using AgriAssist.Api.ExternalServices.Weather;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.Configure<WeatherOptions>(builder.Configuration.GetSection("Weather"));
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (builder.Environment.IsEnvironment("Testing") || string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("AgriAssistDevelopment"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IRequestValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IRequestValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IRequestValidator<FarmRequest>, FarmRequestValidator>();
builder.Services.AddScoped<IRequestValidator<FieldRequest>, FieldRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropTypeRequest>, CropTypeRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropCycleRequest>, CropCycleRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropPlanRequestCreate>, CropPlanRequestCreateValidator>();
builder.Services.AddScoped<IRequestValidator<CropPlanRequestUpdate>, CropPlanRequestUpdateValidator>();
builder.Services.AddScoped<ICropPlanningService, CropPlanningService>();
builder.Services.AddScoped<IRequestValidator<FieldInspectionRequest>, FieldInspectionRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ObservationRequest>, ObservationRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropIssueRequest>, CropIssueRequestValidator>();
builder.Services.AddScoped<IRequestValidator<FollowUpRecommendationRequest>, FollowUpRecommendationRequestValidator>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<IInspectionService, InspectionService>();
builder.Services.AddScoped<IRequestValidator<ResourceCategoryRequest>, ResourceCategoryRequestValidator>();
builder.Services.AddScoped<IRequestValidator<SupplierRequest>, SupplierRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ResourceRequest>, ResourceRequestValidator>();
builder.Services.AddScoped<IRequestValidator<InventoryStockRequest>, InventoryStockRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ResourceReservationRequest>, ResourceReservationRequestValidator>();
builder.Services.AddScoped<IRequestValidator<StockAdjustmentRequest>, StockAdjustmentRequestValidator>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<IRequestValidator<FarmTaskRequest>, FarmTaskRequestValidator>();
builder.Services.AddScoped<IRequestValidator<IrrigationScheduleRequest>, IrrigationScheduleRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ApprovalActionRequest>, ApprovalActionRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CancellationRequest>, CancellationRequestValidator>();
builder.Services.AddScoped<ITaskApprovalService, TaskApprovalService>();
builder.Services.AddHttpClient<IAgenticAIClient, AgenticAIClient>();
builder.Services.AddHttpClient<IWeatherResourceAIClient, AgenticAIClient>();
// OpenWeatherMap takes the API key as a query parameter, so do not log request URLs for this client.
builder.Services.AddHttpClient<IWeatherService, WeatherService>().RemoveAllLoggers();
builder.Services.AddScoped<IWeatherResourceWorkflowService, WeatherResourceWorkflowService>();

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        });
}
else
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();
}

builder.Services.AddAuthorization();

var configuredReactUrl = builder.Configuration["App:ReactUrl"] ?? "http://localhost:5173";
var allowedClientOrigins = new[] { configuredReactUrl, "http://localhost:5173", "http://127.0.0.1:5173" }
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApps", policy =>
    {
        policy.WithOrigins(allowedClientOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AgriAssist API",
        Version = "v1",
        Description = "BASIC non-AI foundation for AgriAssist AI."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT bearer token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.IsInMemory())
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    await SeedData.SeedAsync(dbContext);
}

app.UseHttpsRedirection();
app.UseCors("ClientApps");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;
