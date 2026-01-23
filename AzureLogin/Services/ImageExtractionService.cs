using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace AzureLogin.Services;

/// <summary>
/// Robust image extraction service using GPT-4 Vision for detection and SkiaSharp for cropping
/// Now with Azure AI Vision 4.0 support for pixel-accurate detection
/// </summary>
public class ImageExtractionService : IImageExtractionService
{
    private readonly IAzureOpenAIService _azureOpenAIService;
    private readonly IAzureVisionService? _azureVisionService;
    private readonly ILogger<ImageExtractionService>? _logger;

    public ImageExtractionService(
        IAzureOpenAIService azureOpenAIService,
        IAzureVisionService? azureVisionService = null,
        ILogger<ImageExtractionService>? logger = null)
    {
        _azureOpenAIService = azureOpenAIService;
        _azureVisionService = azureVisionService;
        _logger = logger;
    }

    public async Task<ImageExtractionResult> ExtractImagesAsync(byte[] imageBytes, string mimeType)
    {
        var base64Image = Convert.ToBase64String(imageBytes);
        return await ExtractImagesFromBase64Async(base64Image, mimeType);
    }

    public async Task<ImageExtractionResult> ExtractImagesFromBase64Async(string base64Image, string mimeType)
    {
        try
        {
            _logger?.LogInformation("Starting image extraction...");
            
            var imageBytes = Convert.FromBase64String(base64Image);
            var regionsResult = await IdentifyImageRegionsAsync(imageBytes, mimeType);
            
            if (!regionsResult.Success)
            {
                _logger?.LogWarning("Region identification failed: {Error}", regionsResult.ErrorMessage);
                return new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = regionsResult.ErrorMessage ?? "Failed to identify image regions",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0
                };
            }

            if (regionsResult.Regions == null || regionsResult.Regions.Count == 0)
            {
                _logger?.LogInformation("No regions found in image");
                return new ImageExtractionResult
                {
                    Success = true,
                    ErrorMessage = null,
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0,
                    AnalysisSummary = "No distinct images or graphics were detected in the source image"
                };
            }

            _logger?.LogInformation("Found {Count} regions, starting extraction...", regionsResult.Regions.Count);

            var extractedImages = new List<ExtractedImage>();
            
            foreach (var region in regionsResult.Regions)
            {
                if (region.BoundingBox == null) 
                {
                    _logger?.LogWarning("Region has no bounding box, skipping");
                    continue;
                }
                
                try
                {
                    var cropResult = await CropRegionAsync(imageBytes, mimeType, region.BoundingBox);
                    
                    if (cropResult.Success && cropResult.ImageData != null)
                    {
                        extractedImages.Add(new ExtractedImage
                        {
                            ImageData = cropResult.ImageData,
                            Base64Data = cropResult.Base64Data,
                            Description = region.Description ?? "Extracted image",
                            ImageType = region.ImageType ?? "image",
                            BoundingBox = region.BoundingBox,
                            SuggestedFilename = region.SuggestedFilename ?? $"image-{extractedImages.Count + 1}.png",
                            Confidence = region.Confidence,
                            Width = cropResult.Width,
                            Height = cropResult.Height
                        });
                        _logger?.LogInformation("Successfully extracted: {Desc} ({W}x{H})", 
                            region.Description, cropResult.Width, cropResult.Height);
                    }
                    else
                    {
                        _logger?.LogWarning("Failed to crop region: {Error}", cropResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error cropping region");
                }
            }

            return new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = extractedImages.Count > 0 
                    ? $"Successfully extracted {extractedImages.Count} images/graphics" 
                    : "No images could be extracted from the identified regions"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image extraction failed");
            return new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Extraction failed: {ex.Message}",
                Images = new List<ExtractedImage>(),
                TotalImagesFound = 0
            };
        }
    }

