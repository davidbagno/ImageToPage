using System.Text.Json;
using System.Text.RegularExpressions;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace AzureLogin.Web.Services;

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


    private bool IsTextOnlyRegion(DetectedRegion region, List<TextRegion>? textRegions)
    {
        if (textRegions == null || textRegions.Count == 0) return false;
        int textOverlapArea = 0;
        int regionArea = region.Width * region.Height;

        foreach (var textRegion in textRegions)
        {
            var overlapX = Math.Max(0, Math.Min(region.X + region.Width, textRegion.X + textRegion.Width) - Math.Max(region.X, textRegion.X));
            var overlapY = Math.Max(0, Math.Min(region.Y + region.Height, textRegion.Y + textRegion.Height) - Math.Max(region.Y, textRegion.Y));
            textOverlapArea += overlapX * overlapY;
        }
        return regionArea > 0 && (double)textOverlapArea / regionArea > 0.8;
    }

    private string ClassifyAzureVisionRegion(DetectedRegion region, double aspectRatio)
    {
        var caption = region.Caption?.ToLowerInvariant() ?? "";
        var tags = region.Tags?.Select(t => t.ToLowerInvariant()).ToList() ?? new List<string>();

        if (tags.Any(t => t.Contains("icon") || t.Contains("symbol"))) return "icon";
        if (tags.Any(t => t.Contains("logo") || t.Contains("brand"))) return "logo";
        if (tags.Any(t => t.Contains("chart") || t.Contains("graph"))) return "chart";
        if (tags.Any(t => t.Contains("photo") || t.Contains("photograph"))) return "photo";
        if (tags.Any(t => t.Contains("button"))) return "button";

        if (caption.Contains("icon")) return "icon";
        if (caption.Contains("logo")) return "logo";
        if (caption.Contains("chart") || caption.Contains("graph")) return "chart";
        if (caption.Contains("button")) return "button";
        if (caption.Contains("card")) return "card";

        if (region.Width <= 64 && region.Height <= 64) return "icon";
        if (region.Width <= 120 && region.Height <= 120 && Math.Abs(aspectRatio - 1) < 0.3) return "avatar";
        if (aspectRatio > 3) return "banner";
        if (aspectRatio < 0.33) return "vertical";
        if (region.Width > 150 && region.Height > 100) return "card";

        return "image";
    }

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

    private async Task<ImageExtractionResult> ExtractWithEdgeRefinementAsync(byte[] imageBytes, string mimeType, ExtractionOptions options)
    {
        try
        {
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
                        X = x, Y = y, Width = width, Height = height,
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

            var horizontalDividers = FindHorizontalDividers(bitmap);
            var extractedImages = new List<ExtractedImage>();
            var sectionBounds = GetSectionBoundsFromDividers(horizontalDividers, bitmap.Width, bitmap.Height);

            int sectionIndex = 0;
            foreach (var bounds in sectionBounds)
            {
                if (bounds.Height < 20) continue;

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

            if (extractedImages.Count == 0)
            {
                var fullBounds = new BoundingBox
                {
                    X = 0, Y = 0, Width = bitmap.Width, Height = bitmap.Height,
                    NormalizedX = 0, NormalizedY = 0, NormalizedWidth = 1, NormalizedHeight = 1
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

            var edgeMap = DetectEdges(bitmap);
            var components = FindConnectedComponents(edgeMap, bitmap.Width, bitmap.Height, minSize);
            var extractedImages = new List<ExtractedImage>();

            int componentIndex = 0;
            foreach (var bounds in components)
            {
                var paddedBounds = new BoundingBox
                {
                    X = Math.Max(0, bounds.X - 2),
                    Y = Math.Max(0, bounds.Y - 2),
                    Width = Math.Min(bounds.Width + 4, bitmap.Width - Math.Max(0, bounds.X - 2)),
                    Height = Math.Min(bounds.Height + 4, bitmap.Height - Math.Max(0, bounds.Y - 2)),
                    NormalizedX = (double)Math.Max(0, bounds.X - 2) / bitmap.Width,
                    NormalizedY = (double)Math.Max(0, bounds.Y - 2) / bitmap.Height,
                    NormalizedWidth = (double)Math.Min(bounds.Width + 4, bitmap.Width - Math.Max(0, bounds.X - 2)) / bitmap.Width,
                    NormalizedHeight = (double)Math.Min(bounds.Height + 4, bitmap.Height - Math.Max(0, bounds.Y - 2)) / bitmap.Height
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

            var searchPadding = 10;
            var searchX = Math.Max(0, x - searchPadding);
            var searchY = Math.Max(0, y - searchPadding);
            var searchRight = Math.Min(bitmap.Width, x + width + searchPadding);
            var searchBottom = Math.Min(bitmap.Height, y + height + searchPadding);

            var refinedLeft = FindEdge(bitmap, searchX, x + width / 3, searchY, searchBottom, true, true, threshold);
            var refinedRight = FindEdge(bitmap, x + width * 2 / 3, searchRight, searchY, searchBottom, true, false, threshold);
            var refinedTop = FindEdge(bitmap, searchX, searchRight, searchY, y + height / 3, false, true, threshold);
            var refinedBottom = FindEdge(bitmap, searchX, searchRight, y + height * 2 / 3, searchBottom, false, false, threshold);

            var newX = refinedLeft ?? x;
            var newY = refinedTop ?? y;
            var newRight = refinedRight ?? (x + width);
            var newBottom = refinedBottom ?? (y + height);

            var newWidth = Math.Max(4, newRight - newX);
            var newHeight = Math.Max(4, newBottom - newY);

            var refinedBounds = new BoundingBox
            {
                X = newX, Y = newY, Width = newWidth, Height = newHeight,
                NormalizedX = (double)newX / bitmap.Width,
                NormalizedY = (double)newY / bitmap.Height,
                NormalizedWidth = (double)newWidth / bitmap.Width,
                NormalizedHeight = (double)newHeight / bitmap.Height
            };

            return Task.FromResult(refinedBounds);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to refine bounds, using original");
            return Task.FromResult(approximateBounds);
        }
    }

    public Task<ImageExtractionResult> ExtractByContourDetectionAsync(byte[] imageBytes, string mimeType, int minSize = 20, int colorThreshold = 25)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
                return Task.FromResult(new ImageExtractionResult { Success = false, ErrorMessage = "Failed to decode source image" });

            var backgroundColor = DetectBackgroundColor(bitmap);
            var mask = CreateForegroundMask(bitmap, backgroundColor, colorThreshold);
            var regions = FindRectangularRegions(mask, bitmap.Width, bitmap.Height, minSize);
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
                        ImageData = cropResult.ImageData, Base64Data = cropResult.Base64Data,
                        Description = $"{componentType} ({bounds.Width}×{bounds.Height})",
                        ImageType = componentType, BoundingBox = bounds,
                        SuggestedFilename = $"region-{regionIndex + 1}-{componentType}.png",
                        Confidence = 95, Width = cropResult.Width, Height = cropResult.Height
                    });
                    regionIndex++;
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true, Images = extractedImages, TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Detected {extractedImages.Count} regions via pixel-perfect contour detection"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ImageExtractionResult { Success = false, ErrorMessage = $"Contour detection failed: {ex.Message}" });
        }
    }

    public Task<ImageExtractionResult> ExtractByFloodFillAsync(byte[] imageBytes, string mimeType, int minSize = 30, int colorTolerance = 15)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
                return Task.FromResult(new ImageExtractionResult { Success = false, ErrorMessage = "Failed to decode source image" });

            var width = bitmap.Width;
            var height = bitmap.Height;
            var visited = new bool[width, height];
            var regions = new List<BoundingBox>();
            var backgroundColor = DetectBackgroundColor(bitmap);

            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    if (visited[x, y]) continue;
                    var pixelColor = bitmap.GetPixel(x, y);
                    if (ColorSimilar(pixelColor, backgroundColor, colorTolerance)) { visited[x, y] = true; continue; }

                    var regionBounds = FloodFillColorRegion(bitmap, visited, x, y, pixelColor, colorTolerance, width, height);
                    if (regionBounds.Width >= minSize && regionBounds.Height >= minSize)
                    {
                        bool isUnique = true;
                        foreach (var existing in regions.ToList())
                        {
                            if (RegionsOverlapSignificantly(regionBounds, existing, 0.5))
                            {
                                isUnique = false;
                                if (regionBounds.Width * regionBounds.Height > existing.Width * existing.Height)
                                { regions.Remove(existing); regions.Add(regionBounds); }
                                break;
                            }
                        }
                        if (isUnique) regions.Add(regionBounds);
                    }
                }
            }

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
                        ImageData = cropResult.ImageData, Base64Data = cropResult.Base64Data,
                        Description = $"{componentType} ({bounds.Width}×{bounds.Height})",
                        ImageType = componentType, BoundingBox = bounds,
                        SuggestedFilename = $"region-{regionIndex + 1}-{componentType}.png",
                        Confidence = 90, Width = cropResult.Width, Height = cropResult.Height
                    });
                    regionIndex++;
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true, Images = extractedImages, TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Detected {extractedImages.Count} regions via flood-fill analysis"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ImageExtractionResult { Success = false, ErrorMessage = $"Flood-fill detection failed: {ex.Message}" });
        }
    }

    public Task<ImageExtractionResult> ExtractUICardsAsync(byte[] imageBytes, string mimeType, int minSize = 40)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap == null)
                return Task.FromResult(new ImageExtractionResult { Success = false, ErrorMessage = "Failed to decode source image" });

            var width = bitmap.Width;
            var height = bitmap.Height;
            var backgroundColor = DetectBackgroundColor(bitmap);
            var cards = FindSolidColorCards(bitmap, backgroundColor, minSize);

            var extractedImages = new List<ExtractedImage>();
            int cardIndex = 0;

            foreach (var bounds in cards)
            {
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
                    var cardType = bounds.Width <= 80 && bounds.Height <= 80 ? "widget-small" :
                                   aspectRatio > 2.5 ? "banner-card" :
                                   aspectRatio < 0.5 ? "vertical-card" :
                                   Math.Abs(aspectRatio - 1) < 0.3 ? "square-card" :
                                   bounds.Width > 150 && bounds.Height > 100 ? "dashboard-card" : "card";

                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData, Base64Data = cropResult.Base64Data,
                        Description = $"{cardType} card ({bounds.Width}×{bounds.Height})",
                        ImageType = cardType, BoundingBox = paddedBounds,
                        SuggestedFilename = $"card-{cardIndex + 1}-{cardType}.png",
                        Confidence = 92, Width = cropResult.Width, Height = cropResult.Height
                    });
                    cardIndex++;
                }
            }

            return Task.FromResult(new ImageExtractionResult
            {
                Success = true, Images = extractedImages, TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Detected {extractedImages.Count} UI cards/widgets"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ImageExtractionResult { Success = false, ErrorMessage = $"UI card detection failed: {ex.Message}" });
        }
    }

    private List<BoundingBox> FindSolidColorCards(SKBitmap bitmap, SKColor backgroundColor, int minSize)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var regions = new List<BoundingBox>();
        var visited = new bool[width, height];

        for (int y = 5; y < height - 5; y += 10)
        {
            for (int x = 5; x < width - 5; x += 10)
            {
                if (visited[x, y]) continue;
                var pixel = bitmap.GetPixel(x, y);
                if (ColorSimilar(pixel, backgroundColor, 15)) continue;

                // Check if solid color area
                bool isSolid = true;
                for (int dy = 0; dy < 20 && y + dy < height && isSolid; dy++)
                    for (int dx = 0; dx < 20 && x + dx < width && isSolid; dx++)
                        if (!ColorSimilar(bitmap.GetPixel(x + dx, y + dy), pixel, 20)) isSolid = false;

                if (isSolid)
                {
                    int left = x, right = x, top = y, bottom = y;
                    for (int tx = x; tx >= 0 && ColorSimilar(bitmap.GetPixel(tx, y), pixel, 25); tx--) left = tx;
                    for (int tx = x; tx < width && ColorSimilar(bitmap.GetPixel(tx, y), pixel, 25); tx++) right = tx;
                    for (int ty = y; ty >= 0 && ColorSimilar(bitmap.GetPixel(x, ty), pixel, 25); ty--) top = ty;
                    for (int ty = y; ty < height && ColorSimilar(bitmap.GetPixel(x, ty), pixel, 25); ty++) bottom = ty;

                    var bounds = new BoundingBox
                    {
                        X = left, Y = top, Width = right - left + 1, Height = bottom - top + 1,
                        NormalizedX = (double)left / width, NormalizedY = (double)top / height,
                        NormalizedWidth = (double)(right - left + 1) / width, NormalizedHeight = (double)(bottom - top + 1) / height
                    };

                    if (bounds.Width >= minSize && bounds.Height >= minSize)
                    {
                        for (int vy = bounds.Y; vy < bounds.Y + bounds.Height && vy < height; vy++)
                            for (int vx = bounds.X; vx < bounds.X + bounds.Width && vx < width; vx++)
                                visited[vx, vy] = true;

                        if (!regions.Any(r => RegionsOverlapSignificantly(bounds, r, 0.7)))
                            regions.Add(bounds);
                    }
                }
            }
        }

        return MergeOverlappingRegionsList(regions).OrderByDescending(c => c.Width * c.Height).ToList();
    }

    #region Pixel Analysis Helpers

    private SKColor DetectBackgroundColor(SKBitmap bitmap)
    {
        var samples = new List<SKColor>();
        var w = bitmap.Width; var h = bitmap.Height;
        samples.Add(bitmap.GetPixel(0, 0)); samples.Add(bitmap.GetPixel(w - 1, 0));
        samples.Add(bitmap.GetPixel(0, h - 1)); samples.Add(bitmap.GetPixel(w - 1, h - 1));
        for (int i = 0; i < w; i += Math.Max(1, w / 20)) { samples.Add(bitmap.GetPixel(i, 0)); samples.Add(bitmap.GetPixel(i, h - 1)); }
        for (int i = 0; i < h; i += Math.Max(1, h / 20)) { samples.Add(bitmap.GetPixel(0, i)); samples.Add(bitmap.GetPixel(w - 1, i)); }

        var colorGroups = new Dictionary<(int r, int g, int b), int>();
        foreach (var color in samples)
        {
            var key = (color.Red / 10 * 10, color.Green / 10 * 10, color.Blue / 10 * 10);
            colorGroups[key] = colorGroups.GetValueOrDefault(key, 0) + 1;
        }
        var mostCommon = colorGroups.OrderByDescending(kv => kv.Value).First().Key;
        return new SKColor((byte)mostCommon.r, (byte)mostCommon.g, (byte)mostCommon.b);
    }

    private bool[,] CreateForegroundMask(SKBitmap bitmap, SKColor backgroundColor, int threshold)
    {
        var width = bitmap.Width; var height = bitmap.Height;
        var mask = new bool[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                mask[x, y] = !ColorSimilar(bitmap.GetPixel(x, y), backgroundColor, threshold);
        return mask;
    }

    private bool ColorSimilar(SKColor a, SKColor b, int tolerance) =>
        Math.Abs(a.Red - b.Red) <= tolerance && Math.Abs(a.Green - b.Green) <= tolerance && Math.Abs(a.Blue - b.Blue) <= tolerance;

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
                    var bounds = FloodFillBoundsFromMask(mask, visited, x, y, width, height);
                    if (bounds.Width >= minSize && bounds.Height >= minSize)
                    {
                        var tightBounds = TightenBounds(mask, bounds);
                        if (tightBounds.Width >= minSize && tightBounds.Height >= minSize)
                            regions.Add(tightBounds);
                    }
                }
            }
        }
        return MergeOverlappingRegionsList(regions);
    }

    private BoundingBox FloodFillBoundsFromMask(bool[,] mask, bool[,] visited, int startX, int startY, int width, int height)
    {
        var minX = startX; var maxX = startX; var minY = startY; var maxY = startY;
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (visited[x, y] || !mask[x, y]) continue;
            visited[x, y] = true;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            queue.Enqueue((x - 1, y)); queue.Enqueue((x + 1, y));
            queue.Enqueue((x, y - 1)); queue.Enqueue((x, y + 1));
        }
        return new BoundingBox { X = minX, Y = minY, Width = maxX - minX + 1, Height = maxY - minY + 1,
            NormalizedX = (double)minX / width, NormalizedY = (double)minY / height,
            NormalizedWidth = (double)(maxX - minX + 1) / width, NormalizedHeight = (double)(maxY - minY + 1) / height };
    }

    private BoundingBox TightenBounds(bool[,] mask, BoundingBox bounds)
    {
        int left = bounds.X + bounds.Width, right = bounds.X, top = bounds.Y + bounds.Height, bottom = bounds.Y;
        for (int y = bounds.Y; y < bounds.Y + bounds.Height && y < mask.GetLength(1); y++)
            for (int x = bounds.X; x < bounds.X + bounds.Width && x < mask.GetLength(0); x++)
                if (mask[x, y]) { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        if (left > right || top > bottom) return bounds;
        return new BoundingBox { X = left, Y = top, Width = right - left + 1, Height = bottom - top + 1,
            NormalizedX = bounds.NormalizedX, NormalizedY = bounds.NormalizedY, NormalizedWidth = bounds.NormalizedWidth, NormalizedHeight = bounds.NormalizedHeight };
    }

    private BoundingBox FloodFillColorRegion(SKBitmap bitmap, bool[,] visited, int startX, int startY, SKColor targetColor, int tolerance, int width, int height)
    {
        var minX = startX; var maxX = startX; var minY = startY; var maxY = startY;
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        int pixelCount = 0, maxPixels = 50000;
        while (queue.Count > 0 && pixelCount < maxPixels)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (visited[x, y]) continue;
            var pixel = bitmap.GetPixel(x, y);
            if (!ColorSimilar(pixel, targetColor, tolerance + 20)) continue;
            visited[x, y] = true; pixelCount++;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            queue.Enqueue((x - 1, y)); queue.Enqueue((x + 1, y));
            queue.Enqueue((x, y - 1)); queue.Enqueue((x, y + 1));
        }
        return new BoundingBox { X = minX, Y = minY, Width = maxX - minX + 1, Height = maxY - minY + 1,
            NormalizedX = (double)minX / width, NormalizedY = (double)minY / height,
            NormalizedWidth = (double)(maxX - minX + 1) / width, NormalizedHeight = (double)(maxY - minY + 1) / height };
    }

    private bool RegionsOverlapSignificantly(BoundingBox a, BoundingBox b, double threshold)
    {
        int overlapX = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X));
        int overlapY = Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        int overlapArea = overlapX * overlapY;
        int smallerArea = Math.Min(a.Width * a.Height, b.Width * b.Height);
        return overlapArea > smallerArea * threshold;
    }

    private List<BoundingBox> MergeOverlappingRegionsList(List<BoundingBox> regions)
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
                    { current = MergeRegionsBounds(current, regions[j]); used[j] = true; didMerge = true; }
                }
            } while (didMerge);
            merged.Add(current); used[i] = true;
        }
        return merged;
    }

    private bool RegionsAdjacent(BoundingBox a, BoundingBox b, int margin)
    {
        bool hAdjacent = (a.X + a.Width + margin >= b.X && a.X <= b.X + b.Width + margin);
        bool vOverlap = (a.Y < b.Y + b.Height && a.Y + a.Height > b.Y);
        bool vAdjacent = (a.Y + a.Height + margin >= b.Y && a.Y <= b.Y + b.Height + margin);
        bool hOverlap = (a.X < b.X + b.Width && a.X + a.Width > b.X);
        return (hAdjacent && vOverlap) || (vAdjacent && hOverlap);
    }

    private BoundingBox MergeRegionsBounds(BoundingBox a, BoundingBox b)
    {
        var minX = Math.Min(a.X, b.X); var minY = Math.Min(a.Y, b.Y);
        var maxX = Math.Max(a.X + a.Width, b.X + b.Width); var maxY = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new BoundingBox { X = minX, Y = minY, Width = maxX - minX, Height = maxY - minY,
            NormalizedX = a.NormalizedX, NormalizedY = a.NormalizedY, NormalizedWidth = a.NormalizedWidth, NormalizedHeight = a.NormalizedHeight };
    }

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

    #endregion

    #region Edge Detection Helpers

    private bool[,] DetectEdges(SKBitmap bitmap, int threshold = 30)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var edges = new bool[width, height];

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                var tl = GetGrayscale(bitmap.GetPixel(x - 1, y - 1));
                var t = GetGrayscale(bitmap.GetPixel(x, y - 1));
                var tr = GetGrayscale(bitmap.GetPixel(x + 1, y - 1));
                var l = GetGrayscale(bitmap.GetPixel(x - 1, y));
                var r = GetGrayscale(bitmap.GetPixel(x + 1, y));
                var bl = GetGrayscale(bitmap.GetPixel(x - 1, y + 1));
                var b = GetGrayscale(bitmap.GetPixel(x, y + 1));
                var br = GetGrayscale(bitmap.GetPixel(x + 1, y + 1));

                var gx = (tr + 2 * r + br) - (tl + 2 * l + bl);
                var gy = (bl + 2 * b + br) - (tl + 2 * t + tr);

                var magnitude = Math.Sqrt(gx * gx + gy * gy);
                edges[x, y] = magnitude > threshold;
            }
        }

        return edges;
    }

    private static int GetGrayscale(SKColor color) => (color.Red + color.Green + color.Blue) / 3;

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

        return MergeOverlappingBounds(components);
    }

    private BoundingBox FloodFillBounds(bool[,] edgeMap, bool[,] visited, int startX, int startY, int width, int height)
    {
        var minX = startX; var maxX = startX; var minY = startY; var maxY = startY;
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (visited[x, y] || !edgeMap[x, y]) continue;

            visited[x, y] = true;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);

            stack.Push((x - 1, y)); stack.Push((x + 1, y));
            stack.Push((x, y - 1)); stack.Push((x, y + 1));
            stack.Push((x - 1, y - 1)); stack.Push((x + 1, y - 1));
            stack.Push((x - 1, y + 1)); stack.Push((x + 1, y + 1));
        }

        return new BoundingBox
        {
            X = minX, Y = minY, Width = maxX - minX + 1, Height = maxY - minY + 1,
            NormalizedX = (double)minX / width, NormalizedY = (double)minY / height,
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

    private static bool BoundsOverlapOrClose(BoundingBox a, BoundingBox b, int margin) =>
        !(a.X + a.Width + margin < b.X || b.X + b.Width + margin < a.X ||
          a.Y + a.Height + margin < b.Y || b.Y + b.Height + margin < a.Y);

    private static BoundingBox MergeBounds(BoundingBox a, BoundingBox b)
    {
        var minX = Math.Min(a.X, b.X); var minY = Math.Min(a.Y, b.Y);
        var maxX = Math.Max(a.X + a.Width, b.X + b.Width);
        var maxY = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new BoundingBox
        {
            X = minX, Y = minY, Width = maxX - minX, Height = maxY - minY,
            NormalizedX = a.NormalizedX, NormalizedY = a.NormalizedY,
            NormalizedWidth = a.NormalizedWidth, NormalizedHeight = a.NormalizedHeight
        };
    }

    private int? FindEdge(SKBitmap bitmap, int startX, int endX, int startY, int endY, bool horizontal, bool findFirst, int threshold)
    {
        if (horizontal)
        {
            var range = findFirst ? Enumerable.Range(startX, endX - startX) : Enumerable.Range(startX, endX - startX).Reverse();
            foreach (var x in range)
            {
                var edgeStrength = 0;
                for (int y = startY; y < endY; y++)
                {
                    if (x > 0 && x < bitmap.Width - 1)
                    {
                        var left = GetGrayscale(bitmap.GetPixel(x - 1, y));
                        var right = GetGrayscale(bitmap.GetPixel(x + 1, y));
                        if (Math.Abs(left - right) > threshold) edgeStrength++;
                    }
                }
                if (edgeStrength > (endY - startY) / 4) return x;
            }
        }
        else
        {
            var range = findFirst ? Enumerable.Range(startY, endY - startY) : Enumerable.Range(startY, endY - startY).Reverse();
            foreach (var y in range)
            {
                var edgeStrength = 0;
                for (int x = startX; x < endX; x++)
                {
                    if (y > 0 && y < bitmap.Height - 1)
                    {
                        var top = GetGrayscale(bitmap.GetPixel(x, y - 1));
                        var bottom = GetGrayscale(bitmap.GetPixel(x, y + 1));
                        if (Math.Abs(top - bottom) > threshold) edgeStrength++;
                    }
                }
                if (edgeStrength > (endX - startX) / 4) return y;
            }
        }
        return null;
    }

    private List<int> FindHorizontalDividers(SKBitmap bitmap)
    {
        var dividers = new List<int>();
        var prevRowColor = SKColor.Empty;
        var uniformRowStart = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            var colors = new List<SKColor>();
            for (int x = 0; x < bitmap.Width; x += Math.Max(1, bitmap.Width / 20))
                colors.Add(bitmap.GetPixel(x, y));

            var isUniform = colors.All(c => ColorDistance(c, colors[0]) < 20);
            var avgColor = colors[0];

            if (isUniform)
            {
                if (uniformRowStart < 0) { uniformRowStart = y; prevRowColor = avgColor; }
                else if (ColorDistance(avgColor, prevRowColor) > 30)
                {
                    if (y - uniformRowStart > 2) dividers.Add((uniformRowStart + y) / 2);
                    uniformRowStart = y; prevRowColor = avgColor;
                }
            }
            else
            {
                if (uniformRowStart >= 0 && y - uniformRowStart > 5) dividers.Add((uniformRowStart + y) / 2);
                uniformRowStart = -1;
            }
        }
        return dividers;
    }

    private static double ColorDistance(SKColor a, SKColor b)
    {
        var dr = a.Red - b.Red; var dg = a.Green - b.Green; var db = a.Blue - b.Blue;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private List<BoundingBox> GetSectionBoundsFromDividers(List<int> dividers, int width, int height)
    {
        var bounds = new List<BoundingBox>();
        var sortedDividers = dividers.OrderBy(d => d).ToList();
        if (sortedDividers.Count == 0 || sortedDividers[0] > 20) sortedDividers.Insert(0, 0);
        if (sortedDividers.Last() < height - 20) sortedDividers.Add(height);

        for (int i = 0; i < sortedDividers.Count - 1; i++)
        {
            var startY = sortedDividers[i];
            var endY = sortedDividers[i + 1];
            var sectionHeight = endY - startY;
            if (sectionHeight > 20)
            {
                bounds.Add(new BoundingBox
                {
                    X = 0, Y = startY, Width = width, Height = sectionHeight,
                    NormalizedX = 0, NormalizedY = (double)startY / height,
                    NormalizedWidth = 1, NormalizedHeight = (double)sectionHeight / height
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
                return new ImageExtractionResult { Success = false, ErrorMessage = "Failed to decode source image" };
            }

            var base64Image = Convert.ToBase64String(imageBytes);
            var prompt = BuildExtractionPrompt(bitmap.Width, bitmap.Height, extractionType);
            var systemPrompt = GetSystemPrompt();

            var response = await _azureOpenAIService.GetVisionCompletionAsync(base64Image, mimeType, prompt, systemPrompt);
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
                        ImageData = cropResult.ImageData, Base64Data = cropResult.Base64Data,
                        Description = region.Description, ImageType = region.ImageType ?? extractionType,
                        BoundingBox = region.BoundingBox, SuggestedFilename = region.SuggestedFilename,
                        Confidence = region.Confidence, Width = cropResult.Width, Height = cropResult.Height
                    });
                }
            }

            var typeLabel = extractionType == "icons" ? "icons" : "logos/brand elements";
            return new ImageExtractionResult
            {
                Success = true, Images = extractedImages, TotalImagesFound = extractedImages.Count,
                AnalysisSummary = $"Extracted {extractedImages.Count} {typeLabel}"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "{Type} extraction failed", extractionType);
            return new ImageExtractionResult { Success = false, ErrorMessage = $"{extractionType} extraction failed: {ex.Message}" };
        }
    }

    #region Private Helpers

    private static string GetSystemPrompt() => """
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

    private static string BuildExtractionPrompt(int imageWidth, int imageHeight, string extractionType)
    {
        var typeSpecificInstructions = extractionType switch
        {
            "icons" => "Focus specifically on ICONS and small graphics: navigation icons, action icons, status indicators, social media icons, UI control icons, emoji and symbolic graphics, small decorative elements. Icons are typically small (under 100x100 pixels) and symbolic in nature.",
            "logos" => "Focus specifically on LOGOS and branding elements: company/brand logos, app icons, product logos, watermarks, brand symbols and marks, certification badges, partner/sponsor logos. Logos typically have distinct branding characteristics and may include text.",
            _ => "Identify ALL distinct visual elements: photographs, icons, logos, illustrations, charts, diagrams, screenshots, avatar/profile images, background images with distinct boundaries, decorative graphics, product images."
        };

        return $$"""
            Analyze this UI screenshot/image ({{imageWidth}}x{{imageHeight}} pixels).

            {{typeSpecificInstructions}}

            For EACH distinct visual element found, provide precise bounding box coordinates.

            Return a JSON array with this EXACT structure (no other text):
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

            Be thorough - identify every distinct visual element. Return [] if none found.
            """;
    }

    private List<ImageRegion> ParseRegionsFromResponse(string response, int imageWidth, int imageHeight)
    {
        var regions = new List<ImageRegion>();
        if (string.IsNullOrWhiteSpace(response)) return regions;

        try
        {
            var cleanedResponse = response.Replace("```json", "").Replace("```", "").Trim();
            var jsonMatch = Regex.Match(cleanedResponse, @"\[[\s\S]*\]", RegexOptions.Multiline);
            if (!jsonMatch.Success)
            {
                var objectMatch = Regex.Match(cleanedResponse, @"\{[\s\S]*\}", RegexOptions.Multiline);
                if (objectMatch.Success) cleanedResponse = "[" + objectMatch.Value + "]";
                else return regions;
            }
            else cleanedResponse = jsonMatch.Value;

            using var doc = JsonDocument.Parse(cleanedResponse, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                try
                {
                    var region = ParseRegionElement(element, imageWidth, imageHeight);
                    if (region != null && IsValidRegion(region, imageWidth, imageHeight)) regions.Add(region);
                }
                catch { }
            }
        }
        catch { }

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

        if (!element.TryGetProperty("boundingBox", out var bbox)) return null;

        var normalizedX = GetDoubleProperty(bbox, "normalizedX", 0);
        var normalizedY = GetDoubleProperty(bbox, "normalizedY", 0);
        var normalizedWidth = GetDoubleProperty(bbox, "normalizedWidth", 0);
        var normalizedHeight = GetDoubleProperty(bbox, "normalizedHeight", 0);

        if (normalizedWidth <= 0 || normalizedHeight <= 0) return null;

        normalizedX = Math.Max(0, Math.Min(normalizedX, 1));
        normalizedY = Math.Max(0, Math.Min(normalizedY, 1));
        normalizedWidth = Math.Max(0.01, Math.Min(normalizedWidth, 1 - normalizedX));
        normalizedHeight = Math.Max(0.01, Math.Min(normalizedHeight, 1 - normalizedY));

        region.BoundingBox = new BoundingBox
        {
            NormalizedX = normalizedX, NormalizedY = normalizedY,
            NormalizedWidth = normalizedWidth, NormalizedHeight = normalizedHeight,
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
        if (box.Width < 4 || box.Height < 4) return false;
        if (box.X < 0 || box.Y < 0) return false;
        if (box.X + box.Width > imageWidth + 10) return false;
        if (box.Y + box.Height > imageHeight + 10) return false;
        return true;
    }

    private static string GetStringProperty(JsonElement element, string propertyName, string defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? defaultValue;
        return defaultValue;
    }

    private static int GetIntProperty(JsonElement element, string propertyName, int defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number) return prop.GetInt32();
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var val)) return val;
        }
        return defaultValue;
    }

    private static double GetDoubleProperty(JsonElement element, string propertyName, double defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number) return prop.GetDouble();
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var val)) return val;
        }
        return defaultValue;
    }

    #endregion

    #region Azure Vision Methods

    /// <summary>
    /// Extract images using Azure AI Vision 4.0 for pixel-accurate detection
    /// </summary>
    public async Task<ImageExtractionResult> ExtractWithAzureVisionAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            if (_azureVisionService == null || !_azureVisionService.IsConfigured)
            {
                return new ImageExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Azure Vision service not configured",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0
                };
            }

            var visionResult = await _azureVisionService.AnalyzeComprehensiveAsync(imageBytes);

            if (!visionResult.Success || visionResult.Regions == null || visionResult.Regions.Count == 0)
            {
                return new ImageExtractionResult
                {
                    Success = visionResult.Success,
                    ErrorMessage = visionResult.ErrorMessage ?? "No regions detected",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0
                };
            }

            var extractedImages = new List<ExtractedImage>();
            int regionIndex = 0;

            foreach (var region in visionResult.Regions)
            {
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

                var refinedBox = await RefineRegionBoundsAsync(imageBytes, boundingBox, 30);
                var cropResult = await CropRegionAsync(imageBytes, mimeType, refinedBox);

                if (cropResult.Success && cropResult.ImageData != null)
                {
                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = region.Caption ?? $"Region {regionIndex + 1}",
                        ImageType = $"{region.RegionType ?? "image"}",
                        BoundingBox = refinedBox,
                        SuggestedFilename = $"azure-vision-{regionIndex + 1}.png",
                        Confidence = (int)(region.Confidence * 100),
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
                AnalysisSummary = $"Azure Vision detected {extractedImages.Count} regions"
            };
        }
        catch (Exception ex)
        {
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
    /// Extract people/avatars using Azure Vision
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
                    ErrorMessage = "Azure Vision service not configured",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0
                };
            }

            var peopleResult = await _azureVisionService.DetectPeopleAsync(imageBytes);

            if (!peopleResult.Success || peopleResult.Regions == null || peopleResult.Regions.Count == 0)
            {
                return new ImageExtractionResult
                {
                    Success = peopleResult.Success,
                    ErrorMessage = peopleResult.ErrorMessage ?? "No people detected",
                    Images = new List<ExtractedImage>(),
                    TotalImagesFound = 0
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
                    Height = region.Height
                };

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
                AnalysisSummary = $"Extracted {extractedImages.Count} people"
            };
        }
        catch (Exception ex)
        {
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
    /// Hybrid pixel-perfect extraction combining multiple detection methods
    /// </summary>
    public async Task<ImageExtractionResult> ExtractHybridPixelPerfectAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            var allRegions = new List<(BoundingBox Box, string Description, string Type, double Confidence, string Source)>();

            // Try Azure Vision first
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
                }
            }

            // Add GPT-4o detected regions
            var gptResult = await IdentifyImageRegionsAsync(imageBytes, mimeType);
            if (gptResult.Success && gptResult.Regions != null)
            {
                foreach (var region in gptResult.Regions)
                {
                    if (region.BoundingBox != null && !IsBoxOverlapping(region.BoundingBox, allRegions.Select(r => r.Box).ToList()))
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

            // Refine all bounding boxes
            var extractedImages = new List<ExtractedImage>();
            int regionIndex = 0;

            foreach (var (box, desc, type, conf, source) in allRegions)
            {
                var refinedBox = await RefineRegionBoundsAsync(imageBytes, box, 25);
                var cropResult = await CropRegionAsync(imageBytes, mimeType, refinedBox);

                if (cropResult.Success && cropResult.ImageData != null)
                {
                    extractedImages.Add(new ExtractedImage
                    {
                        ImageData = cropResult.ImageData,
                        Base64Data = cropResult.Base64Data,
                        Description = $"{desc} [{source}]",
                        ImageType = type,
                        BoundingBox = refinedBox,
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
                AnalysisSummary = $"Hybrid extraction found {extractedImages.Count} regions"
            };
        }
        catch (Exception ex)
        {
            return new ImageExtractionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
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
                return true;
        }
        return false;
    }

    #endregion
}
