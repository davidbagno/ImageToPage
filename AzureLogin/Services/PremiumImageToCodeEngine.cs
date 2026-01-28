using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Logging;

namespace AzureLogin.Services;

/// <summary>
/// Premium Image-to-Code Conversion Engine
/// Best-in-class multi-pass conversion combining techniques from leading tools:
/// - Codia.ai: Component hierarchy detection
/// - Fotor/Trickle: Exact pixel measurements and color extraction
/// - Framer: Responsive layout generation
/// - img2html: Semantic HTML5 and accessibility
/// - Refact.ai: Iterative refinement
/// </summary>
public sealed class PremiumImageToCodeEngine
{
    private readonly IAzureOpenAIService _openAI;
    private readonly IVisionService _visionService;
    private readonly IImageExtractionService? _imageExtraction;
    private readonly IAzureVisionService? _azureVision;
    private readonly ILogger<PremiumImageToCodeEngine>? _logger;

    public PremiumImageToCodeEngine(
        IAzureOpenAIService openAI,
        IVisionService visionService,
        IImageExtractionService? imageExtraction = null,
        IAzureVisionService? azureVision = null,
        ILogger<PremiumImageToCodeEngine>? logger = null)
    {
        _openAI = openAI ?? throw new ArgumentNullException(nameof(openAI));
        _visionService = visionService ?? throw new ArgumentNullException(nameof(visionService));
        _imageExtraction = imageExtraction;
        _azureVision = azureVision;
        _logger = logger;
    }

