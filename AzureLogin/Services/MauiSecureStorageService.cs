using AzureLogin.Shared.Services;

namespace AzureLogin.Services;

/// <summary>
/// MAUI implementation of secure storage using platform-specific secure storage
/// - iOS: Keychain
/// - Android: EncryptedSharedPreferences (backed by Android Keystore)
/// - Windows: Data Protection API (DPAPI)
/// - macOS: Keychain
/// </summary>
public class MauiSecureStorageService : ISecureStorageService
{
    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            // SecureStorage may not be available on all platforms/configurations
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to store in secure storage: {ex.Message}");
            throw;
        }
    }

    public Task RemoveAsync(string key)
    {
        try
        {
            SecureStorage.Default.Remove(key);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to remove from secure storage: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        var value = await GetAsync(key);
        return !string.IsNullOrEmpty(value);
    }
}
