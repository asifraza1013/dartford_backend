using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace inflan_api.Services.Payment;

public class PlatformSettingsService : IPlatformSettingsService
{
    private readonly InflanDBContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlatformSettingsService> _logger;
    private const string CachePrefix = "PlatformSetting_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    // Setting keys
    public const string BrandPlatformFeePercentKey = "BrandPlatformFeePercent";
    public const string InfluencerPlatformFeePercentKey = "InfluencerPlatformFeePercent";

    // Default values
    private const decimal DefaultBrandFeePercent = 2.0m;
    private const decimal DefaultInfluencerFeePercent = 2.0m;

    public PlatformSettingsService(
        InflanDBContext context,
        IMemoryCache cache,
        ILogger<PlatformSettingsService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal> GetBrandPlatformFeePercentAsync()
    {
        var value = await GetSettingAsync(BrandPlatformFeePercentKey);
        if (decimal.TryParse(value, out var fee))
            return fee;
        return DefaultBrandFeePercent;
    }

    public async Task<decimal> GetInfluencerPlatformFeePercentAsync()
    {
        var value = await GetSettingAsync(InfluencerPlatformFeePercentKey);
        if (decimal.TryParse(value, out var fee))
            return fee;
        return DefaultInfluencerFeePercent;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        var cacheKey = $"{CachePrefix}{key}";

        if (_cache.TryGetValue(cacheKey, out string? cachedValue))
            return cachedValue;

        var setting = await _context.PlatformSettings
            .FirstOrDefaultAsync(s => s.SettingKey == key);

        if (setting != null)
        {
            _cache.Set(cacheKey, setting.SettingValue, CacheDuration);
            return setting.SettingValue;
        }

        return null;
    }

    public async Task UpdateSettingAsync(string key, string value)
    {
        var setting = await _context.PlatformSettings
            .FirstOrDefaultAsync(s => s.SettingKey == key);

        if (setting != null)
        {
            setting.SettingValue = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            setting = new PlatformSettings
            {
                SettingKey = key,
                SettingValue = value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.PlatformSettings.Add(setting);
        }

        await _context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove($"{CachePrefix}{key}");

        _logger.LogInformation("Updated platform setting {Key} to {Value}", key, value);
    }

    public async Task InitializeDefaultSettingsAsync()
    {
        var settings = new List<(string Key, string Value, string Description)>
        {
            (BrandPlatformFeePercentKey, DefaultBrandFeePercent.ToString(), "Platform fee percentage charged to brands on payments"),
            (InfluencerPlatformFeePercentKey, DefaultInfluencerFeePercent.ToString(), "Platform fee percentage charged to influencers when payments are released")
        };

        foreach (var (key, value, description) in settings)
        {
            var exists = await _context.PlatformSettings
                .AnyAsync(s => s.SettingKey == key);

            if (!exists)
            {
                _context.PlatformSettings.Add(new PlatformSettings
                {
                    SettingKey = key,
                    SettingValue = value,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                _logger.LogInformation("Initialized default platform setting: {Key} = {Value}", key, value);
            }
        }

        await _context.SaveChangesAsync();
    }
}
