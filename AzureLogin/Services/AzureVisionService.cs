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

    private readonly HttpClient _httpClient = new HttpClient();

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

        _httpClient.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
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
            return new AzureLogin.Shared.Services.ImageCaptionResult { Success = false, ErrorMessage = "Azure Vision service not configured" };

        try
        {
            var options = new ImageAnalysisOptions { GenderNeutralCaption = true };
            var result = await _client.AnalyzeAsync(BinaryData.FromBytes(imageBytes), VisualFeatures.Caption, options);
            var caption = result.Value.Caption;
            return new AzureLogin.Shared.Services.ImageCaptionResult
            {
                Success = caption != null,
                Caption = caption?.Text,
                Confidence = caption?.Confidence ?? 0,
                ErrorMessage = caption == null ? "No caption returned" : null
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Caption failed");
            return new AzureLogin.Shared.Services.ImageCaptionResult { Success = false, ErrorMessage = $"Caption failed: {ex.Message}" };
        }
    }

    /// <summary>Gets content tags for the image</summary>
    public async Task<AzureLogin.Shared.Services.ImageTagsResult> GetImageTagsAsync(byte[] imageBytes)
    {
        if (!IsConfigured || _client == null)
            return new AzureLogin.Shared.Services.ImageTagsResult { Success = false, ErrorMessage = "Azure Vision service not configured" };

        try
        {
            var result = await _client.AnalyzeAsync(BinaryData.FromBytes(imageBytes), VisualFeatures.Tags);
            var tags = result.Value.Tags?.Values?.Select(t => new AzureLogin.Shared.Services.ImageTag { Name = t.Name, Confidence = t.Confidence }).ToList();
            return new AzureLogin.Shared.Services.ImageTagsResult
            {
                Success = true,
                Tags = tags ?? new List<AzureLogin.Shared.Services.ImageTag>()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Tags failed");
            return new AzureLogin.Shared.Services.ImageTagsResult { Success = false, ErrorMessage = $"Tags failed: {ex.Message}" };
        }
    }

    /// <summary>Detects brands/logos in the image using Computer Vision 3.2 API</summary>
    public async Task<AzureLogin.Shared.Services.BrandDetectionResult> DetectBrandsAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeWithV32Async(imageBytes, "Brands");
        if (doc == null) return new AzureLogin.Shared.Services.BrandDetectionResult { Success = false, ErrorMessage = "Azure Vision service not configured" };
        try
        {
            var brands = doc.RootElement.GetProperty("brands");
            var list = new List<AzureLogin.Shared.Services.DetectedBrand>();
            foreach (var b in brands.EnumerateArray())
            {
                list.Add(new AzureLogin.Shared.Services.DetectedBrand
                {
                    Name = b.GetProperty("name").GetString(),
                    Confidence = b.GetProperty("confidence").GetDouble(),
                    X = b.GetProperty("rectangle").GetProperty("x").GetInt32(),
                    Y = b.GetProperty("rectangle").GetProperty("y").GetInt32(),
                    Width = b.GetProperty("rectangle").GetProperty("w").GetInt32(),
                    Height = b.GetProperty("rectangle").GetProperty("h").GetInt32()
                });
            }
            return new AzureLogin.Shared.Services.BrandDetectionResult { Success = true, Brands = list };
        }
        catch (Exception ex)
        {
            return new AzureLogin.Shared.Services.BrandDetectionResult { Success = false, ErrorMessage = $"Brand parse failed: {ex.Message}" };
        }
    }

    public async Task<AzureLogin.Shared.Services.CategoryResult> GetImageCategoryAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeWithV32Async(imageBytes, "Categories");
        if (doc == null) return new AzureLogin.Shared.Services.CategoryResult { Success = false, ErrorMessage = "Azure Vision service not configured" };
        try
        {
            var categories = doc.RootElement.GetProperty("categories");
            var list = new List<AzureLogin.Shared.Services.ImageCategory>();
            foreach (var c in categories.EnumerateArray())
            {
                list.Add(new AzureLogin.Shared.Services.ImageCategory { Name = c.GetProperty("name").GetString(), Confidence = c.GetProperty("score").GetDouble() });
            }
            return new AzureLogin.Shared.Services.CategoryResult
            {
                Success = true,
                PrimaryCategory = list.FirstOrDefault()?.Name,
                Categories = list
            };
        }
        catch (Exception ex)
        {
            return new AzureLogin.Shared.Services.CategoryResult { Success = false, ErrorMessage = $"Category parse failed: {ex.Message}" };
        }
    }

    public async Task<AzureLogin.Shared.Services.AdultContentResult> DetectAdultContentAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeWithV32Async(imageBytes, "Adult");
        if (doc == null) return new AzureLogin.Shared.Services.AdultContentResult { Success = false, ErrorMessage = "Azure Vision service not configured" };
        try
        {
            var adult = doc.RootElement.GetProperty("adult");
            return new AzureLogin.Shared.Services.AdultContentResult
            {
                Success = true,
                IsAdultContent = adult.GetProperty("isAdultContent").GetBoolean(),
                IsRacyContent = adult.GetProperty("isRacyContent").GetBoolean(),
                IsGoryContent = adult.GetProperty("isGoryContent").GetBoolean(),
                AdultScore = adult.GetProperty("adultScore").GetDouble(),
                RacyScore = adult.GetProperty("racyScore").GetDouble(),
                GoreScore = adult.GetProperty("goreScore").GetDouble()
            };
        }
        catch (Exception ex)
        {
            return new AzureLogin.Shared.Services.AdultContentResult { Success = false, ErrorMessage = $"Adult parse failed: {ex.Message}" };
        }
    }

    public async Task<AzureLogin.Shared.Services.FaceDetectionResult> DetectFacesAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeWithV32Async(imageBytes, "Faces");
        if (doc == null) return new AzureLogin.Shared.Services.FaceDetectionResult { Success = false, ErrorMessage = "Azure Vision service not configured" };
        try
        {
            var faces = doc.RootElement.GetProperty("faces");
            var list = new List<AzureLogin.Shared.Services.DetectedFace>();
            foreach (var f in faces.EnumerateArray())
            {
                list.Add(new AzureLogin.Shared.Services.DetectedFace
                {
                    Age = f.TryGetProperty("age", out var age) ? age.GetInt32() : null,
                    Gender = f.TryGetProperty("gender", out var gender) ? gender.GetString() : null,
                    X = f.GetProperty("faceRectangle").GetProperty("left").GetInt32(),
                    Y = f.GetProperty("faceRectangle").GetProperty("top").GetInt32(),
                    Width = f.GetProperty("faceRectangle").GetProperty("width").GetInt32(),
                    Height = f.GetProperty("faceRectangle").GetProperty("height").GetInt32(),
                    Confidence = f.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0
                });
            }
            return new AzureLogin.Shared.Services.FaceDetectionResult { Success = true, Faces = list };
        }
        catch (Exception ex)
        {
            return new AzureLogin.Shared.Services.FaceDetectionResult { Success = false, ErrorMessage = $"Face parse failed: {ex.Message}" };
        }
    }

    public async Task<AzureLogin.Shared.Services.ImageTypeResult> GetImageTypeAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeWithV32Async(imageBytes, "ImageType");
        if (doc == null) return new AzureLogin.Shared.Services.ImageTypeResult { Success = false, ErrorMessage = "Azure Vision service not configured" };
        try
        {
            var type = doc.RootElement.GetProperty("imageType");
            return new AzureLogin.Shared.Services.ImageTypeResult
            {
                Success = true,
                ImageType = type.TryGetProperty("clipArtType", out _) ? type.GetProperty("clipArtType").GetInt32() > 0 ? "clipart" : "photo" : null,
                IsClipArt = type.TryGetProperty("clipArtType", out var clip) && clip.GetInt32() > 0,
                IsLineDrawing = type.TryGetProperty("lineDrawingType", out var line) && line.GetInt32() > 0,
                ClipArtConfidence = type.TryGetProperty("clipArtType", out var c) ? c.GetInt32() : 0,
                LineDrawingConfidence = type.TryGetProperty("lineDrawingType", out var l) ? l.GetInt32() : 0
            };
        }
        catch (Exception ex)
        {
            return new AzureLogin.Shared.Services.ImageTypeResult { Success = false, ErrorMessage = $"ImageType parse failed: {ex.Message}" };
        }
    }

    public Task<AzureLogin.Shared.Services.BackgroundRemovalResult> RemoveBackgroundAsync(byte[] imageBytes)
    {
        // Background removal not yet supported via current SDK/REST in this project. Return graceful error.
        return Task.FromResult(new AzureLogin.Shared.Services.BackgroundRemovalResult
        {
            Success = false,
            ErrorMessage = "Background removal not supported in this build"
        });
    }
    #endregion

    #region Private Methods

    private async Task<JsonDocument?> AnalyzeWithV32Async(byte[] imageBytes, string visualFeatures)
    {
        if (!IsConfigured)
            return null;
        var url = $"{_endpoint.TrimEnd('/')}/vision/v3.2/analyze?visualFeatures={visualFeatures}";
        using var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var response = await _httpClient.PostAsync(url, content);
        if (!response.IsSuccessStatusCode)
            return null;
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    #endregion
}
