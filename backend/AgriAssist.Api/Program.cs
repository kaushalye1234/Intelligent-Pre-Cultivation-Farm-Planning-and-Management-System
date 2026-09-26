using System.Text;
using AgriAssist.Api.Data;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using AgriAssist.Api.Configuration;
using AgriAssist.Api.Bootstrap;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Middleware;
using AgriAssist.Api.Models.Shared;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Keep local diagnostics on the console. Windows EventLog can throw when a
// stale DPAPI key or EF warning is emitted, which otherwise resets API calls.
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.Configure<WeatherOptions>(builder.Configuration.GetSection("Weather"));
builder.Services.Configure<PasswordSecurityOptions>(builder.Configuration.GetSection(PasswordSecurityOptions.SectionName));
builder.Services.AddOptions<SecurityRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(SecurityRateLimitOptions.SectionName))
    .Validate(
        options => IsValid(options.Login)
            && IsValid(options.FarmerRegistration)
            && IsValid(options.TemporaryPasswordChange)
            && IsValid(options.AdminReauthentication)
            && IsValid(options.AdminUserManagement),
        "Security rate-limit values must be positive.")
    .ValidateOnStart();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (builder.Environment.IsEnvironment("Testing") || string.IsNullOrWhiteSpace(connectionString))
{
    var inMemoryDatabaseName = builder.Environment.IsEnvironment("Testing")
        ? $"AgriAssistTesting-{Guid.NewGuid():N}"
        : "AgriAssistDevelopment";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase(inMemoryDatabaseName));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IRequestValidator<RegisterFarmerRequest>, RegisterFarmerRequestValidator>();
builder.Services.AddScoped<IRequestValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ChangeTemporaryPasswordRequest>, ChangeTemporaryPasswordRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CreateStaffUserRequest>, CreateStaffUserRequestValidator>();
builder.Services.AddScoped<IRequestValidator<ResetStaffPasswordRequest>, ResetStaffPasswordRequestValidator>();
builder.Services.AddSingleton<ICompromisedPasswordChecker, ConfiguredCompromisedPasswordChecker>();
builder.Services.AddSingleton<IPasswordPolicyService, PasswordPolicyService>();
builder.Services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();
builder.Services.AddScoped<AdminBootstrapCommand>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IRequestValidator<FarmRequest>, FarmRequestValidator>();
builder.Services.AddScoped<IRequestValidator<FieldRequest>, FieldRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropTypeRequest>, CropTypeRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropCycleRequest>, CropCycleRequestValidator>();
builder.Services.AddScoped<IRequestValidator<CropPlanRequestCreate>, CropPlanRequestCreateValidator>();
builder.Services.AddScoped<IRequestValidator<CropPlanRequestUpdate>, CropPlanRequestUpdateValidator>();
builder.Services.AddScoped<IRequestValidator<PrePlantingAssessmentRequest>, PrePlantingAssessmentRequestValidator>();
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
builder.Services.AddHttpClient<ISchedulingValidationAIClient, AgenticAIClient>();
// OpenWeatherMap takes the API key as a query parameter, so do not log request URLs for this client.
builder.Services.AddHttpClient<IWeatherService, WeatherService>().RemoveAllLoggers();
builder.Services.AddScoped<IWeatherResourceWorkflowService, WeatherResourceWorkflowService>();
builder.Services.AddScoped<IWorkflowApprovalService, WorkflowApprovalService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.Events = CreateJwtBearerEvents());
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
        {
            return;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim(AuthenticationClaimNames.TokenUse, AuthenticationTokenUses.Access)
        .Build();

    options.AddPolicy(
        AuthorizationPolicyNames.PasswordChange,
        policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(AuthenticationClaimNames.TokenUse, AuthenticationTokenUses.PasswordChange));
});

var securityRateLimits = builder.Configuration
    .GetSection(SecurityRateLimitOptions.SectionName)
    .Get<SecurityRateLimitOptions>() ?? new SecurityRateLimitOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var leaseRetryAfter)
            ? leaseRetryAfter
            : (TimeSpan?)null;

        await ExceptionHandlingMiddleware.WriteProblemAsync(
            context.HttpContext,
            HttpStatusCode.TooManyRequests,
            "RATE_LIMITED",
            "Too many requests.",
            retryAfter);
    };

    AddFixedWindowPolicy(options, RateLimitPolicyNames.Login, securityRateLimits.Login);
    AddFixedWindowPolicy(options, RateLimitPolicyNames.FarmerRegistration, securityRateLimits.FarmerRegistration);
    AddFixedWindowPolicy(options, RateLimitPolicyNames.TemporaryPasswordChange, securityRateLimits.TemporaryPasswordChange);
    AddFixedWindowPolicy(options, RateLimitPolicyNames.AdminReauthentication, securityRateLimits.AdminReauthentication);
    AddFixedWindowPolicy(options, RateLimitPolicyNames.AdminUserManagement, securityRateLimits.AdminUserManagement);
});

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

