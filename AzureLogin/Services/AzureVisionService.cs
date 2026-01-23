using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureLogin.Services;

/// <summary>
/// Azure AI Vision 4.0 service implementation for pixel-accurate image analysis
/// Uses the official Azure.AI.Vision.ImageAnalysis SDK for best results
/// </summary>
public class AzureVisionService : AzureLogin.Shared.Services.IAzureVisionService
{
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly ILogger<AzureVisionService>? _logger;
    private ImageAnalysisClient? _client;

    public bool IsConfigured => !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_apiKey);

    public AzureVisionService(
        IOptions<AzureLogin.Shared.Services.AzureOpenAISettings> settings,
        ILogger<AzureVisionService>? logger = null)
    {
        _endpoint = settings.Value.VisionEndpoint;
        _apiKey = settings.Value.VisionKey;
        _logger = logger;
        
        if (IsConfigured)
        {
            try
            {
                _client = new ImageAnalysisClient(
                    new Uri(_endpoint),
                    new AzureKeyCredential(_apiKey));
                _logger?.LogInformation("Azure AI Vision SDK client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize Azure AI Vision SDK client");
            }
        }
        else
        {
            _logger?.LogWarning("Azure AI Vision not configured. Set VisionEndpoint and VisionKey in appsettings.json");
        }
    }

    /// <summary>
    /// Comprehensive analysis using all Vision 4.0 features for pixel-perfect extraction
    /// </summary>
    public async Task<AzureLogin.Shared.Services.AzureVisionResult> AnalyzeComprehensiveAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
        {
            return CreateNotConfiguredResult();
        }

        try
        {
            _logger?.LogInformation("Starting comprehensive Azure Vision analysis...");

            // Request all available features for maximum detection
            var features = VisualFeatures.Objects | 
                           VisualFeatures.DenseCaptions | 
                           VisualFeatures.Tags |
                           VisualFeatures.People |
                           VisualFeatures.SmartCrops;

            var options = new ImageAnalysisOptions
            {
                SmartCropsAspectRatios = new float[] { 1.0f, 0.5f, 2.0f, 0.75f, 1.33f, 1.78f },
                GenderNeutralCaption = true
            };

            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                features,
                options);

            return ProcessComprehensiveResult(result.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Comprehensive Azure Vision analysis failed");
            return new AzureLogin.Shared.Services.AzureVisionResult
            {
                Success = false,
                ErrorMessage = $"Analysis failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Analyzes an image using Azure Vision 4.0 with multiple features
    /// </summary>
    public async Task<AzureLogin.Shared.Services.AzureVisionResult> AnalyzeImageAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
        {
            return CreateNotConfiguredResult();
        }

        try
        {
            _logger?.LogInformation("Starting Azure Vision image analysis...");

            // Use SDK for better results
            var features = VisualFeatures.Objects | VisualFeatures.DenseCaptions;

            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                features);

            return ProcessAnalysisResult(result.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Azure Vision analysis failed");
            return new AzureLogin.Shared.Services.AzureVisionResult
            {
                Success = false,
                ErrorMessage = $"Azure Vision analysis failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets dense captions with pixel-accurate bounding boxes
    /// </summary>
    public async Task<AzureLogin.Shared.Services.AzureVisionResult> GetDenseCaptionsAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
        {
            return CreateNotConfiguredResult();
        }

        try
        {
            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                VisualFeatures.DenseCaptions);

            var analysisResult = new AzureLogin.Shared.Services.AzureVisionResult
            {
                Success = true,
                Regions = new List<AzureLogin.Shared.Services.DetectedRegion>(),
                ImageWidth = result.Value.Metadata.Width,
                ImageHeight = result.Value.Metadata.Height
            };

            if (result.Value.DenseCaptions?.Values != null)
            {
                foreach (var caption in result.Value.DenseCaptions.Values)
                {
                    analysisResult.Regions.Add(new AzureLogin.Shared.Services.DetectedRegion
                    {
                        X = caption.BoundingBox.X,
                        Y = caption.BoundingBox.Y,
                        Width = caption.BoundingBox.Width,
                        Height = caption.BoundingBox.Height,
                        Caption = caption.Text,
                        Confidence = caption.Confidence,
                        RegionType = "caption"
                    });
                }
            }

            _logger?.LogInformation("Dense captions found {Count} regions", analysisResult.Regions.Count);
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Dense captions analysis failed");
            return new AzureLogin.Shared.Services.AzureVisionResult
            {
                Success = false,
                ErrorMessage = $"Dense captions failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Detects objects with pixel-accurate bounding boxes
    /// </summary>
    public async Task<AzureLogin.Shared.Services.AzureVisionResult> DetectObjectsAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
        {
            return CreateNotConfiguredResult();
        }

        try
        {
            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                VisualFeatures.Objects);

            var analysisResult = new AzureLogin.Shared.Services.AzureVisionResult
            {
                Success = true,
                Regions = new List<AzureLogin.Shared.Services.DetectedRegion>(),
                ImageWidth = result.Value.Metadata.Width,
                ImageHeight = result.Value.Metadata.Height
            };

            if (result.Value.Objects?.Values != null)
            {
                foreach (var obj in result.Value.Objects.Values)
                {
                    var tags = obj.Tags?.Select(t => t.Name).ToList() ?? new List<string>();
                    analysisResult.Regions.Add(new AzureLogin.Shared.Services.DetectedRegion
                    {
                        X = obj.BoundingBox.X,
                        Y = obj.BoundingBox.Y,
                        Width = obj.BoundingBox.Width,
                        Height = obj.BoundingBox.Height,
                        Caption = tags.FirstOrDefault(),
                        Confidence = obj.Tags?.FirstOrDefault()?.Confidence ?? 0.5,
                        Tags = tags,
                        RegionType = "object"
                    });
                }
            }

            _logger?.LogInformation("Object detection found {Count} objects", analysisResult.Regions.Count);
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Object detection failed");
            return new AzureLogin.Shared.Services.AzureVisionResult
            {
                Success = false,
                ErrorMessage = $"Object detection failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Detects people with bounding boxes
    /// </summary>
    public async Task<AzureLogin.Shared.Services.AzureVisionResult> DetectPeopleAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
        {
            return CreateNotConfiguredResult();
        }

        try
        {
            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                VisualFeatures.People);

            var analysisResult = new AzureLogin.Shared.Services.AzureVisionResult
            {
                Success = true,
                Regions = new List<AzureLogin.Shared.Services.DetectedRegion>(),
                ImageWidth = result.Value.Metadata.Width,
                ImageHeight = result.Value.Metadata.Height
            };

            if (result.Value.People?.Values != null)
            {
                int personIndex = 1;
                foreach (var person in result.Value.People.Values)
                {
                    analysisResult.Regions.Add(new AzureLogin.Shared.Services.DetectedRegion
                    {
                        X = person.BoundingBox.X,
                        Y = person.BoundingBox.Y,
                        Width = person.BoundingBox.Width,
                        Height = person.BoundingBox.Height,
                        Caption = $"Person {personIndex++}",
                        Confidence = person.Confidence,
                        Tags = new List<string> { "person" },
                        RegionType = "person"
                    });
                }
            }

            _logger?.LogInformation("People detection found {Count} people", analysisResult.Regions.Count);
            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "People detection failed");
            return new AzureLogin.Shared.Services.AzureVisionResult
            {
                Success = false,
                ErrorMessage = $"People detection failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets smart crop suggestions for various aspect ratios
    /// </summary>
    public async Task<AzureLogin.Shared.Services.SmartCropResult> GetSmartCropsAsync(byte[] imageBytes, params double[] aspectRatios)
    {
        if (!IsConfigured || _client == null)
        {
            return new AzureLogin.Shared.Services.SmartCropResult
            {
                Success = false,
                ErrorMessage = "Azure Vision service not configured. Set VisionEndpoint and VisionKey in appsettings.json"
            };
        }

        try
        {
            var floatRatios = aspectRatios.Length > 0 
                ? aspectRatios.Select(r => (float)r).ToArray() 
                : new float[] { 1.0f, 0.75f, 1.33f, 1.78f };

            var options = new ImageAnalysisOptions
            {
                SmartCropsAspectRatios = floatRatios
            };

            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                VisualFeatures.SmartCrops,
                options);

            var cropResult = new AzureLogin.Shared.Services.SmartCropResult
            {
                Success = true,
                Crops = new List<AzureLogin.Shared.Services.SmartCropRegion>(),
                ImageWidth = result.Value.Metadata.Width,
                ImageHeight = result.Value.Metadata.Height
            };

            if (result.Value.SmartCrops?.Values != null)
            {
                foreach (var crop in result.Value.SmartCrops.Values)
                {
                    cropResult.Crops.Add(new AzureLogin.Shared.Services.SmartCropRegion
                    {
                        X = crop.BoundingBox.X,
                        Y = crop.BoundingBox.Y,
                        Width = crop.BoundingBox.Width,
                        Height = crop.BoundingBox.Height,
                        AspectRatio = crop.AspectRatio
                    });
                }
            }

            _logger?.LogInformation("Smart crops found {Count} suggestions", cropResult.Crops.Count);
            return cropResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Smart crops failed");
            return new AzureLogin.Shared.Services.SmartCropResult
            {
                Success = false,
                ErrorMessage = $"Smart crops failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Detects text regions using OCR with precise bounding boxes
    /// </summary>
    public async Task<AzureLogin.Shared.Services.TextDetectionResult> DetectTextAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
        {
            return new AzureLogin.Shared.Services.TextDetectionResult
            {
                Success = false,
                ErrorMessage = "Azure Vision service not configured"
            };
        }

        try
        {
            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                VisualFeatures.Read);

            var textResult = new AzureLogin.Shared.Services.TextDetectionResult
            {
                Success = true,
                TextRegions = new List<AzureLogin.Shared.Services.TextRegion>()
            };

            if (result.Value.Read?.Blocks != null)
            {
                foreach (var block in result.Value.Read.Blocks)
                {
                    foreach (var line in block.Lines)
                    {
                        // Get bounding box from polygon
                        var points = line.BoundingPolygon;
                        if (points != null && points.Count >= 4)
                        {
                            var xs = points.Select(p => p.X).ToList();
                            var ys = points.Select(p => p.Y).ToList();

                            textResult.TextRegions.Add(new AzureLogin.Shared.Services.TextRegion
                            {
                                X = xs.Min(),
                                Y = ys.Min(),
                                Width = xs.Max() - xs.Min(),
                                Height = ys.Max() - ys.Min(),
                                Text = line.Text
                            });
                        }
                    }
                }
            }

            _logger?.LogInformation("Text detection found {Count} text regions", textResult.TextRegions.Count);
            return textResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Text detection failed");
            return new AzureLogin.Shared.Services.TextDetectionResult
            {
                Success = false,
                ErrorMessage = $"Text detection failed: {ex.Message}"
            };
        }
    }

    #region Private Helper Methods

    private AzureLogin.Shared.Services.AzureVisionResult CreateNotConfiguredResult()
    {
        return new AzureLogin.Shared.Services.AzureVisionResult
        {
            Success = false,
            ErrorMessage = "Azure Vision service not configured. Set VisionEndpoint and VisionKey in appsettings.json"
        };
    }

    private AzureLogin.Shared.Services.AzureVisionResult ProcessAnalysisResult(ImageAnalysisResult result)
    {
        var analysisResult = new AzureLogin.Shared.Services.AzureVisionResult
        {
            Success = true,
            Regions = new List<AzureLogin.Shared.Services.DetectedRegion>(),
            ImageWidth = result.Metadata.Width,
            ImageHeight = result.Metadata.Height
        };

        // Process dense captions (best for UI regions with semantic meaning)
        if (result.DenseCaptions?.Values != null)
        {
            foreach (var caption in result.DenseCaptions.Values)
            {
                // Skip very small regions
                if (caption.BoundingBox.Width < 10 || caption.BoundingBox.Height < 10) continue;

                analysisResult.Regions.Add(new AzureLogin.Shared.Services.DetectedRegion
                {
                    X = caption.BoundingBox.X,
                    Y = caption.BoundingBox.Y,
                    Width = caption.BoundingBox.Width,
                    Height = caption.BoundingBox.Height,
                    Caption = caption.Text,
                    Confidence = caption.Confidence,
                    RegionType = "caption"
                });
            }
        }

        // Process objects (good for specific items like icons, logos)
        if (result.Objects?.Values != null)
        {
            foreach (var obj in result.Objects.Values)
            {
                if (obj.BoundingBox.Width < 10 || obj.BoundingBox.Height < 10) continue;

                var tags = obj.Tags?.Select(t => t.Name).ToList() ?? new List<string>();
                var region = new AzureLogin.Shared.Services.DetectedRegion
                {
                    X = obj.BoundingBox.X,
                    Y = obj.BoundingBox.Y,
                    Width = obj.BoundingBox.Width,
                    Height = obj.BoundingBox.Height,
                    Caption = tags.FirstOrDefault(),
                    Confidence = obj.Tags?.FirstOrDefault()?.Confidence ?? 0.5,
                    Tags = tags,
                    RegionType = "object"
                };

                // Only add if not significantly overlapping with existing
                if (!IsOverlapping(region, analysisResult.Regions))
                {
                    analysisResult.Regions.Add(region);
                }
            }
        }

        _logger?.LogInformation("Processed {Count} regions from Azure Vision", analysisResult.Regions.Count);
        return analysisResult;
    }

    private AzureLogin.Shared.Services.AzureVisionResult ProcessComprehensiveResult(ImageAnalysisResult result)
    {
        var analysisResult = new AzureLogin.Shared.Services.AzureVisionResult
        {
            Success = true,
            Regions = new List<AzureLogin.Shared.Services.DetectedRegion>(),
            ImageWidth = result.Metadata.Width,
            ImageHeight = result.Metadata.Height
        };

        // 1. First add people (highest priority for avatar extraction)
        if (result.People?.Values != null)
        {
            int personIndex = 1;
            foreach (var person in result.People.Values)
            {
                if (person.BoundingBox.Width < 10 || person.BoundingBox.Height < 10) continue;

                analysisResult.Regions.Add(new AzureLogin.Shared.Services.DetectedRegion
                {
                    X = person.BoundingBox.X,
                    Y = person.BoundingBox.Y,
                    Width = person.BoundingBox.Width,
                    Height = person.BoundingBox.Height,
                    Caption = $"Person {personIndex++}",
                    Confidence = person.Confidence,
                    Tags = new List<string> { "person" },
                    RegionType = "person"
                });
            }
        }

        // 2. Add detected objects
        if (result.Objects?.Values != null)
        {
            foreach (var obj in result.Objects.Values)
            {
                if (obj.BoundingBox.Width < 10 || obj.BoundingBox.Height < 10) continue;

                var tags = obj.Tags?.Select(t => t.Name).ToList() ?? new List<string>();
                var region = new AzureLogin.Shared.Services.DetectedRegion
                {
                    X = obj.BoundingBox.X,
                    Y = obj.BoundingBox.Y,
                    Width = obj.BoundingBox.Width,
                    Height = obj.BoundingBox.Height,
                    Caption = tags.FirstOrDefault(),
                    Confidence = obj.Tags?.FirstOrDefault()?.Confidence ?? 0.5,
                    Tags = tags,
                    RegionType = "object"
                };

                if (!IsOverlapping(region, analysisResult.Regions))
                {
                    analysisResult.Regions.Add(region);
                }
            }
        }

        // 3. Add dense captions for semantic regions
        if (result.DenseCaptions?.Values != null)
        {
            foreach (var caption in result.DenseCaptions.Values)
            {
                if (caption.BoundingBox.Width < 10 || caption.BoundingBox.Height < 10) continue;

                var region = new AzureLogin.Shared.Services.DetectedRegion
                {
                    X = caption.BoundingBox.X,
                    Y = caption.BoundingBox.Y,
                    Width = caption.BoundingBox.Width,
                    Height = caption.BoundingBox.Height,
                    Caption = caption.Text,
                    Confidence = caption.Confidence,
                    RegionType = "caption"
                };

                if (!IsOverlapping(region, analysisResult.Regions))
                {
                    analysisResult.Regions.Add(region);
                }
            }
        }

        // 4. Add smart crop suggestions as regions (good fallback)
        if (result.SmartCrops?.Values != null)
        {
            foreach (var crop in result.SmartCrops.Values)
            {
                var region = new AzureLogin.Shared.Services.DetectedRegion
                {
                    X = crop.BoundingBox.X,
                    Y = crop.BoundingBox.Y,
                    Width = crop.BoundingBox.Width,
                    Height = crop.BoundingBox.Height,
                    Caption = $"Smart crop {crop.AspectRatio:F2}",
                    Confidence = 0.9, // Smart crops are generally reliable
                    Tags = new List<string> { $"aspect-{crop.AspectRatio:F2}" },
                    RegionType = "smartcrop"
                };

                // Only add smart crops that don't significantly overlap existing regions
                if (!IsOverlapping(region, analysisResult.Regions, 0.5))
                {
                    analysisResult.Regions.Add(region);
                }
            }
        }

        _logger?.LogInformation("Comprehensive analysis found {Count} regions (People: {People}, Objects: {Objects}, Captions: {Captions}, SmartCrops: {Crops})",
            analysisResult.Regions.Count,
            result.People?.Values?.Count ?? 0,
            result.Objects?.Values?.Count ?? 0,
            result.DenseCaptions?.Values?.Count ?? 0,
            result.SmartCrops?.Values?.Count ?? 0);

        return analysisResult;
    }

    private bool IsOverlapping(AzureLogin.Shared.Services.DetectedRegion newRegion, 
        List<AzureLogin.Shared.Services.DetectedRegion> existing, double threshold = 0.7)
    {
        foreach (var region in existing)
        {
            var overlapX = Math.Max(0, Math.Min(newRegion.X + newRegion.Width, region.X + region.Width) - Math.Max(newRegion.X, region.X));
            var overlapY = Math.Max(0, Math.Min(newRegion.Y + newRegion.Height, region.Y + region.Height) - Math.Max(newRegion.Y, region.Y));
            var overlapArea = overlapX * overlapY;
            
            var newArea = newRegion.Width * newRegion.Height;
            var existingArea = region.Width * region.Height;
            var smallerArea = Math.Min(newArea, existingArea);

            if (smallerArea > 0 && (double)overlapArea / smallerArea > threshold)
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region New Azure Vision 4.0 Methods

    /// <summary>Gets a single caption for the entire image</summary>
    public async Task<AzureLogin.Shared.Services.ImageCaptionResult> GetImageCaptionAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
            return new AzureLogin.Shared.Services.ImageCaptionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

        try
        {
            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                VisualFeatures.Caption);

            return new AzureLogin.Shared.Services.ImageCaptionResult
            {
                Success = true,
                Caption = result.Value.Caption?.Text,
                Confidence = result.Value.Caption?.Confidence ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image captioning failed");
            return new AzureLogin.Shared.Services.ImageCaptionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Gets content tags for the image</summary>
    public async Task<AzureLogin.Shared.Services.ImageTagsResult> GetImageTagsAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
            return new AzureLogin.Shared.Services.ImageTagsResult { Success = false, ErrorMessage = "Azure Vision not configured" };

        try
        {
            var result = await _client.AnalyzeAsync(
                BinaryData.FromBytes(imageBytes),
                VisualFeatures.Tags);

            var tags = result.Value.Tags?.Values?.Select(t => new AzureLogin.Shared.Services.ImageTag
            {
                Name = t.Name,
                Confidence = t.Confidence
            }).ToList();

            return new AzureLogin.Shared.Services.ImageTagsResult
            {
                Success = true,
                Tags = tags
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image tagging failed");
            return new AzureLogin.Shared.Services.ImageTagsResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Detects brands/logos in the image using Computer Vision 3.2 API</summary>
    public async Task<AzureLogin.Shared.Services.BrandDetectionResult> DetectBrandsAsync(byte[] imageBytes)
    {
        if (!IsConfigured)
            return new AzureLogin.Shared.Services.BrandDetectionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

        try
        {
            var result = await CallComputerVision32Async(imageBytes, "brands");
            
            var brandResult = new AzureLogin.Shared.Services.BrandDetectionResult
            {
                Success = true,
                Brands = new List<AzureLogin.Shared.Services.DetectedBrand>()
            };

            if (result.RootElement.TryGetProperty("brands", out var brands))
            {
                foreach (var brand in brands.EnumerateArray())
                {
                    var detectedBrand = new AzureLogin.Shared.Services.DetectedBrand
                    {
                        Name = brand.GetProperty("name").GetString(),
                        Confidence = brand.GetProperty("confidence").GetDouble()
                    };

                    if (brand.TryGetProperty("rectangle", out var rect))
                    {
                        detectedBrand.X = rect.GetProperty("x").GetInt32();
                        detectedBrand.Y = rect.GetProperty("y").GetInt32();
                        detectedBrand.Width = rect.GetProperty("w").GetInt32();
                        detectedBrand.Height = rect.GetProperty("h").GetInt32();
                    }

                    brandResult.Brands.Add(detectedBrand);
                }
            }

            _logger?.LogInformation("Brand detection found {Count} brands", brandResult.Brands.Count);
            return brandResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Brand detection failed");
            return new AzureLogin.Shared.Services.BrandDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Gets image category using Computer Vision 3.2 API</summary>
    public async Task<AzureLogin.Shared.Services.CategoryResult> GetImageCategoryAsync(byte[] imageBytes)
    {
        if (!IsConfigured)
            return new AzureLogin.Shared.Services.CategoryResult { Success = false, ErrorMessage = "Azure Vision not configured" };

        try
        {
            var result = await CallComputerVision32Async(imageBytes, "categories");
            
            var categoryResult = new AzureLogin.Shared.Services.CategoryResult
            {
                Success = true,
                Categories = new List<AzureLogin.Shared.Services.ImageCategory>()
            };

            if (result.RootElement.TryGetProperty("categories", out var categories))
            {
                double maxScore = 0;
                foreach (var cat in categories.EnumerateArray())
                {
                    var category = new AzureLogin.Shared.Services.ImageCategory
                    {
                        Name = cat.GetProperty("name").GetString(),
                        Confidence = cat.GetProperty("score").GetDouble()
                    };
                    categoryResult.Categories.Add(category);

                    if (category.Confidence > maxScore)
                    {
                        maxScore = category.Confidence;
                        categoryResult.PrimaryCategory = category.Name;
                    }
                }
            }

            _logger?.LogInformation("Category detection found {Count} categories, primary: {Primary}", 
                categoryResult.Categories?.Count ?? 0, categoryResult.PrimaryCategory);
            return categoryResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Category detection failed");
            return new AzureLogin.Shared.Services.CategoryResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Detects adult/racy/gory content using Computer Vision 3.2 API</summary>
    public async Task<AzureLogin.Shared.Services.AdultContentResult> DetectAdultContentAsync(byte[] imageBytes)
    {
        if (!IsConfigured)
            return new AzureLogin.Shared.Services.AdultContentResult { Success = false, ErrorMessage = "Azure Vision not configured" };

        try
        {
            var result = await CallComputerVision32Async(imageBytes, "adult");
            
            var adultResult = new AzureLogin.Shared.Services.AdultContentResult { Success = true };

            if (result.RootElement.TryGetProperty("adult", out var adult))
            {
                adultResult.IsAdultContent = adult.GetProperty("isAdultContent").GetBoolean();
                adultResult.IsRacyContent = adult.GetProperty("isRacyContent").GetBoolean();
                adultResult.IsGoryContent = adult.TryGetProperty("isGoryContent", out var gory) && gory.GetBoolean();
                adultResult.AdultScore = adult.GetProperty("adultScore").GetDouble();
                adultResult.RacyScore = adult.GetProperty("racyScore").GetDouble();
                adultResult.GoreScore = adult.TryGetProperty("goreScore", out var gore) ? gore.GetDouble() : 0;
            }

            _logger?.LogInformation("Adult content detection: Adult={Adult}, Racy={Racy}, Gory={Gory}", 
                adultResult.IsAdultContent, adultResult.IsRacyContent, adultResult.IsGoryContent);
            return adultResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Adult content detection failed");
            return new AzureLogin.Shared.Services.AdultContentResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Detects faces using Computer Vision 3.2 API (basic detection, no detailed attributes)</summary>
    public async Task<AzureLogin.Shared.Services.FaceDetectionResult> DetectFacesAsync(byte[] imageBytes)
    {
        if (!IsConfigured)
            return new AzureLogin.Shared.Services.FaceDetectionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

        try
        {
            var result = await CallComputerVision32Async(imageBytes, "faces");
            
            var faceResult = new AzureLogin.Shared.Services.FaceDetectionResult
            {
                Success = true,
                Faces = new List<AzureLogin.Shared.Services.DetectedFace>()
            };

            if (result.RootElement.TryGetProperty("faces", out var faces))
            {
                foreach (var face in faces.EnumerateArray())
                {
                    var detectedFace = new AzureLogin.Shared.Services.DetectedFace();

                    if (face.TryGetProperty("faceRectangle", out var rect))
                    {
                        detectedFace.X = rect.GetProperty("left").GetInt32();
                        detectedFace.Y = rect.GetProperty("top").GetInt32();
                        detectedFace.Width = rect.GetProperty("width").GetInt32();
                        detectedFace.Height = rect.GetProperty("height").GetInt32();
                    }

                    if (face.TryGetProperty("age", out var age))
                        detectedFace.Age = age.GetInt32();

                    if (face.TryGetProperty("gender", out var gender))
                        detectedFace.Gender = gender.GetString();

                    detectedFace.Confidence = 0.9; // CV 3.2 doesn't return confidence for faces

                    faceResult.Faces.Add(detectedFace);
                }
            }

            _logger?.LogInformation("Face detection found {Count} faces", faceResult.Faces.Count);
            return faceResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Face detection failed");
            return new AzureLogin.Shared.Services.FaceDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Gets image type (photo, clipart, line drawing) using Computer Vision 3.2 API</summary>
    public async Task<AzureLogin.Shared.Services.ImageTypeResult> GetImageTypeAsync(byte[] imageBytes)
    {
        if (!IsConfigured)
            return new AzureLogin.Shared.Services.ImageTypeResult { Success = false, ErrorMessage = "Azure Vision not configured" };

        try
        {
            var result = await CallComputerVision32Async(imageBytes, "imageType");
            
            var typeResult = new AzureLogin.Shared.Services.ImageTypeResult { Success = true };

            if (result.RootElement.TryGetProperty("imageType", out var imageType))
            {
                var clipArtType = imageType.GetProperty("clipArtType").GetInt32();
                var lineDrawingType = imageType.GetProperty("lineDrawingType").GetInt32();

                typeResult.ClipArtConfidence = clipArtType / 3.0; // 0-3 scale
                typeResult.LineDrawingConfidence = lineDrawingType; // 0-1 scale
                typeResult.IsClipArt = clipArtType >= 2;
                typeResult.IsLineDrawing = lineDrawingType == 1;

                if (typeResult.IsLineDrawing)
                    typeResult.ImageType = "linedrawing";
                else if (typeResult.IsClipArt)
                    typeResult.ImageType = "clipart";
                else
                    typeResult.ImageType = "photo";
            }

            _logger?.LogInformation("Image type detection: Type={Type}, ClipArt={ClipArt}, LineDrawing={Line}", 
                typeResult.ImageType, typeResult.IsClipArt, typeResult.IsLineDrawing);
            return typeResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image type detection failed");
            return new AzureLogin.Shared.Services.ImageTypeResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Removes background from image using Vision 4.0 Segment API</summary>
    public async Task<AzureLogin.Shared.Services.BackgroundRemovalResult> RemoveBackgroundAsync(byte[] imageBytes)
    {
        if (!IsConfigured)
            return new AzureLogin.Shared.Services.BackgroundRemovalResult { Success = false, ErrorMessage = "Azure Vision not configured" };

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);

            var requestUri = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:segment?api-version=2023-02-01-preview&mode=backgroundRemoval";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await httpClient.PostAsync(requestUri, content);

            if (response.IsSuccessStatusCode)
            {
                var resultBytes = await response.Content.ReadAsByteArrayAsync();
                _logger?.LogInformation("Background removal succeeded, output size: {Size} bytes", resultBytes.Length);
                return new AzureLogin.Shared.Services.BackgroundRemovalResult
                {
                    Success = true,
                    ImageWithoutBackground = resultBytes,
                    Base64Image = Convert.ToBase64String(resultBytes)
                };
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger?.LogWarning("Background removal failed: {Error}", error);
            return new AzureLogin.Shared.Services.BackgroundRemovalResult
            {
                Success = false,
                ErrorMessage = $"Background removal failed: {error}"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Background removal failed");
            return new AzureLogin.Shared.Services.BackgroundRemovalResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>Helper method to call Computer Vision 3.2 REST API</summary>
    private async Task<JsonDocument> CallComputerVision32Async(byte[] imageBytes, string visualFeatures)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);

        // Use Computer Vision 3.2 endpoint for features not in 4.0
        var requestUri = $"{_endpoint.TrimEnd('/')}/vision/v3.2/analyze?visualFeatures={visualFeatures}";

        using var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var response = await httpClient.PostAsync(requestUri, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("Computer Vision 3.2 API call failed: {Status} - {Content}", response.StatusCode, responseContent);
            throw new Exception($"API call failed: {response.StatusCode} - {responseContent}");
        }

        return JsonDocument.Parse(responseContent);
    }

    #endregion
}
