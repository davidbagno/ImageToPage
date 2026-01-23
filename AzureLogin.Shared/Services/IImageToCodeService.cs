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
