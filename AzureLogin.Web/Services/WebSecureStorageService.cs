using AzureLogin.Shared.Services;
using Microsoft.JSInterop;

namespace AzureLogin.Web.Services;

/// <summary>
/// Web implementation of secure storage.
/// Uses localStorage for development. In production, consider using:
/// - Server-side secure storage
/// - Azure Key Vault
/// - Environment variables
/// </summary>
public class WebSecureStorageService : ISecureStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebSecureStorageService> _logger;

    public WebSecureStorageService(
        IJSRuntime jsRuntime, 
        IConfiguration configuration,
        ILogger<WebSecureStorageService> logger)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            // First check environment variable / configuration
            var configKey = key switch
            {
                SecureStorageKeys.AzureOpenAIApiKey => "AzureOpenAI:ApiKey",
                SecureStorageKeys.AzureVisionKey => "AzureOpenAI:VisionKey",
                SecureStorageKeys.DocumentIntelligenceKey => "AzureOpenAI:DocumentIntelligenceKey",
                SecureStorageKeys.CustomVisionKey => "AzureOpenAI:CustomVisionKey",
                SecureStorageKeys.VideoIndexerApiKey => "AzureOpenAI:VideoIndexerApiKey",
                _ => null
            };

            if (configKey != null)
            {
                var configValue = _configuration[configKey];
                if (!string.IsNullOrEmpty(configValue) && !configValue.Contains("YOUR-"))
                {
                    return configValue;
                }
            }

            // Fall back to localStorage
            var result = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", $"secure_{key}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve from secure storage: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            // Store in localStorage with a prefix to identify secure items
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", $"secure_{key}", value);
            _logger.LogInformation("Stored key in secure storage: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store in secure storage: {Key}", key);
            throw;
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", $"secure_{key}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove from secure storage: {Key}", key);
        }
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        var value = await GetAsync(key);
        return !string.IsNullOrEmpty(value);
    }
}
