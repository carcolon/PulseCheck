using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Api.Hubs;
using PulseCheck.Api.Models;
using PulseCheck.Api.Notifications;
using PulseCheck.Api.Services;
using PulseCheck.Application.Models;
using PulseCheck.Application.Ports;
using PulseCheck.Application.Services;
using PulseCheck.Infrastructure;
using PulseCheck.Infrastructure.Persistence;

var (environmentName, forwardedArgs) = ResolveEnvironment(args);

if (!string.IsNullOrWhiteSpace(environmentName))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environmentName);
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environmentName);
}

var builder = WebApplication.CreateBuilder(forwardedArgs);

builder.Services.Configure<PulseCheckOptions>(
    builder.Configuration.GetSection(PulseCheckOptions.SectionName));
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection("EmailSettings"));

var pulseOptions = builder.Configuration
    .GetSection(PulseCheckOptions.SectionName)
    .Get<PulseCheckOptions>() ?? new PulseCheckOptions();
var useSqlServer = pulseOptions.DatabaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
var useHangfire = useSqlServer;
var useTransformationalLeaderAssignmentSync =
    pulseOptions.TransformationalLeaderAssignmentSync.Enabled && useSqlServer;

builder.Services.AddPulseCheckInfrastructure(builder.Configuration, pulseOptions.DatabaseProvider);
if (useHangfire)
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            ResolvePulseCheckConnectionString(builder.Configuration),
            new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.FromSeconds(15),
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
                PrepareSchemaIfNecessary = true
            }));
    builder.Services.AddHangfireServer();
}

builder.Services.AddHttpClient<GraphEmployeeIdentityResolver>();
builder.Services.AddScoped<IEmployeeIdentityResolver, GraphEmployeeIdentityResolver>();
builder.Services.AddScoped<IEmployeeOperationsProfileResolver, FabricEmployeeOperationsProfileResolver>();
builder.Services.AddScoped<ILeaderAlertEmailService, AcsLeaderAlertEmailService>();
builder.Services.AddScoped<PulseCheckService>();
builder.Services.AddScoped<TransformationalLeaderService>();
builder.Services.AddScoped<TransformationalLeaderAuthService>();
builder.Services.AddScoped<TransformationalLeaderDashboardService>();
builder.Services.AddScoped<TransformationalLeaderAssignmentSyncService>();
builder.Services.AddScoped<TransformationalLeaderAssignmentSyncJob>();
builder.Services.AddScoped<TlExportJobRunner>();
builder.Services.AddScoped<TlExportFileStore>();
builder.Services.AddScoped<EmployeeProfileBackfillService>();
builder.Services.AddHostedService<EmployeeProfileBackfillHostedService>();
builder.Services.AddScoped<INotificationPublisher, ApiNotificationPublisher>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EntraAccessTokenValidator>();
builder.Services.AddHttpClient<EntraAuthorizationCodeFlow>();
builder.Services.AddSingleton<AdminLoginAttemptLimiter>();
builder.Services
    .AddAuthentication(AdminTokenAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, AdminTokenAuthenticationHandler>(
        AdminTokenAuthenticationDefaults.Scheme,
        _ => { })
    .AddScheme<AuthenticationSchemeOptions, TransformationalLeaderAuthenticationHandler>(
        TransformationalLeaderAuthenticationDefaults.Scheme,
        _ => { })
    .AddScheme<AuthenticationSchemeOptions, AgentTokenAuthenticationHandler>(
        AgentTokenAuthenticationDefaults.Scheme,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveClientPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = EnsurePositive(pulseOptions.RateLimits.AuthPermitLimitPerMinute, 60),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("admin-api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveClientPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = EnsurePositive(pulseOptions.RateLimits.AdminApiPermitLimitPerMinute, 300),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("agent-api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveClientPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = EnsurePositive(pulseOptions.RateLimits.AgentApiPermitLimitPerMinute, 300),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection($"{PulseCheckOptions.SectionName}:AllowedOrigins")
            .Get<string[]>();

        if (allowedOrigins is { Length: > 0 })
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<PulseCheckDbInitializer>();
    await initializer.InitializeAsync();
}

if (useTransformationalLeaderAssignmentSync)
{
    try
    {
        var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
        var runHourUtc = Math.Clamp(pulseOptions.TransformationalLeaderAssignmentSync.DailyRunHourUtc, 0, 23);
        recurringJobs.AddOrUpdate<TransformationalLeaderAssignmentSyncJob>(
            TransformationalLeaderAssignmentSyncJob.JobId,
            job => job.RunAsync(),
            Cron.Daily(runHourUtc),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to schedule Transformational Leader assignment sync recurring job.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("web");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var isUnsafeMethod = HttpMethods.IsPost(context.Request.Method) ||
                         HttpMethods.IsPut(context.Request.Method) ||
                         HttpMethods.IsPatch(context.Request.Method) ||
                         HttpMethods.IsDelete(context.Request.Method);
    var usesAdminCookie = context.Request.Cookies.ContainsKey(AdminSessionCookie.Name);
    var hasBearerHeader = context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    var isAuthLoginEndpoint = context.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
                              context.Request.Path.Equals("/api/auth/entra", StringComparison.OrdinalIgnoreCase) ||
                              context.Request.Path.Equals("/api/auth/entra/callback", StringComparison.OrdinalIgnoreCase);
    var isSignalREndpoint = context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);
    var isHangfireEndpoint = context.Request.Path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase);

    if (isUnsafeMethod &&
        !isAuthLoginEndpoint &&
        !isSignalREndpoint &&
        !isHangfireEndpoint &&
        usesAdminCookie &&
        !hasBearerHeader &&
        !HasValidCsrfToken(context))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { message = "CSRF validation failed." });
        return;
    }

    await next();
});
app.UseRateLimiter();
app.UseAuthorization();

