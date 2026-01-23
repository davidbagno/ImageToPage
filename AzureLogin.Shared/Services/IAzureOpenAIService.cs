namespace AzureLogin.Shared.Services;

/// <summary>
/// Interface for Azure OpenAI service operations
/// </summary>
public interface IAzureOpenAIService
{
    /// <summary>
    /// Gets the current authentication status
    /// </summary>
    Task<AuthenticationStatus> GetAuthenticationStatusAsync();
    
    /// <summary>
    /// Sends a chat completion request to Azure OpenAI
    /// </summary>
    Task<string> GetChatCompletionAsync(string userMessage, string? systemPrompt = null);
    
    /// <summary>
    /// Sends a vision chat completion request with an image to Azure OpenAI GPT-4o
    /// </summary>
    Task<string> GetVisionCompletionAsync(string imageBase64, string mimeType, string userPrompt, string? systemPrompt = null);
    
    /// <summary>
    /// Generates an image using Azure OpenAI gpt-image-1.5
    /// </summary>
    Task<ImageGenerationResult> GenerateImageAsync(string prompt, string size = "1024x1024");
    
    /// <summary>
    /// Signs out the current user
    /// </summary>
    void SignOut();
    
    string Endpoint { get; }
    string DeploymentName { get; }
    string? UserCode { get; }
    string? VerificationUrl { get; }
}

/// <summary>
/// Result of an image generation request
/// </summary>
public class ImageGenerationResult
{
    public bool Success { get; set; }
    public string? ImageUrl { get; set; }
    public string? RevisedPrompt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents the authentication status for Azure OpenAI
/// </summary>
public class AuthenticationStatus
{
    public bool IsAuthenticated { get; set; }
    public string? AuthenticationMethod { get; set; }
    public string? UserName { get; set; }
    public string? TenantId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? TokenExpiresOn { get; set; }
}
