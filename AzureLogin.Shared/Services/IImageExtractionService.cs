namespace AzureLogin.Shared.Services;

/// <summary>
/// Crop mode for image extraction
/// </summary>
public enum CropMode
{
    /// <summary>AI-detected regions with edge refinement for pixel-perfect bounds</summary>
    SmartDetect,
    /// <summary>Divide image into uniform grid cells</summary>
    Grid,
    /// <summary>Detect horizontal/vertical sections</summary>
    Sections,
    /// <summary>Detect individual UI components using edge detection</summary>
    Components,
    /// <summary>Manual exact pixel coordinates</summary>
    ExactCrop,
    /// <summary>AI-detected regions without refinement (original behavior)</summary>
    AIRegions,
    /// <summary>Pixel-perfect contour detection - finds actual image boundaries by color analysis</summary>
    ContourDetection,
    /// <summary>Flood-fill based detection - finds distinct colored regions</summary>
    FloodFillRegions,
    /// <summary>Smart detection of UI cards and widgets with rounded corners</summary>
    SmartUICards,
    /// <summary>Azure AI Vision 4.0 - pixel-accurate object detection and dense captioning</summary>
    AzureVision,
    /// <summary>Azure AI Vision 4.0 Comprehensive - combines Objects, People, Dense Captions, and Smart Crops for maximum detection</summary>
    AzureVisionComprehensive,
    /// <summary>Azure AI Vision 4.0 - detect and extract only people/avatars</summary>
    AzureVisionPeople,
    /// <summary>Combined AI + Edge refinement + Azure Vision for best results</summary>
    HybridPixelPerfect
}

/// <summary>
/// Options for image extraction
/// </summary>
public class ExtractionOptions
{
    public CropMode Mode { get; set; } = CropMode.SmartDetect;
    public int GridRows { get; set; } = 2;
    public int GridColumns { get; set; } = 2;
    public bool RefineWithEdgeDetection { get; set; } = true;
    public int EdgeDetectionThreshold { get; set; } = 30;
    public int MinComponentSize { get; set; } = 20;
    public int Padding { get; set; } = 2;
    public bool DetectOnly { get; set; } = false;
    public string ExtractionType { get; set; } = "all"; // all, icons, logos, etc.
}

/// <summary>
/// Interface for extracting and cropping images/regions from a composite image
/// Uses GPT-4 Vision to identify image regions, then image processing to crop them
/// </summary>
public interface IImageExtractionService
{
    /// <summary>
    /// Extracts all identifiable images/graphics from a screenshot or composite image
    /// </summary>
    /// <param name="imageBytes">The source image data as bytes</param>
    /// <param name="mimeType">The MIME type of the image</param>
    /// <returns>List of extracted images with metadata</returns>
    Task<ImageExtractionResult> ExtractImagesAsync(byte[] imageBytes, string mimeType);
    
    /// <summary>
    /// Extracts images from a base64 encoded image
    /// </summary>
    Task<ImageExtractionResult> ExtractImagesFromBase64Async(string base64Image, string mimeType);
    
    /// <summary>
    /// Extracts images using specified options
    /// </summary>
    Task<ImageExtractionResult> ExtractWithOptionsAsync(byte[] imageBytes, string mimeType, ExtractionOptions options);
    
    /// <summary>
    /// Identifies all image regions in a composite image without cropping
    /// Returns bounding box coordinates for each detected image
    /// </summary>
    Task<ImageRegionResult> IdentifyImageRegionsAsync(byte[] imageBytes, string mimeType);
    
    /// <summary>
    /// Crops a specific region from an image given bounding box coordinates
    /// </summary>
    Task<CroppedImageResult> CropRegionAsync(byte[] imageBytes, string mimeType, BoundingBox region);
    
    /// <summary>
    /// Crops using grid-based segmentation
    /// </summary>
    Task<ImageExtractionResult> ExtractGridAsync(byte[] imageBytes, string mimeType, int rows, int columns);
    
    /// <summary>
    /// Detects and crops horizontal/vertical sections
    /// </summary>
    Task<ImageExtractionResult> ExtractSectionsAsync(byte[] imageBytes, string mimeType);
    
    /// <summary>
    /// Detects UI components using edge detection and connected component analysis
    /// </summary>
    Task<ImageExtractionResult> ExtractComponentsAsync(byte[] imageBytes, string mimeType, int minSize = 20);
    
    /// <summary>
    /// Refines a bounding box to snap to actual pixel edges
    /// </summary>
    Task<BoundingBox> RefineRegionBoundsAsync(byte[] imageBytes, BoundingBox approximateBounds, int threshold = 30);
    
    /// <summary>
    /// Extracts images using pixel-perfect contour detection (no AI - pure image analysis)
    /// </summary>
    Task<ImageExtractionResult> ExtractByContourDetectionAsync(byte[] imageBytes, string mimeType, int minSize = 20, int colorThreshold = 25);
    
