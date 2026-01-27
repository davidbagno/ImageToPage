using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AzureLogin.Shared.Services;
using AzureLogin.Services;

namespace AzureLogin;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        // Load configuration from Raw assets - use async in a sync context for startup
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
            var config = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();
            builder.Configuration.AddConfiguration(config);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not load appsettings.json: {ex.Message}");
        }

        // Configure Azure OpenAI settings from configuration
        builder.Services.Configure<AzureOpenAISettings>(
            builder.Configuration.GetSection(AzureOpenAISettings.SectionName));

        // Add device-specific services used by the AzureLogin.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        
        // Register URL launcher for opening browser
        builder.Services.AddSingleton<IUrlLauncher, MauiUrlLauncher>();
        
        // Register secure storage service for API keys and sensitive data
        builder.Services.AddSingleton<ISecureStorageService, MauiSecureStorageService>();
        
        // Register Azure OpenAI service with API key from secure storage
        builder.Services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();
        
        // Register Azure Vision service for pixel-accurate image detection
        builder.Services.AddSingleton<IAzureVisionService, AzureVisionService>();
        
        // Register Image to Code service
        builder.Services.AddSingleton<IImageToCodeService, ImageToCodeService>();
        
        // Register Vision service for comprehensive image analysis
        builder.Services.AddSingleton<IVisionService, VisionService>();
        
        // Register Image Extraction service for extracting images from composites
        builder.Services.AddSingleton<IImageExtractionService, ImageExtractionService>();
        
        // Register Advanced AI Service for Document Intelligence, Custom Vision, Video Analysis, etc.
        builder.Services.AddSingleton<IAdvancedAIService, AdvancedAIService>();
        
        // Register Dynamic Razor Renderer for live preview of generated Razor components
        builder.Services.AddSingleton<IDynamicRazorRenderer, DynamicRazorRenderer>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}