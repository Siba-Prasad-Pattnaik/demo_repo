using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/documentanalyzer-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.ModelValidatorProviders.Clear();
    options.Filters.Add<ValidationActionFilter>();
});

// API Versioning
builder.Services.AddApiVersioning(config =>
{
    config.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("version"),
        new HeaderApiVersionReader("X-Version"),
        new MediaTypeApiVersionReader("ver")
    );
    config.DefaultApiVersion = new ApiVersion(1, 0);
    config.AssumeDefaultVersionWhenUnspecified = true;
});

builder.Services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// Database Configuration
builder.Services.AddDbContext<DocumentAnalyzerDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(30);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Authentication & Authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"];

    if (string.IsNullOrEmpty(secretKey))
    {
        throw new InvalidOperationException("JWT SecretKey is not configured");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.FromMinutes(5)
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Log.Warning("Authentication failed: {Exception}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Log.Information("Token validated for user: {User}", context.Principal?.Identity?.Name);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserAccess", policy => policy.RequireRole("User", "Admin"));
    options.AddPolicy("DocumentAccess", policy =>
        policy.RequireClaim("permissions", "document:read"));
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("GlobalLimit", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 100;
        limiterOptions.QueueLimit = 10;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddFixedWindowLimiter("AuthLimit", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(15);
        limiterOptions.PermitLimit = 5;
        limiterOptions.QueueLimit = 0;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded", token);
        Log.Warning("Rate limit exceeded for {IP}", context.HttpContext.Connection.RemoteIpAddress);
    };
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCors", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "https://localhost:3000" };

        policy.WithOrigins(allowedOrigins)
              .AllowedHeaders("Content-Type", "Authorization", "X-Requested-With", "X-Version")
              .AllowedMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });

    options.AddPolicy("DevelopmentCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Document Analyzer API",
        Version = "v1",
        Description = "A comprehensive document analysis and processing API",
        Contact = new OpenApiContact
        {
            Name = "Development Team",
            Email = "dev@documentanalyzer.com"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
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

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Application Services Registration
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Repository Pattern
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAnalysisRepository, AnalysisRepository>();

// Background Services
builder.Services.AddHostedService<DocumentProcessingService>();
builder.Services.AddHostedService<CleanupService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DocumentAnalyzerDbContext>("database")
    .AddCheck("storage", () =>
    {
        // Add storage health check logic
        return HealthCheckResult.Healthy("Storage is accessible");
    })
    .AddCheck("external-api", async () =>
    {
        // Add external API health check
        return HealthCheckResult.Healthy("External APIs are responsive");
    });

// Memory Cache
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024;
    options.TrackStatistics = true;
});

// HTTP Client Configuration
builder.Services.AddHttpClient("DocumentAnalyzer", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "DocumentAnalyzer/1.0");
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Analyzer API v1");
        c.RoutePrefix = "swagger";
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    });
    app.UseDeveloperExceptionPage();
    app.UseCors("DevelopmentCors");
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseCors("ProductionCors");
}

// Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline';");

    await next();
});

// Request/Response Logging Middleware
app.Use(async (context, next) =>
{
    Log.Information("Request {Method} {Path} from {IP}",
        context.Request.Method,
        context.Request.Path,
        context.Connection.RemoteIpAddress);

    await next();

    Log.Information("Response {StatusCode} for {Method} {Path}",
        context.Response.StatusCode,
        context.Request.Method,
        context.Request.Path);
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Health Check Endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapControllers().RequireRateLimiting("GlobalLimit");

// Global Exception Handler
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (contextFeature != null)
        {
            Log.Error(contextFeature.Error, "Unhandled exception occurred");

            await context.Response.WriteAsync(new
            {
                error = "An error occurred processing your request",
                statusCode = context.Response.StatusCode,
                requestId = context.TraceIdentifier
            }.ToString());
        }
    });
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
