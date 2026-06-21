using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.RateLimiting;
using my_comment_api.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using my_comment_api.Data;
using my_comment_api.Services;
using Serilog;
using my_comment_api.Options;
using Microsoft.AspNetCore.Authorization;
using my_comment_api.Authorization;
using MediatR;
using my_comment_api.Behavior;
using Polly;
using System.IO.Pipelines;

namespace my_comment_api.Extensions;

public static class ServiceExtensions
{
    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.AddSerilog();
        builder.AddControllerStack();
        builder.AddSwagger();
        builder.AddDatabase();
        builder.AddJwtAuthentication();
        builder.AddValidation();
        builder.AddCorsPolicy();
        builder.Services.AddScoped<TokenService>();
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(ServiceExtensions).Assembly));
        builder.Services.AddOptions<JwtSettings>().Bind(builder.Configuration.GetSection(nameof(JwtSettings)));
        builder.Services.AddSingleton<IAuthorizationHandler, CommentOwnerHandler>();
        builder.Services.AddHttpClient<ModerationService>();
        builder.Services.AddOptions<ClaudeSettings>().Bind(builder.Configuration.GetSection("Claude"));
        builder.Services.AddStackExchangeRedisCache(options =>
            options.Configuration = builder.Configuration.GetConnectionString("Redis"));
        builder.Services.AddScoped<CacheService>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        builder.Services.AddScoped<FlakeyExternalService>();
        builder.Services.AddResiliencePipeline("flake-retry", pipeline =>
        {
            pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 4,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = Polly.DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new Polly.PredicateBuilder().Handle<HttpRequestException>()
            });
        });
        builder.Services.AddResiliencePipeline("circuit-breaker", pipeline =>
        {
            pipeline.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
            });

        });
        builder.AddRateLimiting();
        return builder;
    }

    public static void AddCorsPolicy(this WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins("http://localhost:4200", "http://51.8.228.236")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()));
    }

    private static void AddDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        );
    }

    private static void AddControllerStack(this WebApplicationBuilder builder)
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<AuditFilter>();
            options.Filters.Add<IdempotencyFilter>();
            options.Filters.Add<ETagFilter>();
        }).AddXmlSerializerFormatters();
        builder.Services.AddSignalR();
    }

    private static void AddSerilog(this WebApplicationBuilder builder) =>
        builder.Host.UseSerilog((context, config) =>
        {
            config
                .ReadFrom.Configuration(context.Configuration)
                .Destructure.ByTransforming<LoginRequest>(r => new
                {
                    r.Username,
                    Password = "***"
                });
        });

    private static void AddSwagger(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Comments API",
                Version = "v1",
                Description = "A simple comments API with authentication"
            });

            // Add JWT authorization button to Swagger UI
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token. Example: eyJhbGci..."
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

    }

    private static void AddJwtAuthentication(this WebApplicationBuilder builder)
    {
        var jwtSettings = builder.Configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>()!;
        var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            // Fall back to the cookie when no Authorization header is present.
            // This lets browser clients (Angular app) use HttpOnly cookies
            // without needing to manually attach the token to every request.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    if (string.IsNullOrEmpty(ctx.Token) &&
                        ctx.Request.Cookies.TryGetValue("access_token", out var cookieToken))
                    {
                        ctx.Token = cookieToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy =>
            {
                policy.RequireRole("Admin");
            });

            options.AddPolicy("Author", policy =>
            {
                policy.RequireRole("Author");
            });

            options.AddPolicy("CreateComment", policy =>
            {
                policy.RequireRole("Admin", "Author");
            });
        });
    }

    private static void AddValidation(this WebApplicationBuilder builder)
    {
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssembly(typeof(ServiceExtensions).Assembly);
    }

    private static void AddRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            // global fallback — 100 requests per minute per IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            // named policy for auth endpoints — stricter: 10 per minute per IP
            options.AddFixedWindowLimiter("auth", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromMinutes(1);
            });

            options.RejectionStatusCode = 429; // Too Many Requests
        });
    }

}