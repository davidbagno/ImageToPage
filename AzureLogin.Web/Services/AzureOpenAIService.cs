using Azure;
using Azure.AI.OpenAI;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Images;

namespace AzureLogin.Web.Services;

/// <summary>
/// Azure OpenAI service using API Key authentication from appsettings.json
/// </summary>
public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly AzureOpenAISettings _settings;
    private readonly ILogger<AzureOpenAIService> _logger;
    private AzureOpenAIClient? _client;
    private string? _userName;
    private bool _isAuthenticated;

    public AzureOpenAIService(IOptions<AzureOpenAISettings> settings, ILogger<AzureOpenAIService> logger)
    {
        _settings = settings.Value ?? new AzureOpenAISettings();
        _logger = logger;
        
        // Auto-initialize with API key if available
        InitializeClient();
    }

    private void InitializeClient()
    {
        if (!string.IsNullOrEmpty(_settings.Endpoint) && !string.IsNullOrEmpty(_settings.ApiKey))
        {
            try
            {
                var credential = new AzureKeyCredential(_settings.ApiKey);
                _client = new AzureOpenAIClient(new Uri(_settings.Endpoint), credential);
                _isAuthenticated = true;
                _userName = "API Key User";
                _logger.LogInformation("Azure OpenAI client initialized with API key");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure OpenAI client with API key");
                _isAuthenticated = false;
            }
        }
    }

    public string Endpoint => _settings.Endpoint ?? "Not configured";
    public string DeploymentName => _settings.DefaultDeployment ?? "Not configured";
    
    // These are no longer used with API key auth, but kept for interface compatibility
    public string? UserCode => null;
    public string? VerificationUrl => null;

    public Task<AuthenticationStatus> GetAuthenticationStatusAsync()
    {
        var status = new AuthenticationStatus();
        
        if (string.IsNullOrEmpty(_settings.Endpoint))
        {
            status.IsAuthenticated = false;
            status.ErrorMessage = "Azure OpenAI endpoint not configured in appsettings.json";
            return Task.FromResult(status);
        }

        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            status.IsAuthenticated = false;
            status.ErrorMessage = "Azure OpenAI API key not configured in appsettings.json";
            return Task.FromResult(status);
        }

        if (_isAuthenticated && _client != null)
        {
            status.IsAuthenticated = true;
            status.UserName = _userName;
            status.AuthenticationMethod = "API Key";
            return Task.FromResult(status);
        }

        // Try to initialize if not already done
        InitializeClient();
        
        if (_isAuthenticated && _client != null)
        {
            status.IsAuthenticated = true;
            status.UserName = _userName;
            status.AuthenticationMethod = "API Key";
        }
        else
        {
            status.IsAuthenticated = false;
            status.ErrorMessage = "Failed to initialize Azure OpenAI client";
        }
        
        return Task.FromResult(status);
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
            InitializeClient();
            if (_client == null)
                throw new InvalidOperationException("Azure OpenAI client not configured. Check your API key and endpoint in appsettings.json");
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
            InitializeClient();
            if (_client == null)
                throw new InvalidOperationException("Azure OpenAI client not configured. Check your API key and endpoint in appsettings.json");
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
            InitializeClient();
            if (_client == null)
                return new ImageGenerationResult { Success = false, ErrorMessage = "Azure OpenAI client not configured. Check your API key and endpoint in appsettings.json" };
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
