using AzureLogin.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureLogin.Services;

/// <summary>
/// Service for converting images to code using Azure OpenAI GPT-4o Vision
/// </summary>
public class ImageToCodeService : IImageToCodeService
{
    private readonly AzureOpenAISettings _settings;
    private readonly ILogger<ImageToCodeService>? _logger;
    private readonly IAzureOpenAIService _azureOpenAIService;
    private readonly IVisionService? _visionService;
    private readonly IImageExtractionService? _imageExtractionService;
    private readonly IAzureVisionService? _azureVisionService;
    private PremiumImageToCodeEngine? _premiumEngine;

    public ImageToCodeService(
        IOptions<AzureOpenAISettings> settings, 
        IAzureOpenAIService azureOpenAIService,
        IVisionService? visionService = null,
        IImageExtractionService? imageExtractionService = null,
        IAzureVisionService? azureVisionService = null,
        ILogger<ImageToCodeService>? logger = null)
    {
        _settings = settings.Value;
        _azureOpenAIService = azureOpenAIService;
        _visionService = visionService;
        _imageExtractionService = imageExtractionService;
        _azureVisionService = azureVisionService;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates the premium engine instance (lazy initialization)
    /// </summary>
    private PremiumImageToCodeEngine GetPremiumEngine()
    {
        if (_premiumEngine == null && _visionService != null)
        {
            _premiumEngine = new PremiumImageToCodeEngine(
                _azureOpenAIService,
                _visionService,
                _imageExtractionService,
                _azureVisionService,
                null // Logger - could inject if needed
            );
        }
        
        return _premiumEngine ?? throw new InvalidOperationException(
            "Premium engine requires VisionService to be configured");
    }

    public async Task<ImageToCodeResult> GenerateCodeFromImageAsync(byte[] imageBytes, string mimeType, string framework = "HTML/CSS")
    {
        var base64Image = Convert.ToBase64String(imageBytes);
        return await GenerateCodeFromBase64Async(base64Image, mimeType, framework);
    }

    public async Task<ImageToCodeResult> GenerateCodeFromBase64Async(string base64Image, string mimeType, string framework = "HTML/CSS")
    {
        try
        {
            var status = await _azureOpenAIService.GetAuthenticationStatusAsync();
            if (!status.IsAuthenticated)
            {
                return new ImageToCodeResult
                {
                    Success = false,
                    ErrorMessage = "Please sign in first to use image-to-code conversion."
                };
            }

            var prompt = GetCombinedPrompt(framework);
            var systemPrompt = GetSystemPrompt(framework);
            
            _logger?.LogInformation("Generating code for framework: {Framework}", framework);
            
            var generatedCode = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, 
                mimeType, 
                prompt, 
                systemPrompt
            );
            
            generatedCode = CleanCodeOutput(generatedCode);
            var languageHint = GetLanguageHint(framework);

            return new ImageToCodeResult
            {
                Success = true,
                Code = generatedCode,
                Language = languageHint
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image to code conversion failed");
            return new ImageToCodeResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string GetCombinedPrompt(string framework)
    {
        var frameworkPrompt = GetFrameworkPrompt(framework);

        return $"""
            Analyze this UI design image and generate production-ready {framework} code.

            First, internally analyze:
            - Layout structure (grid, flexbox, sections)
            - Color palette (extract exact hex codes)
            - Typography (fonts, sizes, weights)
            - Spacing patterns (padding, margins, gaps)
            - Components (buttons, inputs, cards, etc.)
            - Visual effects (shadows, borders, gradients)

            Then generate complete {framework} code that:
            1. Pixel-Perfect: Match the design exactly
            2. Semantic: Use proper HTML5 elements
            3. Accessible: Include ARIA labels, alt text
            4. Responsive: Mobile-first with breakpoints
            5. Design Tokens: Use CSS variables for colors, spacing
            6. Interactive: Include hover, focus, active states

            Framework Instructions:
            {frameworkPrompt}

            Output ONLY the complete, working code. No explanations or markdown.
            """;
    }

    private static string GetFrameworkPrompt(string framework)
    {
        return framework switch
        {
            "React" => "Use functional components with hooks, CSS-in-JS or CSS modules, useState/useEffect where appropriate.",
            "Blazor" => "Use Blazor component syntax (.razor), @code section for logic, Bootstrap 5 classes.",
            "SwiftUI" => "Use SwiftUI View protocol, proper modifiers chain, @State/@Binding, SF Symbols.",
            "MAUI XAML" => "Use proper XAML namespaces, Grid/StackLayout/FlexLayout, ResourceDictionary for styles.",
            "Tailwind CSS" => "Use Tailwind CSS utility classes, responsive prefixes (sm:, md:, lg:, xl:).",
            "Vue" => "Use Vue 3 Composition API with <script setup>, scoped styles, ref/reactive.",
            _ => "Use semantic HTML5, CSS custom properties, Flexbox/Grid, media queries."
        };
    }

    private static string GetSystemPrompt(string framework)
    {
        return $"""
            You are an elite frontend developer specializing in converting UI designs to production-ready code.
            Framework: {framework}
            
            Generate code that is:
            - Pixel-perfect matching the design
            - Using CSS custom properties for design tokens
            - Mobile-first responsive
            - Accessible (WCAG 2.1)
            - Including hover, focus, active states
            
            Output ONLY code, no explanations or markdown.
            """;
    }

    private static string GetLanguageHint(string framework)
    {
        return framework switch
        {
            "React" => "jsx",
            "Blazor" => "razor",
            "SwiftUI" => "swift",
            "MAUI XAML" => "xml",
            "Vue" => "vue",
            "Tailwind CSS" => "html",
            _ => "html"
        };
    }

    private static string CleanCodeOutput(string code)
    {
        code = code.Trim();
        
        if (code.StartsWith("```"))
        {
            var firstNewline = code.IndexOf('\n');
            if (firstNewline > 0)
                code = code[(firstNewline + 1)..];
        }
        
        if (code.EndsWith("```"))
            code = code[..^3];
        
        return code.Trim();
    }

    /// <summary>
    /// Premium multi-pass conversion engine - Best-in-class image to pixel-perfect code.
    /// Combines structure analysis, component detection, design token extraction,
    /// asset extraction, code generation, and iterative refinement.
    /// </summary>
    public async Task<PremiumConversionResult> ConvertWithPremiumEngineAsync(
        byte[] imageBytes,
        string mimeType,
        PremiumConversionOptions? options = null,
        Action<ConversionProgress>? onProgress = null)
    {
        try
        {
            // Check authentication
            var status = await _azureOpenAIService.GetAuthenticationStatusAsync();
            if (!status.IsAuthenticated)
            {
                return new PremiumConversionResult
                {
                    Success = false,
                    ErrorMessage = "Please sign in first to use premium image-to-code conversion."
                };
            }

            // Use premium engine
            var engine = GetPremiumEngine();
            return await engine.ConvertAsync(imageBytes, mimeType, options, onProgress);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("VisionService"))
        {
            _logger?.LogWarning("Premium engine not available, falling back to standard conversion");
            
            // Fallback to standard conversion
            options ??= new PremiumConversionOptions();
            var standardResult = await GenerateCodeFromImageAsync(imageBytes, mimeType, options.Framework);
            
            return new PremiumConversionResult
            {
                Success = standardResult.Success,
                Code = standardResult.Code,
                Language = standardResult.Language,
                ErrorMessage = standardResult.ErrorMessage,
                FidelityScore = 70,
                Warnings = new List<string> { "Used standard conversion (premium engine unavailable)" }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Premium conversion failed");
            return new PremiumConversionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