if (args.Length == 1 && string.Equals(args[0], "bootstrap-admin", StringComparison.OrdinalIgnoreCase))
{
    using var bootstrapScope = app.Services.CreateScope();
    var command = bootstrapScope.ServiceProvider.GetRequiredService<AdminBootstrapCommand>();
    Environment.ExitCode = await command.ExecuteAsync(CancellationToken.None);
    return;
}

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
}

// Local development and the documented HTTP profile run on port 5087. Redirect
// HTTPS only for deployed environments where an HTTPS endpoint is configured.
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseCors("ClientApps");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

static bool IsValid(RateLimitRuleOptions options) =>
    options.PermitLimit > 0 && options.WindowSeconds > 0;

static JwtBearerEvents CreateJwtBearerEvents() =>
    new()
    {
        OnTokenValidated = async context =>
        {
            var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleValue = context.Principal?.FindFirstValue(ClaimTypes.Role);
            var tokenVersionValue = context.Principal?.FindFirstValue(AuthenticationClaimNames.TokenVersion);
            var tokenUse = context.Principal?.FindFirstValue(AuthenticationClaimNames.TokenUse);

            if (!Guid.TryParse(userIdValue, out var userId)
                || !Enum.TryParse<ApplicationRole>(roleValue, out var tokenRole)
                || !int.TryParse(tokenVersionValue, out var tokenVersion)
                || tokenUse is not (AuthenticationTokenUses.Access or AuthenticationTokenUses.PasswordChange))
            {
                FailAuthentication(context, "TOKEN_INVALID_OR_EXPIRED");
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.Id == userId, context.HttpContext.RequestAborted);

            if (user is null
                || user.IsDeleted
                || !user.IsActive
                || user.Role != tokenRole
                || user.TokenVersion != tokenVersion)
            {
                FailAuthentication(context, "SESSION_SECURITY_VERSION_INVALID");
                return;
            }

            if (tokenUse == AuthenticationTokenUses.Access && user.MustChangePassword)
            {
                FailAuthentication(context, "PASSWORD_CHANGE_REQUIRED");
                return;
            }

            if (tokenUse == AuthenticationTokenUses.PasswordChange && !user.MustChangePassword)
            {
                FailAuthentication(context, "TOKEN_INVALID_OR_EXPIRED");
            }
        },
        OnChallenge = async context =>
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.HandleResponse();
            var failureCode = FindAuthenticationSecurityException(context.AuthenticateFailure)?.Code;
            var code = failureCode switch
            {
                "SESSION_SECURITY_VERSION_INVALID" => failureCode,
                "PASSWORD_CHANGE_REQUIRED" => failureCode,
                "TOKEN_INVALID_OR_EXPIRED" => failureCode,
                _ when context.AuthenticateFailure is null => "AUTH_REQUIRED",
                _ => "TOKEN_INVALID_OR_EXPIRED"
            };

            await ExceptionHandlingMiddleware.WriteProblemAsync(
                context.HttpContext,
                HttpStatusCode.Unauthorized,
                code,
                "Authentication is required or the session is no longer valid.");
        },
        OnForbidden = async context =>
        {
            var isPasswordChangeToken =
                context.HttpContext.User.FindFirstValue(AuthenticationClaimNames.TokenUse)
                == AuthenticationTokenUses.PasswordChange;
            await ExceptionHandlingMiddleware.WriteProblemAsync(
                context.HttpContext,
                HttpStatusCode.Forbidden,
                isPasswordChangeToken ? "PASSWORD_CHANGE_REQUIRED" : "ROLE_NOT_AUTHORIZED",
                isPasswordChangeToken
                    ? "The temporary password must be changed before accessing this resource."
                    : "The current role is not authorized for this resource.");
        }
    };

static void FailAuthentication(TokenValidatedContext context, string code)
{
    context.HttpContext.Items[AuthenticationFailureItems.ErrorCode] = code;
    context.Properties.Items[AuthenticationFailureItems.ErrorCode] = code;
    context.Fail(new AuthenticationSecurityException(code));
}

static AuthenticationSecurityException? FindAuthenticationSecurityException(Exception? exception)
{
    while (exception is not null)
    {
        if (exception is AuthenticationSecurityException authenticationSecurityException)
        {
            return authenticationSecurityException;
        }

        exception = exception.InnerException;
    }

    return null;
}

static void AddFixedWindowPolicy(
    RateLimiterOptions options,
    string policyName,
    RateLimitRuleOptions policyOptions)
{
    options.AddPolicy(policyName, context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? remoteAddress
            : $"{userId}:{remoteAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policyOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(policyOptions.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
}

public partial class Program;

internal sealed class AuthenticationSecurityException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
