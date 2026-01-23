using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Images;

namespace AzureLogin.Services;

public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly AzureOpenAISettings _settings;
    private readonly ILogger<AzureOpenAIService>? _logger;
    private TokenCredential? _credential;
    private AzureOpenAIClient? _client;
    private string? _userName;
    private DateTime? _tokenExpiry;
    private bool _isAuthenticated;
    private string? _userCode;
    private string? _verificationUrl;

    public AzureOpenAIService(IOptions<AzureOpenAISettings> settings, ILogger<AzureOpenAIService>? logger = null)
    {
        _settings = settings.Value ?? new AzureOpenAISettings();
        _logger = logger;
    }

    public string Endpoint => _settings.Endpoint ?? "Not configured";
    public string DeploymentName => _settings.DefaultDeployment ?? "Not configured";
    public string? UserCode => _userCode;
    public string? VerificationUrl => _verificationUrl;

    public async Task<AuthenticationStatus> GetAuthenticationStatusAsync()
    {
        var status = new AuthenticationStatus();
        
        if (string.IsNullOrEmpty(_settings.Endpoint))
        {
            status.IsAuthenticated = false;
            status.ErrorMessage = "Azure OpenAI endpoint not configured.";
            return status;
        }

        if (_isAuthenticated && _client != null)
        {
            status.IsAuthenticated = true;
            status.UserName = _userName;
            status.TokenExpiresOn = _tokenExpiry;
            status.AuthenticationMethod = "Device Code Flow";
            return status;
        }

        try
        {
            var credential = new DeviceCodeCredential(new DeviceCodeCredentialOptions
            {
                DeviceCodeCallback = (info, cancel) =>
                {
                    _userCode = info.UserCode;
                    _verificationUrl = info.VerificationUri.ToString();
                    return Task.CompletedTask;
                }
            });

            var tokenRequest = new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]);
            var token = await credential.GetTokenAsync(tokenRequest, default);
            
            _credential = credential;
            _tokenExpiry = token.ExpiresOn.DateTime;
            _client = new AzureOpenAIClient(new Uri(_settings.Endpoint), credential);
            _isAuthenticated = true;
            
            ExtractUserInfo(token.Token);
            
            status.IsAuthenticated = true;
            status.UserName = _userName;
            status.TokenExpiresOn = _tokenExpiry;
            status.AuthenticationMethod = "Device Code Flow";
            
            _userCode = null;
            _verificationUrl = null;
        }
        catch (Exception ex)
        {
            status.IsAuthenticated = false;
            status.ErrorMessage = ex.Message;
        }
        
        return status;
    }
    
    private void ExtractUserInfo(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length >= 2)
            {
                var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var doc = System.Text.Json.JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("name", out var name))
                    _userName = name.GetString();
                else if (doc.RootElement.TryGetProperty("upn", out var upn))
                    _userName = upn.GetString();
            }
        }
        catch { _userName = "User"; }
    }

    public void SignOut()
    {
        _isAuthenticated = false;
        _client = null;
        _credential = null;
        _userName = null;
        _tokenExpiry = null;
        _userCode = null;
        _verificationUrl = null;
    }

    public async Task<string> GetChatCompletionAsync(string userMessage, string? systemPrompt = null)
    {
        if (_client == null)
            throw new InvalidOperationException("Please sign in first.");
        
        var chatClient = _client.GetChatClient(_settings.DefaultDeployment);
        
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(new SystemChatMessage(systemPrompt));
        messages.Add(new UserChatMessage(userMessage));
        
        var response = await chatClient.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }

    public async Task<string> GetVisionCompletionAsync(string imageBase64, string mimeType, string userPrompt, string? systemPrompt = null)
    {
        if (_client == null)
            throw new InvalidOperationException("Please sign in first.");
        
        // Use gpt-4o for vision tasks
        var visionDeployment = !string.IsNullOrEmpty(_settings.VisionDeployment) 
            ? _settings.VisionDeployment 
            : "gpt-4o";
        
        var chatClient = _client.GetChatClient(visionDeployment);
        
        var dataUrl = $"data:{mimeType};base64,{imageBase64}";
        
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(new SystemChatMessage(systemPrompt));
        
        messages.Add(new UserChatMessage(
            ChatMessageContentPart.CreateTextPart(userPrompt),
            ChatMessageContentPart.CreateImagePart(new Uri(dataUrl))
        ));
        
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 8192,
            Temperature = 0.2f
        };
        
        var response = await chatClient.CompleteChatAsync(messages, options);
        return response.Value.Content[0].Text;
    }

    public async Task<ImageGenerationResult> GenerateImageAsync(string prompt, string size = "1024x1024")
    {
        if (_client == null)
            return new ImageGenerationResult { Success = false, ErrorMessage = "Please sign in first." };

        try
        {
            var imageClient = _client.GetImageClient(_settings.ImageDeployment);
            
            var options = new ImageGenerationOptions
            {
                Size = size switch
                {
                    "1792x1024" => GeneratedImageSize.W1792xH1024,
                    "1024x1792" => GeneratedImageSize.W1024xH1792,
                    _ => GeneratedImageSize.W1024xH1024
                }
            };

            var response = await imageClient.GenerateImageAsync(prompt, options);
            var image = response.Value;

            // Handle both URL and base64 responses
            string? imageUrl = null;
            if (image.ImageUri != null)
            {
                imageUrl = image.ImageUri.ToString();
            }
            else if (image.ImageBytes != null && image.ImageBytes.ToArray().Length > 0)
            {
                // Convert base64 bytes to data URL for display
                var base64 = Convert.ToBase64String(image.ImageBytes.ToArray());
                imageUrl = $"data:image/png;base64,{base64}";
            }

            return new ImageGenerationResult
            {
                Success = true,
                ImageUrl = imageUrl,
                RevisedPrompt = image.RevisedPrompt
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image generation failed");
            return new ImageGenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
