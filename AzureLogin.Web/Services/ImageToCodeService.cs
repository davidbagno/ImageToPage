using AzureLogin.Shared.Services;
using Microsoft.Extensions.Options;

namespace AzureLogin.Web.Services;

/// <summary>
/// Service for converting images to code using Azure OpenAI GPT-4o Vision
/// with comprehensive UI analysis and extraction
/// </summary>
public class ImageToCodeService : IImageToCodeService
{
    private readonly ILogger<ImageToCodeService> _logger;
    private readonly IAzureOpenAIService _azureOpenAIService;

    public ImageToCodeService(
        IOptions<AzureOpenAISettings> settings, 
        IAzureOpenAIService azureOpenAIService,
        ILogger<ImageToCodeService> logger)
    {
        _ = settings.Value; // Validate settings are provided
        _azureOpenAIService = azureOpenAIService;
        _logger = logger;
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

            // Use the authenticated service's vision completion method
            var prompt = GetCombinedPrompt(framework);
            var systemPrompt = GetSystemPrompt(framework);
            
            _logger.LogInformation("Generating code for framework: {Framework}", framework);
            
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
            _logger.LogError(ex, "Image to code conversion failed");
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
            1. **Pixel-Perfect**: Match the design exactly
            2. **Semantic**: Use proper HTML5 elements
            3. **Accessible**: Include ARIA labels, alt text
            4. **Responsive**: Mobile-first with breakpoints
            5. **Design Tokens**: Use CSS variables for colors, spacing
            6. **Interactive**: Include hover, focus, active states

            Framework Instructions:
            {frameworkPrompt}

            Output ONLY the complete, working code. No explanations or markdown.
            """;
    }

    private static string GetFrameworkPrompt(string framework)
    {
        return framework switch
        {
            "React" => """
                - Use functional components with hooks
                - Use CSS-in-JS (styled-components style) or CSS modules
                - Include useState/useEffect where appropriate
                - Use proper prop types or TypeScript interfaces
                - Structure: imports, styled components/CSS, component, export
                """,
            "Blazor" => """
                - Use Blazor component syntax (.razor)
                - Include @code section for any logic
                - Use Bootstrap 5 classes where appropriate
                - Include scoped CSS in a separate section
                - Use proper Blazor event handlers (@onclick, @onchange)
                """,
            "SwiftUI" => """
                - Use SwiftUI View protocol
                - Use proper modifiers chain
                - Include @State/@Binding where needed
                - Follow Apple Human Interface Guidelines
                - Use SF Symbols for icons
                """,
            "MAUI XAML" => """
                - Use proper XAML namespaces
                - Use Grid, StackLayout, FlexLayout appropriately
                - Include ResourceDictionary for styles
                - Use proper binding syntax
                - Include x:Name for elements needing code-behind access
                """,
            "Tailwind CSS" => """
                - Use Tailwind CSS utility classes
                - Use responsive prefixes (sm:, md:, lg:, xl:)
                - Use Tailwind's color palette or custom colors
                - Include dark mode variants where appropriate
                - Use @apply in style blocks for repeated patterns
                """,
            "Vue" => """
                - Use Vue 3 Composition API with <script setup>
                - Use scoped styles
                - Include ref/reactive for state
                - Use proper v-bind, v-on directives
                - Structure: template, script setup, scoped styles
                """,
            _ => """
                - Use semantic HTML5 elements
                - Include CSS custom properties (variables)
                - Use Flexbox/Grid for layouts
                - Include media queries for responsiveness
                - Add CSS transitions for smooth interactions
                """
        };
    }

    private static string GetSystemPrompt(string framework)
    {
        return $"""
            You are an elite frontend developer with 15+ years of experience in converting UI designs to production-ready code.
            
            Your expertise includes:
            - Pixel-perfect implementation of designs
            - Modern CSS (Grid, Flexbox, Custom Properties)
            - Responsive design patterns
            - Accessibility best practices (WCAG 2.1)
            - Performance optimization
            - Clean, maintainable code architecture
            
            Framework expertise: {framework}
            
            When generating code:
            1. Start with CSS custom properties for design tokens
            2. Use semantic HTML structure
            3. Implement mobile-first responsive design
            4. Add smooth transitions and micro-interactions
            5. Include hover, focus, and active states
            6. Comment complex sections
            7. Ensure accessibility compliance
            
            Output ONLY the code, no explanations or markdown code blocks.
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
            {
                code = code[(firstNewline + 1)..];
            }
        }
        
        if (code.EndsWith("```"))
        {
            code = code[..^3];
        }
        
        return code.Trim();
    }
}
