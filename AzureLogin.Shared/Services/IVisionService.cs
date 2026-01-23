namespace AzureLogin.Shared.Services;

/// <summary>
/// Interface for comprehensive image analysis using Azure OpenAI GPT-4o Vision
/// Provides all vision capabilities for UI analysis, code generation, and image understanding
/// </summary>
public interface IVisionService
{
    // ===== EXISTING METHODS =====
    
    /// <summary>Analyzes an image with a custom prompt</summary>
    Task<VisionResult> AnalyzeImageAsync(string base64Image, string mimeType, string prompt);
    
    /// <summary>Generates code from a UI image for a specific framework</summary>
    Task<VisionResult> GenerateCodeFromImageAsync(string base64Image, string mimeType, string framework = "HTML/CSS");
    
    /// <summary>Provides a detailed description of the image</summary>
    Task<VisionResult> DescribeImageAsync(string base64Image, string mimeType);
    
    /// <summary>Extracts all UI components from an image as structured data</summary>
    Task<UIComponentsResult> ExtractUIComponentsAsync(string base64Image, string mimeType);
    
    /// <summary>Extracts the color palette from an image</summary>
    Task<ColorPaletteResult> ExtractColorPaletteAsync(string base64Image, string mimeType);
    
    /// <summary>Extracts typography information from an image</summary>
    Task<TypographyResult> ExtractTypographyAsync(string base64Image, string mimeType);
    
    /// <summary>Extracts layout structure and spacing from an image</summary>
    Task<LayoutResult> ExtractLayoutAsync(string base64Image, string mimeType);
    
    /// <summary>Performs comprehensive UI analysis returning all extracted information</summary>
    Task<ComprehensiveUIAnalysis> AnalyzeUIAsync(string base64Image, string mimeType);
    
    /// <summary>Extracts text content from an image (OCR-like functionality)</summary>
    Task<VisionResult> ExtractTextAsync(string base64Image, string mimeType);
    
    /// <summary>Identifies icons and their likely sources/libraries</summary>
    Task<IconsResult> IdentifyIconsAsync(string base64Image, string mimeType);
    
    /// <summary>Suggests improvements for a UI design</summary>
    Task<VisionResult> SuggestImprovementsAsync(string base64Image, string mimeType);
    
    /// <summary>Compares two UI images and describes differences</summary>
    Task<VisionResult> CompareImagesAsync(string base64Image1, string base64Image2, string mimeType);
    
    /// <summary>Generates accessibility recommendations for a UI</summary>
    Task<AccessibilityResult> AnalyzeAccessibilityAsync(string base64Image, string mimeType);
    
    // ===== NEW METHODS =====
    
    /// <summary>Compares multiple images and describes differences</summary>
    Task<ImageComparisonResult> CompareMultipleImagesAsync(List<string> base64Images, string mimeType);
    
    /// <summary>Answers questions about specific areas in an image</summary>
    Task<VisionResult> AskAboutImageAsync(string base64Image, string mimeType, string question);
    
    /// <summary>Parses documents like invoices, receipts, forms</summary>
    Task<DocumentParseResult> ParseDocumentAsync(string base64Image, string mimeType);
    
    /// <summary>Extracts data from charts and graphs</summary>
    Task<ChartDataResult> ExtractChartDataAsync(string base64Image, string mimeType);
    
    /// <summary>Parses mathematical notation and equations</summary>
    Task<MathOcrResult> ParseMathEquationAsync(string base64Image, string mimeType);
    
    /// <summary>Reads handwritten text</summary>
    Task<HandwritingResult> ReadHandwritingAsync(string base64Image, string mimeType);
    
    /// <summary>Extracts table data to structured format</summary>
    Task<TableExtractionResult> ExtractTableAsync(string base64Image, string mimeType);
    
    /// <summary>Answers spatial questions about image ("What's to the left of X?")</summary>
    Task<VisionResult> AnswerSpatialQuestionAsync(string base64Image, string mimeType, string question);
    
    /// <summary>Highlights differences between two image versions</summary>
    Task<ImageDiffResult> GetImageDiffAsync(string base64Image1, string base64Image2, string mimeType);
}

#region Result Classes

/// <summary>
/// Base result class for vision operations
/// </summary>
public class VisionResult
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Creates a successful result with content</summary>
    public static VisionResult Ok(string content) => new() { Success = true, Content = content };

    /// <summary>Creates a failed result with error message</summary>
    public static VisionResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result containing extracted UI components
