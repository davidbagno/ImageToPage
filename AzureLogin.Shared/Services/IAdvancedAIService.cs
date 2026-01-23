namespace AzureLogin.Shared.Services;

/// <summary>
/// Interface for advanced AI services including Document Intelligence, Custom Vision, Video Analysis, etc.
/// </summary>
public interface IAdvancedAIService
{
    // Service availability checks
    bool IsDocumentIntelligenceConfigured { get; }
    bool IsCustomVisionConfigured { get; }
    bool IsVideoIndexerConfigured { get; }
    bool IsDalleConfigured { get; }
    
    // ===== Azure Document Intelligence =====
    
    /// <summary>Analyzes documents like invoices, receipts, IDs</summary>
    Task<DocumentAnalysisResult> AnalyzeDocumentAsync(byte[] documentBytes, string modelId = "prebuilt-document");
    
    /// <summary>Extracts data from invoices</summary>
    Task<InvoiceResult> AnalyzeInvoiceAsync(byte[] documentBytes);
    
    /// <summary>Extracts data from receipts</summary>
    Task<ReceiptResult> AnalyzeReceiptAsync(byte[] documentBytes);
    
    /// <summary>Extracts data from ID documents</summary>
    Task<IdDocumentResult> AnalyzeIdDocumentAsync(byte[] documentBytes);
    
    /// <summary>Extracts data from business cards</summary>
    Task<BusinessCardResult> AnalyzeBusinessCardAsync(byte[] documentBytes);
    
    // ===== Azure Custom Vision =====
    
    /// <summary>Classifies image using custom trained model</summary>
    Task<CustomVisionResult> ClassifyImageAsync(byte[] imageBytes, string projectId = null);
    
    /// <summary>Detects objects using custom trained model</summary>
    Task<CustomVisionDetectionResult> DetectObjectsCustomAsync(byte[] imageBytes, string projectId = null);
    
    // ===== Video Analysis =====
    
    /// <summary>Extracts frames from video at specified intervals</summary>
    Task<VideoFramesResult> ExtractVideoFramesAsync(byte[] videoBytes, int frameIntervalSeconds = 1);
    
    /// <summary>Analyzes video content using Video Indexer</summary>
    Task<VideoAnalysisResult> AnalyzeVideoAsync(byte[] videoBytes);
    
    // ===== DALL-E Image Operations =====
    
    /// <summary>Edits specific regions of an image</summary>
    Task<ImageEditResult> EditImageAsync(byte[] imageBytes, byte[] maskBytes, string prompt);
    
    /// <summary>Creates variations of an existing image</summary>
    Task<ImageVariationResult> CreateImageVariationAsync(byte[] imageBytes, int count = 1);
    
    // ===== Image Enhancement (Local + AI) =====
    
    /// <summary>Upscales image using AI (local implementation)</summary>
    Task<ImageEnhanceResult> UpscaleImageAsync(byte[] imageBytes, int scaleFactor = 2);
    
    /// <summary>Applies style transfer to image</summary>
    Task<StyleTransferResult> ApplyStyleTransferAsync(byte[] contentImage, string style);
    
    /// <summary>Estimates depth map from image</summary>
    Task<DepthEstimationResult> EstimateDepthAsync(byte[] imageBytes);
    
    /// <summary>Segments image into regions (simplified version)</summary>
    Task<SegmentationResult> SegmentImageAsync(byte[] imageBytes);
}

#region Document Intelligence Results

public class DocumentAnalysisResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DocumentType { get; set; }
    public Dictionary<string, DocumentField>? Fields { get; set; }
    public List<DocumentTable>? Tables { get; set; }
    public string? RawText { get; set; }
}

public class DocumentField
{
    public string? Value { get; set; }
    public string? ValueType { get; set; }
    public double Confidence { get; set; }
    public BoundingRegion? BoundingRegion { get; set; }
}

public class BoundingRegion
{
    public int PageNumber { get; set; }
    public List<double>? Polygon { get; set; }
}

public class DocumentTable
{
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<DocumentTableCell>? Cells { get; set; }
}

public class DocumentTableCell
{
    public int RowIndex { get; set; }
    public int ColumnIndex { get; set; }
    public string? Content { get; set; }
}

