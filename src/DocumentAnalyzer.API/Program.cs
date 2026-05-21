using DocumentAnalyzer.Core.Interfaces;
using DocumentAnalyzer.Infrastructure.Data;
using DocumentAnalyzer.Infrastructure.Storage;
using DocumentAnalyzer.Infrastructure.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text;
using FluentValidation;
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

try
{
    Log.Information("Starting Document Analyzer API");

    // Add services to the container
    var services = builder.Services;
    var configuration = builder.Configuration;

    // Database Configuration
    services.AddDbContext<ApplicationDbContext>(options =>
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(connectionString, b =>
        {
            b.MigrationsAssembly("DocumentAnalyzer.Infrastructure");
            b.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(30), null);
        });
    });

    // Identity Configuration
    services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // JWT Authentication
    var jwtSettings = configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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
                Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Debug("JWT Token validated for user: {User}", context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

    // Authorization Policies
    services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
        options.AddPolicy("DocumentAccess", policy =>
            policy.RequireClaim("document_access", "read", "write"));
    });

    // Rate Limiting
    services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("ApiPolicy", limiterOptions =>
        {
            limiterOptions.PermitLimit = int.Parse(configuration["RateLimit:PermitLimit"] ?? "100");
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 10;
        });

        options.AddSlidingWindowLimiter("UploadPolicy", limiterOptions =>
        {
            limiterOptions.PermitLimit = 10;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.SegmentsPerWindow = 4;
        });

        options.OnRejected = (context, cancellationToken) =>
        {
            Log.Warning("Rate limit exceeded for {Endpoint} from {IP}",
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return new ValueTask();
        };
    });

    // CORS Configuration
    services.AddCors(options =>
    {
        options.AddPolicy("AllowedOrigins", policy =>
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000" };

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });
    });

    // Controllers and API Configuration
    services.AddControllers(options =>
    {
        options.SuppressAsyncSuffixInActionNames = false;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors)
                .Select(x => x.ErrorMessage);

            return new BadRequestObjectResult(new
            {
                Message = "Validation failed",
                Errors = errors
            });
        };
    });

    // Validation
    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

    // Health Checks
    services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database")
        .AddCheck("storage", () =>
        {
            // Custom health check for storage service
            return HealthCheckResult.Healthy("Storage service is running");
        })
        .AddCheck("external-apis", () =>
        {
            // Custom health check for external APIs
            return HealthCheckResult.Healthy("External APIs are accessible");
        });

    // HTTP Clients with Polly
    services.AddHttpClient("ExternalAPI", client =>
    {
        client.BaseAddress = new Uri(configuration["ExternalAPI:BaseUrl"] ?? "https://api.example.com");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

    // Swagger/OpenAPI
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Document Analyzer API",
            Version = "v1",
            Description = "API for document analysis and management",
            Contact = new OpenApiContact
            {
                Name = "Document Analyzer Team",
                Email = "support@documentanalyzer.com"
            }
        });

        // Include XML comments
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }

        // JWT Authentication in Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
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
                new string[] {}
            }
        });
    });

    // Application Services (will be implemented by Infrastructure layer)
    // These registrations assume interfaces will be available from referenced projects
    services.AddScoped<IDocumentService, DocumentService>();
    services.AddScoped<IAnalysisService, AnalysisService>();
    services.AddScoped<ISearchService, SearchService>();
    services.AddScoped<IStorageService, StorageService>();
    services.AddScoped<IAuthService, AuthService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Analyzer API V1");
            c.RoutePrefix = string.Empty; // Serve Swagger at root
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.EnableFilter();
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

        if (!app.Environment.IsDevelopment())
        {
            context.Response.Headers.Add("Strict-Transport-Security",
                "max-age=31536000; includeSubDomains; preload");
        }

        await next();
    });

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseRateLimiter();
    app.UseCors("AllowedOrigins");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers().RequireRateLimiting("ApiPolicy");
    app.MapHealthChecks("/health").AllowAnonymous();

    // Global exception handling
    app.UseExceptionHandler(appError =>
    {
        appError.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var contextFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (contextFeature != null)
            {
                Log.Error(contextFeature.Error, "Unhandled exception occurred");

                await context.Response.WriteAsync(new
                {
                    StatusCode = context.Response.StatusCode,
                    Message = app.Environment.IsDevelopment()
                        ? contextFeature.Error.Message
                        : "An error occurred processing your request."
                }.ToString() ?? "{}");
            }
        });
    });

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

// Helper methods for Polly policies
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, duration, retryCount, context) =>
            {
                Log.Warning("HTTP retry attempt {RetryCount} after {Duration}ms", retryCount, duration.TotalMilliseconds);
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (result, duration) =>
            {
                Log.Warning("Circuit breaker opened for {Duration}ms", duration.TotalMilliseconds);
            },
            onReset: () =>
            {
                Log.Information("Circuit breaker reset");
            });
}

// Placeholder service implementations (to be moved to Infrastructure layer)
public class DocumentService : IDocumentService { }
public class AnalysisService : IAnalysisService { }
public class SearchService : ISearchService { }
public class StorageService : IStorageService { }
public class AuthService : IAuthService { }
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
}

// Placeholder interfaces (to be defined in Core layer)
public interface IDocumentService { }
public interface IAnalysisService { }
public interface ISearchService { }
public interface IStorageService { }
public interface IAuthService { }