/// </summary>
public class UIComponentsResult : VisionResult
{
    public List<ExtractedComponent>? Components { get; set; }
    public string? LayoutType { get; set; }
    public List<string>? Sections { get; set; }
}

/// <summary>
/// Represents an extracted UI component
/// </summary>
public class ExtractedComponent
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Text { get; set; }
    public ComponentStyle? Style { get; set; }
    public ComponentPosition? Position { get; set; }
    public List<ExtractedComponent>? Children { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
}

/// <summary>
/// Style properties of a component
/// </summary>
public class ComponentStyle
{
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public string? BorderColor { get; set; }
    public string? BorderRadius { get; set; }
    public string? FontSize { get; set; }
    public string? FontWeight { get; set; }
    public string? Shadow { get; set; }
    public string? Padding { get; set; }
    public string? Margin { get; set; }
}

/// <summary>
/// Position information for a component
/// </summary>
public class ComponentPosition
{
    public string? Layout { get; set; }
    public string? Alignment { get; set; }
    public int? Row { get; set; }
    public int? Column { get; set; }
    public string? Width { get; set; }
    public string? Height { get; set; }
}

/// <summary>
/// Result containing extracted color palette
/// </summary>
public class ColorPaletteResult : VisionResult
{
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public string? TextSecondaryColor { get; set; }
    public string? BorderColor { get; set; }
    public string? SuccessColor { get; set; }
    public string? WarningColor { get; set; }
    public string? ErrorColor { get; set; }
    public List<string>? Gradients { get; set; }
    public List<string>? AllColors { get; set; }
    public string? ColorScheme { get; set; } // "light", "dark", "mixed"
}

/// <summary>
/// Result containing extracted typography information
/// </summary>
public class TypographyResult : VisionResult
{
    public string? PrimaryFont { get; set; }
    public string? SecondaryFont { get; set; }
    public string? HeadingFont { get; set; }
    public string? BodyFont { get; set; }
    public Dictionary<string, string>? FontSizes { get; set; }
    public List<string>? FontWeights { get; set; }
    public string? LineHeight { get; set; }
    public string? LetterSpacing { get; set; }
    public List<FontUsage>? FontUsages { get; set; }
}

/// <summary>
/// Describes how a font is used in the design
/// </summary>
public class FontUsage
{
    public string? Element { get; set; }
    public string? Font { get; set; }
    public string? Size { get; set; }
    public string? Weight { get; set; }
    public string? Color { get; set; }
}

/// <summary>
/// Result containing extracted layout information
/// </summary>
public class LayoutResult : VisionResult
{
    public string? LayoutType { get; set; } // "grid", "flexbox", "fixed", "responsive"
    public string? ContainerWidth { get; set; }
    public string? ContainerPadding { get; set; }
    public int? Columns { get; set; }
    public string? Gap { get; set; }
    public string? RowGap { get; set; }
    public string? ColumnGap { get; set; }
    public List<string>? Breakpoints { get; set; }
    public List<LayoutSection>? Sections { get; set; }
    public SpacingSystem? Spacing { get; set; }
}

/// <summary>
/// Describes a section in the layout
/// </summary>
public class LayoutSection
{
    public string? Name { get; set; }
    public string? Type { get; set; } // "header", "hero", "content", "sidebar", "footer"
    public string? Layout { get; set; }
    public string? Width { get; set; }
    public string? Height { get; set; }
    public string? Padding { get; set; }
    public string? Margin { get; set; }
}

/// <summary>
/// Spacing system used in the design
/// </summary>
public class SpacingSystem
{
    public string? BaseUnit { get; set; }
    public Dictionary<string, string>? Scale { get; set; } // xs, sm, md, lg, xl
    public string? PaddingPattern { get; set; }
    public string? MarginPattern { get; set; }
    public string? BorderRadius { get; set; }
}

/// <summary>
/// Comprehensive UI analysis result
/// </summary>
public class ComprehensiveUIAnalysis : VisionResult
{
    public string? Description { get; set; }
    public UIComponentsResult? Components { get; set; }
    public ColorPaletteResult? Colors { get; set; }
    public TypographyResult? Typography { get; set; }
    public LayoutResult? Layout { get; set; }
    public IconsResult? Icons { get; set; }
    public AccessibilityResult? Accessibility { get; set; }
    public List<string>? DesignPatterns { get; set; }
    public string? OverallStyle { get; set; } // "modern", "minimal", "corporate", "playful", etc.
    public List<string>? SuggestedFrameworks { get; set; }
}

