using Common.Auth;
using Common.Config;
using Common.Http;
using Common.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using HealthChecks.NpgSql;
using HealthChecks.Redis;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions;
using DigitalTwin.Controllers;
using DigitalTwin.Services;
using StateSync;
using DigitalTwin.Streams;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.Section));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.Section));
builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection(PostgresOptions.Section));
builder.Services.Configure<StateSyncOptions>(builder.Configuration.GetSection(StateSyncOptions.Section));
builder.Services.Configure<RealtimeOptions>(builder.Configuration.GetSection(RealtimeOptions.Section));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.Section));

builder.Services.AddControllers();

builder.Services.AddSingleton<RedisConnection>();
builder.Services.AddSingleton<RedisJsonStore>();
builder.Services.AddSingleton<FleetReadService>();
builder.Services.AddHostedService<FleetCacheJob>();
builder.Services.AddSingleton<ShadowReadService>();
builder.Services.AddHostedService<ShadowBootstrapper>();
builder.Services.AddSingleton<RealtimeSessionManager>();
builder.Services.AddHostedService<RealtimeSupervisor>();
builder.Services.AddSingleton<RateLimitGovernor>();
builder.Services.AddSingleton<IShadowEventBus, InMemoryShadowEventBus>();

// var pg = builder.Configuration.GetSection(PostgresOptions.Section).Get<PostgresOptions>()!;
// builder.Services.AddDbContext<TwinDbContext>(opt => opt.UseNpgsql(pg.ConnectionString, o => o.CommandTimeout(pg.CommandTimeoutSeconds)));

builder.Services.AddDbContext<TwinDbContext>((serviceProvider, opt) =>
    {
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var pgConn = config["ConnectionStrings:Postgres"]
                ?? config["Postgres:ConnectionString"]
                ?? "Host=postgres;Port=5432;Database=twin;Username=twin;Password=twinpass";
        
        var pgOptions = config.GetSection(PostgresOptions.Section).Get<PostgresOptions>();
        var timeout = pgOptions?.CommandTimeoutSeconds ?? 30;
        
        opt.UseNpgsql(pgConn, o => o.CommandTimeout(timeout));
    });

// builder.Services.AddTransient<ProxyAuthHeaderHandler>();
// builder.Services.AddHttpClient<ProxyHttpClient>()
//     .AddHttpMessageHandler<ProxyAuthHeaderHandler>()
//     .AddResilienceHandler("proxy-pipeline", (pipelineBuilder, context) =>
//     {
//         var opts = context.ServiceProvider
//             .GetRequiredService<IConfiguration>()
//             .GetSection(ProxyOptions.Section)
//             .Get<ProxyOptions>()!;
//         var logger = context.ServiceProvider.GetRequiredService<ILogger<ProxyHttpClient>>();
//         pipelineBuilder.AddPipeline(HttpPolicies.CreateResiliencePolicy(opts.MaxRetries, logger));
//     });

builder.Services.AddTransient<ProxyAuthHeaderHandler>();
builder.Services.AddHttpClient<ProxyHttpClient>()
    .AddHttpMessageHandler<ProxyAuthHeaderHandler>()
    .AddResilienceHandler("proxy-pipeline", (pipelineBuilder, context) =>
    {
        var config = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var opts = config.GetSection(ProxyOptions.Section).Get<ProxyOptions>();
        var maxRetries = opts?.MaxRetries ?? 3;
        
        var logger = context.ServiceProvider.GetRequiredService<ILogger<ProxyHttpClient>>();
        pipelineBuilder.AddPipeline(HttpPolicies.CreateResiliencePolicy(maxRetries, logger));
    });

var config = builder.Configuration;

var pgConn = config["ConnectionStrings:Postgres"]
          ?? config["Postgres:ConnectionString"]
          ?? "Host=postgres;Port=5432;Database=twin;Username=twin;Password=twinpass";

var redisConn = config["Redis:ConnectionString"] ?? "redis:6379";

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("OK"))
    .AddNpgSql(pgConn, name: "postgres")
    .AddRedis(redisConn, name: "redis");
    
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<Common.Storage.TwinDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB migrate failed: " + ex);
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.MapGet("/", () => Results.Text("Core Twin API Host (Phase 1) — OK\n"));
app.MapGet("/debug/proxy-ping", async (ProxyHttpClient proxy) =>
{
    var ok = await proxy.PingAsync();
    return Results.Json(new { proxyReachable = ok });
});

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Name == "self" });
app.MapHealthChecks("/health/ready");

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

// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<TwinDbContext>();
//     await db.Database.MigrateAsync();
// }

app.Run();