using DocumentAnalyzer.Core;
using DocumentAnalyzer.Core.Interfaces;
using DocumentAnalyzer.Infrastructure;
using DocumentAnalyzer.Infrastructure.Data;
using DocumentAnalyzer.Infrastructure.Services;
using DocumentAnalyzer.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/documentanalyzer-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container
var services = builder.Services;
var configuration = builder.Configuration;

// Add Controllers
services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer()));
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = true;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

// Database Configuration
services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("DocumentAnalyzer.Infrastructure"));
});

// Redis Configuration
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
});

// Add API versioning
services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion = new ApiVersion(1, 0);
    opt.AssumeDefaultVersionWhenUnspecified = true;
    opt.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Version"),
        new MediaTypeApiVersionReader("ver"));
});

services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// Add Authentication
var jwtSettings = configuration.GetSection("Authentication:Jwt");
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? configuration["Jwt:Issuer"],
        ValidAudience = jwtSettings["Audience"] ?? configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? configuration["Jwt:SecretKey"]!)),
        ClockSkew = TimeSpan.Parse(jwtSettings["ClockSkew"] ?? "00:00:00")
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Log.Warning("Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Log.Information("Token validated for user: {User}",
                context.Principal?.Identity?.Name ?? "Unknown");
            return Task.CompletedTask;
        }
    };
});

// Add Authorization
services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("RequireUserRole", policy =>
        policy.RequireRole("User", "Admin"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

// Add CORS
var corsSettings = configuration.GetSection("Cors");
services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>() ?? configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" };
        var allowedMethods = corsSettings.GetSection("AllowedMethods").Get<string[]>() ?? Array.Empty<string>();
        var allowedHeaders = corsSettings.GetSection("AllowedHeaders").Get<string[]>() ?? Array.Empty<string>();
        var allowCredentials = corsSettings.GetValue<bool>("AllowCredentials");

        if (allowedOrigins.Any())
            policy.WithOrigins(allowedOrigins);
        else
            policy.AllowAnyOrigin();

        if (allowedMethods.Any())
            policy.WithMethods(allowedMethods);
        else
            policy.AllowAnyMethod();

        if (allowedHeaders.Any() && !allowedHeaders.Contains("*"))
            policy.WithHeaders(allowedHeaders);
        else
            policy.AllowAnyHeader();

        if (allowCredentials)
            policy.AllowCredentials();
    });

    options.AddPolicy("DefaultPolicy", policy =>
    {
        policy.WithOrigins(configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Rate Limiting
services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    var rateLimitConfig = configuration.GetSection("RateLimiting");
    var generalRules = rateLimitConfig.GetSection("GeneralRules");

    options.AddFixedWindowLimiter("GeneralApi", limiterOptions =>
    {
        limiterOptions.PermitLimit = generalRules.GetValue<int>("PermitLimit");
        limiterOptions.Window = TimeSpan.Parse(generalRules["Window"]!);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });

    var uploadRules = rateLimitConfig.GetSection("ApiRules:Upload");
    options.AddFixedWindowLimiter("UploadApi", limiterOptions =>
    {
        limiterOptions.PermitLimit = uploadRules.GetValue<int>("PermitLimit");
        limiterOptions.Window = TimeSpan.Parse(uploadRules["Window"]!);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
    });

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

// Add Health Checks
services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddDbContextCheck<ApplicationDbContext>()
    .AddRedis(configuration.GetConnectionString("Redis"))
    .AddUrlGroup(new Uri(configuration["ExternalServices:StorageHealthCheck"]), "storage");

// Add Swagger/OpenAPI
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(c =>
{
    var swaggerConfig = configuration.GetSection("Swagger");
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = swaggerConfig["Title"] ?? "Document Analyzer API",
        Version = swaggerConfig["Version"] ?? "v1",
        Description = swaggerConfig["Description"] ?? "API for document analysis and processing",
        Contact = new OpenApiContact
        {
            Name = swaggerConfig["ContactName"],
            Email = swaggerConfig["ContactEmail"]
        }
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
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

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add Application Insights
if (!string.IsNullOrEmpty(configuration["ApplicationInsights:ConnectionString"]))
{
    services.AddApplicationInsightsTelemetry(configuration);
}

// Add Core and Infrastructure services
services.AddCoreServices(configuration);
services.AddInfrastructureServices(configuration);

// Application Services
services.AddScoped<IDocumentService, DocumentService>();
services.AddScoped<IAnalysisService, AnalysisService>();
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<ISearchService, SearchService>();
services.AddScoped<IStorageService, StorageService>();
services.AddScoped<INotificationService, NotificationService>();

// Background Services
services.AddHostedService<DocumentProcessingService>();

// Add AutoMapper
services.AddAutoMapper(Assembly.GetExecutingAssembly());

// Add MediatR
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Add FluentValidation
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// Memory Cache
services.AddMemoryCache();

// HTTP Client Factory
services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Analyzer API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
        c.EnableValidator();
    });
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Add Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "Handled {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode > 499
            ? LogEventLevel.Error
            : LogEventLevel.Information;
});

// Security headers
app.UseSecurityHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();

// HTTPS and redirections
app.UseHttpsRedirection();

// Rate limiting
app.UseRateLimiter();

// CORS
app.UseCors("DefaultCorsPolicy");
app.UseCors("DefaultPolicy");

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map health checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                exception = x.Value.Exception?.Message,
                duration = x.Value.Duration.ToString()
            }),
            duration = report.TotalDuration.ToString()
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

app.UseHealthChecks("/health/ready");

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Application is shutting down...");
});

Log.Information("Starting Document Analyzer API...");

try
{
    Log.Information("Starting Document Analyzer API");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Helper classes
public class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string TransformOutbound(object value)
    {
        return value?.ToString()?.ToLowerInvariant() ?? string.Empty;
    }
}

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        await _next(context);
    }
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