    /// <summary>
    /// Main entry point for premium conversion
    /// </summary>
    public async Task<PremiumConversionResult> ConvertAsync(
        byte[] imageBytes,
        string mimeType,
        PremiumConversionOptions? options = null,
        Action<ConversionProgress>? onProgress = null)
    {
        options ??= new PremiumConversionOptions();
        var result = new PremiumConversionResult
        {
            Success = true,
            ProcessingStages = new List<ProcessingStage>(),
            Warnings = new List<string>()
        };

        var totalStopwatch = Stopwatch.StartNew();
        var base64Image = Convert.ToBase64String(imageBytes);

        try
        {
            // Determine number of steps based on quality
            int totalSteps = options.Quality switch
            {
                ConversionQuality.Draft => 1,
                ConversionQuality.Standard => 3,
                ConversionQuality.Premium => 6,
                _ => 3
            };

            int currentStep = 0;

            // === PASS 1: Structure Analysis ===
            ReportProgress(onProgress, "Analyzing Structure", ++currentStep, totalSteps, "Detecting layout, grid system, and sections...");
            var structureStage = await ExecuteStageAsync("Structure Analysis", async () =>
            {
                result.Analysis = await AnalyzeStructureAsync(base64Image, mimeType);
                return result.Analysis != null;
            });
            result.ProcessingStages.Add(structureStage);

            if (options.Quality == ConversionQuality.Draft)
            {
                // Draft: Single-pass code generation
                ReportProgress(onProgress, "Generating Code", currentStep, totalSteps, "Creating code output...");
                var codeResult = await GenerateCodeSinglePassAsync(base64Image, mimeType, options);
                result.Code = codeResult.Code;
                result.Language = codeResult.Language;
                result.FidelityScore = 70; // Estimate for draft
            }
            else
            {
                // === PASS 2: Component Detection ===
                ReportProgress(onProgress, "Detecting Components", ++currentStep, totalSteps, "Identifying UI elements with bounding boxes...");
                var componentStage = await ExecuteStageAsync("Component Detection", async () =>
                {
                    result.ComponentTree = await DetectComponentsAsync(base64Image, mimeType);
                    return result.ComponentTree?.Root != null;
                });
                result.ProcessingStages.Add(componentStage);

                // === PASS 3: Design Token Extraction ===
                ReportProgress(onProgress, "Extracting Design Tokens", ++currentStep, totalSteps, "Analyzing colors, typography, and spacing...");
                var tokenStage = await ExecuteStageAsync("Design Token Extraction", async () =>
                {
                    result.DesignTokens = await ExtractDesignTokensAsync(base64Image, mimeType);
                    return result.DesignTokens != null;
                });
                result.ProcessingStages.Add(tokenStage);

                if (options.Quality == ConversionQuality.Premium)
                {
                    // === PASS 4: Asset Extraction (Premium only) ===
                    if (options.ExtractAssets && _imageExtraction != null)
                    {
                        ReportProgress(onProgress, "Extracting Assets", ++currentStep, totalSteps, "Detecting and cropping images, icons, logos...");
                        var assetStage = await ExecuteStageAsync("Asset Extraction", async () =>
                        {
                            result.ExtractedAssets = await ExtractAssetsAsync(imageBytes, mimeType);
                            return true;
                        });
                        result.ProcessingStages.Add(assetStage);
                    }
                    else
                    {
                        currentStep++;
                    }

                    // === PASS 5: Premium Code Generation ===
                    ReportProgress(onProgress, "Generating Code", ++currentStep, totalSteps, "Creating pixel-perfect code with all analysis data...");
                    var codeStage = await ExecuteStageAsync("Code Generation", async () =>
                    {
                        var codeResult = await GenerateCodeWithContextAsync(base64Image, mimeType, options, result);
                        result.Code = codeResult.Code;
                        result.Language = codeResult.Language;
                        result.CssCode = codeResult.CssCode;
                        result.ScriptCode = codeResult.ScriptCode;
                        result.CodeBehind = codeResult.CodeBehind;
                        return !string.IsNullOrEmpty(result.Code);
                    });
                    result.ProcessingStages.Add(codeStage);

                    // === PASS 6: Refinement (Premium only) ===
                    ReportProgress(onProgress, "Refining Output", ++currentStep, totalSteps, "Validating and improving code accuracy...");
                    var refineStage = await ExecuteStageAsync("Refinement", async () =>
                    {
                        var refined = await RefineCodeAsync(base64Image, mimeType, result.Code!, options);
                        if (!string.IsNullOrEmpty(refined.ImprovedCode))
                        {
                            result.Code = refined.ImprovedCode;
                            result.FidelityScore = refined.FidelityScore;
                            if (refined.Suggestions?.Count > 0)
                                result.Warnings!.AddRange(refined.Suggestions);
                        }
                        return true;
                    });
                    result.ProcessingStages.Add(refineStage);
                }
                else
                {
                    // Standard: Code generation without refinement
                    ReportProgress(onProgress, "Generating Code", ++currentStep, totalSteps, "Creating code with analysis data...");
                    var codeResult = await GenerateCodeWithContextAsync(base64Image, mimeType, options, result);
                    result.Code = codeResult.Code;
                    result.Language = codeResult.Language;
                    result.FidelityScore = 80; // Estimate for standard
                }
            }

            // Generate CSS variables from design tokens
            if (result.DesignTokens != null && options.UseDesignTokens)
            {
                result.DesignTokens.CssVariables = GenerateCssVariables(result.DesignTokens);
            }

            result.TotalProcessingTimeMs = totalStopwatch.ElapsedMilliseconds;
            ReportProgress(onProgress, "Complete", totalSteps, totalSteps, $"Conversion completed in {result.TotalProcessingTimeMs}ms");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Premium conversion failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.TotalProcessingTimeMs = totalStopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    #region Pass 1: Structure Analysis

    private async Task<UIAnalysisDetail> AnalyzeStructureAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Analyze this UI screenshot's structure comprehensively. Return JSON only:
            {
                "layoutType": "single-column|two-column|grid|dashboard|form|landing|modal|card",
                "designStyle": "modern|minimal|material|glassmorphism|flat|skeuomorphic|corporate",
                "description": "Brief description of the UI",
                "sections": ["header", "hero", "content", "sidebar", "footer"],
                "grid": {
                    "columns": 12,
                    "gutterWidth": "24px",
                    "containerWidth": "1200px",
                    "gridType": "flex|grid|table",
                    "breakpoints": ["576px", "768px", "992px", "1200px"]
                },
                "spacing": {
                    "baseUnit": "8px",
                    "scale": {"xs": "4px", "sm": "8px", "md": "16px", "lg": "24px", "xl": "32px"},
                    "verticalRhythm": "24px"
                },
                "imageWidth": 1920,
                "imageHeight": 1080
            }
            Return ONLY valid JSON, no markdown or explanation.
            """;

        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            var json = CleanJsonResponse(response);
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var analysis = new UIAnalysisDetail
            {
                LayoutType = GetString(root, "layoutType"),
                DesignStyle = GetString(root, "designStyle"),
                Description = GetString(root, "description"),
                ImageWidth = GetInt(root, "imageWidth") ?? 0,
                ImageHeight = GetInt(root, "imageHeight") ?? 0
            };

            if (root.TryGetProperty("sections", out var sections))
            {
                analysis.Sections = sections.EnumerateArray()
                    .Select(s => s.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            if (root.TryGetProperty("grid", out var grid))
            {
                analysis.Grid = new GridAnalysis
                {
                    Columns = GetInt(grid, "columns") ?? 12,
                    GutterWidth = GetString(grid, "gutterWidth"),
                    ContainerWidth = GetString(grid, "containerWidth"),
                    GridType = GetString(grid, "gridType")
                };
                
                if (grid.TryGetProperty("breakpoints", out var bp))
                {
                    analysis.Grid.Breakpoints = bp.EnumerateArray()
                        .Select(b => b.GetString() ?? "")
                        .ToList();
                }
            }

            if (root.TryGetProperty("spacing", out var spacing))
            {
                analysis.Spacing = new SpacingAnalysis
                {
                    BaseUnit = GetString(spacing, "baseUnit"),
                    VerticalRhythm = GetString(spacing, "verticalRhythm")
                };
                
                if (spacing.TryGetProperty("scale", out var scale))
                {
                    analysis.Spacing.Scale = scale.EnumerateObject()
                        .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
                }
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Structure analysis failed, using defaults");
            return new UIAnalysisDetail
            {
                LayoutType = "unknown",
                DesignStyle = "modern",
                Grid = new GridAnalysis { Columns = 12 }
            };
        }
    }

    #endregion

    #region Pass 2: Component Detection

    private async Task<ComponentTree> DetectComponentsAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Detect ALL UI components in this screenshot with their hierarchy. Return JSON only:
            {
                "root": {
                    "type": "page",
                    "tagName": "div",
                    "className": "container",
                    "children": [
                        {
                            "type": "header|nav|button|input|card|image|text|icon|form|list|table|modal|dropdown",
                            "name": "descriptive-name",
                            "tagName": "header|nav|button|input|div|img|p|h1|span|form|ul|table",
                            "className": "suggested-class",
                            "textContent": "visible text if any",
                            "bounds": {"x": 0, "y": 0, "width": 100, "height": 50},
                            "styles": {
                                "backgroundColor": "#hex",
                                "color": "#hex",
                                "fontSize": "16px",
                                "fontWeight": "600",
                                "padding": "12px 24px",
                                "borderRadius": "8px"
                            },
                            "attributes": {"href": "#", "type": "submit"},
                            "children": []
                        }
                    ]
                },
                "totalComponents": 25,
                "maxDepth": 5,
                "componentTypes": ["header", "button", "input", "card", "image"]
            }
            Be thorough - detect every visible element. Return ONLY valid JSON.
            """;

        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            var json = CleanJsonResponse(response);
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tree = new ComponentTree
            {
                TotalComponents = GetInt(root, "totalComponents") ?? 0,
                MaxDepth = GetInt(root, "maxDepth") ?? 0
            };

            if (root.TryGetProperty("componentTypes", out var types))
            {
                tree.ComponentTypes = types.EnumerateArray()
                    .Select(t => t.GetString() ?? "")
                    .ToList();
            }

            if (root.TryGetProperty("root", out var rootNode))
            {
                tree.Root = ParseComponentNode(rootNode);
            }

            return tree;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Component detection failed");
            return new ComponentTree { TotalComponents = 0 };
        }
    }

