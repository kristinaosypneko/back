using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using WeightApiService.Core.Interfaces;
using WeightApiService.Infrastructure.Data;
using WeightApiService.Infrastructure.Persistence;
using WeightApiService.Infrastructure.Services;
using WeightMeasurement.Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// 1. Конфигурация (уже подтягивается из appsettings.json по умолчанию)
// IConfiguration configuration = builder.Configuration; // Доступ к конфигурации

// 2. Регистрация DbContext
builder.Services.AddDbContext<TgDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
    // Опционально: настройка для повторных попыток при сбоях подключения к БД
    sqlOptions => sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(30),
        errorCodesToAdd: null)));


// 3. Регистрация Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrEmpty(redisConnectionString))
    {
        logger.LogCritical("Строка подключения Redis не найдена в конфигурации!");
        throw new InvalidOperationException("Строка подключения Redis не найдена.");
    }
    logger.LogInformation("Worker: Попытка подключения к Redis: {RedisConnectionString}", redisConnectionString);
    try
    {
        var connection = ConnectionMultiplexer.Connect(redisConnectionString);
        logger.LogInformation("Worker: Успешное подключение к Redis.");
        connection.ConnectionFailed += (_, e) => logger.LogError(e.Exception, "Redis connection failed: {FailureType}", e.FailureType);
        connection.ConnectionRestored += (_, e) => logger.LogInformation("Redis connection restored: {FailureType}", e.FailureType);
        connection.ErrorMessage += (_, e) => logger.LogError("Redis error message: {Message}", e.Message);
        return connection;
    }
    catch (RedisConnectionException ex)
    {
        logger.LogCritical(ex, "Worker: Критическая ошибка подключения к Redis. {RedisConnectionString}", redisConnectionString);
        throw;
    }
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMeasurementRepository, MeasurementRepository>();
builder.Services.AddSingleton<ICacheService, RedisCacheService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMeasurementService, MeasurementService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();