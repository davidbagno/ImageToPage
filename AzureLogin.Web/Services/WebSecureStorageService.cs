using System.Text;
using AzureLogin.Shared.Services;
using Microsoft.JSInterop;

namespace AzureLogin.Web.Services;

/// <summary>
/// Web implementation of storage service using localStorage with runtime encoding/decoding.
/// API keys are encoded using XOR + Base64 before storage and decoded on retrieval.
/// </summary>
public class WebSecureStorageService : ISecureStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebSecureStorageService> _logger;
    private const string EncodingPrefix = "ENC:";

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
            // First check configuration
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

            // Fall back to localStorage with decoding
            var storedValue = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", $"app_key_{key}");
            return DecodeValue(storedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve value: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            var encodedValue = EncodeValue(value);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", $"app_key_{key}", encodedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store value: {Key}", key);
            throw;
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", $"app_key_{key}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove value: {Key}", key);
        }
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        var value = await GetAsync(key);
        return !string.IsNullOrEmpty(value);
    }
    
    /// <summary>
    /// Encodes a value using XOR obfuscation + Base64.
    /// </summary>
    private static string EncodeValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        
        try
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var key = GetObfuscationKey();
            
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ key[i % key.Length]);
            }
            
            return EncodingPrefix + Convert.ToBase64String(bytes);
        }
        catch
        {
            return value;
        }
    }
    
    /// <summary>
    /// Decodes a value that was encoded with EncodeValue.
    /// </summary>
    private static string? DecodeValue(string? storedValue)
    {
        if (string.IsNullOrEmpty(storedValue)) return storedValue;
        
        if (!storedValue.StartsWith(EncodingPrefix))
        {
            return storedValue; // Backwards compatibility
        }
        
        try
        {
            var base64 = storedValue.Substring(EncodingPrefix.Length);
            var bytes = Convert.FromBase64String(base64);
            var key = GetObfuscationKey();
            
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ key[i % key.Length]);
            }
            
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return storedValue.Substring(EncodingPrefix.Length);
        }
    }
    
    /// <summary>
    /// Gets the obfuscation key for encoding/decoding.
    /// </summary>
    private static byte[] GetObfuscationKey()
    {
        var salt = "AzL0g1nW3b$3cur3K3y!2026";
        var keyBytes = Encoding.UTF8.GetBytes(salt);
        var result = new byte[32];
        
        for (int i = 0; i < keyBytes.Length; i++)
        {
            result[i % 32] ^= keyBytes[i];
        }
        
        return result;
    }
}