    public async Task<ImageRegionResult> IdentifyImageRegionsAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return new ImageRegionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                };
            }

            var imageWidth = bitmap.Width;
            var imageHeight = bitmap.Height;
            var base64Image = Convert.ToBase64String(imageBytes);

            _logger?.LogInformation("Analyzing image: {W}x{H} pixels", imageWidth, imageHeight);

            var prompt = BuildExtractionPrompt(imageWidth, imageHeight, "all");
            var systemPrompt = GetSystemPrompt();

            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt, systemPrompt);

            _logger?.LogInformation("Got response from vision API, parsing...");
            _logger?.LogDebug("Raw response: {Response}", response);

            var regions = ParseRegionsFromResponse(response, imageWidth, imageHeight);

            _logger?.LogInformation("Parsed {Count} regions from response", regions.Count);

            return new ImageRegionResult
            {
                Success = true,
                Regions = regions,
                SourceImageWidth = imageWidth,
                SourceImageHeight = imageHeight
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to identify image regions");
            return new ImageRegionResult
            {
                Success = false,
                ErrorMessage = $"Region identification failed: {ex.Message}"
            };
        }
    }

    public Task<CroppedImageResult> CropRegionAsync(byte[] imageBytes, string mimeType, BoundingBox region)
    {
        try
        {
            using var originalBitmap = SKBitmap.Decode(imageBytes);
            if (originalBitmap == null)
            {
                return Task.FromResult(new CroppedImageResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                });
            }

            int x, y, width, height;
            
            // Prefer normalized coordinates if available
            if (region.NormalizedWidth > 0.001 && region.NormalizedHeight > 0.001)
            {
                x = (int)Math.Round(region.NormalizedX * originalBitmap.Width);
                y = (int)Math.Round(region.NormalizedY * originalBitmap.Height);
                width = (int)Math.Round(region.NormalizedWidth * originalBitmap.Width);
                height = (int)Math.Round(region.NormalizedHeight * originalBitmap.Height);
            }
            else if (region.Width > 0 && region.Height > 0)
            {
                x = region.X;
                y = region.Y;
                width = region.Width;
                height = region.Height;
            }
            else
            {
                return Task.FromResult(new CroppedImageResult
                {
                    Success = false,
                    ErrorMessage = "Invalid bounding box coordinates"
                });
            }

            // Add small padding (2px) to avoid cutting edges
            var padding = 2;
            x = Math.Max(0, x - padding);
            y = Math.Max(0, y - padding);
            width = Math.Min(width + padding * 2, originalBitmap.Width - x);
            height = Math.Min(height + padding * 2, originalBitmap.Height - y);

            // Ensure minimum size
            if (width < 4 || height < 4)
            {
                return Task.FromResult(new CroppedImageResult
                {
                    Success = false,
                    ErrorMessage = "Region too small to extract"
                });
            }

            // Clamp to image bounds
            x = Math.Max(0, Math.Min(x, originalBitmap.Width - 1));
            y = Math.Max(0, Math.Min(y, originalBitmap.Height - 1));
            width = Math.Min(width, originalBitmap.Width - x);
            height = Math.Min(height, originalBitmap.Height - y);

            // Create cropped bitmap
            var cropRect = new SKRectI(x, y, x + width, y + height);
            using var croppedBitmap = new SKBitmap(width, height);
            
            using (var canvas = new SKCanvas(croppedBitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(originalBitmap, cropRect, new SKRect(0, 0, width, height));
            }

            // Encode to PNG with transparency
            using var image = SKImage.FromBitmap(croppedBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var croppedBytes = data.ToArray();

            return Task.FromResult(new CroppedImageResult
            {
                Success = true,
                ImageData = croppedBytes,
                Base64Data = Convert.ToBase64String(croppedBytes),
                Width = width,
                Height = height
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to crop image region at ({X},{Y}) size ({W}x{H})", 
                region.X, region.Y, region.Width, region.Height);
            return Task.FromResult(new CroppedImageResult
            {
                Success = false,
                ErrorMessage = $"Crop failed: {ex.Message}"
            });
        }
    }

    public async Task<ImageExtractionResult> ExtractIconsAsync(byte[] imageBytes, string mimeType)
    {
        return await ExtractByTypeAsync(imageBytes, mimeType, "icons");
    }

    public async Task<ImageExtractionResult> ExtractLogosAsync(byte[] imageBytes, string mimeType)
    {
        return await ExtractByTypeAsync(imageBytes, mimeType, "logos");
    }

    /// <summary>
    /// Extract images using Azure AI Vision 4.0 for pixel-accurate detection
    /// Uses Dense Captioning, Object Detection, People Detection, and Smart Crops
    /// </summary>
    public async Task<ImageExtractionResult> ExtractWithAzureVisionAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            if (_azureVisionService == null)
            {
                _logger?.LogWarning("Azure Vision service not injected, falling back to AI detection");
                return await ExtractImagesAsync(imageBytes, mimeType);
            }

            // Check if service is configured
            if (!_azureVisionService.IsConfigured)
            {
                _logger?.LogWarning("Azure Vision service not configured (missing VisionEndpoint/VisionKey), falling back to AI detection");
                return new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Azure Vision not configured. Set VisionEndpoint and VisionKey in appsettings.json to enable pixel-accurate extraction.",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0,
                    AnalysisSummary = "Azure Vision requires configuration"
                };
            }

            _logger?.LogInformation("Starting Azure Vision comprehensive extraction...");

            // Use comprehensive analysis for maximum detection
            var visionResult = await _azureVisionService.AnalyzeComprehensiveAsync(imageBytes);

            if (!visionResult.Success || visionResult.Regions == null || visionResult.Regions.Count == 0)
            {
                _logger?.LogWarning("Azure Vision returned no regions: {Error}", visionResult.ErrorMessage);
                return new ImageExtractionResult
                {
                    Success = visionResult.Success,
                    ErrorMessage = visionResult.ErrorMessage ?? "No regions detected by Azure Vision",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0,
                    AnalysisSummary = "Azure Vision found no distinct regions"
                };
            }

            _logger?.LogInformation("Azure Vision found {Count} regions", visionResult.Regions.Count);

            // Also get text regions to potentially filter them out
            var textResult = await _azureVisionService.DetectTextAsync(imageBytes);
            var textRegions = textResult.Success ? textResult.TextRegions : new List<TextRegion>();

            var extractedImages = new List<ExtractedImage>();
            int regionIndex = 0;

            foreach (var region in visionResult.Regions)
            {
                // Skip if this is predominantly a text region
                if (IsTextOnlyRegion(region, textRegions))
                {
                    _logger?.LogDebug("Skipping text-only region: {Caption}", region.Caption);
                    continue;
                }

                // Create bounding box with pixel-accurate coordinates
                var boundingBox = new BoundingBox
                {
                    X = region.X,
                    Y = region.Y,
                    Width = region.Width,
                    Height = region.Height,
                    NormalizedX = visionResult.ImageWidth > 0 ? (double)region.X / visionResult.ImageWidth : 0,
                    NormalizedY = visionResult.ImageHeight > 0 ? (double)region.Y / visionResult.ImageHeight : 0,
                    NormalizedWidth = visionResult.ImageWidth > 0 ? (double)region.Width / visionResult.ImageWidth : 0,
                    NormalizedHeight = visionResult.ImageHeight > 0 ? (double)region.Height / visionResult.ImageHeight : 0
                };

                // Apply edge refinement for even more precise bounds
                var refinedBox = await RefineRegionBoundsAsync(imageBytes, boundingBox, 30);

                // Crop the region
                var cropResult = await CropRegionAsync(imageBytes, mimeType, refinedBox);

                if (cropResult.Success && cropResult.ImageData != null)
                {
                    var aspectRatio = (double)region.Width / region.Height;
                    var imageType = ClassifyAzureVisionRegion(region, aspectRatio);

                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = region.Caption ?? $"Region {regionIndex + 1}",
                        ImageType = $"{imageType} ({region.RegionType})",
                        BoundingBox = refinedBox,
                        SuggestedFilename = $"azure-vision-{regionIndex + 1}-{imageType}.png",
                        Confidence = (int)(region.Confidence * 100),
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                    regionIndex++;

                    _logger?.LogDebug("Extracted region: {Caption} at ({X},{Y}) size {W}x{H} type={Type}",
                        region.Caption, region.X, region.Y, region.Width, region.Height, region.RegionType);
                }
            }

            return new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Azure Vision detected {extractedImages.Count} regions with pixel-accurate bounds (using Objects, People, Dense Captions, Smart Crops)"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Azure Vision extraction failed");
            return new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Azure Vision extraction failed: {ex.Message}",
                Images = new List<ExtractedImage>(),
                TotalImagesFound = 0
            };
        }
    }

    /// <summary>
    /// Extract only people/avatars using Azure AI Vision 4.0 People Detection
    /// </summary>
    public async Task<ImageExtractionResult> ExtractPeopleWithAzureVisionAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            if (_azureVisionService == null || !_azureVisionService.IsConfigured)
            {
                return new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Azure Vision service not configured for people detection",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0
                };
            }

            _logger?.LogInformation("Starting Azure Vision people extraction...");

            var peopleResult = await _azureVisionService.DetectPeopleAsync(imageBytes);

            if (!peopleResult.Success || peopleResult.Regions == null || peopleResult.Regions.Count == 0)
            {
                return new ImageExtractionResult
                {
                    Success = peopleResult.Success,
                    ErrorMessage = peopleResult.ErrorMessage ?? "No people detected",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0,
                    AnalysisSummary = "No people found in image"
                };
            }

            var extractedImages = new List<ExtractedImage>();
            int personIndex = 0;

            foreach (var region in peopleResult.Regions)
            {
                var boundingBox = new BoundingBox
                {
                    X = region.X,
                    Y = region.Y,
                    Width = region.Width,
                    Height = region.Height,
                    NormalizedX = peopleResult.ImageWidth > 0 ? (double)region.X / peopleResult.ImageWidth : 0,
                    NormalizedY = peopleResult.ImageHeight > 0 ? (double)region.Y / peopleResult.ImageHeight : 0,
                    NormalizedWidth = peopleResult.ImageWidth > 0 ? (double)region.Width / peopleResult.ImageWidth : 0,
                    NormalizedHeight = peopleResult.ImageHeight > 0 ? (double)region.Height / peopleResult.ImageHeight : 0
                };

                // Refine bounds for precise extraction
                var refinedBox = await RefineRegionBoundsAsync(imageBytes, boundingBox, 20);

                var cropResult = await CropRegionAsync(imageBytes, mimeType, refinedBox);

                if (cropResult.Success && cropResult.ImageData != null)
                {
                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = region.Caption ?? $"Person {personIndex + 1}",
                        ImageType = "person",
                        BoundingBox = refinedBox,
                        SuggestedFilename = $"person-{personIndex + 1}.png",
                        Confidence = (int)(region.Confidence * 100),
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                    personIndex++;
                }
            }

            return new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Extracted {extractedImages.Count} people with pixel-accurate bounds"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "People extraction failed");
            return new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"People extraction failed: {ex.Message}",
                Images = new List<ExtractedImage>(),
                TotalImagesFound = 0
            };
        }
    }

    /// <summary>
    /// Hybrid pixel-perfect extraction - combines multiple detection methods for best results
    /// 1. Azure Vision for initial detection (if available)
    /// 2. GPT-4o for semantic understanding
    /// 3. Edge detection for pixel-perfect refinement
    /// 4. Contour detection for validation
    /// </summary>
    public async Task<ImageExtractionResult> ExtractHybridPixelPerfectAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            _logger?.LogInformation("Starting hybrid pixel-perfect extraction...");

            var allRegions = new List<(BoundingBox Box, string Description, string Type, double Confidence, string Source)>();

            // 1. Try Azure Vision first (most accurate for bounding boxes)
            if (_azureVisionService != null && _azureVisionService.IsConfigured)
            {
                var azureResult = await _azureVisionService.AnalyzeComprehensiveAsync(imageBytes);
                if (azureResult.Success && azureResult.Regions != null)
                {
                    foreach (var region in azureResult.Regions)
                    {
                        allRegions.Add((
                            new BoundingBox
                            {
                                X = region.X,
                                Y = region.Y,
                                Width = region.Width,
                                Height = region.Height,
                                NormalizedX = azureResult.ImageWidth > 0 ? (double)region.X / azureResult.ImageWidth : 0,
                                NormalizedY = azureResult.ImageHeight > 0 ? (double)region.Y / azureResult.ImageHeight : 0,
                                NormalizedWidth = azureResult.ImageWidth > 0 ? (double)region.Width / azureResult.ImageWidth : 0,
                                NormalizedHeight = azureResult.ImageHeight > 0 ? (double)region.Height / azureResult.ImageHeight : 0
                            },
                            region.Caption ?? "Azure Vision region",
                            region.RegionType ?? "image",
                            region.Confidence,
                            "AzureVision"
                        ));
                    }
                    _logger?.LogInformation("Azure Vision found {Count} regions", azureResult.Regions.Count);
                }
            }

            // 2. Add GPT-4o detected regions (good for semantic understanding)
            var gptResult = await IdentifyImageRegionsAsync(imageBytes, mimeType);
            if (gptResult.Success && gptResult.Regions != null)
            {
                foreach (var region in gptResult.Regions)
                {
                    if (region.BoundingBox != null)
                    {
                        // Only add if not significantly overlapping with Azure Vision results
                        if (!IsBoxOverlapping(region.BoundingBox, allRegions.Select(r => r.Box).ToList()))
                        {
                            allRegions.Add((
                                region.BoundingBox,
                                region.Description ?? "GPT-4o region",
                                region.ImageType ?? "image",
                                region.Confidence / 100.0,
                                "GPT4o"
                            ));
                        }
                    }
                }
                _logger?.LogInformation("GPT-4o added {Count} additional regions", 
                    gptResult.Regions.Count(r => r.BoundingBox != null && !IsBoxOverlapping(r.BoundingBox, allRegions.Take(allRegions.Count - gptResult.Regions.Count).Select(x => x.Box).ToList())));
            }

            // 3. Refine all bounding boxes with edge detection
            var refinedRegions = new List<(BoundingBox Box, string Description, string Type, double Confidence, string Source)>();
            foreach (var (box, desc, type, conf, source) in allRegions)
            {
                var refinedBox = await RefineRegionBoundsAsync(imageBytes, box, 25);
                refinedRegions.Add((refinedBox, desc, type, conf, source));
            }

            // 4. Extract images with refined bounds
            var extractedImages = new List<ExtractedImage>();
            int regionIndex = 0;

            foreach (var (box, desc, type, conf, source) in refinedRegions)
            {
                var cropResult = await CropRegionAsync(imageBytes, mimeType, box);

                if (cropResult.Success && cropResult.ImageData != null)
                {
                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = $"{desc} [{source}]",
                        ImageType = type,
                        BoundingBox = box,
                        SuggestedFilename = $"hybrid-{regionIndex + 1}-{type}.png",
                        Confidence = (int)(conf * 100),
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                    regionIndex++;
                }
            }

            return new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Hybrid extraction found {extractedImages.Count} regions using Azure Vision + GPT-4o + Edge Refinement"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Hybrid extraction failed");
            return new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Hybrid extraction failed: {ex.Message}",
                Images = new List<ExtractedImage>(),
                TotalImagesFound = 0
            };
        }
    }

    private bool IsBoxOverlapping(BoundingBox newBox, List<BoundingBox> existing, double threshold = 0.5)
    {
        foreach (var box in existing)
        {
            var overlapX = Math.Max(0, Math.Min(newBox.X + newBox.Width, box.X + box.Width) - Math.Max(newBox.X, box.X));
            var overlapY = Math.Max(0, Math.Min(newBox.Y + newBox.Height, box.Y + box.Height) - Math.Max(newBox.Y, box.Y));
            var overlapArea = overlapX * overlapY;
            
            var newArea = newBox.Width * newBox.Height;
            var existingArea = box.Width * box.Height;
            var smallerArea = Math.Min(newArea, existingArea);

            if (smallerArea > 0 && (double)overlapArea / smallerArea > threshold)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Check if a region is predominantly text (should be filtered out for image extraction)
    /// </summary>
    private bool IsTextOnlyRegion(DetectedRegion region, List<TextRegion>? textRegions)
    {
        if (textRegions == null || textRegions.Count == 0) return false;

        int textOverlapArea = 0;
        int regionArea = region.Width * region.Height;

        foreach (var textRegion in textRegions)
        {
            // Calculate overlap
            var overlapX = Math.Max(0, Math.Min(region.X + region.Width, textRegion.X + textRegion.Width) - Math.Max(region.X, textRegion.X));
            var overlapY = Math.Max(0, Math.Min(region.Y + region.Height, textRegion.Y + textRegion.Height) - Math.Max(region.Y, textRegion.Y));
            textOverlapArea += overlapX * overlapY;
        }

        // If more than 80% of the region is text, consider it text-only
        return regionArea > 0 && (double)textOverlapArea / regionArea > 0.8;
    }

    /// <summary>
    /// Classify an Azure Vision region by its characteristics
    /// </summary>
    private string ClassifyAzureVisionRegion(DetectedRegion region, double aspectRatio)
    {
        var caption = region.Caption?.ToLowerInvariant() ?? "";
        var tags = region.Tags?.Select(t => t.ToLowerInvariant()).ToList() ?? new List<string>();

        // Check for specific object types
        if (tags.Any(t => t.Contains("icon") || t.Contains("symbol"))) return "icon";
        if (tags.Any(t => t.Contains("logo") || t.Contains("brand"))) return "logo";
        if (tags.Any(t => t.Contains("chart") || t.Contains("graph"))) return "chart";
        if (tags.Any(t => t.Contains("photo") || t.Contains("photograph"))) return "photo";
        if (tags.Any(t => t.Contains("button"))) return "button";

        // Check caption
        if (caption.Contains("icon")) return "icon";
        if (caption.Contains("logo")) return "logo";
        if (caption.Contains("chart") || caption.Contains("graph")) return "chart";
        if (caption.Contains("button")) return "button";
        if (caption.Contains("card")) return "card";

        // Classify by size and aspect ratio
        if (region.Width <= 64 && region.Height <= 64) return "icon";
        if (region.Width <= 120 && region.Height <= 120 && Math.Abs(aspectRatio - 1) < 0.3) return "avatar";
        if (aspectRatio > 3) return "banner";
        if (aspectRatio < 0.33) return "vertical";
        if (region.Width > 150 && region.Height > 100) return "card";

        return "image";
    }

    /// <summary>
    /// Extract images using specified options and mode
    /// </summary>
    public async Task<ImageExtractionResult> ExtractWithOptionsAsync(byte[] imageBytes, string mimeType, ExtractionOptions options)
    {
        try
        {
            _logger?.LogInformation("Starting extraction with mode: {Mode}", options.Mode);

            return options.Mode switch
            {
                CropMode.Grid => await ExtractGridAsync(imageBytes, mimeType, options.GridRows, options.GridColumns),
                CropMode.Sections => await ExtractSectionsAsync(imageBytes, mimeType),
                CropMode.Components => await ExtractComponentsAsync(imageBytes, mimeType, options.MinComponentSize),
                CropMode.SmartDetect => await ExtractWithEdgeRefinementAsync(imageBytes, mimeType, options),
                CropMode.AIRegions => await ExtractImagesAsync(imageBytes, mimeType),
                CropMode.ExactCrop => await ExtractImagesAsync(imageBytes, mimeType),
                CropMode.ContourDetection => await ExtractByContourDetectionAsync(imageBytes, mimeType, options.MinComponentSize, options.EdgeDetectionThreshold),
                CropMode.FloodFillRegions => await ExtractByFloodFillAsync(imageBytes, mimeType, options.MinComponentSize, 15),
                CropMode.SmartUICards => await ExtractUICardsAsync(imageBytes, mimeType, options.MinComponentSize),
                CropMode.AzureVision => await ExtractWithAzureVisionAsync(imageBytes, mimeType),
                CropMode.AzureVisionComprehensive => await ExtractWithAzureVisionAsync(imageBytes, mimeType),
                CropMode.AzureVisionPeople => await ExtractPeopleWithAzureVisionAsync(imageBytes, mimeType),
                CropMode.HybridPixelPerfect => await ExtractHybridPixelPerfectAsync(imageBytes, mimeType),
                _ => await ExtractImagesAsync(imageBytes, mimeType)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Extraction with options failed");
            return new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Extraction failed: {ex.Message}",
                Images = new List<ExtractedImage>(),
                TotalImagesFound = 0
            };
        }
    }

    /// <summary>
    /// Extract using AI detection with edge refinement for pixel-perfect bounds
    /// </summary>
    private async Task<ImageExtractionResult> ExtractWithEdgeRefinementAsync(byte[] imageBytes, string mimeType, ExtractionOptions options)
    {
        try
        {
            // First get AI-detected regions
            var regionsResult = await IdentifyImageRegionsAsync(imageBytes, mimeType);
            
            if (!regionsResult.Success || regionsResult.Regions == null || regionsResult.Regions.Count == 0)
            {
                return new ImageExtractionResult
                {
                    Success = regionsResult.Success,
                    ErrorMessage = regionsResult.ErrorMessage,
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0,
                    AnalysisSummary = "No regions detected"
                };
            }

            var extractedImages = new List<ExtractedImage>();

            foreach (var region in regionsResult.Regions)
            {
                if (region.BoundingBox == null) continue;

                // Refine the bounding box using edge detection
                var refinedBox = options.RefineWithEdgeDetection 
                    ? await RefineRegionBoundsAsync(imageBytes, region.BoundingBox, options.EdgeDetectionThreshold)
                    : region.BoundingBox;

                if (!options.DetectOnly)
                {
                    var cropResult = await CropRegionAsync(imageBytes, mimeType, refinedBox);
                    if (cropResult.Success && cropResult.ImageData != null)
                    {
                        extractedImages.Add(new ExtractedImage
                        {
                            ImageData = cropResult.ImageData,
                            Base64Data = cropResult.Base64Data,
                            Description = region.Description ?? "Extracted image",
                            ImageType = region.ImageType ?? "image",
                            BoundingBox = refinedBox,
                            SuggestedFilename = region.SuggestedFilename ?? $"image-{extractedImages.Count + 1}.png",
                            Confidence = region.Confidence,
                            Width = cropResult.Width,
                            Height = cropResult.Height
                        });
                    }
                }
                else
                {
                    extractedImages.Add(new ExtractedImage
                    {
                        Description = region.Description ?? "Detected region",
                        ImageType = region.ImageType ?? "image",
                        BoundingBox = refinedBox,
                        SuggestedFilename = region.SuggestedFilename,
                        Confidence = region.Confidence,
                        Width = refinedBox.Width,
                        Height = refinedBox.Height
                    });
                }
            }

            return new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Extracted {extractedImages.Count} images with edge-refined bounds"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Edge-refined extraction failed");
            return new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Images = new List<ExtractedImage>(),
                TotalImagesFound = 0
            };
        }
    }

    /// <summary>
    /// Extract image using grid-based segmentation
    /// </summary>
    public Task<ImageExtractionResult> ExtractGridAsync(byte[] imageBytes, string mimeType, int rows, int columns)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return Task.FromResult(new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                });
            }

            var cellWidth = bitmap.Width / columns;
            var cellHeight = bitmap.Height / rows;
            var extractedImages = new List<ExtractedImage>();

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    var x = col * cellWidth;
                    var y = row * cellHeight;
                    var width = col == columns - 1 ? bitmap.Width - x : cellWidth;
                    var height = row == rows - 1 ? bitmap.Height - y : cellHeight;

                    var boundingBox = new BoundingBox
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        NormalizedX = (double)x / bitmap.Width,
                        NormalizedY = (double)y / bitmap.Height,
                        NormalizedWidth = (double)width / bitmap.Width,
                        NormalizedHeight = (double)height / bitmap.Height
                    };

                    var cropResult = CropRegionAsync(imageBytes, mimeType, boundingBox).Result;
                    if (cropResult.Success && cropResult.ImageData != null)
                    {
                        extractedImages.Add(new ExtractedImage
                        {
                            ImageData = cropResult.ImageData,
                            Base64Data = cropResult.Base64Data,
                            Description = $"Grid cell [{row},{col}]",
                            ImageType = "grid-cell",
                            BoundingBox = boundingBox,
                            SuggestedFilename = $"grid-{row}-{col}.png",
                            Confidence = 100,
                            Width = cropResult.Width,
                            Height = cropResult.Height
                        });
                    }
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Extracted {rows}×{columns} grid ({extractedImages.Count} cells)"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Grid extraction failed");
            return Task.FromResult(new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Grid extraction failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Detect and extract horizontal/vertical sections based on edge detection
    /// </summary>
    public Task<ImageExtractionResult> ExtractSectionsAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return Task.FromResult(new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                });
            }

            // Find horizontal section dividers (rows with consistent color indicating section boundaries)
            var horizontalDividers = FindHorizontalDividers(bitmap);
            var extractedImages = new List<ExtractedImage>();

            // Create sections from dividers
            var sectionBounds = GetSectionBoundsFromDividers(horizontalDividers, bitmap.Width, bitmap.Height);

            int sectionIndex = 0;
            foreach (var bounds in sectionBounds)
            {
                if (bounds.Height < 20) continue; // Skip very thin sections

                var cropResult = CropRegionAsync(imageBytes, mimeType, bounds).Result;
                if (cropResult.Success && cropResult.ImageData != null)
                {
                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = $"Section {sectionIndex + 1}",
                        ImageType = "section",
                        BoundingBox = bounds,
                        SuggestedFilename = $"section-{sectionIndex + 1}.png",
                        Confidence = 90,
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                    sectionIndex++;
                }
            }

            // If no sections found, return the whole image as one section
            if (extractedImages.Count == 0)
            {
                var fullBounds = new BoundingBox
                {
                    X = 0, Y = 0,
                    Width = bitmap.Width, Height = bitmap.Height,
                    NormalizedX = 0, NormalizedY = 0,
                    NormalizedWidth = 1, NormalizedHeight = 1
                };
                var cropResult = CropRegionAsync(imageBytes, mimeType, fullBounds).Result;
                if (cropResult.Success && cropResult.ImageData != null)
                {
                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = "Full image (no sections detected)",
                        ImageType = "section",
                        BoundingBox = fullBounds,
                        SuggestedFilename = "full-image.png",
                        Confidence = 100,
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Detected {extractedImages.Count} sections"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Section extraction failed");
            return Task.FromResult(new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Section extraction failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Detect UI components using improved color-based region detection
    /// </summary>
    public Task<ImageExtractionResult> ExtractComponentsAsync(byte[] imageBytes, string mimeType, int minSize = 20)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return Task.FromResult(new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                });
            }

            _logger?.LogInformation("Starting improved component detection on {W}x{H} image", bitmap.Width, bitmap.Height);

            // Use a combination of color variance and edge detection
            var components = DetectUIComponentsImproved(bitmap, minSize);
            
            _logger?.LogInformation("Found {Count} UI components", components.Count);
            
            var extractedImages = new List<ExtractedImage>();

            int componentIndex = 0;
            foreach (var bounds in components)
            {
                // Add padding around detected components
                var padding = 4;
                var paddedBounds = new BoundingBox
                {
                    X = Math.Max(0, bounds.X - padding),
                    Y = Math.Max(0, bounds.Y - padding),
                    Width = Math.Min(bounds.Width + padding * 2, bitmap.Width - Math.Max(0, bounds.X - padding)),
                    Height = Math.Min(bounds.Height + padding * 2, bitmap.Height - Math.Max(0, bounds.Y - padding)),
                    NormalizedX = (double)Math.Max(0, bounds.X - padding) / bitmap.Width,
                    NormalizedY = (double)Math.Max(0, bounds.Y - padding) / bitmap.Height,
                    NormalizedWidth = (double)Math.Min(bounds.Width + padding * 2, bitmap.Width - Math.Max(0, bounds.X - padding)) / bitmap.Width,
                    NormalizedHeight = (double)Math.Min(bounds.Height + padding * 2, bitmap.Height - Math.Max(0, bounds.Y - padding)) / bitmap.Height
                };

                var cropResult = CropRegionAsync(imageBytes, mimeType, paddedBounds).Result;
                if (cropResult.Success && cropResult.ImageData != null)
                {
                    var aspectRatio = (double)bounds.Width / bounds.Height;
                    var componentType = DetermineComponentType(bounds.Width, bounds.Height, aspectRatio);

                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = $"{componentType} component ({bounds.Width}×{bounds.Height})",
                        ImageType = componentType,
                        BoundingBox = paddedBounds,
                        SuggestedFilename = $"component-{componentIndex + 1}-{componentType}.png",
                        Confidence = 85,
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                    componentIndex++;
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Detected {extractedImages.Count} UI components via edge detection"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Component extraction failed");
            return Task.FromResult(new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Component extraction failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Refine bounding box to snap to actual pixel edges using edge detection
    /// </summary>
    public Task<BoundingBox> RefineRegionBoundsAsync(byte[] imageBytes, BoundingBox approximateBounds, int threshold = 30)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return Task.FromResult(approximateBounds);
            }

            int x = approximateBounds.X;
            int y = approximateBounds.Y;
            int width = approximateBounds.Width;
            int height = approximateBounds.Height;

            // Expand search area slightly
            var searchPadding = 10;
            var searchX = Math.Max(0, x - searchPadding);
            var searchY = Math.Max(0, y - searchPadding);
            var searchRight = Math.Min(bitmap.Width, x + width + searchPadding);
            var searchBottom = Math.Min(bitmap.Height, y + height + searchPadding);

            // Find actual left edge
            var refinedLeft = FindEdge(bitmap, searchX, x + width / 3, searchY, searchBottom, true, true, threshold);
            
            // Find actual right edge  
            var refinedRight = FindEdge(bitmap, x + width * 2 / 3, searchRight, searchY, searchBottom, true, false, threshold);
            
            // Find actual top edge
            var refinedTop = FindEdge(bitmap, searchX, searchRight, searchY, y + height / 3, false, true, threshold);
            
            // Find actual bottom edge
            var refinedBottom = FindEdge(bitmap, searchX, searchRight, y + height * 2 / 3, searchBottom, false, false, threshold);

            // Calculate refined bounds
            var newX = refinedLeft ?? x;
            var newY = refinedTop ?? y;
            var newRight = refinedRight ?? (x + width);
            var newBottom = refinedBottom ?? (y + height);

            var newWidth = Math.Max(4, newRight - newX);
            var newHeight = Math.Max(4, newBottom - newY);

            var refinedBounds = new BoundingBox
            {
                X = newX,
                Y = newY,
                Width = newWidth,
                Height = newHeight,
                NormalizedX = (double)newX / bitmap.Width,
                NormalizedY = (double)newY / bitmap.Height,
                NormalizedWidth = (double)newWidth / bitmap.Width,
                NormalizedHeight = (double)newHeight / bitmap.Height
            };

            _logger?.LogDebug("Refined bounds from ({OldX},{OldY},{OldW}x{OldH}) to ({NewX},{NewY},{NewW}x{NewH})",
                x, y, width, height, newX, newY, newWidth, newHeight);

            return Task.FromResult(refinedBounds);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to refine bounds, using original");
            return Task.FromResult(approximateBounds);
        }
    }

    /// <summary>
    /// Extract images using pixel-perfect contour detection - finds actual image boundaries by analyzing background color
    /// This method finds rectangular regions that differ from the background color
    /// </summary>
    public Task<ImageExtractionResult> ExtractByContourDetectionAsync(byte[] imageBytes, string mimeType, int minSize = 20, int colorThreshold = 25)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return Task.FromResult(new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                });
            }

            _logger?.LogInformation("Starting contour detection on {W}x{H} image", bitmap.Width, bitmap.Height);

            // Step 1: Detect the background color (sample corners and edges)
            var backgroundColor = DetectBackgroundColor(bitmap);
            _logger?.LogDebug("Detected background color: R={R} G={G} B={B}", backgroundColor.Red, backgroundColor.Green, backgroundColor.Blue);

            // Step 2: Create a mask of pixels that differ from background
            var mask = CreateForegroundMask(bitmap, backgroundColor, colorThreshold);

            // Step 3: Find rectangular regions in the mask using run-length encoding
            var regions = FindRectangularRegions(mask, bitmap.Width, bitmap.Height, minSize);
            
            _logger?.LogInformation("Found {Count} regions via contour detection", regions.Count);

            // Step 4: Crop each region
            var extractedImages = new List<ExtractedImage>();
            int regionIndex = 0;
            
            foreach (var bounds in regions)
            {
                var cropResult = CropRegionAsync(imageBytes, mimeType, bounds).Result;
                if (cropResult.Success && cropResult.ImageData != null)
                {
                    var aspectRatio = (double)bounds.Width / bounds.Height;
                    var componentType = ClassifyRegionByShape(bounds.Width, bounds.Height, aspectRatio);

                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = $"{componentType} ({bounds.Width}×{bounds.Height})",
                        ImageType = componentType,
                        BoundingBox = bounds,
                        SuggestedFilename = $"region-{regionIndex + 1}-{componentType}.png",
                        Confidence = 95, // High confidence since we're using pixel analysis
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                    regionIndex++;
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Detected {extractedImages.Count} regions via pixel-perfect contour detection"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Contour detection failed");
            return Task.FromResult(new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Contour detection failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Extract images using flood-fill to find distinct colored regions
    /// </summary>
    public Task<ImageExtractionResult> ExtractByFloodFillAsync(byte[] imageBytes, string mimeType, int minSize = 30, int colorTolerance = 15)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return Task.FromResult(new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                });
            }

            _logger?.LogInformation("Starting flood-fill detection on {W}x{H} image", bitmap.Width, bitmap.Height);

            var width = bitmap.Width;
            var height = bitmap.Height;
            var visited = new bool[width, height];
            var regions = new List<BoundingBox>();

            // Detect background color
            var backgroundColor = DetectBackgroundColor(bitmap);

            // Scan the image for distinct regions
            for (int y = 0; y < height; y += 2) // Step by 2 for performance
            {
                for (int x = 0; x < width; x += 2)
                {
                    if (visited[x, y]) continue;

                    var pixelColor = bitmap.GetPixel(x, y);
                    
                    // Skip if this is background color
                    if (ColorSimilar(pixelColor, backgroundColor, colorTolerance))
                    {
                        visited[x, y] = true;
                        continue;
                    }

                    // Flood fill to find the extent of this region
                    var regionBounds = FloodFillRegion(bitmap, visited, x, y, pixelColor, colorTolerance, width, height);
                    
                    if (regionBounds.Width >= minSize && regionBounds.Height >= minSize)
                    {
                        // Check if this region overlaps significantly with an existing one
                        bool isUnique = true;
                        foreach (var existing in regions)
                        {
                            if (RegionsOverlapSignificantly(regionBounds, existing, 0.5))
                            {
                                isUnique = false;
                                // Merge into existing if this one is larger
                                if (regionBounds.Width * regionBounds.Height > existing.Width * existing.Height)
                                {
                                    regions.Remove(existing);
                                    regions.Add(regionBounds);
                                }
                                break;
                            }
                        }
                        if (isUnique)
                        {
                            regions.Add(regionBounds);
                        }
                    }
                }
            }

            _logger?.LogInformation("Found {Count} regions via flood-fill", regions.Count);

            // Crop each region
            var extractedImages = new List<ExtractedImage>();
            int regionIndex = 0;

            foreach (var bounds in regions)
            {
                var cropResult = CropRegionAsync(imageBytes, mimeType, bounds).Result;
                if (cropResult.Success && cropResult.ImageData != null)
                {
                    var aspectRatio = (double)bounds.Width / bounds.Height;
                    var componentType = ClassifyRegionByShape(bounds.Width, bounds.Height, aspectRatio);

                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = $"{componentType} ({bounds.Width}×{bounds.Height})",
                        ImageType = componentType,
                        BoundingBox = bounds,
                        SuggestedFilename = $"region-{regionIndex + 1}-{componentType}.png",
                        Confidence = 90,
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                    regionIndex++;
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Detected {extractedImages.Count} regions via flood-fill analysis"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Flood-fill detection failed");
            return Task.FromResult(new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"Flood-fill detection failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Extract UI cards - rectangular regions with distinct backgrounds, shadows, or borders
    /// Optimized for detecting health app cards, dashboard widgets, etc.
    /// </summary>
    public Task<ImageExtractionResult> ExtractUICardsAsync(byte[] imageBytes, string mimeType, int minSize = 40)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return Task.FromResult(new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                });
            }

            _logger?.LogInformation("Starting UI card detection on {W}x{H} image", bitmap.Width, bitmap.Height);

            var width = bitmap.Width;
            var height = bitmap.Height;
            
            // Detect background color
            var backgroundColor = DetectBackgroundColor(bitmap);
            
            // Find UI cards using a multi-pass approach:
            // 1. Find regions with distinct solid backgrounds (card backgrounds are usually solid colors)
            // 2. Find regions bounded by shadows/gradients
            // 3. Find regions with consistent borders
            
            var cards = DetectCardsMultiPass(bitmap, backgroundColor, minSize);
            
            _logger?.LogInformation("Found {Count} UI cards", cards.Count);

            var extractedImages = new List<ExtractedImage>();
            int cardIndex = 0;

            foreach (var bounds in cards)
            {
                // Add padding to capture shadows and rounded corners
                var padding = 6;
                var paddedBounds = new BoundingBox
                {
                    X = Math.Max(0, bounds.X - padding),
                    Y = Math.Max(0, bounds.Y - padding),
                    Width = Math.Min(bounds.Width + padding * 2, width - Math.Max(0, bounds.X - padding)),
                    Height = Math.Min(bounds.Height + padding * 2, height - Math.Max(0, bounds.Y - padding)),
                    NormalizedX = (double)Math.Max(0, bounds.X - padding) / width,
                    NormalizedY = (double)Math.Max(0, bounds.Y - padding) / height,
                    NormalizedWidth = (double)Math.Min(bounds.Width + padding * 2, width - Math.Max(0, bounds.X - padding)) / width,
                    NormalizedHeight = (double)Math.Min(bounds.Height + padding * 2, height - Math.Max(0, bounds.Y - padding)) / height
                };

                var cropResult = CropRegionAsync(imageBytes, mimeType, paddedBounds).Result;
                if (cropResult.Success && cropResult.ImageData != null)
                {
                    var aspectRatio = (double)bounds.Width / bounds.Height;
                    var cardType = ClassifyCardType(bounds.Width, bounds.Height, aspectRatio);

                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = $"{cardType} card ({bounds.Width}×{bounds.Height})",
                        ImageType = cardType,
                        BoundingBox = paddedBounds,
                        SuggestedFilename = $"card-{cardIndex + 1}-{cardType}.png",
                        Confidence = 92,
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                    cardIndex++;
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Detected {extractedImages.Count} UI cards/widgets"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "UI card detection failed");
            return Task.FromResult(new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"UI card detection failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Detect UI cards using multiple detection strategies
    /// </summary>
    private List<BoundingBox> DetectCardsMultiPass(SKBitmap bitmap, SKColor backgroundColor, int minSize)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var allCards = new List<BoundingBox>();
        
        // Pass 1: Find solid-colored rectangular regions (card backgrounds)
        var solidRegions = FindSolidColorRegions(bitmap, backgroundColor, minSize);
        allCards.AddRange(solidRegions);
        
        // Pass 2: Find regions bounded by horizontal/vertical lines (borders, dividers)
        var borderedRegions = FindBorderedRegions(bitmap, backgroundColor, minSize);
        foreach (var region in borderedRegions)
        {
            // Only add if not overlapping significantly with existing
            if (!allCards.Any(existing => RegionsOverlapSignificantly(region, existing, 0.7)))
            {
                allCards.Add(region);
            }
        }
        
        // Pass 3: Find shadow-bounded regions
        var shadowRegions = FindShadowBoundedRegions(bitmap, backgroundColor, minSize);
        foreach (var region in shadowRegions)
        {
            if (!allCards.Any(existing => RegionsOverlapSignificantly(region, existing, 0.7)))
            {
                allCards.Add(region);
            }
        }
        
        // Merge overlapping cards and sort by area (largest first)
        var merged = MergeOverlappingRegions(allCards);
        return merged.OrderByDescending(c => c.Width * c.Height).ToList();
    }

    /// <summary>
    /// Find rectangular regions with solid background colors
    /// </summary>
    private List<BoundingBox> FindSolidColorRegions(SKBitmap bitmap, SKColor backgroundColor, int minSize)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var regions = new List<BoundingBox>();
        var visited = new bool[width, height];
        
        // Scan with a larger step to find solid color areas
        for (int y = 5; y < height - 5; y += 10)
        {
            for (int x = 5; x < width - 5; x += 10)
            {
                if (visited[x, y]) continue;
                
                var pixel = bitmap.GetPixel(x, y);
                
                // Skip background
                if (ColorSimilar(pixel, backgroundColor, 15)) continue;
                
                // Check if this starts a solid color region
                if (IsSolidColorArea(bitmap, x, y, 20, 20, pixel, 20))
                {
                    // Expand to find full extent
                    var bounds = ExpandSolidRegion(bitmap, visited, x, y, pixel, 25, width, height);
                    
                    if (bounds.Width >= minSize && bounds.Height >= minSize)
                    {
                        regions.Add(bounds);
                    }
                }
            }
        }
        
        return regions;
    }

    /// <summary>
    /// Check if an area has a solid color
    /// </summary>
    private bool IsSolidColorArea(SKBitmap bitmap, int startX, int startY, int checkWidth, int checkHeight, SKColor targetColor, int tolerance)
    {
        int matchingPixels = 0;
        int totalPixels = 0;
        
        for (int y = startY; y < startY + checkHeight && y < bitmap.Height; y++)
        {
            for (int x = startX; x < startX + checkWidth && x < bitmap.Width; x++)
            {
                totalPixels++;
                if (ColorSimilar(bitmap.GetPixel(x, y), targetColor, tolerance))
                {
                    matchingPixels++;
                }
            }
        }
        
        return totalPixels > 0 && (double)matchingPixels / totalPixels > 0.8;
    }

    /// <summary>
    /// Expand from a starting point to find the full extent of a solid-colored region
    /// </summary>
    private BoundingBox ExpandSolidRegion(SKBitmap bitmap, bool[,] visited, int startX, int startY, SKColor regionColor, int tolerance, int width, int height)
    {
        // Find bounds by scanning outward
        int left = startX, right = startX, top = startY, bottom = startY;
        
        // Scan left
        for (int x = startX; x >= 0; x--)
        {
            if (ColorSimilar(bitmap.GetPixel(x, startY), regionColor, tolerance))
                left = x;
            else
                break;
        }
        
        // Scan right
        for (int x = startX; x < width; x++)
        {
            if (ColorSimilar(bitmap.GetPixel(x, startY), regionColor, tolerance))
                right = x;
            else
                break;
        }
        
        // Scan up
        for (int y = startY; y >= 0; y--)
        {
            if (ColorSimilar(bitmap.GetPixel(startX, y), regionColor, tolerance))
                top = y;
            else
                break;
        }
        
        // Scan down
        for (int y = startY; y < height; y++)
        {
            if (ColorSimilar(bitmap.GetPixel(startX, y), regionColor, tolerance))
                bottom = y;
            else
                break;
        }
        
        // Refine bounds by checking corners
        var bounds = RefineSolidRegionBounds(bitmap, left, top, right, bottom, regionColor, tolerance);
        
        // Mark as visited
        for (int y = bounds.Y; y < bounds.Y + bounds.Height && y < height; y++)
        {
            for (int x = bounds.X; x < bounds.X + bounds.Width && x < width; x++)
            {
                visited[x, y] = true;
            }
        }
        
        return bounds;
    }

    /// <summary>
    /// Refine bounds of a solid region by checking uniformity
    /// </summary>
    private BoundingBox RefineSolidRegionBounds(SKBitmap bitmap, int left, int top, int right, int bottom, SKColor regionColor, int tolerance)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        
        // Shrink bounds if edges are not uniform
        while (left < right && !IsVerticalLineUniform(bitmap, left, top, bottom, regionColor, tolerance, 0.7))
            left++;
        while (right > left && !IsVerticalLineUniform(bitmap, right, top, bottom, regionColor, tolerance, 0.7))
            right--;
        while (top < bottom && !IsHorizontalLineUniform(bitmap, top, left, right, regionColor, tolerance, 0.7))
            top++;
        while (bottom > top && !IsHorizontalLineUniform(bitmap, bottom, left, right, regionColor, tolerance, 0.7))
            bottom--;
        
        return new BoundingBox
        {
            X = left,
            Y = top,
            Width = right - left + 1,
            Height = bottom - top + 1,
            NormalizedX = (double)left / width,
            NormalizedY = (double)top / height,
            NormalizedWidth = (double)(right - left + 1) / width,
            NormalizedHeight = (double)(bottom - top + 1) / height
        };
    }

    private bool IsVerticalLineUniform(SKBitmap bitmap, int x, int startY, int endY, SKColor color, int tolerance, double threshold)
    {
        int matches = 0;
        int total = 0;
        for (int y = startY; y <= endY && y < bitmap.Height; y++)
        {
            total++;
            if (ColorSimilar(bitmap.GetPixel(x, y), color, tolerance)) matches++;
        }
        return total > 0 && (double)matches / total >= threshold;
    }

    private bool IsHorizontalLineUniform(SKBitmap bitmap, int y, int startX, int endX, SKColor color, int tolerance, double threshold)
    {
        int matches = 0;
        int total = 0;
        for (int x = startX; x <= endX && x < bitmap.Width; x++)
        {
            total++;
            if (ColorSimilar(bitmap.GetPixel(x, y), color, tolerance)) matches++;
        }
        return total > 0 && (double)matches / total >= threshold;
    }

    /// <summary>
    /// Find regions bounded by visible borders or lines
    /// </summary>
    private List<BoundingBox> FindBorderedRegions(SKBitmap bitmap, SKColor backgroundColor, int minSize)
    {
        // Simplified: look for regions with distinct edges
        var width = bitmap.Width;
        var height = bitmap.Height;
        var regions = new List<BoundingBox>();
        
        // Find horizontal lines
        var horizontalLines = new List<int>();
        for (int y = 0; y < height; y++)
        {
            if (IsHorizontalEdgeLine(bitmap, y, backgroundColor, 0.3))
            {
                horizontalLines.Add(y);
            }
        }
        
        // Find vertical lines
        var verticalLines = new List<int>();
        for (int x = 0; x < width; x++)
        {
            if (IsVerticalEdgeLine(bitmap, x, backgroundColor, 0.3))
            {
                verticalLines.Add(x);
            }
        }
        
        // Create regions from line intersections
        for (int i = 0; i < horizontalLines.Count - 1; i++)
        {
            for (int j = 0; j < verticalLines.Count - 1; j++)
            {
                var regionWidth = verticalLines[j + 1] - verticalLines[j];
                var regionHeight = horizontalLines[i + 1] - horizontalLines[i];
                
                if (regionWidth >= minSize && regionHeight >= minSize)
                {
                    regions.Add(new BoundingBox
                    {
                        X = verticalLines[j],
                        Y = horizontalLines[i],
                        Width = regionWidth,
                        Height = regionHeight,
                        NormalizedX = (double)verticalLines[j] / width,
                        NormalizedY = (double)horizontalLines[i] / height,
                        NormalizedWidth = (double)regionWidth / width,
                        NormalizedHeight = (double)regionHeight / height
                    });
                }
            }
        }
        
        return regions;
    }

    private bool IsHorizontalEdgeLine(SKBitmap bitmap, int y, SKColor backgroundColor, double threshold)
    {
        int edgePixels = 0;
        int step = Math.Max(1, bitmap.Width / 50);
        int total = 0;
        
        for (int x = 0; x < bitmap.Width; x += step)
        {
            total++;
            var pixel = bitmap.GetPixel(x, y);
            
            // Check for edge (significant color change from background or adjacent pixels)
            if (!ColorSimilar(pixel, backgroundColor, 30))
            {
                if (y > 0 && !ColorSimilar(pixel, bitmap.GetPixel(x, y - 1), 20))
                    edgePixels++;
                else if (y < bitmap.Height - 1 && !ColorSimilar(pixel, bitmap.GetPixel(x, y + 1), 20))
                    edgePixels++;
            }
        }
        
        return total > 0 && (double)edgePixels / total >= threshold;
    }

    private bool IsVerticalEdgeLine(SKBitmap bitmap, int x, SKColor backgroundColor, double threshold)
    {
        int edgePixels = 0;
        int step = Math.Max(1, bitmap.Height / 50);
        int total = 0;
        
        for (int y = 0; y < bitmap.Height; y += step)
        {
            total++;
            var pixel = bitmap.GetPixel(x, y);
            
            if (!ColorSimilar(pixel, backgroundColor, 30))
            {
                if (x > 0 && !ColorSimilar(pixel, bitmap.GetPixel(x - 1, y), 20))
                    edgePixels++;
                else if (x < bitmap.Width - 1 && !ColorSimilar(pixel, bitmap.GetPixel(x + 1, y), 20))
                    edgePixels++;
            }
        }
        
        return total > 0 && (double)edgePixels / total >= threshold;
    }

    /// <summary>
    /// Find regions bounded by shadows (gradual color transitions to darker)
    /// </summary>
    private List<BoundingBox> FindShadowBoundedRegions(SKBitmap bitmap, SKColor backgroundColor, int minSize)
    {
        // Shadows appear as gradual darkening at edges
        // For now, return empty - can be enhanced later
        return new List<BoundingBox>();
    }

    /// <summary>
    /// Classify a card by its proportions
    /// </summary>
    private string ClassifyCardType(int width, int height, double aspectRatio)
    {
        if (width <= 80 && height <= 80) return "widget-small";
        if (aspectRatio > 2.5) return "banner-card";
        if (aspectRatio < 0.5) return "vertical-card";
        if (Math.Abs(aspectRatio - 1) < 0.3) return "square-card";
        if (width > 150 && height > 100) return "dashboard-card";
        return "card";
    }

    #region Pixel Analysis Helpers

    /// <summary>
    /// Detect the most common background color by sampling edges and corners
    /// </summary>
    private SKColor DetectBackgroundColor(SKBitmap bitmap)
    {
        var samples = new List<SKColor>();
        var w = bitmap.Width;
        var h = bitmap.Height;

        // Sample corners
        samples.Add(bitmap.GetPixel(0, 0));
        samples.Add(bitmap.GetPixel(w - 1, 0));
        samples.Add(bitmap.GetPixel(0, h - 1));
        samples.Add(bitmap.GetPixel(w - 1, h - 1));

        // Sample edges
        for (int i = 0; i < w; i += Math.Max(1, w / 20))
        {
            samples.Add(bitmap.GetPixel(i, 0));
            samples.Add(bitmap.GetPixel(i, h - 1));
        }
        for (int i = 0; i < h; i += Math.Max(1, h / 20))
        {
            samples.Add(bitmap.GetPixel(0, i));
            samples.Add(bitmap.GetPixel(w - 1, i));
        }

        // Find most common color (by grouping similar colors)
        var colorGroups = new Dictionary<(int r, int g, int b), int>();
        foreach (var color in samples)
        {
            // Round to nearest 10 to group similar colors
            var key = (color.Red / 10 * 10, color.Green / 10 * 10, color.Blue / 10 * 10);
            colorGroups[key] = colorGroups.GetValueOrDefault(key, 0) + 1;
        }

        var mostCommon = colorGroups.OrderByDescending(kv => kv.Value).First().Key;
        return new SKColor((byte)mostCommon.r, (byte)mostCommon.g, (byte)mostCommon.b);
    }

    /// <summary>
    /// Create a boolean mask where true = foreground (differs from background)
    /// </summary>
    private bool[,] CreateForegroundMask(SKBitmap bitmap, SKColor backgroundColor, int threshold)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var mask = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                mask[x, y] = !ColorSimilar(pixel, backgroundColor, threshold);
            }
        }

        return mask;
    }

    /// <summary>
    /// Check if two colors are similar within a tolerance
    /// </summary>
    private bool ColorSimilar(SKColor a, SKColor b, int tolerance)
    {
        return Math.Abs(a.Red - b.Red) <= tolerance &&
               Math.Abs(a.Green - b.Green) <= tolerance &&
               Math.Abs(a.Blue - b.Blue) <= tolerance;
    }

    /// <summary>
    /// Find rectangular regions in a foreground mask using scanline analysis
    /// </summary>
    private List<BoundingBox> FindRectangularRegions(bool[,] mask, int width, int height, int minSize)
    {
        var visited = new bool[width, height];
        var regions = new List<BoundingBox>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mask[x, y] && !visited[x, y])
                {
                    // Found a foreground pixel, flood fill to find the region
                    var bounds = FloodFillBoundsFromMask(mask, visited, x, y, width, height);
                    
                    if (bounds.Width >= minSize && bounds.Height >= minSize)
                    {
                        // Tighten bounds by trimming empty rows/columns
                        var tightBounds = TightenBounds(mask, bounds);
                        if (tightBounds.Width >= minSize && tightBounds.Height >= minSize)
                        {
                            regions.Add(tightBounds);
                        }
                    }
                }
            }
        }

        // Merge overlapping regions
        return MergeOverlappingRegions(regions);
    }

    /// <summary>
    /// Flood fill from a mask to find region bounds
    /// </summary>
    private BoundingBox FloodFillBoundsFromMask(bool[,] mask, bool[,] visited, int startX, int startY, int width, int height)
    {
        var minX = startX;
        var maxX = startX;
        var minY = startY;
        var maxY = startY;

        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (visited[x, y] || !mask[x, y]) continue;

            visited[x, y] = true;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);

            // 4-connected neighbors
            queue.Enqueue((x - 1, y));
            queue.Enqueue((x + 1, y));
            queue.Enqueue((x, y - 1));
            queue.Enqueue((x, y + 1));
        }

        return new BoundingBox
        {
            X = minX,
            Y = minY,
            Width = maxX - minX + 1,
            Height = maxY - minY + 1,
            NormalizedX = (double)minX / width,
            NormalizedY = (double)minY / height,
            NormalizedWidth = (double)(maxX - minX + 1) / width,
            NormalizedHeight = (double)(maxY - minY + 1) / height
        };
    }

    /// <summary>
    /// Tighten bounds by finding the actual content rectangle
    /// </summary>
    private BoundingBox TightenBounds(bool[,] mask, BoundingBox bounds)
    {
        int left = bounds.X + bounds.Width;
        int right = bounds.X;
        int top = bounds.Y + bounds.Height;
        int bottom = bounds.Y;

        for (int y = bounds.Y; y < bounds.Y + bounds.Height && y < mask.GetLength(1); y++)
        {
            for (int x = bounds.X; x < bounds.X + bounds.Width && x < mask.GetLength(0); x++)
            {
                if (mask[x, y])
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        if (left > right || top > bottom)
            return bounds;

        return new BoundingBox
        {
            X = left,
            Y = top,
            Width = right - left + 1,
            Height = bottom - top + 1,
            NormalizedX = bounds.NormalizedX,
            NormalizedY = bounds.NormalizedY,
            NormalizedWidth = bounds.NormalizedWidth,
            NormalizedHeight = bounds.NormalizedHeight
        };
    }

    /// <summary>
    /// Flood fill to find the extent of a colored region
    /// </summary>
    private BoundingBox FloodFillRegion(SKBitmap bitmap, bool[,] visited, int startX, int startY, SKColor targetColor, int tolerance, int width, int height)
    {
        var minX = startX;
        var maxX = startX;
        var minY = startY;
        var maxY = startY;

        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));

        int pixelCount = 0;
        int maxPixels = 50000; // Limit to prevent very large fills

        while (queue.Count > 0 && pixelCount < maxPixels)
        {
            var (x, y) = queue.Dequeue();

            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (visited[x, y]) continue;

            var pixel = bitmap.GetPixel(x, y);
            if (!ColorSimilar(pixel, targetColor, tolerance + 20)) continue; // Slightly more tolerance for connected regions

            visited[x, y] = true;
            pixelCount++;
            
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);

            // 4-connected neighbors
            queue.Enqueue((x - 1, y));
            queue.Enqueue((x + 1, y));
            queue.Enqueue((x, y - 1));
            queue.Enqueue((x, y + 1));
        }

        return new BoundingBox
        {
            X = minX,
            Y = minY,
            Width = maxX - minX + 1,
            Height = maxY - minY + 1,
            NormalizedX = (double)minX / width,
            NormalizedY = (double)minY / height,
            NormalizedWidth = (double)(maxX - minX + 1) / width,
            NormalizedHeight = (double)(maxY - minY + 1) / height
        };
    }

    /// <summary>
    /// Check if two regions overlap significantly
    /// </summary>
    private bool RegionsOverlapSignificantly(BoundingBox a, BoundingBox b, double threshold)
    {
        int overlapX = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X));
        int overlapY = Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        int overlapArea = overlapX * overlapY;
        int smallerArea = Math.Min(a.Width * a.Height, b.Width * b.Height);
        
        return overlapArea > smallerArea * threshold;
    }

    /// <summary>
    /// Merge overlapping regions into larger ones
    /// </summary>
    private List<BoundingBox> MergeOverlappingRegions(List<BoundingBox> regions)
    {
        if (regions.Count <= 1) return regions;

        var merged = new List<BoundingBox>();
        var used = new bool[regions.Count];

        for (int i = 0; i < regions.Count; i++)
        {
            if (used[i]) continue;

            var current = regions[i];
            bool didMerge;

            do
            {
                didMerge = false;
                for (int j = i + 1; j < regions.Count; j++)
                {
                    if (used[j]) continue;

                    if (RegionsOverlapSignificantly(current, regions[j], 0.3) || RegionsAdjacent(current, regions[j], 5))
                    {
                        current = MergeRegions(current, regions[j]);
                        used[j] = true;
                        didMerge = true;
                    }
                }
            } while (didMerge);

            merged.Add(current);
            used[i] = true;
        }

        return merged;
    }

    /// <summary>
    /// Check if two regions are adjacent (within margin pixels)
    /// </summary>
    private bool RegionsAdjacent(BoundingBox a, BoundingBox b, int margin)
    {
        // Check horizontal adjacency
        bool hAdjacent = (a.X + a.Width + margin >= b.X && a.X <= b.X + b.Width + margin);
        bool vOverlap = (a.Y < b.Y + b.Height && a.Y + a.Height > b.Y);
        
        // Check vertical adjacency
        bool vAdjacent = (a.Y + a.Height + margin >= b.Y && a.Y <= b.Y + b.Height + margin);
        bool hOverlap = (a.X < b.X + b.Width && a.X + a.Width > b.X);

        return (hAdjacent && vOverlap) || (vAdjacent && hOverlap);
    }

    /// <summary>
    /// Merge two regions into their combined bounding box
    /// </summary>
    private BoundingBox MergeRegions(BoundingBox a, BoundingBox b)
    {
        var minX = Math.Min(a.X, b.X);
        var minY = Math.Min(a.Y, b.Y);
        var maxX = Math.Max(a.X + a.Width, b.X + b.Width);
        var maxY = Math.Max(a.Y + a.Height, b.Y + b.Height);

        return new BoundingBox
        {
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY,
            NormalizedX = a.NormalizedX, // Will be recalculated if needed
            NormalizedY = a.NormalizedY,
            NormalizedWidth = a.NormalizedWidth,
            NormalizedHeight = a.NormalizedHeight
        };
    }

    /// <summary>
    /// Classify a region by its shape/size
    /// </summary>
    private string ClassifyRegionByShape(int width, int height, double aspectRatio)
    {
        if (width <= 64 && height <= 64) return "icon";
        if (width <= 120 && height <= 120 && Math.Abs(aspectRatio - 1) < 0.3) return "avatar";
        if (aspectRatio > 3) return "banner";
        if (aspectRatio < 0.33) return "sidebar";
        if (Math.Abs(aspectRatio - 1) < 0.2) return "square";
        if (width > 200 && height > 150) return "card";
        if (height <= 80 && width > 100) return "button";
        return "image";
    }

    /// <summary>
    /// Improved UI component detection using color variance and gradient analysis
    /// </summary>
    private List<BoundingBox> DetectUIComponentsImproved(SKBitmap bitmap, int minSize)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        
        // Step 1: Detect background color
        var backgroundColor = DetectBackgroundColor(bitmap);
        
        // Step 2: Create a map of "interesting" regions (not background, not text-like)
        var interestingMap = new bool[width, height];
        var colorVarianceMap = new double[width, height];
        
        // Calculate local color variance (helps identify images vs text/flat areas)
        for (int y = 2; y < height - 2; y++)
        {
            for (int x = 2; x < width - 2; x++)
            {
                var centerColor = bitmap.GetPixel(x, y);
                
                // Skip if it's background
                if (ColorSimilar(centerColor, backgroundColor, 20))
                {
                    interestingMap[x, y] = false;
                    continue;
                }
                
                // Calculate color variance in a 5x5 neighborhood
                var colors = new List<SKColor>();
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        colors.Add(bitmap.GetPixel(x + dx, y + dy));
                    }
                }
                
                var variance = CalculateColorVariance(colors);
                colorVarianceMap[x, y] = variance;
                
                // Mark as interesting if it has moderate variance (images) or distinct from background
                // Text tends to have very high local variance (sharp edges)
                // Images tend to have moderate variance
                // Flat UI elements have low variance
                interestingMap[x, y] = variance > 5 && variance < 150;
            }
        }
        
        // Step 3: Dilate the interesting map to connect nearby pixels
        var dilatedMap = DilateMask(interestingMap, width, height, 3);
        
        // Step 4: Find connected components in the dilated map
        var visited = new bool[width, height];
        var components = new List<BoundingBox>();
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (dilatedMap[x, y] && !visited[x, y])
                {
                    var bounds = FloodFillBoundsFromMask(dilatedMap, visited, x, y, width, height);
                    
                    // Filter: must be reasonable size and aspect ratio
                    if (bounds.Width >= minSize && bounds.Height >= minSize &&
                        bounds.Width <= width * 0.9 && bounds.Height <= height * 0.9)
                    {
                        // Check if this region has enough "content" (not mostly empty)
                        var contentRatio = CalculateContentRatio(interestingMap, bounds);
                        if (contentRatio > 0.1) // At least 10% non-background content
                        {
                            components.Add(bounds);
                        }
                    }
                }
            }
        }
        
        // Step 5: Merge overlapping/adjacent components
        var merged = MergeOverlappingRegions(components);
        
        // Step 6: Filter out regions that are likely text-only
        var filtered = merged.Where(b => !IsLikelyTextOnly(bitmap, b, backgroundColor)).ToList();
        
        return filtered;
    }
    
    /// <summary>
    /// Dilate a boolean mask to connect nearby pixels
    /// </summary>
    private bool[,] DilateMask(bool[,] mask, int width, int height, int radius)
    {
        var dilated = new bool[width, height];
        
        for (int y = radius; y < height - radius; y++)
        {
            for (int x = radius; x < width - radius; x++)
            {
                if (mask[x, y])
                {
                    // Set all pixels in radius to true
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            dilated[x + dx, y + dy] = true;
                        }
                    }
                }
            }
        }
        
        return dilated;
    }
    
    /// <summary>
    /// Calculate color variance in a list of colors
    /// </summary>
    private double CalculateColorVariance(List<SKColor> colors)
    {
        if (colors.Count == 0) return 0;
        
        var avgR = colors.Average(c => c.Red);
        var avgG = colors.Average(c => c.Green);
        var avgB = colors.Average(c => c.Blue);
        
        var variance = colors.Sum(c =>
            Math.Pow(c.Red - avgR, 2) +
            Math.Pow(c.Green - avgG, 2) +
            Math.Pow(c.Blue - avgB, 2)
        ) / colors.Count;
        
        return Math.Sqrt(variance);
    }
    
    /// <summary>
    /// Calculate ratio of content pixels in a bounding box
    /// </summary>
    private double CalculateContentRatio(bool[,] contentMap, BoundingBox bounds)
    {
        int contentPixels = 0;
        int totalPixels = 0;
        
        for (int y = bounds.Y; y < bounds.Y + bounds.Height && y < contentMap.GetLength(1); y++)
        {
            for (int x = bounds.X; x < bounds.X + bounds.Width && x < contentMap.GetLength(0); x++)
            {
                totalPixels++;
                if (contentMap[x, y]) contentPixels++;
            }
        }
        
        return totalPixels > 0 ? (double)contentPixels / totalPixels : 0;
    }
    
    /// <summary>
    /// Check if a region is likely text-only (high contrast, thin elements)
    /// </summary>
    private bool IsLikelyTextOnly(SKBitmap bitmap, BoundingBox bounds, SKColor backgroundColor)
    {
        // Sample the region and analyze
        var distinctColors = new HashSet<int>();
        var textLikePixels = 0;
        var totalSampled = 0;
        
        for (int y = bounds.Y; y < bounds.Y + bounds.Height && y < bitmap.Height; y += 2)
        {
            for (int x = bounds.X; x < bounds.X + bounds.Width && x < bitmap.Width; x += 2)
            {
                var pixel = bitmap.GetPixel(x, y);
                totalSampled++;
                
                // Quantize color to reduce noise
                var colorKey = ((pixel.Red / 32) << 10) | ((pixel.Green / 32) << 5) | (pixel.Blue / 32);
                distinctColors.Add(colorKey);
                
                // Check if pixel is very dark or very light (text-like)
                var brightness = (pixel.Red + pixel.Green + pixel.Blue) / 3;
                if (brightness < 50 || brightness > 220)
                {
                    textLikePixels++;
                }
            }
        }
        
        // Text regions typically have:
        // 1. Very few distinct colors (usually 2-3: text color + background)
        // 2. High ratio of very dark or very light pixels
        var colorRatio = (double)distinctColors.Count / Math.Max(1, totalSampled);
        var textRatio = (double)textLikePixels / Math.Max(1, totalSampled);
        
        // If very few colors and high text-like ratio, it's probably text
        return distinctColors.Count <= 5 && textRatio > 0.4;
    }

    #endregion

    #region Edge Detection Helpers

    /// <summary>
    /// Detect edges using Sobel-like gradient detection
    /// </summary>
    private bool[,] DetectEdges(SKBitmap bitmap, int threshold = 30)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var edges = new bool[width, height];

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // Get surrounding pixels for Sobel operator
                var tl = GetGrayscale(bitmap.GetPixel(x - 1, y - 1));
                var t = GetGrayscale(bitmap.GetPixel(x, y - 1));
                var tr = GetGrayscale(bitmap.GetPixel(x + 1, y - 1));
                var l = GetGrayscale(bitmap.GetPixel(x - 1, y));
                var r = GetGrayscale(bitmap.GetPixel(x + 1, y));
                var bl = GetGrayscale(bitmap.GetPixel(x - 1, y + 1));
                var b = GetGrayscale(bitmap.GetPixel(x, y + 1));
                var br = GetGrayscale(bitmap.GetPixel(x + 1, y + 1));

                // Sobel gradients
                var gx = (tr + 2 * r + br) - (tl + 2 * l + bl);
                var gy = (bl + 2 * b + br) - (tl + 2 * t + tr);

                var magnitude = Math.Sqrt(gx * gx + gy * gy);
                edges[x, y] = magnitude > threshold;
            }
        }

        return edges;
    }

    private static int GetGrayscale(SKColor color)
    {
        return (color.Red + color.Green + color.Blue) / 3;
    }

    /// <summary>
    /// Find connected components from edge map
    /// </summary>
    private List<BoundingBox> FindConnectedComponents(bool[,] edgeMap, int width, int height, int minSize)
    {
        var visited = new bool[width, height];
        var components = new List<BoundingBox>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (edgeMap[x, y] && !visited[x, y])
                {
                    var bounds = FloodFillBounds(edgeMap, visited, x, y, width, height);
                    if (bounds.Width >= minSize && bounds.Height >= minSize)
                    {
                        components.Add(bounds);
                    }
                }
            }
        }

        // Merge overlapping components
        return MergeOverlappingBounds(components);
    }

    private BoundingBox FloodFillBounds(bool[,] edgeMap, bool[,] visited, int startX, int startY, int width, int height)
    {
        var minX = startX;
        var maxX = startX;
        var minY = startY;
        var maxY = startY;

        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            
            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (visited[x, y] || !edgeMap[x, y]) continue;

            visited[x, y] = true;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);

            // Check 8-connected neighbors
            stack.Push((x - 1, y));
            stack.Push((x + 1, y));
            stack.Push((x, y - 1));
            stack.Push((x, y + 1));
            stack.Push((x - 1, y - 1));
            stack.Push((x + 1, y - 1));
            stack.Push((x - 1, y + 1));
            stack.Push((x + 1, y + 1));
        }

        return new BoundingBox
        {
            X = minX,
            Y = minY,
            Width = maxX - minX + 1,
            Height = maxY - minY + 1,
            NormalizedX = (double)minX / width,
            NormalizedY = (double)minY / height,
            NormalizedWidth = (double)(maxX - minX + 1) / width,
            NormalizedHeight = (double)(maxY - minY + 1) / height
        };
    }

    private List<BoundingBox> MergeOverlappingBounds(List<BoundingBox> components)
    {
        if (components.Count <= 1) return components;

        var merged = new List<BoundingBox>();
        var used = new bool[components.Count];

        for (int i = 0; i < components.Count; i++)
        {
            if (used[i]) continue;

            var current = components[i];
            bool didMerge;

            do
            {
                didMerge = false;
                for (int j = i + 1; j < components.Count; j++)
                {
                    if (used[j]) continue;

                    if (BoundsOverlapOrClose(current, components[j], 5))
                    {
                        current = MergeBounds(current, components[j]);
                        used[j] = true;
                        didMerge = true;
                    }
                }
            } while (didMerge);

            merged.Add(current);
            used[i] = true;
        }

        return merged;
    }

    private static bool BoundsOverlapOrClose(BoundingBox a, BoundingBox b, int margin)
    {
        return !(a.X + a.Width + margin < b.X ||
                 b.X + b.Width + margin < a.X ||
                 a.Y + a.Height + margin < b.Y ||
                 b.Y + b.Height + margin < a.Y);
    }

    private static BoundingBox MergeBounds(BoundingBox a, BoundingBox b)
    {
        var minX = Math.Min(a.X, b.X);
        var minY = Math.Min(a.Y, b.Y);
        var maxX = Math.Max(a.X + a.Width, b.X + b.Width);
        var maxY = Math.Max(a.Y + a.Height, b.Y + b.Height);

        return new BoundingBox
        {
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY,
            NormalizedX = a.NormalizedX, // Will be recalculated if needed
            NormalizedY = a.NormalizedY,
            NormalizedWidth = a.NormalizedWidth,
            NormalizedHeight = a.NormalizedHeight
        };
    }

    /// <summary>
    /// Find an edge (left/right/top/bottom) in a search area
    /// </summary>
    private int? FindEdge(SKBitmap bitmap, int startX, int endX, int startY, int endY, 
        bool horizontal, bool findFirst, int threshold)
    {
        if (horizontal)
        {
            // Search columns
            var range = findFirst 
                ? Enumerable.Range(startX, endX - startX)
                : Enumerable.Range(startX, endX - startX).Reverse();

            foreach (var x in range)
            {
                var edgeStrength = 0;
                for (int y = startY; y < endY; y++)
                {
                    if (x > 0 && x < bitmap.Width - 1)
                    {
                        var left = GetGrayscale(bitmap.GetPixel(x - 1, y));
                        var right = GetGrayscale(bitmap.GetPixel(x + 1, y));
                        if (Math.Abs(left - right) > threshold)
                            edgeStrength++;
                    }
                }
                if (edgeStrength > (endY - startY) / 4) // At least 25% of vertical line shows edge
                    return x;
            }
        }
        else
        {
            // Search rows
            var range = findFirst 
                ? Enumerable.Range(startY, endY - startY)
                : Enumerable.Range(startY, endY - startY).Reverse();

            foreach (var y in range)
            {
                var edgeStrength = 0;
                for (int x = startX; x < endX; x++)
                {
                    if (y > 0 && y < bitmap.Height - 1)
                    {
                        var top = GetGrayscale(bitmap.GetPixel(x, y - 1));
                        var bottom = GetGrayscale(bitmap.GetPixel(x, y + 1));
                        if (Math.Abs(top - bottom) > threshold)
                            edgeStrength++;
                    }
                }
                if (edgeStrength > (endX - startX) / 4) // At least 25% of horizontal line shows edge
                    return y;
            }
        }

        return null;
    }

    /// <summary>
    /// Find horizontal divider lines (consistent color rows)
    /// </summary>
    private List<int> FindHorizontalDividers(SKBitmap bitmap)
    {
        var dividers = new List<int>();
        var prevRowColor = SKColor.Empty;
        var uniformRowStart = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            // Sample colors across the row
            var colors = new List<SKColor>();
            for (int x = 0; x < bitmap.Width; x += Math.Max(1, bitmap.Width / 20))
            {
                colors.Add(bitmap.GetPixel(x, y));
            }

            // Check if row is uniform (all similar colors)
            var isUniform = colors.All(c => ColorDistance(c, colors[0]) < 20);
            var avgColor = colors[0];

            if (isUniform)
            {
                if (uniformRowStart < 0)
                {
                    uniformRowStart = y;
                    prevRowColor = avgColor;
                }
                else if (ColorDistance(avgColor, prevRowColor) > 30)
                {
                    // Color changed, this might be a section boundary
                    if (y - uniformRowStart > 2) // At least 3px uniform
                    {
                        dividers.Add((uniformRowStart + y) / 2);
                    }
                    uniformRowStart = y;
                    prevRowColor = avgColor;
                }
            }
            else
            {
                if (uniformRowStart >= 0 && y - uniformRowStart > 5)
                {
                    // End of uniform section
                    dividers.Add((uniformRowStart + y) / 2);
                }
                uniformRowStart = -1;
            }
        }

        return dividers;
    }

    private static double ColorDistance(SKColor a, SKColor b)
    {
        var dr = a.Red - b.Red;
        var dg = a.Green - b.Green;
        var db = a.Blue - b.Blue;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private List<BoundingBox> GetSectionBoundsFromDividers(List<int> dividers, int width, int height)
    {
        var bounds = new List<BoundingBox>();
        var sortedDividers = dividers.OrderBy(d => d).ToList();

        // Add implicit start
        if (sortedDividers.Count == 0 || sortedDividers[0] > 20)
        {
            sortedDividers.Insert(0, 0);
        }

        // Add implicit end
        if (sortedDividers.Last() < height - 20)
        {
            sortedDividers.Add(height);
        }

        for (int i = 0; i < sortedDividers.Count - 1; i++)
        {
            var startY = sortedDividers[i];
            var endY = sortedDividers[i + 1];
            var sectionHeight = endY - startY;

            if (sectionHeight > 20) // Minimum section height
            {
                bounds.Add(new BoundingBox
                {
                    X = 0,
                    Y = startY,
                    Width = width,
                    Height = sectionHeight,
                    NormalizedX = 0,
                    NormalizedY = (double)startY / height,
                    NormalizedWidth = 1,
                    NormalizedHeight = (double)sectionHeight / height
                });
            }
        }

        return bounds;
    }

    private static string DetermineComponentType(int width, int height, double aspectRatio)
    {
        if (width <= 48 && height <= 48) return "icon";
        if (width <= 100 && height <= 100 && Math.Abs(aspectRatio - 1) < 0.3) return "avatar";
        if (aspectRatio > 3) return "banner";
        if (aspectRatio < 0.3) return "sidebar";
        if (width > 200 && height > 100) return "card";
        if (height <= 60 && width > 100) return "button";
        return "component";
    }

    #endregion

    private async Task<ImageExtractionResult> ExtractByTypeAsync(byte[] imageBytes, string mimeType, string extractionType)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
            {
                return new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to decode source image"
                };
            }

            var base64Image = Convert.ToBase64String(imageBytes);
            var prompt = BuildExtractionPrompt(bitmap.Width, bitmap.Height, extractionType);
            var systemPrompt = GetSystemPrompt();

            var response = await _azureOpenAIService.GetVisionCompletionAsync(
                base64Image, mimeType, prompt, systemPrompt);

            var regions = ParseRegionsFromResponse(response, bitmap.Width, bitmap.Height);
            var extractedImages = new List<ExtractedImage>();

            foreach (var region in regions)
            {
                if (region.BoundingBox == null) continue;
                
                var cropResult = await CropRegionAsync(imageBytes, mimeType, region.BoundingBox);
                if (cropResult.Success && cropResult.ImageData != null)
                {
                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = region.Description,
                        ImageType = region.ImageType ?? extractionType,
                        BoundingBox = region.BoundingBox,
                        SuggestedFilename = region.SuggestedFilename,
                        Confidence = region.Confidence,
                        Width = cropResult.Width,
                        Height = cropResult.Height
                    });
                }
            }

            var typeLabel = extractionType == "icons" ? "icons" : "logos/brand elements";
            return new ImageExtractionResult
            {
                Success = true,
                Images = extractedImages,
                TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Extracted {extractedImages.Count} {typeLabel}"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "{Type} extraction failed", extractionType);
            return new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = $"{extractionType} extraction failed: {ex.Message}"
            };
        }
    }

    #region Private Helpers

    private static string GetSystemPrompt()
    {
        return """
            You are an expert image analysis AI specialized in detecting and precisely locating visual elements within screenshots and composite images.
            
            Your task is to identify distinct images, graphics, icons, logos, photos, illustrations, and other visual elements, then provide their exact bounding box coordinates.
            
            CRITICAL RULES:
            1. Return ONLY valid JSON - no explanations, no markdown formatting, just the JSON array
            2. Use normalized coordinates (0.0 to 1.0) relative to image dimensions
            3. normalizedX and normalizedY represent the TOP-LEFT corner of the bounding box
            4. Be precise - these coordinates will be used to crop the actual images
            5. Include ALL visible distinct visual elements, even small ones
            6. If no images found, return an empty array: []
            """;
    }

    private static string BuildExtractionPrompt(int imageWidth, int imageHeight, string extractionType)
    {
        var typeSpecificInstructions = extractionType switch
        {
            "icons" => """
                Focus specifically on ICONS and small graphics:
                - Navigation icons (home, menu, back, forward, search)
                - Action icons (edit, delete, add, share, download, upload)
                - Status indicators (notifications, battery, wifi, signal)
                - Social media icons
                - UI control icons (play, pause, settings, close)
                - Emoji and symbolic graphics
                - Small decorative elements
                
                Icons are typically small (under 100x100 pixels in the original) and symbolic in nature.
                """,
            "logos" => """
                Focus specifically on LOGOS and branding elements:
                - Company/brand logos
                - App icons
                - Product logos
                - Watermarks
                - Brand symbols and marks
                - Certification badges
                - Partner/sponsor logos
                
                Logos typically have distinct branding characteristics and may include text.
                """,
            _ => """
                Identify ALL distinct visual elements:
                - Photographs and images
                - Icons and small graphics
                - Logos and brand marks
                - Illustrations and drawings
                - Charts, graphs, and diagrams
                - Screenshots within screenshots
                - Avatar/profile images
                - Background images with distinct boundaries
                - Decorative graphics
                - Product images
                """
        };

        return $"""
            Analyze this UI screenshot/image ({imageWidth}x{imageHeight} pixels).

            {typeSpecificInstructions}

            For EACH distinct visual element found, provide precise bounding box coordinates.

            Return a JSON array with this EXACT structure (no other text):
            """ + """
            [
              {
                "description": "Clear description of what this image shows",
                "imageType": "photo|icon|logo|illustration|chart|avatar|screenshot|background|graphic",
                "boundingBox": {
                  "normalizedX": 0.15,
                  "normalizedY": 0.20,
                  "normalizedWidth": 0.25,
                  "normalizedHeight": 0.30
                },
                "confidence": 85,
                "suggestedFilename": "descriptive-name.png"
              }
            ]

            COORDINATE GUIDE:
            - normalizedX: 0.0 = left edge, 1.0 = right edge (this is the LEFT side of the box)
            - normalizedY: 0.0 = top edge, 1.0 = bottom edge (this is the TOP of the box)
            - normalizedWidth: width as fraction of image width
            - normalizedHeight: height as fraction of image height
            
            Example: An element in the top-left quadrant might have normalizedX=0.05, normalizedY=0.05
            Example: An element centered might have normalizedX=0.35, normalizedY=0.35 (if it takes up ~30% of dimensions)

            Be thorough - identify every distinct visual element. Return [] if none found.
            """;
    }

    private List<ImageRegion> ParseRegionsFromResponse(string response, int imageWidth, int imageHeight)
    {
        var regions = new List<ImageRegion>();
        
        if (string.IsNullOrWhiteSpace(response))
        {
            _logger?.LogWarning("Empty response from vision API");
            return regions;
        }

        try
        {
            // Clean the response - remove markdown code blocks and extra whitespace
            var cleanedResponse = response
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // Try to find JSON array in the response
            var jsonMatch = Regex.Match(cleanedResponse, @"\[[\s\S]*\]", RegexOptions.Multiline);
            if (!jsonMatch.Success)
            {
                // Try to find a single object and wrap it
                var objectMatch = Regex.Match(cleanedResponse, @"\{[\s\S]*\}", RegexOptions.Multiline);
                if (objectMatch.Success)
                {
                    cleanedResponse = "[" + objectMatch.Value + "]";
                }
                else
                {
                    _logger?.LogWarning("No JSON found in response: {Response}", 
                        response.Length > 200 ? response[..200] + "..." : response);
                    return regions;
                }
            }
            else
            {
                cleanedResponse = jsonMatch.Value;
            }

            _logger?.LogDebug("Parsing JSON: {Json}", cleanedResponse.Length > 500 ? cleanedResponse[..500] + "..." : cleanedResponse);

            using var doc = JsonDocument.Parse(cleanedResponse, new JsonDocumentOptions 
            { 
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                try
                {
                    var region = ParseRegionElement(element, imageWidth, imageHeight);
                    if (region != null && IsValidRegion(region, imageWidth, imageHeight))
                    {
                        regions.Add(region);
                        _logger?.LogDebug("Added region: {Desc} at ({X},{Y}) size ({W}x{H})", 
                            region.Description, region.BoundingBox?.X, region.BoundingBox?.Y,
                            region.BoundingBox?.Width, region.BoundingBox?.Height);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse region element");
                }
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "JSON parsing failed for response: {Response}", 
                response.Length > 500 ? response[..500] + "..." : response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse regions");
        }

        return regions;
    }

    private ImageRegion? ParseRegionElement(JsonElement element, int imageWidth, int imageHeight)
    {
        var region = new ImageRegion
        {
            Description = GetStringProperty(element, "description", "Extracted image"),
            ImageType = GetStringProperty(element, "imageType", "image"),
            Confidence = GetIntProperty(element, "confidence", 70),
            SuggestedFilename = GetStringProperty(element, "suggestedFilename", "extracted-image.png")
        };

        if (!element.TryGetProperty("boundingBox", out var bbox))
        {
            _logger?.LogWarning("No boundingBox property found");
            return null;
        }

        var normalizedX = GetDoubleProperty(bbox, "normalizedX", 0);
        var normalizedY = GetDoubleProperty(bbox, "normalizedY", 0);
        var normalizedWidth = GetDoubleProperty(bbox, "normalizedWidth", 0);
        var normalizedHeight = GetDoubleProperty(bbox, "normalizedHeight", 0);

        // Validate normalized values
        if (normalizedWidth <= 0 || normalizedHeight <= 0)
        {
            _logger?.LogWarning("Invalid normalized dimensions: {W}x{H}", normalizedWidth, normalizedHeight);
            return null;
        }

        // Clamp normalized values to valid range
        normalizedX = Math.Max(0, Math.Min(normalizedX, 1));
        normalizedY = Math.Max(0, Math.Min(normalizedY, 1));
        normalizedWidth = Math.Max(0.01, Math.Min(normalizedWidth, 1 - normalizedX));
        normalizedHeight = Math.Max(0.01, Math.Min(normalizedHeight, 1 - normalizedY));

        region.BoundingBox = new BoundingBox
        {
            NormalizedX = normalizedX,
            NormalizedY = normalizedY,
            NormalizedWidth = normalizedWidth,
            NormalizedHeight = normalizedHeight,
            X = (int)Math.Round(normalizedX * imageWidth),
            Y = (int)Math.Round(normalizedY * imageHeight),
            Width = (int)Math.Round(normalizedWidth * imageWidth),
            Height = (int)Math.Round(normalizedHeight * imageHeight)
        };

        return region;
    }

    private static bool IsValidRegion(ImageRegion region, int imageWidth, int imageHeight)
    {
        if (region.BoundingBox == null) return false;
        
        var box = region.BoundingBox;
        
        // Check minimum size (at least 4x4 pixels)
        if (box.Width < 4 || box.Height < 4) return false;
        
        // Check that region is within image bounds
        if (box.X < 0 || box.Y < 0) return false;
        if (box.X + box.Width > imageWidth + 10) return false; // Small tolerance
        if (box.Y + box.Height > imageHeight + 10) return false;
        
        return true;
    }

    private static string GetStringProperty(JsonElement element, string propertyName, string defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? defaultValue;
        }
        return defaultValue;
    }

    private static int GetIntProperty(JsonElement element, string propertyName, int defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var val))
            {
                return val;
            }
        }
        return defaultValue;
    }

    private static double GetDoubleProperty(JsonElement element, string propertyName, double defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetDouble();
            }
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var val))
            {
                return val;
            }
        }
        return defaultValue;
    }

    #endregion
}