/// <summary>
/// Result containing identified icons
/// </summary>
public class IconsResult : VisionResult
{
    public string? IconStyle { get; set; } // "outlined", "filled", "rounded", "sharp"
    public string? SuggestedLibrary { get; set; } // "Material Icons", "Font Awesome", "Heroicons", etc.
    public List<IdentifiedIcon>? Icons { get; set; }
}

/// <summary>
/// Represents an identified icon
/// </summary>
public class IdentifiedIcon
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? SuggestedName { get; set; } // e.g., "home", "search", "menu"
    public string? Location { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
}

/// <summary>
/// Result containing accessibility analysis
/// </summary>
public class AccessibilityResult : VisionResult
{
    public string? OverallScore { get; set; } // "Good", "Needs Improvement", "Poor"
    public List<AccessibilityIssue>? Issues { get; set; }
    public List<string>? Recommendations { get; set; }
    public ContrastAnalysis? ContrastAnalysis { get; set; }
    public bool? HasSufficientTextSize { get; set; }
    public bool? HasClearHierarchy { get; set; }
    public bool? HasTouchTargets { get; set; }
}

/// <summary>
/// Represents an accessibility issue
/// </summary>
public class AccessibilityIssue
{
    public string? Type { get; set; } // "contrast", "text-size", "touch-target", "color-only"
    public string? Severity { get; set; } // "critical", "major", "minor"
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Recommendation { get; set; }
    public string? WcagCriteria { get; set; }
}

/// <summary>
/// Color contrast analysis
/// </summary>
public class ContrastAnalysis
{
    public bool? PassesAANormal { get; set; }
    public bool? PassesAALarge { get; set; }
    public bool? PassesAAA { get; set; }
    public List<ContrastPair>? AnalyzedPairs { get; set; }
}

/// <summary>
/// A pair of colors analyzed for contrast
/// </summary>
public class ContrastPair
{
    public string? Foreground { get; set; }
    public string? Background { get; set; }
    public double? Ratio { get; set; }
    public bool? PassesAA { get; set; }
    public bool? PassesAAA { get; set; }
}

#endregion

#region New Result Classes for GPT-4o Vision

public class ImageComparisonResult : VisionResult
{
    public List<ImageDifference>? Differences { get; set; }
    public double SimilarityScore { get; set; }
}

public class ImageDifference
{
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Severity { get; set; } // minor, moderate, significant
}

public class DocumentParseResult : VisionResult
{
    public string? DocumentType { get; set; } // invoice, receipt, form, etc.
    public Dictionary<string, string>? Fields { get; set; }
    public List<DocumentLineItem>? LineItems { get; set; }
    public string? RawText { get; set; }
}

public class DocumentLineItem
{
    public string? Description { get; set; }
    public string? Quantity { get; set; }
    public string? UnitPrice { get; set; }
    public string? Total { get; set; }
}

public class ChartDataResult : VisionResult
{
    public string? ChartType { get; set; } // bar, line, pie, scatter, etc.
    public string? Title { get; set; }
    public List<string>? Labels { get; set; }
    public List<ChartSeries>? Series { get; set; }
    public string? XAxisLabel { get; set; }
    public string? YAxisLabel { get; set; }
}

public class ChartSeries
{
    public string? Name { get; set; }
    public List<double>? Values { get; set; }
    public string? Color { get; set; }
}

public class MathOcrResult : VisionResult
{
    public string? Latex { get; set; }
    public string? MathML { get; set; }
    public string? PlainText { get; set; }
    public List<MathExpression>? Expressions { get; set; }
}

public class MathExpression
{
    public string? Latex { get; set; }
    public string? Description { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class HandwritingResult : VisionResult
{
    public string? RecognizedText { get; set; }
    public double Confidence { get; set; }
    public List<HandwrittenLine>? Lines { get; set; }
}

public class HandwrittenLine
{
    public string? Text { get; set; }
    public double Confidence { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class TableExtractionResult : VisionResult
{
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<string>? Headers { get; set; }
    public List<List<string>>? Rows { get; set; }
    public string? CsvData { get; set; }
    public string? JsonData { get; set; }
}

public class ImageDiffResult : VisionResult
{
    public List<DiffRegion>? AddedRegions { get; set; }
    public List<DiffRegion>? RemovedRegions { get; set; }
    public List<DiffRegion>? ModifiedRegions { get; set; }
    public double OverallChangePercentage { get; set; }
    public string? DiffImageBase64 { get; set; }
}

public class DiffRegion
{
    public string? Description { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

#endregion

