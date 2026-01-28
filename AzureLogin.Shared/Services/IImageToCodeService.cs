namespace AzureLogin.Shared.Services;

/// <summary>
/// Interface for converting images to code using Azure OpenAI Vision
/// </summary>
public interface IImageToCodeService
{
    /// <summary>
    /// Generates code from an uploaded image
    /// </summary>
    /// <param name="imageBytes">The image data as bytes</param>
    /// <param name="mimeType">The MIME type of the image (e.g., "image/png")</param>
    /// <param name="framework">Target framework (HTML/CSS, React, Blazor, SwiftUI, MAUI XAML)</param>
    /// <returns>Generated code as string</returns>
    Task<ImageToCodeResult> GenerateCodeFromImageAsync(byte[] imageBytes, string mimeType, string framework = "HTML/CSS");
    
    /// <summary>
    /// Generates code from a base64 encoded image
    /// </summary>
    Task<ImageToCodeResult> GenerateCodeFromBase64Async(string base64Image, string mimeType, string framework = "HTML/CSS");
    
    /// <summary>
    /// Premium multi-pass conversion engine - Best-in-class image to pixel-perfect code.
    /// Combines structure analysis, component detection, design token extraction, 
    /// asset extraction, code generation, and iterative refinement.
    /// </summary>
    /// <param name="imageBytes">The image data as bytes</param>
    /// <param name="mimeType">The MIME type of the image</param>
    /// <param name="options">Conversion options for customization</param>
    /// <param name="onProgress">Optional callback for progress updates</param>
    /// <returns>Premium conversion result with code, assets, and analysis</returns>
    Task<PremiumConversionResult> ConvertWithPremiumEngineAsync(
        byte[] imageBytes, 
        string mimeType, 
        PremiumConversionOptions? options = null,
        Action<ConversionProgress>? onProgress = null);
}

/// <summary>
/// Result of image-to-code conversion
/// </summary>
public class ImageToCodeResult
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Language { get; set; }
    public string? ErrorMessage { get; set; }
    public UIAnalysis? Analysis { get; set; }
}

#region Premium Conversion Engine Classes

/// <summary>
/// Options for premium image-to-code conversion
/// </summary>
public class PremiumConversionOptions
{
    /// <summary>Target framework for code generation</summary>
    public string Framework { get; set; } = "HTML/CSS";
    
    /// <summary>Enable responsive breakpoints generation</summary>
    public bool GenerateResponsive { get; set; } = true;
    
    /// <summary>Extract and include images/icons as base64 or URLs</summary>
    public bool ExtractAssets { get; set; } = true;
    
    /// <summary>Generate CSS custom properties for design tokens</summary>
    public bool UseDesignTokens { get; set; } = true;
    
    /// <summary>Include accessibility attributes (ARIA, alt text)</summary>
    public bool IncludeAccessibility { get; set; } = true;
    
    /// <summary>Generate interactive states (hover, focus, active)</summary>
    public bool IncludeInteractiveStates { get; set; } = true;
    
    /// <summary>Include animations/transitions</summary>
    public bool IncludeAnimations { get; set; } = true;
    
    /// <summary>Quality level: Draft (fast), Standard, Premium (best)</summary>
    public ConversionQuality Quality { get; set; } = ConversionQuality.Premium;
    
    /// <summary>Specific CSS framework to use (Bootstrap, Tailwind, none)</summary>
    public string? CssFramework { get; set; }
    
    /// <summary>Custom instructions for the conversion</summary>
    public string? CustomInstructions { get; set; }
    
    /// <summary>Maximum refinement iterations for Premium quality</summary>
    public int MaxRefinementIterations { get; set; } = 2;
}

public enum ConversionQuality
{
    /// <summary>Fast single-pass generation (1 API call)</summary>
    Draft,
    
    /// <summary>Standard multi-pass with analysis (3 API calls)</summary>
    Standard,
    
    /// <summary>Premium with full analysis, refinement, and validation (5-6 API calls)</summary>
    Premium
}

/// <summary>
/// Progress update during conversion
/// </summary>
public class ConversionProgress
{
    public string Stage { get; set; } = "";
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public string Message { get; set; } = "";
    public int PercentComplete => TotalSteps > 0 ? (CurrentStep * 100) / TotalSteps : 0;
}

/// <summary>
/// Premium conversion result with comprehensive output
/// </summary>
public class PremiumConversionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    // === Generated Code ===
    /// <summary>The main generated code</summary>
    public string? Code { get; set; }
    
    /// <summary>Language hint for syntax highlighting</summary>
    public string? Language { get; set; }
    
    /// <summary>Separate CSS file content (if applicable)</summary>
    public string? CssCode { get; set; }
    
    /// <summary>Separate JavaScript/TypeScript (if applicable)</summary>
    public string? ScriptCode { get; set; }
    
    /// <summary>Code-behind or ViewModel (for Blazor/MAUI)</summary>
    public string? CodeBehind { get; set; }
    
    // === Design Analysis ===
    /// <summary>Comprehensive UI analysis</summary>
    public UIAnalysisDetail? Analysis { get; set; }
    
    /// <summary>Extracted design tokens</summary>
    public DesignTokens? DesignTokens { get; set; }
    
    /// <summary>Component hierarchy tree</summary>
    public ComponentTree? ComponentTree { get; set; }
    
    // === Extracted Assets ===
    /// <summary>Extracted images as base64</summary>
    public List<ExtractedAsset>? ExtractedAssets { get; set; }
    
    // === Quality Metrics ===
    /// <summary>Estimated fidelity score (0-100)</summary>
    public int FidelityScore { get; set; }
    
    /// <summary>Conversion warnings or suggestions</summary>
    public List<string>? Warnings { get; set; }
    
    /// <summary>Processing stages completed</summary>
    public List<ProcessingStage>? ProcessingStages { get; set; }
    
    /// <summary>Total processing time in milliseconds</summary>
    public long TotalProcessingTimeMs { get; set; }
}