    private ComponentNode? ParseComponentNode(JsonElement element)
    {
        var node = new ComponentNode
        {
            Type = GetString(element, "type"),
            Name = GetString(element, "name"),
            TagName = GetString(element, "tagName"),
            ClassName = GetString(element, "className"),
            TextContent = GetString(element, "textContent")
        };

        if (element.TryGetProperty("bounds", out var bounds))
        {
            node.Bounds = new BoundingBox
            {
                X = GetInt(bounds, "x") ?? 0,
                Y = GetInt(bounds, "y") ?? 0,
                Width = GetInt(bounds, "width") ?? 0,
                Height = GetInt(bounds, "height") ?? 0
            };
        }

        if (element.TryGetProperty("styles", out var styles))
        {
            node.Styles = styles.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
        }

        if (element.TryGetProperty("attributes", out var attrs))
        {
            node.Attributes = attrs.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
        }

        if (element.TryGetProperty("children", out var children))
        {
            node.Children = children.EnumerateArray()
                .Select(ParseComponentNode)
                .Where(c => c != null)
                .Cast<ComponentNode>()
                .ToList();
        }

        return node;
    }

    #endregion

    #region Pass 3: Design Token Extraction

    private async Task<DesignTokens> ExtractDesignTokensAsync(string base64Image, string mimeType)
    {
        var prompt = """
            Extract ALL design tokens from this UI with exact values. Return JSON only:
            {
                "colors": {
                    "primary": "#667eea",
                    "secondary": "#764ba2",
                    "accent": "#ec4899",
                    "background": "#ffffff",
                    "backgroundSecondary": "#f8fafc",
                    "text": "#1e293b",
                    "textSecondary": "#64748b",
                    "border": "#e2e8f0",
                    "success": "#10b981",
                    "error": "#ef4444",
                    "warning": "#f59e0b"
                },
                "gradients": [
                    {
                        "type": "linear",
                        "direction": "135deg",
                        "colorStops": ["#667eea 0%", "#764ba2 50%", "#ec4899 100%"],
                        "cssValue": "linear-gradient(135deg, #667eea 0%, #764ba2 50%, #ec4899 100%)"
                    }
                ],
                "fonts": {
                    "heading": {"family": "Inter", "size": "32px", "weight": "700", "lineHeight": "1.2"},
                    "subheading": {"family": "Inter", "size": "24px", "weight": "600", "lineHeight": "1.3"},
                    "body": {"family": "Inter", "size": "16px", "weight": "400", "lineHeight": "1.5"},
                    "small": {"family": "Inter", "size": "14px", "weight": "400", "lineHeight": "1.4"},
                    "button": {"family": "Inter", "size": "14px", "weight": "600", "lineHeight": "1", "textTransform": "none"}
                },
                "primaryFont": "Inter, system-ui, sans-serif",
                "headingFont": "Inter, system-ui, sans-serif",
                "spacingUnit": "8px",
                "spacingScale": {"xs": "4px", "sm": "8px", "md": "16px", "lg": "24px", "xl": "32px", "2xl": "48px"},
                "shadows": {
                    "sm": "0 1px 2px rgba(0,0,0,0.05)",
                    "md": "0 4px 6px rgba(0,0,0,0.1)",
                    "lg": "0 10px 15px rgba(0,0,0,0.1)",
                    "xl": "0 20px 25px rgba(0,0,0,0.15)"
                },
                "borderRadii": {"sm": "4px", "md": "8px", "lg": "12px", "xl": "16px", "full": "9999px"},
                "transitionDuration": "200ms",
                "transitionEasing": "cubic-bezier(0.4, 0, 0.2, 1)"
            }
            Extract EXACT hex colors from the image. Return ONLY valid JSON.
            """;

        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            var json = CleanJsonResponse(response);
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tokens = new DesignTokens
            {
                PrimaryFont = GetString(root, "primaryFont"),
                HeadingFont = GetString(root, "headingFont"),
                SpacingUnit = GetString(root, "spacingUnit"),
                TransitionDuration = GetString(root, "transitionDuration"),
                TransitionEasing = GetString(root, "transitionEasing")
            };

            // Parse colors
            if (root.TryGetProperty("colors", out var colors))
            {
                tokens.Colors = colors.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
                
                tokens.PrimaryColor = GetString(colors, "primary");
                tokens.SecondaryColor = GetString(colors, "secondary");
                tokens.AccentColor = GetString(colors, "accent");
                tokens.BackgroundColor = GetString(colors, "background");
                tokens.TextColor = GetString(colors, "text");
                tokens.TextSecondaryColor = GetString(colors, "textSecondary");
                tokens.BorderColor = GetString(colors, "border");
                tokens.SuccessColor = GetString(colors, "success");
                tokens.ErrorColor = GetString(colors, "error");
                tokens.WarningColor = GetString(colors, "warning");
            }

            // Parse gradients
            if (root.TryGetProperty("gradients", out var gradients))
            {
                tokens.Gradients = gradients.EnumerateArray()
                    .Select(g => new GradientToken
                    {
                        Type = GetString(g, "type"),
                        Direction = GetString(g, "direction"),
                        CssValue = GetString(g, "cssValue"),
                        ColorStops = g.TryGetProperty("colorStops", out var stops)
                            ? stops.EnumerateArray().Select(s => s.GetString() ?? "").ToList()
                            : null
                    })
                    .ToList();
            }

            // Parse fonts
            if (root.TryGetProperty("fonts", out var fonts))
            {
                tokens.Fonts = new Dictionary<string, FontToken>();
                foreach (var font in fonts.EnumerateObject())
                {
                    tokens.Fonts[font.Name] = new FontToken
                    {
                        Family = GetString(font.Value, "family"),
                        Size = GetString(font.Value, "size"),
                        Weight = GetString(font.Value, "weight"),
                        LineHeight = GetString(font.Value, "lineHeight"),
                        LetterSpacing = GetString(font.Value, "letterSpacing"),
                        TextTransform = GetString(font.Value, "textTransform")
                    };
                }
            }

            // Parse spacing scale
            if (root.TryGetProperty("spacingScale", out var spacingScale))
            {
                tokens.SpacingScale = spacingScale.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            }

            // Parse shadows
            if (root.TryGetProperty("shadows", out var shadows))
            {
                tokens.Shadows = shadows.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            }

            // Parse border radii
            if (root.TryGetProperty("borderRadii", out var radii))
            {
                tokens.BorderRadii = radii.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            }

            return tokens;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Design token extraction failed");
            return new DesignTokens();
        }
    }

    #endregion

    #region Pass 4: Asset Extraction

    private async Task<List<ExtractedAsset>> ExtractAssetsAsync(byte[] imageBytes, string mimeType)
    {
        var assets = new List<ExtractedAsset>();

        if (_imageExtraction == null)
            return assets;

        try
        {
            var options = new ExtractionOptions
            {
                Mode = CropMode.AIRegions,
                ExtractionType = "all",
                MinComponentSize = 16,
                DetectOnly = false
            };

            var result = await _imageExtraction.ExtractWithOptionsAsync(imageBytes, mimeType, options);
            
            if (result.Success && result.Images != null)
            {
                foreach (var img in result.Images)
                {
                    assets.Add(new ExtractedAsset
                    {
                        Type = img.ImageType ?? "image",
                        Name = img.Description ?? $"asset_{assets.Count + 1}",
                        Base64Data = img.Base64Data,
                        MimeType = "image/png" ?? "image/png",
                        Width = img.Width,
                        Height = img.Height,
                        SuggestedFilename = $"{img.ImageType ?? "asset"}_{assets.Count + 1}.png",
                        Bounds = img.BoundingBox != null ? new BoundingBox
                        {
                            X = img.BoundingBox.X,
                            Y = img.BoundingBox.Y,
                            Width = img.BoundingBox.Width,
                            Height = img.BoundingBox.Height
                        } : null
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Asset extraction failed");
        }

        return assets;
    }

    #endregion

    #region Pass 5: Code Generation

    private async Task<(string? Code, string? Language, string? CssCode, string? ScriptCode, string? CodeBehind)> 
        GenerateCodeWithContextAsync(
            string base64Image, 
            string mimeType, 
            PremiumConversionOptions options,
            PremiumConversionResult context)
    {
        var contextPrompt = BuildContextPrompt(options, context);
        
        var prompt = $"""
            Generate production-ready {options.Framework} code for this UI screenshot.
            
            === ANALYSIS CONTEXT ===
            {contextPrompt}
            
            === REQUIREMENTS ===
            1. PIXEL-PERFECT: Match the design exactly - colors, spacing, typography
            2. SEMANTIC: Use proper HTML5 elements (header, nav, main, section, article, aside, footer)
            3. ACCESSIBLE: Include ARIA labels, roles, alt text, proper heading hierarchy
            4. RESPONSIVE: Mobile-first with breakpoints at 640px, 768px, 1024px, 1280px
            {(options.UseDesignTokens ? "5. DESIGN TOKENS: Use CSS custom properties (--color-primary, --spacing-md, etc.)" : "")}
            {(options.IncludeInteractiveStates ? "6. INTERACTIVE: Include :hover, :focus, :active, :disabled states" : "")}
            {(options.IncludeAnimations ? "7. ANIMATIONS: Add subtle transitions (200ms ease) on interactive elements" : "")}
            {(options.CssFramework == "Tailwind" ? "8. Use Tailwind CSS utility classes" : options.CssFramework == "Bootstrap" ? "8. Use Bootstrap 5 classes" : "")}
            {(!string.IsNullOrEmpty(options.CustomInstructions) ? $"9. CUSTOM: {options.CustomInstructions}" : "")}
            
            === FRAMEWORK: {options.Framework} ===
            {GetFrameworkInstructions(options.Framework)}
            
            Output ONLY the complete, working code. No explanations or markdown code blocks.
            """;

        var systemPrompt = $"""
            You are an elite frontend developer specializing in pixel-perfect UI implementation.
            You have analyzed this design and extracted:
            - Layout structure and grid system
            - Complete component hierarchy
            - Exact design tokens (colors, fonts, spacing)
            
            Generate code that would make this design's creator proud - every pixel matters.
            Framework: {options.Framework}
            """;

        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt, systemPrompt);
            var code = CleanCodeResponse(response);
            var language = GetLanguageHint(options.Framework);

            // For certain frameworks, extract separate CSS/JS
            string? cssCode = null;
            string? scriptCode = null;
            string? codeBehind = null;

            if (options.Framework.Contains("Blazor") || options.Framework.Contains("Razor"))
            {
                codeBehind = ExtractCodeBehind(code);
            }

            return (code, language, cssCode, scriptCode, codeBehind);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Code generation failed");
            return (null, null, null, null, null);
        }
    }

    private async Task<(string? Code, string? Language)> GenerateCodeSinglePassAsync(
        string base64Image, string mimeType, PremiumConversionOptions options)
    {
        var prompt = $"""
            Generate production-ready {options.Framework} code for this UI screenshot.
            Be pixel-perfect, semantic, accessible, and responsive.
            {GetFrameworkInstructions(options.Framework)}
            Output ONLY complete, working code. No explanations.
            """;

        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            return (CleanCodeResponse(response), GetLanguageHint(options.Framework));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Single-pass code generation failed");
            return (null, null);
        }
    }

    private string BuildContextPrompt(PremiumConversionOptions options, PremiumConversionResult context)
    {
        var sb = new StringBuilder();

        if (context.Analysis != null)
        {
            sb.AppendLine($"Layout: {context.Analysis.LayoutType} ({context.Analysis.DesignStyle})");
            if (context.Analysis.Grid != null)
                sb.AppendLine($"Grid: {context.Analysis.Grid.Columns} columns, {context.Analysis.Grid.GutterWidth} gutter");
            if (context.Analysis.Sections?.Count > 0)
                sb.AppendLine($"Sections: {string.Join(", ", context.Analysis.Sections)}");
        }

        if (context.DesignTokens != null)
        {
            sb.AppendLine("\nDesign Tokens:");
            if (!string.IsNullOrEmpty(context.DesignTokens.PrimaryColor))
                sb.AppendLine($"  Primary: {context.DesignTokens.PrimaryColor}");
            if (!string.IsNullOrEmpty(context.DesignTokens.SecondaryColor))
                sb.AppendLine($"  Secondary: {context.DesignTokens.SecondaryColor}");
            if (!string.IsNullOrEmpty(context.DesignTokens.BackgroundColor))
                sb.AppendLine($"  Background: {context.DesignTokens.BackgroundColor}");
            if (!string.IsNullOrEmpty(context.DesignTokens.TextColor))
                sb.AppendLine($"  Text: {context.DesignTokens.TextColor}");
            if (!string.IsNullOrEmpty(context.DesignTokens.PrimaryFont))
                sb.AppendLine($"  Font: {context.DesignTokens.PrimaryFont}");
        }

        if (context.ComponentTree?.ComponentTypes?.Count > 0)
        {
            sb.AppendLine($"\nComponents detected: {string.Join(", ", context.ComponentTree.ComponentTypes)}");
            sb.AppendLine($"Total: {context.ComponentTree.TotalComponents}, Max depth: {context.ComponentTree.MaxDepth}");
        }

        return sb.ToString();
    }

    private string GetFrameworkInstructions(string framework) => framework switch
    {
        "React" => """
            - Use functional components with hooks (useState, useEffect)
            - Use CSS modules or styled-components
            - Export default component
            - Use semantic JSX
            """,
        "Vue" => """
            - Use Vue 3 Composition API with <script setup>
            - Use scoped <style> block
            - Use ref() and reactive()
            - Use v-bind and v-on directives
            """,
        "Blazor Razor" or "Blazor" => """
            - Use proper .razor component syntax
            - Include @code block for C# logic
            - Use @bind for two-way binding
            - Use Bootstrap 5 classes
            - Include [Parameter] attributes
            - Use EventCallback for events
            """,
        "Razor Pages" => """
            - Use @page and @model directives
            - Use tag helpers (asp-for, asp-action)
            - Include anti-forgery tokens for forms
            - Generate PageModel class with handlers
            """,
        "SwiftUI" => """
            - Use View protocol
            - Chain modifiers properly
            - Use @State, @Binding, @ObservedObject
            - Use SF Symbols for icons
            """,
        "MAUI XAML" => """
            - Use proper XAML namespaces
            - Use Grid, StackLayout, FlexLayout
            - Define styles in ResourceDictionary
            - Use x:Name and x:Bind
            """,
        "Tailwind CSS" => """
            - Use Tailwind utility classes only
            - Use responsive prefixes (sm:, md:, lg:, xl:)
            - Use Tailwind color classes
            - Include dark mode variants if applicable
            """,
        "Angular" => """
            - Use standalone component
            - Use Angular template syntax
            - Include TypeScript component class
            - Use Angular Material if UI matches
            """,
        _ => """
            - Use semantic HTML5 elements
            - Use CSS custom properties for theming
            - Use Flexbox and CSS Grid for layout
            - Include media queries for responsiveness
            """
    };

    private string GetLanguageHint(string framework) => framework switch
    {
        "React" => "jsx",
        "Vue" => "vue",
        "Blazor Razor" or "Blazor" => "razor",
        "Razor Pages" => "cshtml",
        "SwiftUI" => "swift",
        "MAUI XAML" => "xml",
        "Angular" => "typescript",
        "Tailwind CSS" => "html",
        _ => "html"
    };

    private string? ExtractCodeBehind(string code)
    {
        // For Blazor, the @code block is embedded - we could split it out
        // For now, return null as it's inline
        return null;
    }

    #endregion

    #region Pass 6: Refinement

    private async Task<(string? ImprovedCode, int FidelityScore, List<string>? Suggestions)> RefineCodeAsync(
        string base64Image, string mimeType, string currentCode, PremiumConversionOptions options)
    {
        var truncatedCode = currentCode.Length > 4000 
            ? currentCode[..4000] + "\n... (truncated)" 
            : currentCode;
            
        var prompt = $"""
            Compare this UI screenshot with the generated code below and improve it.
            
            === CURRENT CODE ===
            {truncatedCode}
            
            === TASK ===
            1. Identify any visual differences between the screenshot and what this code would render
            2. Fix any pixel-level discrepancies (colors, spacing, sizing, alignment)
            3. Ensure all elements are present and positioned correctly
            4. Rate the fidelity from 0-100
            
            Return JSON only with keys: fidelityScore (number), issues (array), suggestions (array), improvedCode (string)
            
            If the code is already good (90+), return the same code in improvedCode.
            Return ONLY valid JSON.
            """;

        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            var json = CleanJsonResponse(response);
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var score = GetInt(root, "fidelityScore") ?? 75;
            var improvedCode = GetString(root, "improvedCode");
            
            List<string>? suggestions = null;
            if (root.TryGetProperty("suggestions", out var sugg))
            {
                suggestions = sugg.EnumerateArray()
                    .Select(s => s.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            // Only use improved code if it's substantially different and better
            if (!string.IsNullOrEmpty(improvedCode) && improvedCode.Length > 100)
            {
                return (CleanCodeResponse(improvedCode), score, suggestions);
            }

            return (null, score, suggestions);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Refinement failed, keeping original code");
            return (null, 75, null);
        }
    }

    #endregion

    #region CSS Variable Generation

    private string GenerateCssVariables(DesignTokens tokens)
    {
        var sb = new StringBuilder();
        sb.AppendLine(":root {");
        sb.AppendLine("  /* Colors */");

        if (tokens.Colors != null)
        {
            foreach (var (name, value) in tokens.Colors)
            {
                sb.AppendLine($"  --color-{ToKebabCase(name)}: {value};");
            }
        }

        if (tokens.Gradients?.Count > 0)
        {
            sb.AppendLine("\n  /* Gradients */");
            for (int i = 0; i < tokens.Gradients.Count; i++)
            {
                var g = tokens.Gradients[i];
                if (!string.IsNullOrEmpty(g.CssValue))
                    sb.AppendLine($"  --gradient-{i + 1}: {g.CssValue};");
            }
        }

        sb.AppendLine("\n  /* Typography */");
        if (!string.IsNullOrEmpty(tokens.PrimaryFont))
            sb.AppendLine($"  --font-primary: {tokens.PrimaryFont};");
        if (!string.IsNullOrEmpty(tokens.HeadingFont))
            sb.AppendLine($"  --font-heading: {tokens.HeadingFont};");

        if (tokens.Fonts != null)
        {
            foreach (var (name, font) in tokens.Fonts)
            {
                if (!string.IsNullOrEmpty(font.Size))
                    sb.AppendLine($"  --font-size-{ToKebabCase(name)}: {font.Size};");
                if (!string.IsNullOrEmpty(font.Weight))
                    sb.AppendLine($"  --font-weight-{ToKebabCase(name)}: {font.Weight};");
            }
        }

        sb.AppendLine("\n  /* Spacing */");
        if (!string.IsNullOrEmpty(tokens.SpacingUnit))
            sb.AppendLine($"  --spacing-unit: {tokens.SpacingUnit};");
        
        if (tokens.SpacingScale != null)
        {
            foreach (var (name, value) in tokens.SpacingScale)
            {
                sb.AppendLine($"  --spacing-{name}: {value};");
            }
        }

        sb.AppendLine("\n  /* Effects */");
        if (tokens.Shadows != null)
        {
            foreach (var (name, value) in tokens.Shadows)
            {
                sb.AppendLine($"  --shadow-{name}: {value};");
            }
        }

        if (tokens.BorderRadii != null)
        {
            foreach (var (name, value) in tokens.BorderRadii)
            {
                sb.AppendLine($"  --radius-{name}: {value};");
            }
        }

        sb.AppendLine("\n  /* Transitions */");
        if (!string.IsNullOrEmpty(tokens.TransitionDuration))
            sb.AppendLine($"  --transition-duration: {tokens.TransitionDuration};");
        if (!string.IsNullOrEmpty(tokens.TransitionEasing))
            sb.AppendLine($"  --transition-easing: {tokens.TransitionEasing};");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('-');
                sb.Append(char.ToLower(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    #endregion

    #region Helpers

    private static void ReportProgress(Action<ConversionProgress>? onProgress, string stage, int current, int total, string message)
    {
        onProgress?.Invoke(new ConversionProgress
        {
            Stage = stage,
            CurrentStep = current,
            TotalSteps = total,
            Message = message
        });
    }

    private async Task<ProcessingStage> ExecuteStageAsync(string name, Func<Task<bool>> action)
    {
        var sw = Stopwatch.StartNew();
        var stage = new ProcessingStage { Name = name };

        try
        {
            stage.Success = await action();
            stage.Completed = true;
        }
        catch (Exception ex)
        {
            stage.Success = false;
            stage.Error = ex.Message;
            _logger?.LogWarning(ex, "Stage {Stage} failed", name);
        }

        stage.DurationMs = (int)sw.ElapsedMilliseconds;
        return stage;
    }

    private static string CleanJsonResponse(string response)
    {
        response = response.Trim();
        if (response.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            response = response[7..];
        else if (response.StartsWith("```"))
            response = response[3..];
        if (response.EndsWith("```"))
            response = response[..^3];
        return response.Trim();
    }

    private static string CleanCodeResponse(string response)
    {
        response = response.Trim();
        if (response.StartsWith("```"))
        {
            var newline = response.IndexOf('\n');
            if (newline > 0) response = response[(newline + 1)..];
        }
        if (response.EndsWith("```"))
            response = response[..^3];
        return response.Trim();
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) ? val.GetString() : null;

    private static int? GetInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) && val.TryGetInt32(out var i) ? i : null;

    #endregion
}
