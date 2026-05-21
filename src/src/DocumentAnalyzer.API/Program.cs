using DocumentAnalyzer.Core;
using DocumentAnalyzer.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
        ClockSkew = TimeSpan.Parse(jwtSettings["ClockSkew"]!)
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
});

// Add CORS
var corsSettings = configuration.GetSection("Cors");
services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
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
});

// Add Rate Limiting
services.AddRateLimiter(options =>
{
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
});

// Add Health Checks
services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddDbContextCheck<DocumentAnalyzer.Infrastructure.Data.ApplicationDbContext>();

// Add Swagger/OpenAPI
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(c =>
{
    var swaggerConfig = configuration.GetSection("Swagger");
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = swaggerConfig["Title"],
        Version = swaggerConfig["Version"],
        Description = swaggerConfig["Description"],
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

// Add AutoMapper
services.AddAutoMapper(Assembly.GetExecutingAssembly());

// Add MediatR
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Add FluentValidation
services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

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

// HTTPS and redirections
app.UseHttpsRedirection();

// CORS
app.UseCors("DefaultCorsPolicy");

// Rate limiting
app.UseRateLimiter();

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

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Application is shutting down...");
});

Log.Information("Starting Document Analyzer API...");

try
{
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
    public string? TransformOutbound(object? value)
    {
        return value?.ToString()?.ToLowerInvariant();
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Add("Content-Security-Policy",
                "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline';");

            await next();
        });
    }
}
