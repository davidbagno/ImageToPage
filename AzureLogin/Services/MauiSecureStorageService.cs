using System.Text;
using AzureLogin.Shared.Services;

namespace AzureLogin.Services;

/// <summary>
/// MAUI implementation of storage service using Preferences with runtime encoding/decoding.
/// API keys are encoded using XOR + Base64 before storage and decoded on retrieval.
/// This avoids the need for keychain entitlements while providing basic obfuscation.
/// </summary>
public class MauiSecureStorageService : ISecureStorageService
{
    private const string StoragePrefix = "app_key_";
    private const string EncodingPrefix = "ENC:";

    public Task<string?> GetAsync(string key)
    {
        var storedValue = Preferences.Default.Get<string?>(StoragePrefix + key, null);
        var decodedValue = DecodeValue(storedValue);
        return Task.FromResult(decodedValue);
    }

    public Task SetAsync(string key, string value)
    {
        var encodedValue = EncodeValue(value);
        Preferences.Default.Set(StoragePrefix + key, encodedValue);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        Preferences.Default.Remove(StoragePrefix + key);
        return Task.CompletedTask;
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
        var appId = AppInfo.Current.PackageName ?? "com.azurelogin.app";
        var salt = "AzL0g1n$3cur3K3y!2026";
        var combined = appId + salt;
        
        var keyBytes = Encoding.UTF8.GetBytes(combined);
        var result = new byte[32];
        
        for (int i = 0; i < keyBytes.Length; i++)
        {
            result[i % 32] ^= keyBytes[i];
        }
        
        return result;
    }
}
