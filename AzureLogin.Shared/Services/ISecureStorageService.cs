namespace AzureLogin.Shared.Services;

/// <summary>
/// Interface for secure storage of sensitive data like API keys
/// </summary>
public interface ISecureStorageService
{
    /// <summary>
    /// Get a value from secure storage
    /// </summary>
    Task<string?> GetAsync(string key);
    
    /// <summary>
    /// Set a value in secure storage
    /// </summary>
    Task SetAsync(string key, string value);
    
    /// <summary>
    /// Remove a value from secure storage
    /// </summary>
    Task RemoveAsync(string key);
    
    /// <summary>
    /// Check if a key exists in secure storage
    /// </summary>
    Task<bool> ContainsKeyAsync(string key);
}

/// <summary>
/// Keys for secure storage
/// </summary>
public static class SecureStorageKeys
{
    public const string AzureOpenAIApiKey = "AzureOpenAI_ApiKey";
    public const string AzureVisionKey = "AzureVision_Key";
    public const string DocumentIntelligenceKey = "DocumentIntelligence_Key";
    public const string CustomVisionKey = "CustomVision_Key";
    public const string VideoIndexerApiKey = "VideoIndexer_ApiKey";
}
