using DocumentAnalyzer.Core.Interfaces;
using DocumentAnalyzer.Infrastructure.Data;
using DocumentAnalyzer.Infrastructure.Services;
using DocumentAnalyzer.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/documentanalyzer-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Database Configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("DocumentAnalyzer.Infrastructure"));
});

// Redis Configuration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Authentication & Authorization
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"])),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("DocumentUpload", configure =>
    {
        configure.PermitLimit = 10;
        configure.Window = TimeSpan.FromMinutes(1);
        configure.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        configure.QueueLimit = 5;
    });

    options.AddFixedWindowLimiter("Analysis", configure =>
    {
        configure.PermitLimit = 50;
        configure.Window = TimeSpan.FromMinutes(1);
        configure.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        configure.QueueLimit = 10;
    });
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// API Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Document Analyzer API",
        Version = "v1",
        Description = "API for document analysis and processing"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddRedis(builder.Configuration.GetConnectionString("Redis"))
    .AddUrlGroup(new Uri(builder.Configuration["ExternalServices:StorageHealthCheck"]), "storage");

// Application Services (These will be defined in Infrastructure layer)
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Background Services
builder.Services.AddHostedService<DocumentProcessingService>();

// Memory Cache
builder.Services.AddMemoryCache();

// HTTP Client Factory
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Analyzer API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

// Security Headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// Rate Limiting
app.UseRateLimiter();

// CORS
app.UseCors("DefaultPolicy");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Request Logging
app.UseSerilogRequestLogging();

// Health Checks
app.UseHealthChecks("/health");
app.UseHealthChecks("/health/ready");

// API Controllers
app.MapControllers();

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Application is shutting down...");
});

try
{
    Log.Information("Starting Document Analyzer API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
