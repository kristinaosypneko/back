using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using WeightApiService.Core.Interfaces;
using WeightApiService.Infrastructure.Data;
using WeightApiService.Infrastructure.Persistence;
using WeightApiService.Infrastructure.Services;
using WeigthApiService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TgDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrEmpty(redisConnectionString))
{
    Console.WriteLine("Redis connection string not found in configuration. Using default 'redis:6379' for Docker or 'localhost:6379' for local.");
    redisConnectionString = "127.0.0.1:6379";
}

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Attempting to connect to Redis with connection string: {RedisConnectionString}", redisConnectionString);
        var connection = ConnectionMultiplexer.Connect(redisConnectionString);
        logger.LogInformation("Successfully connected to Redis.");
        return connection;
    }
    catch (RedisConnectionException ex)
    {
        logger.LogError(ex, "Failed to connect to Redis with connection string: {RedisConnectionString}", redisConnectionString);
        throw;
    }
});

builder.Services.AddHostedService<HttpRequestMetricsLogger>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMeasurementRepository, MeasurementRepository>();

builder.Services.AddSingleton<ICacheService, RedisCacheService>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMeasurementService, MeasurementService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<TgDbContext>(
        name: "database_health_check",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "postgresql" });

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Weight API Service", 
        Version = "v1",
        Description = "API для управления измерениями веса"
    });
});



var app = builder.Build();

app.Use(async (context, next) =>
{
    Interlocked.Increment(ref HttpRequestMetricsLogger.HttpRequestsLastInterval);
    await next.Invoke();
});

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Weight API Service V1");
        c.RoutePrefix = string.Empty; // Делает Swagger доступным по корневому URL
    });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        var result = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.ToString(),
                    exception = e.Value.Exception?.Message
                }),
                totalDuration = report.TotalDuration.ToString()
            },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
        );
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(result);
    }
});

app.MapControllers();

app.Run();