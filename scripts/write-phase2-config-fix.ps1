$program = @'
using System.Text;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Middleware;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Shared;
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

var reactUrl = builder.Configuration["App:ReactUrl"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApps", policy =>
    {
        policy.WithOrigins(reactUrl)
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
        await SeedData.SeedAsync(dbContext);
    }
}

app.UseHttpsRedirection();
app.UseCors("ClientApps");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;
'@

$backendEnv = @'
# Database
ConnectionStrings__DefaultConnection=

# JWT
Jwt__Secret=local-development-secret-change-before-production-32chars
Jwt__Issuer=AgriAssist
Jwt__Audience=AgriAssistUsers
Jwt__ExpiryMinutes=60

# Cloudinary
Cloudinary__CloudName=
Cloudinary__ApiKey=
Cloudinary__ApiSecret=

# Weather integration
Weather__ApiKey=bbd5a96f99bf034f14c8713fe55496bc
Weather__BaseUrl=https://api.openweathermap.org/data/2.5

# Internal Agentic AI service
AI__ServiceUrl=http://localhost:8001
AI__ServiceToken=

# Token used by the AI service when calling allow-listed internal ASP.NET tool endpoints
AI__ToolToken=

# CORS / client
App__ReactUrl=http://localhost:5173
'@

Set-Content -LiteralPath "backend/AgriAssist.Api/Program.cs" -Value $program -Encoding ASCII
Set-Content -LiteralPath "backend/AgriAssist.Api/.env" -Value $backendEnv -Encoding ASCII
