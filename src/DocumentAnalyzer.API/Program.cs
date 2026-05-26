using DocumentAnalyzer.Core.Interfaces;
using DocumentAnalyzer.Infrastructure.Data;
using DocumentAnalyzer.Infrastructure.Storage;
using DocumentAnalyzer.Infrastructure.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
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

    // Add services to the container
    builder.Services.AddControllers(options =>
    {
        options.ModelValidatorProviders.Clear();
        options.Filters.Add<ValidationActionFilter>();
        options.Filters.Add<GlobalExceptionFilter>();
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

    // Configure Entity Framework
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly("DocumentAnalyzer.Infrastructure");
        });
        options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());

        if (builder.Environment.IsDevelopment())
        {
            options.EnableDetailedErrors();
        }
    });

    // Database Configuration for DocumentAnalyzerDbContext
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

    // Configure JWT Authentication
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var jwtConfig = builder.Configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? jwtConfig["Key"];

        if (string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException("JWT SecretKey/Key is not configured");
        }

        options.SaveToken = true;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? jwtConfig["Issuer"],
            ValidAudience = jwtSettings["Audience"] ?? jwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
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
                Log.Information("Token validated for user: {UserId}", 
                    context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                    context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

    // Configure Authorization
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("UserAccess", policy => 
            policy.RequireAssertion(context =>
                context.User.IsInRole("User") || 
                context.User.IsInRole("Admin") || 
                context.User.Identity.IsAuthenticated));
        options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
        options.AddPolicy("DocumentAccess", policy =>
            policy.RequireAssertion(context =>
                context.User.HasClaim("permission", "documents.read") ||
                context.User.HasClaim("permissions", "document:read") ||
                context.User.HasClaim("document_access", "read") ||
                context.User.HasClaim("document_access", "write") ||
                context.User.IsInRole("Admin")));
    });

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DocumentAnalyzerPolicy", policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                policy.WithOrigins(allowedOrigins)
                      .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                      .WithHeaders("Content-Type", "Authorization", "X-Requested-With", "X-Version")
                      .AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            }
        });

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

    // Configure Rate Limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("ApiRateLimit", limiterOptions =>
        {
            limiterOptions.PermitLimit = builder.Configuration.GetValue<int>("RateLimit:PermitLimit", 100);
            limiterOptions.Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimit:WindowMinutes", 1));
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = builder.Configuration.GetValue<int>("RateLimit:QueueLimit", 10);
        });

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
            Log.Warning("Rate limit exceeded for {Endpoint} from {IP}",
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);

            context.HttpContext.Response.StatusCode = 429;
            await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", token);
        };
    });

    // Configure Response Compression
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<GzipCompressionProvider>();
    });

    // Configure Memory Cache
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1024;
        options.TrackStatistics = true;
    });
    builder.Services.AddResponseCaching();

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

    // Configure Swagger/OpenAPI
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
                Name = "Document Analyzer Team",
                Email = "support@documentanalyzer.com"
            }
        });

        // Add JWT authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
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
        await next();
    });

    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseResponseCompression();
    app.UseResponseCaching();

    app.UseCors("DocumentAnalyzerPolicy");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

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

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(3, TimeSpan.FromMinutes(1));
}