/// <summary>
/// Detailed UI analysis for premium conversion
/// </summary>
public class UIAnalysisDetail
{
    public string? LayoutType { get; set; }
    public string? DesignStyle { get; set; }
    public string? Description { get; set; }
    public List<string>? Sections { get; set; }
    public List<DetectedUIComponent>? Components { get; set; }
    public GridAnalysis? Grid { get; set; }
    public SpacingAnalysis? Spacing { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}

public class DetectedUIComponent
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Text { get; set; }
    public BoundingBox? Bounds { get; set; }
    public Dictionary<string, string>? Styles { get; set; }
    public List<DetectedUIComponent>? Children { get; set; }
    public double Confidence { get; set; }
}

// Note: BoundingBox is defined in IImageExtractionService.cs - reuse that class

public class GridAnalysis
{
    public int Columns { get; set; }
    public string? GutterWidth { get; set; }
    public string? ContainerWidth { get; set; }
    public List<string>? Breakpoints { get; set; }
    public string? GridType { get; set; } // flex, grid, table
}

public class SpacingAnalysis
{
    public string? BaseUnit { get; set; }
    public Dictionary<string, string>? Scale { get; set; }
    public string? VerticalRhythm { get; set; }
}

/// <summary>
/// Design tokens extracted from the image
/// </summary>
public class DesignTokens
{
    // Colors
    public Dictionary<string, string>? Colors { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public string? TextSecondaryColor { get; set; }
    public string? BorderColor { get; set; }
    public string? SuccessColor { get; set; }
    public string? ErrorColor { get; set; }
    public string? WarningColor { get; set; }
    public List<GradientToken>? Gradients { get; set; }
    
    // Typography
    public Dictionary<string, FontToken>? Fonts { get; set; }
    public string? PrimaryFont { get; set; }
    public string? HeadingFont { get; set; }
    public string? MonospaceFont { get; set; }
    
    // Spacing
    public string? SpacingUnit { get; set; }
    public Dictionary<string, string>? SpacingScale { get; set; }
    
    // Effects
    public Dictionary<string, string>? Shadows { get; set; }
    public Dictionary<string, string>? BorderRadii { get; set; }
    
    // Transitions
    public string? TransitionDuration { get; set; }
    public string? TransitionEasing { get; set; }
    
    // Generated CSS Variables
    public string? CssVariables { get; set; }
}

public class FontToken
{
    public string? Family { get; set; }
    public string? Size { get; set; }
    public string? Weight { get; set; }
    public string? LineHeight { get; set; }
    public string? LetterSpacing { get; set; }
    public string? TextTransform { get; set; }
}

public class GradientToken
{
    public string? Type { get; set; } // linear, radial
    public string? Direction { get; set; }
    public List<string>? ColorStops { get; set; }
    public string? CssValue { get; set; }
}

/// <summary>
/// Hierarchical component tree
/// </summary>
public class ComponentTree
{
    public ComponentNode? Root { get; set; }
    public int TotalComponents { get; set; }
    public int MaxDepth { get; set; }
    public List<string>? ComponentTypes { get; set; }
}

public class ComponentNode
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? TagName { get; set; }
    public string? ClassName { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
    public Dictionary<string, string>? Styles { get; set; }
    public string? TextContent { get; set; }
    public BoundingBox? Bounds { get; set; }
    public List<ComponentNode>? Children { get; set; }
}

/// <summary>
/// Extracted asset from the image
/// </summary>
public class ExtractedAsset
{
    public string? Type { get; set; } // icon, image, logo, avatar, background
    public string? Name { get; set; }
    public string? Base64Data { get; set; }
    public string? MimeType { get; set; }
    public BoundingBox? Bounds { get; set; }
    public string? SuggestedFilename { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Processing stage information
/// </summary>
public class ProcessingStage
{
    public string? Name { get; set; }
    public bool Completed { get; set; }
    public int DurationMs { get; set; }
    public string? Details { get; set; }
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
}

#endregion

/// <summary>
/// Comprehensive UI analysis result
/// </summary>
public class UIAnalysis
{
    public string? LayoutType { get; set; }
    public List<string>? Sections { get; set; }
    public List<UIComponent>? Components { get; set; }
    public ColorPalette? Colors { get; set; }
    public Typography? Typography { get; set; }
    public Spacing? Spacing { get; set; }
    public VisualEffects? Effects { get; set; }
    public IconInfo? Icons { get; set; }
    public List<string>? InteractiveElements { get; set; }
    public string? RawAnalysis { get; set; }
}

public class UIComponent
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public List<UIComponent>? Children { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}

public class ColorPalette
{
    public string? Primary { get; set; }
    public string? Secondary { get; set; }
    public string? Background { get; set; }
    public string? Text { get; set; }
    public string? Accent { get; set; }
    public List<string>? Gradients { get; set; }
}

public class Typography
{
    public string? HeadingFont { get; set; }
    public string? BodyFont { get; set; }
    public Dictionary<string, string>? Sizes { get; set; }
    public List<string>? Weights { get; set; }
}

public class Spacing
{
    public string? Padding { get; set; }
    public string? Margin { get; set; }
    public string? Gap { get; set; }
    public string? BorderRadius { get; set; }
}

public class VisualEffects
{
    public List<string>? Shadows { get; set; }
    public List<string>? Borders { get; set; }
    public List<string>? Animations { get; set; }
}

public class IconInfo
{
    public string? Style { get; set; }
    public string? Library { get; set; }
    public List<string>? IconsUsed { get; set; }
}
