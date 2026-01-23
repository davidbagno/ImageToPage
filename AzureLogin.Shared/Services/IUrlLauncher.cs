namespace AzureLogin.Shared.Services;

/// <summary>
/// Interface for launching URLs - implemented differently for MAUI vs Web
/// </summary>
public interface IUrlLauncher
{
    /// <summary>
    /// Opens a URL in the appropriate browser/window
    /// </summary>
    /// <param name="url">The URL to open</param>
    /// <param name="code">Optional code to auto-fill (for device code flow)</param>
    Task OpenUrlAsync(string url, string? code = null);
}
