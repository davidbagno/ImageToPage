using System.Text.Json;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Options;

namespace AzureLogin.Web.Services;

/// <summary>
/// Comprehensive image analysis service using Azure OpenAI GPT-4o Vision
/// Provides all vision capabilities for UI analysis, code generation, and image understanding
/// </summary>
public class VisionService : IVisionService
{
    private readonly IAzureOpenAIService _azureOpenAIService;
    private readonly ILogger<VisionService> _logger;

    public VisionService(
        IAzureOpenAIService azureOpenAIService,
        ILogger<VisionService> logger)
    {
        _azureOpenAIService = azureOpenAIService;
        _logger = logger;
    }

    #region Core Analysis Methods

    public async Task<VisionResult> AnalyzeImageAsync(string base64Image, string mimeType, string prompt)
    {
        try
        {
            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt);
            
            return new VisionResult
            {
                Success = true,
                Content = response
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image analysis failed");
            return new VisionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<VisionResult> GenerateCodeFromImageAsync(string base64Image, string mimeType, string framework = "HTML/CSS")
    {
        var prompt = GetCodeGenerationPrompt(framework);
        var systemPrompt = GetCodeGenerationSystemPrompt(framework);
        
        try
        {
            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt, systemPrompt);
            
            return new VisionResult
            {
                Success = true,
                Content = CleanCodeOutput(response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code generation failed");
            return new VisionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<VisionResult> DescribeImageAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Provide a comprehensive description of this UI design image. Include:
            
            1. **Overall Purpose**: What is this UI for? (e.g., login page, dashboard, e-commerce)
            2. **Visual Style**: Describe the design aesthetic (modern, minimal, corporate, playful)
            3. **Layout Structure**: How is the content organized?
            4. **Key Components**: List all major UI elements visible
            5. **Color Scheme**: Describe the color palette being used
            6. **Typography**: Describe the fonts and text styles
            7. **Interactive Elements**: Identify buttons, links, inputs, etc.
            8. **Notable Design Choices**: Any unique or interesting design decisions
            
            Be specific and detailed in your description.
            """;
        
        return await AnalyzeImageAsync(base64Image, mimeType, prompt);
    }

    public async Task<VisionResult> ExtractTextAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Extract ALL text content visible in this image. Include:
            
            1. Headings and titles
            2. Body text and paragraphs
            3. Button labels
            4. Input placeholders
            5. Navigation items
            6. Footer text
            7. Any other visible text
            
            Format the output as a structured list, grouping text by its location/purpose in the UI.
            Preserve the hierarchy (e.g., main heading > subheading > body text).
            """;
        
        return await AnalyzeImageAsync(base64Image, mimeType, prompt);
    }

    public async Task<VisionResult> SuggestImprovementsAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Analyze this UI design and provide detailed improvement suggestions:
            
            1. **Usability Issues**: Identify any UX problems
            2. **Visual Hierarchy**: Suggest improvements to content organization
            3. **Accessibility**: Identify accessibility concerns
            4. **Consistency**: Point out inconsistencies in design
            5. **Modern Best Practices**: Suggest updates to align with current design trends
            6. **Performance**: Suggest optimizations for web/mobile performance
            7. **Responsive Design**: Recommendations for different screen sizes
            
            For each suggestion, explain:
            - What the issue is
            - Why it matters
            - How to fix it
            
            Be constructive and specific with actionable recommendations.
            """;
        
        return await AnalyzeImageAsync(base64Image, mimeType, prompt);
    }

    public async Task<VisionResult> CompareImagesAsync(string base64Image1, string base64Image2, string mimeType)
    {
        // For comparison, we'll analyze both images and compare
        // Note: GPT-4o can handle multiple images in one request
        var prompt = """
            Compare these two UI designs and provide a detailed analysis:
            
            1. **Layout Differences**: How do the layouts differ?
            2. **Color Changes**: What colors have changed?
            3. **Typography Changes**: Any font/text changes?
            4. **Component Differences**: What UI elements are different?
            5. **Spacing/Alignment**: Changes in spacing or alignment?
            6. **Overall Impression**: Which design is better and why?
            
            Be specific about the differences and their impact on user experience.
            """;
        
        // For now, analyze the first image with context about comparison
        // A full implementation would send both images to the API
        return await AnalyzeImageAsync(base64Image1, mimeType, 
            "Analyze this UI design in detail, focusing on elements that could be compared with another version: " + prompt);
    }

    #endregion

    #region Structured Extraction Methods

    public async Task<UIComponentsResult> ExtractUIComponentsAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Analyze this UI image and extract all components as structured JSON data.
            
            Return a JSON object with this exact structure:
            {
                "layoutType": "grid|flexbox|fixed|absolute",
                "sections": ["header", "hero", "content", "footer"],
                "components": [
                    {
                        "type": "button|input|card|image|text|nav|form|etc",
                        "name": "descriptive name",
                        "text": "visible text content",
                        "style": {
                            "backgroundColor": "#hex",
                            "textColor": "#hex",
                            "borderColor": "#hex or none",
                            "borderRadius": "Xpx",
                            "fontSize": "Xpx",
                            "fontWeight": "normal|bold|etc",
                            "shadow": "description or none",
                            "padding": "Xpx",
                            "margin": "Xpx"
                        },
                        "position": {
                            "layout": "description",
                            "alignment": "left|center|right",
                            "width": "Xpx or X%",
                            "height": "Xpx or auto"
                        },
                        "children": [],
                        "attributes": {}
                    }
                ]
            }
            
            Return ONLY valid JSON, no markdown or explanations.
            """;
        
        try
        {
            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt);
            
            var json = CleanJsonOutput(response);
            var result = ParseUIComponentsJson(json);
            result.Success = true;
            result.Content = json;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UI components extraction failed");
            return new UIComponentsResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ColorPaletteResult> ExtractColorPaletteAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Analyze this UI image and extract the complete color palette as JSON.
            
            Return a JSON object with this exact structure:
            {
                "primaryColor": "#hex",
                "secondaryColor": "#hex",
                "accentColor": "#hex",
                "backgroundColor": "#hex",
                "textColor": "#hex",
                "textSecondaryColor": "#hex",
                "borderColor": "#hex",
                "successColor": "#hex",
                "warningColor": "#hex",
                "errorColor": "#hex",
                "gradients": ["linear-gradient(...)", "..."],
                "allColors": ["#hex1", "#hex2", "..."],
                "colorScheme": "light|dark|mixed"
            }
            
            Extract exact hex color codes where visible. For colors not clearly visible, make educated guesses based on the design.
            Return ONLY valid JSON, no markdown or explanations.
            """;
        
        try
        {
            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt);
            
            var json = CleanJsonOutput(response);
            var result = ParseColorPaletteJson(json);
            result.Success = true;
            result.Content = json;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Color palette extraction failed");
            return new ColorPaletteResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<TypographyResult> ExtractTypographyAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Analyze this UI image and extract typography information as JSON.
            
            Return a JSON object with this exact structure:
            {
                "primaryFont": "font name or 'sans-serif'",
                "secondaryFont": "font name or null",
                "headingFont": "font name",
                "bodyFont": "font name",
                "fontSizes": {
                    "h1": "Xpx",
                    "h2": "Xpx",
                    "h3": "Xpx",
                    "body": "Xpx",
                    "small": "Xpx",
                    "button": "Xpx"
                },
                "fontWeights": ["300", "400", "500", "600", "700"],
                "lineHeight": "1.5 or Xpx",
                "letterSpacing": "normal or Xpx",
                "fontUsages": [
                    {
                        "element": "main heading",
                        "font": "font name",
                        "size": "Xpx",
                        "weight": "700",
                        "color": "#hex"
                    }
                ]
            }
            
            Return ONLY valid JSON, no markdown or explanations.
            """;
        
        try
        {
            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt);
            
            var json = CleanJsonOutput(response);
            var result = ParseTypographyJson(json);
            result.Success = true;
            result.Content = json;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Typography extraction failed");
            return new TypographyResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<LayoutResult> ExtractLayoutAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Analyze this UI image and extract layout structure as JSON.
            
            Return a JSON object with this exact structure:
            {
                "layoutType": "grid|flexbox|fixed|responsive",
                "containerWidth": "Xpx or X%",
                "containerPadding": "Xpx",
                "columns": 12,
                "gap": "Xpx",
                "rowGap": "Xpx",
                "columnGap": "Xpx",
                "breakpoints": ["768px", "1024px", "1280px"],
                "sections": [
                    {
                        "name": "header",
                        "type": "header|hero|content|sidebar|footer",
                        "layout": "flex row|grid|stack",
                        "width": "100%",
                        "height": "auto or Xpx",
                        "padding": "Xpx",
                        "margin": "Xpx"
                    }
                ],
                "spacing": {
                    "baseUnit": "8px",
                    "scale": {
                        "xs": "4px",
                        "sm": "8px",
                        "md": "16px",
                        "lg": "24px",
                        "xl": "32px"
                    },
                    "paddingPattern": "description",
                    "marginPattern": "description",
                    "borderRadius": "Xpx"
                }
            }
            
            Return ONLY valid JSON, no markdown or explanations.
            """;
        
        try
        {
            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt);
            
            var json = CleanJsonOutput(response);
            var result = ParseLayoutJson(json);
            result.Success = true;
            result.Content = json;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Layout extraction failed");
            return new LayoutResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IconsResult> IdentifyIconsAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Analyze this UI image and identify all icons as JSON.
            
            Return a JSON object with this exact structure:
            {
                "iconStyle": "outlined|filled|rounded|sharp|duotone",
                "suggestedLibrary": "Material Icons|Font Awesome|Heroicons|Lucide|Phosphor|SF Symbols",
                "icons": [
                    {
                        "name": "descriptive name",
                        "description": "what the icon represents",
                        "suggestedName": "standard icon name (e.g., 'home', 'search')",
                        "location": "where in the UI",
                        "size": "Xpx",
                        "color": "#hex"
                    }
                ]
            }
            
            Return ONLY valid JSON, no markdown or explanations.
            """;
        
        try
        {
            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt);
            
            var json = CleanJsonOutput(response);
            var result = ParseIconsJson(json);
            result.Success = true;
            result.Content = json;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Icons identification failed");
            return new IconsResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<AccessibilityResult> AnalyzeAccessibilityAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Analyze this UI image for accessibility issues and return as JSON.
            
            Return a JSON object with this exact structure:
            {
                "overallScore": "Good|Needs Improvement|Poor",
                "issues": [
                    {
                        "type": "contrast|text-size|touch-target|color-only|hierarchy|focus",
                        "severity": "critical|major|minor",
                        "description": "description of the issue",
                        "location": "where in the UI",
                        "recommendation": "how to fix",
                        "wcagCriteria": "WCAG 2.1 reference (e.g., 1.4.3)"
                    }
                ],
                "recommendations": ["general recommendation 1", "..."],
                "contrastAnalysis": {
                    "passesAANormal": true,
                    "passesAALarge": true,
                    "passesAAA": false,
                    "analyzedPairs": [
                        {
                            "foreground": "#hex",
                            "background": "#hex",
                            "ratio": 4.5,
                            "passesAA": true,
                            "passesAAA": false
                        }
                    ]
                },
                "hasSufficientTextSize": true,
                "hasClearHierarchy": true,
                "hasTouchTargets": true
            }
            
            Return ONLY valid JSON, no markdown or explanations.
            """;
        
        try
        {
            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt);
            
            var json = CleanJsonOutput(response);
            var result = ParseAccessibilityJson(json);
            result.Success = true;
            result.Content = json;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accessibility analysis failed");
            return new AccessibilityResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ComprehensiveUIAnalysis> AnalyzeUIAsync(string base64Image, string mimeType)
    {
        _logger.LogInformation("Starting comprehensive UI analysis");
        
        var result = new ComprehensiveUIAnalysis { Success = true };
        
        try
        {
            // Run all analyses in parallel for efficiency
            var tasks = new List<Task>
            {
                Task.Run(async () => result.Description = (await DescribeImageAsync(base64Image, mimeType)).Content),
                Task.Run(async () => result.Components = await ExtractUIComponentsAsync(base64Image, mimeType)),
                Task.Run(async () => result.Colors = await ExtractColorPaletteAsync(base64Image, mimeType)),
                Task.Run(async () => result.Typography = await ExtractTypographyAsync(base64Image, mimeType)),
                Task.Run(async () => result.Layout = await ExtractLayoutAsync(base64Image, mimeType)),
                Task.Run(async () => result.Icons = await IdentifyIconsAsync(base64Image, mimeType)),
                Task.Run(async () => result.Accessibility = await AnalyzeAccessibilityAsync(base64Image, mimeType))
            };
            
            await Task.WhenAll(tasks);
            
            // Get overall style and framework suggestions
            var stylePrompt = """
                Based on this UI design, provide:
                1. The overall design style (modern, minimal, corporate, playful, etc.)
                2. Common design patterns used
                3. Suggested frameworks for implementation
                
                Return as JSON:
                {
                    "overallStyle": "style name",
                    "designPatterns": ["pattern1", "pattern2"],
                    "suggestedFrameworks": ["React", "Vue", "etc"]
                }
                
                Return ONLY valid JSON.
                """;
            
            var styleResponse = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, stylePrompt);
            
            var styleJson = CleanJsonOutput(styleResponse);
            try
            {
                using var doc = JsonDocument.Parse(styleJson);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("overallStyle", out var style))
                    result.OverallStyle = style.GetString();
                
                if (root.TryGetProperty("designPatterns", out var patterns))
                    result.DesignPatterns = patterns.EnumerateArray()
                        .Select(p => p.GetString()!)
                        .Where(s => s != null)
                        .ToList();
                
                if (root.TryGetProperty("suggestedFrameworks", out var frameworks))
                    result.SuggestedFrameworks = frameworks.EnumerateArray()
                        .Select(f => f.GetString()!)
                        .Where(s => s != null)
                        .ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse style JSON");
            }
            
            result.Content = "Comprehensive analysis completed successfully";
            _logger.LogInformation("Comprehensive UI analysis completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Comprehensive UI analysis failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        
        return result;
    }

    #endregion

    #region Helper Methods

    private static string GetCodeGenerationPrompt(string framework)
    {
        var frameworkInstructions = framework switch
        {
            "React" => "Use functional components with hooks, styled-components or CSS modules",
            "Vue" => "Use Vue 3 Composition API with <script setup> and scoped styles",
            "Blazor" => "Use Blazor component syntax (.razor) with @code section",
            "SwiftUI" => "Use SwiftUI View protocol with proper modifiers",
            "MAUI XAML" => "Use XAML with proper namespaces and resource dictionaries",
            "Tailwind CSS" => "Use Tailwind CSS utility classes with responsive prefixes",
            _ => "Use semantic HTML5 with CSS custom properties and modern CSS features"
        };
        
        return $"""
            Analyze this UI design and generate production-ready {framework} code.
            
            Requirements:
            1. **Pixel-Perfect**: Match the design exactly
            2. **Semantic**: Use proper elements and structure
            3. **Accessible**: Include ARIA labels, alt text, proper heading hierarchy
            4. **Responsive**: Mobile-first with appropriate breakpoints
            5. **Interactive**: Include hover, focus, and active states
            
            Framework: {frameworkInstructions}
            
            Output ONLY the complete, working code. No explanations, no markdown code blocks.
            """;
    }

    private static string GetCodeGenerationSystemPrompt(string framework)
    {
        return $"""
            You are an expert frontend developer specializing in {framework}.
            Convert UI designs to production-ready code with:
            - Clean, maintainable code structure
            - CSS custom properties for design tokens
            - Proper accessibility attributes
            - Responsive design patterns
            - Modern best practices
            
            Output ONLY code, never explanations or markdown.
            """;
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

    private static string CleanJsonOutput(string json)
    {
        json = json.Trim();
        
        // Remove markdown code blocks
        if (json.StartsWith("```json"))
            json = json[7..];
        else if (json.StartsWith("```"))
            json = json[3..];
        
        if (json.EndsWith("```"))
            json = json[..^3];
        
        return json.Trim();
    }

    private UIComponentsResult ParseUIComponentsJson(string json)
    {
        var result = new UIComponentsResult();
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("layoutType", out var layout))
                result.LayoutType = layout.GetString();
            
            if (root.TryGetProperty("sections", out var sections))
                result.Sections = sections.EnumerateArray()
                    .Select(s => s.GetString()!)
                    .Where(s => s != null)
                    .ToList();
            
            if (root.TryGetProperty("components", out var components))
                result.Components = ParseComponents(components);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse UI components JSON");
        }
        
        return result;
    }

    private List<ExtractedComponent> ParseComponents(JsonElement componentsElement)
    {
        var components = new List<ExtractedComponent>();
        
        foreach (var comp in componentsElement.EnumerateArray())
        {
            var component = new ExtractedComponent
            {
                Type = comp.TryGetProperty("type", out var t) ? t.GetString() : null,
                Name = comp.TryGetProperty("name", out var n) ? n.GetString() : null,
                Text = comp.TryGetProperty("text", out var txt) ? txt.GetString() : null
            };
            
            if (comp.TryGetProperty("style", out var style))
            {
                component.Style = new ComponentStyle
                {
                    BackgroundColor = style.TryGetProperty("backgroundColor", out var bg) ? bg.GetString() : null,
                    TextColor = style.TryGetProperty("textColor", out var tc) ? tc.GetString() : null,
                    BorderColor = style.TryGetProperty("borderColor", out var bc) ? bc.GetString() : null,
                    BorderRadius = style.TryGetProperty("borderRadius", out var br) ? br.GetString() : null,
                    FontSize = style.TryGetProperty("fontSize", out var fs) ? fs.GetString() : null,
                    FontWeight = style.TryGetProperty("fontWeight", out var fw) ? fw.GetString() : null,
                    Shadow = style.TryGetProperty("shadow", out var sh) ? sh.GetString() : null,
                    Padding = style.TryGetProperty("padding", out var p) ? p.GetString() : null,
                    Margin = style.TryGetProperty("margin", out var m) ? m.GetString() : null
                };
            }
            
            if (comp.TryGetProperty("children", out var children))
                component.Children = ParseComponents(children);
            
            components.Add(component);
        }
        
        return components;
    }

    private ColorPaletteResult ParseColorPaletteJson(string json)
    {
        var result = new ColorPaletteResult();
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            result.PrimaryColor = root.TryGetProperty("primaryColor", out var pc) ? pc.GetString() : null;
            result.SecondaryColor = root.TryGetProperty("secondaryColor", out var sc) ? sc.GetString() : null;
            result.AccentColor = root.TryGetProperty("accentColor", out var ac) ? ac.GetString() : null;
            result.BackgroundColor = root.TryGetProperty("backgroundColor", out var bgc) ? bgc.GetString() : null;
            result.TextColor = root.TryGetProperty("textColor", out var tc) ? tc.GetString() : null;
            result.TextSecondaryColor = root.TryGetProperty("textSecondaryColor", out var tsc) ? tsc.GetString() : null;
            result.BorderColor = root.TryGetProperty("borderColor", out var bc) ? bc.GetString() : null;
            result.SuccessColor = root.TryGetProperty("successColor", out var suc) ? suc.GetString() : null;
            result.WarningColor = root.TryGetProperty("warningColor", out var wc) ? wc.GetString() : null;
            result.ErrorColor = root.TryGetProperty("errorColor", out var ec) ? ec.GetString() : null;
            result.ColorScheme = root.TryGetProperty("colorScheme", out var cs) ? cs.GetString() : null;
            
            if (root.TryGetProperty("gradients", out var gradients))
                result.Gradients = gradients.EnumerateArray().Select(g => g.GetString()!).Where(s => s != null).ToList();
            
            if (root.TryGetProperty("allColors", out var colors))
                result.AllColors = colors.EnumerateArray().Select(c => c.GetString()!).Where(s => s != null).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse color palette JSON");
        }
        
        return result;
    }

    private TypographyResult ParseTypographyJson(string json)
    {
        var result = new TypographyResult();
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            result.PrimaryFont = root.TryGetProperty("primaryFont", out var pf) ? pf.GetString() : null;
            result.SecondaryFont = root.TryGetProperty("secondaryFont", out var sf) ? sf.GetString() : null;
            result.HeadingFont = root.TryGetProperty("headingFont", out var hf) ? hf.GetString() : null;
            result.BodyFont = root.TryGetProperty("bodyFont", out var bf) ? bf.GetString() : null;
            result.LineHeight = root.TryGetProperty("lineHeight", out var lh) ? lh.GetString() : null;
            result.LetterSpacing = root.TryGetProperty("letterSpacing", out var ls) ? ls.GetString() : null;
            
            if (root.TryGetProperty("fontSizes", out var sizes))
            {
                result.FontSizes = new Dictionary<string, string>();
                foreach (var prop in sizes.EnumerateObject())
                {
                    result.FontSizes[prop.Name] = prop.Value.GetString() ?? "";
                }
            }
            
            if (root.TryGetProperty("fontWeights", out var weights))
                result.FontWeights = weights.EnumerateArray().Select(w => w.GetString()!).Where(s => s != null).ToList();
            
            if (root.TryGetProperty("fontUsages", out var usages))
            {
                result.FontUsages = usages.EnumerateArray().Select(u => new FontUsage
                {
                    Element = u.TryGetProperty("element", out var e) ? e.GetString() : null,
                    Font = u.TryGetProperty("font", out var f) ? f.GetString() : null,
                    Size = u.TryGetProperty("size", out var s) ? s.GetString() : null,
                    Weight = u.TryGetProperty("weight", out var w) ? w.GetString() : null,
                    Color = u.TryGetProperty("color", out var c) ? c.GetString() : null
                }).ToList();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse typography JSON");
        }
        
        return result;
    }

    private LayoutResult ParseLayoutJson(string json)
    {
        var result = new LayoutResult();
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            result.LayoutType = root.TryGetProperty("layoutType", out var lt) ? lt.GetString() : null;
            result.ContainerWidth = root.TryGetProperty("containerWidth", out var cw) ? cw.GetString() : null;
            result.ContainerPadding = root.TryGetProperty("containerPadding", out var cp) ? cp.GetString() : null;
            result.Columns = root.TryGetProperty("columns", out var cols) ? cols.GetInt32() : null;
            result.Gap = root.TryGetProperty("gap", out var gap) ? gap.GetString() : null;
            result.RowGap = root.TryGetProperty("rowGap", out var rg) ? rg.GetString() : null;
            result.ColumnGap = root.TryGetProperty("columnGap", out var cg) ? cg.GetString() : null;
            
            if (root.TryGetProperty("breakpoints", out var bp))
                result.Breakpoints = bp.EnumerateArray().Select(b => b.GetString()!).Where(s => s != null).ToList();
            
            if (root.TryGetProperty("sections", out var sections))
            {
                result.Sections = sections.EnumerateArray().Select(s => new LayoutSection
                {
                    Name = s.TryGetProperty("name", out var n) ? n.GetString() : null,
                    Type = s.TryGetProperty("type", out var t) ? t.GetString() : null,
                    Layout = s.TryGetProperty("layout", out var l) ? l.GetString() : null,
                    Width = s.TryGetProperty("width", out var w) ? w.GetString() : null,
                    Height = s.TryGetProperty("height", out var h) ? h.GetString() : null,
                    Padding = s.TryGetProperty("padding", out var p) ? p.GetString() : null,
                    Margin = s.TryGetProperty("margin", out var m) ? m.GetString() : null
                }).ToList();
            }
            
            if (root.TryGetProperty("spacing", out var spacing))
            {
                result.Spacing = new SpacingSystem
                {
                    BaseUnit = spacing.TryGetProperty("baseUnit", out var bu) ? bu.GetString() : null,
                    PaddingPattern = spacing.TryGetProperty("paddingPattern", out var pp) ? pp.GetString() : null,
                    MarginPattern = spacing.TryGetProperty("marginPattern", out var mp) ? mp.GetString() : null,
                    BorderRadius = spacing.TryGetProperty("borderRadius", out var br) ? br.GetString() : null
                };
                
                if (spacing.TryGetProperty("scale", out var scale))
                {
                    result.Spacing.Scale = new Dictionary<string, string>();
                    foreach (var prop in scale.EnumerateObject())
                    {
                        result.Spacing.Scale[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse layout JSON");
        }
        
        return result;
    }

    private IconsResult ParseIconsJson(string json)
    {
        var result = new IconsResult();
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            result.IconStyle = root.TryGetProperty("iconStyle", out var style) ? style.GetString() : null;
            result.SuggestedLibrary = root.TryGetProperty("suggestedLibrary", out var lib) ? lib.GetString() : null;
            
            if (root.TryGetProperty("icons", out var icons))
            {
                result.Icons = icons.EnumerateArray().Select(i => new IdentifiedIcon
                {
                    Name = i.TryGetProperty("name", out var n) ? n.GetString() : null,
                    Description = i.TryGetProperty("description", out var d) ? d.GetString() : null,
                    SuggestedName = i.TryGetProperty("suggestedName", out var sn) ? sn.GetString() : null,
                    Location = i.TryGetProperty("location", out var l) ? l.GetString() : null,
                    Size = i.TryGetProperty("size", out var s) ? s.GetString() : null,
                    Color = i.TryGetProperty("color", out var c) ? c.GetString() : null
                }).ToList();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse icons JSON");
        }
        
        return result;
    }

    private AccessibilityResult ParseAccessibilityJson(string json)
    {
        var result = new AccessibilityResult();
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            result.OverallScore = root.TryGetProperty("overallScore", out var score) ? score.GetString() : null;
            result.HasSufficientTextSize = root.TryGetProperty("hasSufficientTextSize", out var ts) ? ts.GetBoolean() : null;
            result.HasClearHierarchy = root.TryGetProperty("hasClearHierarchy", out var ch) ? ch.GetBoolean() : null;
            result.HasTouchTargets = root.TryGetProperty("hasTouchTargets", out var tt) ? tt.GetBoolean() : null;
            
            if (root.TryGetProperty("recommendations", out var recs))
                result.Recommendations = recs.EnumerateArray().Select(r => r.GetString()!).Where(s => s != null).ToList();
            
            if (root.TryGetProperty("issues", out var issues))
            {
                result.Issues = issues.EnumerateArray().Select(i => new AccessibilityIssue
                {
                    Type = i.TryGetProperty("type", out var t) ? t.GetString() : null,
                    Severity = i.TryGetProperty("severity", out var s) ? s.GetString() : null,
                    Description = i.TryGetProperty("description", out var d) ? d.GetString() : null,
                    Location = i.TryGetProperty("location", out var l) ? l.GetString() : null,
                    Recommendation = i.TryGetProperty("recommendation", out var r) ? r.GetString() : null,
                    WcagCriteria = i.TryGetProperty("wcagCriteria", out var w) ? w.GetString() : null
                }).ToList();
            }
            
            if (root.TryGetProperty("contrastAnalysis", out var contrast))
            {
                result.ContrastAnalysis = new ContrastAnalysis
                {
                    PassesAANormal = contrast.TryGetProperty("passesAANormal", out var aan) ? aan.GetBoolean() : null,
                    PassesAALarge = contrast.TryGetProperty("passesAALarge", out var aal) ? aal.GetBoolean() : null,
                    PassesAAA = contrast.TryGetProperty("passesAAA", out var aaa) ? aaa.GetBoolean() : null
                };
                
                if (contrast.TryGetProperty("analyzedPairs", out var pairs))
                {
                    result.ContrastAnalysis.AnalyzedPairs = pairs.EnumerateArray().Select(p => new ContrastPair
                    {
                        Foreground = p.TryGetProperty("foreground", out var fg) ? fg.GetString() : null,
                        Background = p.TryGetProperty("background", out var bg) ? bg.GetString() : null,
                        Ratio = p.TryGetProperty("ratio", out var ratio) ? ratio.GetDouble() : null,
                        PassesAA = p.TryGetProperty("passesAA", out var aa) ? aa.GetBoolean() : null,
                        PassesAAA = p.TryGetProperty("passesAAA", out var paaa) ? paaa.GetBoolean() : null
                    }).ToList();
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse accessibility JSON");
        }
        
        return result;
    }

    #endregion
}
