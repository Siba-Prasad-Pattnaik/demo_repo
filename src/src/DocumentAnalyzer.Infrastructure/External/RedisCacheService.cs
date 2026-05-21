using StackExchange.Redis;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DocumentAnalyzer.Core.Interfaces;

namespace DocumentAnalyzer.Infrastructure.External;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly RedisConfiguration _config;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<RedisConfiguration> config,
        ILogger<RedisCacheService> logger)
    {
        _database = redis.GetDatabase(config.Value.Database);
        _config = config.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await _database.StringGetAsync(GetKey(key));

            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            return null;
        }
    }

    public async Task<string?> GetStringAsync(string key)
    {
        try
        {
            return await _database.StringGetAsync(GetKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting string cache value for key: {Key}", key);
            return null;
        }
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var expiry = expiration ?? TimeSpan.FromMinutes(_config.DefaultExpirationMinutes);

            return await _database.StringSetAsync(GetKey(key), serializedValue, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
            return false;
        }
    }

    public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiration = null)
    {
        try
        {
            var expiry = expiration ?? TimeSpan.FromMinutes(_config.DefaultExpirationMinutes);
            return await _database.StringSetAsync(GetKey(key), value, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting string cache value for key: {Key}", key);
            return false;
        }
    }

    public async Task<bool> RemoveAsync(string key)
    {
        try
        {
            return await _database.KeyDeleteAsync(GetKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(GetKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
            return false;
        }
    }

    public async Task<bool> SetHashAsync(string key, string field, string value, TimeSpan? expiration = null)
    {
        try
        {
            var result = await _database.HashSetAsync(GetKey(key), field, value);

            if (expiration.HasValue)
            {
                await _database.KeyExpireAsync(GetKey(key), expiration.Value);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting hash cache value for key: {Key}, field: {Field}", key, field);
            return false;
        }
    }

    public async Task<string?> GetHashAsync(string key, string field)
    {
        try
        {
            return await _database.HashGetAsync(GetKey(key), field);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hash cache value for key: {Key}, field: {Field}", key, field);
            return null;
        }
    }

    public async Task<long> IncrementAsync(string key, long value = 1, TimeSpan? expiration = null)
    {
        try
        {
            var result = await _database.StringIncrementAsync(GetKey(key), value);

            if (expiration.HasValue)
            {
                await _database.KeyExpireAsync(GetKey(key), expiration.Value);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing cache value for key: {Key}", key);
            return 0;
        }
    }

    private string GetKey(string key)
    {
        return $"{_config.InstanceName}:{key}";
    }
}
