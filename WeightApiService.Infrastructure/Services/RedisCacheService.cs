using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using WeightApiService.Core.Interfaces;

namespace WeightApiService.Infrastructure.Services;

public class RedisCacheService : ICacheService
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisCacheService> _logger;

        public RedisCacheService(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisCacheService> logger)
        {
            _database = connectionMultiplexer.GetDatabase();
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var redisValue = await _database.StringGetAsync(key);
                if (redisValue.IsNullOrEmpty)
                {
                    _logger.LogInformation("Cache miss for key: {CacheKey}", key);
                    return default;
                }
                _logger.LogInformation("Cache hit for key: {CacheKey}", key);
                return JsonSerializer.Deserialize<T>(redisValue.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting data from Redis for key: {CacheKey}", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var stringValue = JsonSerializer.Serialize(value);
                await _database.StringSetAsync(key, stringValue, expiry);
                _logger.LogInformation("Successfully set data to Redis for key: {CacheKey}, expiry: {Expiry}", key, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting data to Redis for key: {CacheKey}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _database.KeyDeleteAsync(key);
                _logger.LogInformation("Successfully removed data from Redis for key: {CacheKey}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing data from Redis for key: {CacheKey}", key);
            }
        }
    }