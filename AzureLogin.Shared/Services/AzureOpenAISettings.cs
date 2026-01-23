namespace AzureLogin.Shared.Services;

/// <summary>
/// Configuration settings for Azure OpenAI and related AI services
/// </summary>
public class AzureOpenAISettings
{
    public const string SectionName = "AzureOpenAI";
    
    // Azure OpenAI settings
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DefaultDeployment { get; set; } = string.Empty;
    public string ImageDeployment { get; set; } = "gpt-image-1.5";
    public string VisionDeployment { get; set; } = "gpt-4o";
    
    // Azure AI Vision (Computer Vision 4.0) settings
    public string VisionEndpoint { get; set; } = string.Empty;
    public string VisionKey { get; set; } = string.Empty;
    
    // Azure Document Intelligence (Form Recognizer) settings
    public string DocumentIntelligenceEndpoint { get; set; } = string.Empty;
    public string DocumentIntelligenceKey { get; set; } = string.Empty;
    
    // Azure Custom Vision settings
    public string CustomVisionEndpoint { get; set; } = string.Empty;
    public string CustomVisionKey { get; set; } = string.Empty;
    public string CustomVisionProjectId { get; set; } = string.Empty;
    
    // Azure Video Indexer settings
    public string VideoIndexerAccountId { get; set; } = string.Empty;
    public string VideoIndexerLocation { get; set; } = "trial"; // or specific region
    public string VideoIndexerApiKey { get; set; } = string.Empty;
    
    // DALL-E settings (uses same endpoint as OpenAI)
    public string DalleDeployment { get; set; } = "dall-e-3";
}
