namespace inflan_api.Interfaces;

public interface IPlatformSettingsService
{
    /// <summary>
    /// Get brand platform fee percentage (default 2%)
    /// </summary>
    Task<decimal> GetBrandPlatformFeePercentAsync();

    /// <summary>
    /// Get influencer platform fee percentage (default 2%)
    /// </summary>
    Task<decimal> GetInfluencerPlatformFeePercentAsync();

    /// <summary>
    /// Update a platform setting
    /// </summary>
    Task UpdateSettingAsync(string key, string value);

    /// <summary>
    /// Get a setting value
    /// </summary>
    Task<string?> GetSettingAsync(string key);

    /// <summary>
    /// Initialize default settings if they don't exist
    /// </summary>
    Task InitializeDefaultSettingsAsync();
}
