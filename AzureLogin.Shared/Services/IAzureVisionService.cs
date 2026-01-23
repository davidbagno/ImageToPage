namespace AzureLogin.Shared.Services;

/// <summary>
/// Azure AI Vision 4.0 service for pixel-accurate image analysis
/// </summary>
public interface IAzureVisionService
{
    // ===== EXISTING METHODS =====
    
    /// <summary>Analyzes an image and returns detected objects with pixel-accurate bounding boxes</summary>
    Task<AzureVisionResult> AnalyzeImageAsync(byte[] imageBytes);
    
    /// <summary>Gets dense captions for regions in an image with bounding boxes</summary>
    Task<AzureVisionResult> GetDenseCaptionsAsync(byte[] imageBytes);
    
    /// <summary>Detects objects in an image with bounding boxes</summary>
    Task<AzureVisionResult> DetectObjectsAsync(byte[] imageBytes);
    
    /// <summary>Performs OCR to detect text regions</summary>
    Task<TextDetectionResult> DetectTextAsync(byte[] imageBytes);
    
    /// <summary>Comprehensive analysis using all Vision 4.0 features</summary>
    Task<AzureVisionResult> AnalyzeComprehensiveAsync(byte[] imageBytes);
    
    /// <summary>Gets smart crop suggestions at various aspect ratios</summary>
    Task<SmartCropResult> GetSmartCropsAsync(byte[] imageBytes, params double[] aspectRatios);
    
    /// <summary>Detects people with bounding boxes</summary>
    Task<AzureVisionResult> DetectPeopleAsync(byte[] imageBytes);
    
    // ===== NEW METHODS =====
    
    /// <summary>Gets a single caption for the entire image</summary>
    Task<ImageCaptionResult> GetImageCaptionAsync(byte[] imageBytes);
    
    /// <summary>Gets content tags for the image (nature, indoor, person, etc.)</summary>
    Task<ImageTagsResult> GetImageTagsAsync(byte[] imageBytes);
    
    /// <summary>Detects brands/logos in the image</summary>
    Task<BrandDetectionResult> DetectBrandsAsync(byte[] imageBytes);
    
    /// <summary>Detects image category (building, landscape, etc.)</summary>
    Task<CategoryResult> GetImageCategoryAsync(byte[] imageBytes);
    
    /// <summary>Detects adult/racy/gory content</summary>
    Task<AdultContentResult> DetectAdultContentAsync(byte[] imageBytes);
    
    /// <summary>Detects faces with landmarks, age, emotion</summary>
    Task<FaceDetectionResult> DetectFacesAsync(byte[] imageBytes);
    
    /// <summary>Determines image type (photo, clipart, line drawing)</summary>
    Task<ImageTypeResult> GetImageTypeAsync(byte[] imageBytes);
    
    /// <summary>Removes background from image</summary>
    Task<BackgroundRemovalResult> RemoveBackgroundAsync(byte[] imageBytes);
    
    /// <summary>Check if the service is configured and available</summary>
    bool IsConfigured { get; }
}

#region Existing Result Classes

public class AzureVisionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DetectedRegion>? Regions { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}

public class DetectedRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Caption { get; set; }
    public double Confidence { get; set; }
    public List<string>? Tags { get; set; }
    public string? RegionType { get; set; }
}

public class SmartCropResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SmartCropRegion>? Crops { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}

public class SmartCropRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double AspectRatio { get; set; }
}

public class TextDetectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<TextRegion>? TextRegions { get; set; }
    public string? FullText { get; set; }
}

public class TextRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Text { get; set; }
    public double Confidence { get; set; }
}

#endregion

#region New Result Classes

public class ImageCaptionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Caption { get; set; }
    public double Confidence { get; set; }
}

public class ImageTagsResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ImageTag>? Tags { get; set; }
}

public class ImageTag
{
    public string? Name { get; set; }
    public double Confidence { get; set; }
}

public class BrandDetectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DetectedBrand>? Brands { get; set; }
}

public class DetectedBrand
{
    public string? Name { get; set; }
    public double Confidence { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class CategoryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PrimaryCategory { get; set; }
    public List<ImageCategory>? Categories { get; set; }
}

public class ImageCategory
{
    public string? Name { get; set; }
    public double Confidence { get; set; }
}

public class AdultContentResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsAdultContent { get; set; }
    public bool IsRacyContent { get; set; }
    public bool IsGoryContent { get; set; }
    public double AdultScore { get; set; }
    public double RacyScore { get; set; }
    public double GoreScore { get; set; }
}

public class FaceDetectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DetectedFace>? Faces { get; set; }
}

public class DetectedFace
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? Emotion { get; set; }
    public double Confidence { get; set; }
}

public class ImageTypeResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ImageType { get; set; } // photo, clipart, linedrawing
    public bool IsClipArt { get; set; }
    public bool IsLineDrawing { get; set; }
    public double ClipArtConfidence { get; set; }
    public double LineDrawingConfidence { get; set; }
}

public class BackgroundRemovalResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? ImageWithoutBackground { get; set; }
    public string? Base64Image { get; set; }
}

#endregion