public class InvoiceResult : DocumentAnalysisResult
{
    public string? VendorName { get; set; }
    public string? CustomerName { get; set; }
    public string? InvoiceId { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? TotalTax { get; set; }
    public decimal? Total { get; set; }
    public List<InvoiceLineItem>? LineItems { get; set; }
}

public class InvoiceLineItem
{
    public string? Description { get; set; }
    public double? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Amount { get; set; }
}

public class ReceiptResult : DocumentAnalysisResult
{
    public string? MerchantName { get; set; }
    public string? MerchantAddress { get; set; }
    public string? MerchantPhone { get; set; }
    public DateTime? TransactionDate { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Tip { get; set; }
    public decimal? Total { get; set; }
    public List<ReceiptLineItem>? Items { get; set; }
}

public class ReceiptLineItem
{
    public string? Description { get; set; }
    public double? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? TotalPrice { get; set; }
}

public class IdDocumentResult : DocumentAnalysisResult
{
    public string? DocumentNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }
}

public class BusinessCardResult : DocumentAnalysisResult
{
    public List<string>? ContactNames { get; set; }
    public List<string>? CompanyNames { get; set; }
    public List<string>? JobTitles { get; set; }
    public List<string>? Emails { get; set; }
    public List<string>? PhoneNumbers { get; set; }
    public List<string>? Addresses { get; set; }
    public List<string>? Websites { get; set; }
}

#endregion

#region Custom Vision Results

public class CustomVisionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CustomVisionPrediction>? Predictions { get; set; }
}

public class CustomVisionPrediction
{
    public string? TagName { get; set; }
    public double Probability { get; set; }
}

public class CustomVisionDetectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CustomVisionDetection>? Detections { get; set; }
}

public class CustomVisionDetection
{
    public string? TagName { get; set; }
    public double Probability { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

#endregion

#region Video Results

public class VideoFramesResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<VideoFrame>? Frames { get; set; }
    public double DurationSeconds { get; set; }
    public int TotalFrames { get; set; }
}

public class VideoFrame
{
    public int FrameNumber { get; set; }
    public double TimestampSeconds { get; set; }
    public byte[]? ImageData { get; set; }
    public string? Base64Image { get; set; }
}

public class VideoAnalysisResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double DurationSeconds { get; set; }
    public List<VideoInsight>? Insights { get; set; }
    public List<VideoFace>? Faces { get; set; }
    public List<VideoLabel>? Labels { get; set; }
    public string? Transcript { get; set; }
}

public class VideoInsight
{
    public string? Type { get; set; }
    public string? Description { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}

public class VideoFace
{
    public string? Id { get; set; }
    public double Confidence { get; set; }
    public List<VideoAppearance>? Appearances { get; set; }
}

public class VideoAppearance
{
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}

public class VideoLabel
{
    public string? Name { get; set; }
    public double Confidence { get; set; }
    public List<VideoAppearance>? Appearances { get; set; }
}

#endregion

#region DALL-E / Image Edit Results

public class ImageEditResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? EditedImage { get; set; }
    public string? Base64Image { get; set; }
    public string? RevisedPrompt { get; set; }
}

public class ImageVariationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<GeneratedImage>? Variations { get; set; }
}

public class GeneratedImage
{
    public byte[]? ImageData { get; set; }
    public string? Base64Image { get; set; }
    public string? Url { get; set; }
}

#endregion

#region Image Enhancement Results

public class ImageEnhanceResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? EnhancedImage { get; set; }
    public string? Base64Image { get; set; }
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int NewWidth { get; set; }
    public int NewHeight { get; set; }
}

public class StyleTransferResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? StyledImage { get; set; }
    public string? Base64Image { get; set; }
    public string? AppliedStyle { get; set; }
}

public class DepthEstimationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? DepthMap { get; set; }
    public string? Base64DepthMap { get; set; }
    public string? Description { get; set; }
}

public class SegmentationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ImageSegment>? Segments { get; set; }
    public byte[]? SegmentedImage { get; set; }
    public string? Base64Image { get; set; }
}

public class ImageSegment
{
    public string? Label { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Confidence { get; set; }
    public string? Color { get; set; }
}

#endregion
