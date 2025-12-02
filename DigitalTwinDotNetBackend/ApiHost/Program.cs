using Common.Auth;
using Common.Config;
using Common.Http;
using Common.Storage;
using Contracts;
using DigitalTwin.Controllers;
using DigitalTwin.Services;
using DigitalTwin.Streams;
using HealthChecks.NpgSql;
using HealthChecks.Redis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using StateSync;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------
// Configure basic host
// ----------------------------
builder.WebHost.UseUrls("http://0.0.0.0:5251");

// ----------------------------
// Load Options
// ----------------------------
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.Section));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.Section));
builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection(PostgresOptions.Section));
builder.Services.Configure<StateSyncOptions>(builder.Configuration.GetSection(StateSyncOptions.Section));
builder.Services.Configure<RealtimeOptions>(builder.Configuration.GetSection(RealtimeOptions.Section));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.Section));

builder.Services.AddControllers();

// ----------------------------
// Redis
// ----------------------------
builder.Services.AddSingleton<RedisConnection>();
builder.Services.AddSingleton<RedisJsonStore>();

// ----------------------------
// StateSync services
// ----------------------------
builder.Services.AddSingleton<FleetReadService>();
builder.Services.AddHostedService<FleetCacheJob>();
builder.Services.AddSingleton<ShadowReadService>();
builder.Services.AddHostedService<ShadowBootstrapper>();
builder.Services.AddSingleton<RealtimeSessionManager>();
builder.Services.AddHostedService<RealtimeSupervisor>();
builder.Services.AddSingleton<RateLimitGovernor>();
builder.Services.AddSingleton<IShadowEventBus, InMemoryShadowEventBus>();

// ----------------------------
// PostgreSQL + EF Core
// ----------------------------
var pgConn = builder.Configuration["ConnectionStrings:Postgres"]
          ?? builder.Configuration["Postgres:ConnectionString"]
          ?? "Host=postgres;Port=5432;Database=twin;Username=twin;Password=twinpass";

builder.Services.AddDbContext<TwinDbContext>((serviceProvider, opt) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();

    var pgOptions = config.GetSection(PostgresOptions.Section).Get<PostgresOptions>();
    var timeout = pgOptions?.CommandTimeoutSeconds ?? 30;

    opt.UseNpgsql(pgConn, npgsql =>
    {
        npgsql.MigrationsAssembly("ApiHost");   // <-- IMPORTANT
        npgsql.CommandTimeout(timeout);
    });
});

// ----------------------------
// HttpClient + Polly Resilience
// ----------------------------
builder.Services.AddTransient<ProxyAuthHeaderHandler>();

builder.Services.AddHttpClient<ProxyHttpClient>()
    .AddHttpMessageHandler<ProxyAuthHeaderHandler>()
    .AddResilienceHandler("proxy-pipeline", (pipeline, context) =>
    {
        var config = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var opts = config.GetSection(ProxyOptions.Section).Get<ProxyOptions>();
        var retry = opts?.MaxRetries ?? 3;

        var logger = context.ServiceProvider.GetRequiredService<ILogger<ProxyHttpClient>>();
        pipeline.AddPipeline(HttpPolicies.CreateResiliencePolicy(retry, logger));
    });

// ----------------------------
// Health Checks
// ----------------------------
var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("OK"))
    .AddNpgSql(pgConn, name: "postgres")
    .AddRedis(redisConn, name: "redis");

// ----------------------------
// Build App
// ----------------------------
var app = builder.Build();

// Auto-migrate DB on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<TwinDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB migrate failed: " + ex);
    }
}

// ----------------------------
// Static files + Controllers
// ----------------------------
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.MapFallbackToFile("index.html");

// ----------------------------
// Debug endpoints
// ----------------------------
app.MapGet("/api", () => Results.Text("Core Twin API Host — OK\n"));

app.MapGet("/debug/proxy-ping", async (ProxyHttpClient proxy) =>
{
    var ok = await proxy.PingAsync();
    return Results.Json(new { proxyReachable = ok });
});

app.MapPost("/debug/subscribe/{devId}", (string devId, RealtimeSessionManager mgr) =>
{
    mgr.Subscribe(devId);
    return Results.Ok(new { subscribed = devId });
});

app.MapPost("/debug/unsubscribe/{devId}", (string devId, RealtimeSessionManager mgr) =>
{
    mgr.Unsubscribe(devId);
    return Results.Ok(new { unsubscribed = devId });
});

// ----------------------------
// Health Checks
// ----------------------------
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Name == "self" });
app.MapHealthChecks("/health/ready");

// ----------------------------
// Run
// ----------------------------
app.Run();