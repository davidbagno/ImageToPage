using Azure;
using Azure.AI.OpenAI;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Images;

namespace AzureLogin.Web.Services;

/// <summary>
/// Azure OpenAI service using API Key from secure storage
/// </summary>
public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly AzureOpenAISettings _settings;
    private readonly ISecureStorageService _secureStorage;
    private readonly ILogger<AzureOpenAIService> _logger;
    private AzureOpenAIClient? _client;
    private string? _userName;
    private bool _isAuthenticated;
    private string? _cachedApiKey;

    public AzureOpenAIService(
        IOptions<AzureOpenAISettings> settings, 
        ISecureStorageService secureStorage,
        ILogger<AzureOpenAIService> logger)
    {
        _settings = settings.Value ?? new AzureOpenAISettings();
        _secureStorage = secureStorage;
        _logger = logger;
        
        // Try to initialize on construction
        _ = InitializeClientAsync();
    }

    private async Task<string?> GetApiKeyAsync()
    {
        if (!string.IsNullOrEmpty(_cachedApiKey))
            return _cachedApiKey;
        
        var secureKey = await _secureStorage.GetAsync(SecureStorageKeys.AzureOpenAIApiKey);
        if (!string.IsNullOrEmpty(secureKey))
        {
            _cachedApiKey = secureKey;
            return secureKey;
        }
        
        // Fall back to appsettings
        if (!string.IsNullOrEmpty(_settings.ApiKey) && !_settings.ApiKey.Contains("YOUR-"))
        {
            await _secureStorage.SetAsync(SecureStorageKeys.AzureOpenAIApiKey, _settings.ApiKey);
            _cachedApiKey = _settings.ApiKey;
            return _settings.ApiKey;
        }
        
        return null;
    }

    private async Task InitializeClientAsync()
    {
        var apiKey = await GetApiKeyAsync();
        
        if (!string.IsNullOrEmpty(_settings.Endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            try
            {
                var credential = new AzureKeyCredential(apiKey);
                _client = new AzureOpenAIClient(new Uri(_settings.Endpoint), credential);
                _isAuthenticated = true;
                _userName = "API Key User";
                _logger.LogInformation("Azure OpenAI client initialized with API key from secure storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure OpenAI client");
                _isAuthenticated = false;
            }
        }
    }

    public string Endpoint => _settings.Endpoint ?? "Not configured";
    public string DeploymentName => _settings.DefaultDeployment ?? "Not configured";
    public string? UserCode => null;
    public string? VerificationUrl => null;

    public async Task<AuthenticationStatus> GetAuthenticationStatusAsync()
    {
        var status = new AuthenticationStatus();
        
        if (string.IsNullOrEmpty(_settings.Endpoint))
        {
            status.IsAuthenticated = false;
            status.ErrorMessage = "Azure OpenAI endpoint not configured in appsettings.json";
            return status;
        }

        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            status.IsAuthenticated = false;
            status.ErrorMessage = "Azure OpenAI API key not configured. Please set your API key in Settings.";
            return status;
        }

        if (_isAuthenticated && _client != null)
        {
            status.IsAuthenticated = true;
            status.UserName = _userName;
            status.AuthenticationMethod = "API Key (Secure Storage)";
            return status;
        }

        await InitializeClientAsync();
        
        if (_isAuthenticated && _client != null)
        {
            status.IsAuthenticated = true;
            status.UserName = _userName;
            status.AuthenticationMethod = "API Key (Secure Storage)";
        }
        else
        {
            status.IsAuthenticated = false;
            status.ErrorMessage = "Failed to initialize Azure OpenAI client";
        }
        
        return status;
    }

    public void SignOut()
    {
        _isAuthenticated = false;
        _client = null;
        _userName = null;
    }

    public async Task<string> GetChatCompletionAsync(string userMessage, string? systemPrompt = null)
    {
        if (_client == null)
        {
            await InitializeClientAsync();
            if (_client == null)
                throw new InvalidOperationException("Azure OpenAI client not configured. Check your API key in Settings.");
        }
        
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
        {
            await InitializeClientAsync();
            if (_client == null)
                throw new InvalidOperationException("Azure OpenAI client not configured. Check your API key in Settings.");
        }
        
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
        {
            await InitializeClientAsync();
            if (_client == null)
                return new ImageGenerationResult { Success = false, ErrorMessage = "Azure OpenAI client not configured. Check your API key in Settings." };
        }

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

            string? imageUrl = null;
            if (image.ImageUri != null)
            {
                imageUrl = image.ImageUri.ToString();
            }
            else if (image.ImageBytes != null && image.ImageBytes.ToArray().Length > 0)
            {
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
            _logger.LogError(ex, "Image generation failed");
            return new ImageGenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