if (useHangfire)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter()]
    });
}

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications")
    .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
    {
        AuthenticationSchemes = AgentTokenAuthenticationDefaults.Scheme
    })
    .RequireRateLimiting("agent-api");
app.MapHub<AdminNotificationHub>("/hubs/admin-notifications").RequireRateLimiting("admin-api");
app.MapHub<TlNotificationHub>("/hubs/tl-notifications").RequireRateLimiting("admin-api");

app.Run();

static (string? EnvironmentName, string[] ForwardedArgs) ResolveEnvironment(string[] args)
{
    if (args.Length == 0)
    {
        return (null, args);
    }

    var firstArg = args[0].Trim().ToLowerInvariant();

    return firstArg switch
    {
        "dev" => ("Development", args.Skip(1).ToArray()),
        "prod" => ("Production", args.Skip(1).ToArray()),
        _ => (null, args)
    };
}

static string ResolveClientPartition(HttpContext httpContext)
{
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static bool HasValidCsrfToken(HttpContext httpContext)
{
    if (!httpContext.Request.Cookies.TryGetValue(AdminSessionCookie.CsrfCookieName, out var cookieToken) ||
        !httpContext.Request.Headers.TryGetValue(AdminSessionCookie.CsrfHeaderName, out var headerTokenValues))
    {
        return false;
    }

    var headerToken = headerTokenValues.ToString();
    if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(headerToken))
    {
        return false;
    }

    var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
    var headerBytes = Encoding.UTF8.GetBytes(headerToken);
    return cookieBytes.Length == headerBytes.Length &&
           CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
}

static string ResolvePulseCheckConnectionString(IConfiguration configuration)
    => configuration.GetConnectionString("PulseCheckDb")
       ?? "Server=localhost\\SQLEXPRESS;Database=PulseCheckDb;Trusted_Connection=True;TrustServerCertificate=True;";

static int EnsurePositive(int value, int fallback)
{
    return value > 0 ? value : fallback;
}