    /// <summary>
    /// Extracts distinct colored regions using flood-fill algorithm
    /// </summary>
    Task<ImageExtractionResult> ExtractByFloodFillAsync(byte[] imageBytes, string mimeType, int minSize = 30, int colorTolerance = 15);
    
    /// <summary>
    /// Extracts UI cards and widgets (rectangular regions with distinct backgrounds/borders)
    /// </summary>
    Task<ImageExtractionResult> ExtractUICardsAsync(byte[] imageBytes, string mimeType, int minSize = 40);
    
    /// <summary>
    /// Extracts images using Azure AI Vision 4.0 for pixel-accurate detection
    /// Uses Dense Captioning and Object Detection for precise bounding boxes
    /// </summary>
    Task<ImageExtractionResult> ExtractWithAzureVisionAsync(byte[] imageBytes, string mimeType);
    
    /// <summary>
    /// Extracts people/avatars using Azure AI Vision 4.0 People Detection
    /// </summary>
    Task<ImageExtractionResult> ExtractPeopleWithAzureVisionAsync(byte[] imageBytes, string mimeType);
    
    /// <summary>
    /// Hybrid pixel-perfect extraction - combines AI detection, Azure Vision, and edge refinement
    /// for the most accurate possible extraction
    /// </summary>
    Task<ImageExtractionResult> ExtractHybridPixelPerfectAsync(byte[] imageBytes, string mimeType);
    
    /// <summary>
    /// Extracts icons and small graphics from an image
    /// </summary>
    Task<ImageExtractionResult> ExtractIconsAsync(byte[] imageBytes, string mimeType);
    
    /// <summary>
    /// Extracts logos and branding elements from an image
    /// </summary>
    Task<ImageExtractionResult> ExtractLogosAsync(byte[] imageBytes, string mimeType);
}

#region Result Classes

/// <summary>
/// Result of image extraction operation
/// </summary>
public class ImageExtractionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ExtractedImage>? Images { get; set; }
    public int TotalImagesFound { get; set; }
    public string? AnalysisSummary { get; set; }
}

/// <summary>
/// Represents a single extracted image
/// </summary>
public class ExtractedImage
{
    /// <summary>
    /// The cropped image data as PNG bytes
    /// </summary>
    public byte[]? ImageData { get; set; }
    
    /// <summary>
    /// Base64 encoded image data for display
    /// </summary>
    public string? Base64Data { get; set; }
    
    /// <summary>
    /// AI-generated description of the image
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Type of image (photo, icon, logo, illustration, chart, etc.)
    /// </summary>
    public string? ImageType { get; set; }
    
    /// <summary>
    /// Bounding box coordinates in the original image
    /// </summary>
    public BoundingBox? BoundingBox { get; set; }
    
    /// <summary>
    /// Suggested filename for the image
    /// </summary>
    public string? SuggestedFilename { get; set; }
    
    /// <summary>
    /// Confidence score (0-100) of the detection
    /// </summary>
    public int Confidence { get; set; }
    
    /// <summary>
    /// Width of the extracted image in pixels
    /// </summary>
    public int Width { get; set; }
    
    /// <summary>
    /// Height of the extracted image in pixels
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
/// Bounding box coordinates for an image region
/// </summary>
public class BoundingBox
{
    /// <summary>
    /// X coordinate of the top-left corner (pixels from left)
    /// </summary>
    public int X { get; set; }
    
    /// <summary>
    /// Y coordinate of the top-left corner (pixels from top)
    /// </summary>
    public int Y { get; set; }
    
    /// <summary>
    /// Width of the bounding box in pixels
    /// </summary>
    public int Width { get; set; }
    
    /// <summary>
    /// Height of the bounding box in pixels
    /// </summary>
    public int Height { get; set; }
    
    /// <summary>
    /// Normalized X (0-1 relative to image width)
    /// </summary>
    public double NormalizedX { get; set; }
    
    /// <summary>
    /// Normalized Y (0-1 relative to image height)
    /// </summary>
    public double NormalizedY { get; set; }
    
    /// <summary>
    /// Normalized width (0-1 relative to image width)
    /// </summary>
    public double NormalizedWidth { get; set; }
    
    /// <summary>
    /// Normalized height (0-1 relative to image height)
    /// </summary>
    public double NormalizedHeight { get; set; }
}

/// <summary>
/// Result containing identified image regions without cropping
/// </summary>
public class ImageRegionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ImageRegion>? Regions { get; set; }
    public int SourceImageWidth { get; set; }
    public int SourceImageHeight { get; set; }
}

/// <summary>
/// Represents an identified image region
/// </summary>
public class ImageRegion
{
    public string? Description { get; set; }
    public string? ImageType { get; set; }
    public BoundingBox? BoundingBox { get; set; }
    public int Confidence { get; set; }
    public string? SuggestedFilename { get; set; }
}

/// <summary>
/// Result of a crop operation
/// </summary>
public class CroppedImageResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? ImageData { get; set; }
    public string? Base64Data { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

#endregion
