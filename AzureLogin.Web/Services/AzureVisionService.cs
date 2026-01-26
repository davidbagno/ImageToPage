using System.Net.Http.Headers;
using System.Text.Json;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Options;

namespace AzureLogin.Web.Services;

/// <summary>
/// Azure AI Vision 4.0 service implementation for pixel-accurate image analysis
/// </summary>
public class AzureVisionService : IAzureVisionService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiKey;

    public bool IsConfigured => !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_apiKey);

    public AzureVisionService(IOptions<AzureOpenAISettings> settings)
    {
        _httpClient = new HttpClient();
        _endpoint = settings.Value.VisionEndpoint;
        _apiKey = settings.Value.VisionKey;
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
        }
    }

    public async Task<AzureVisionResult> AnalyzeComprehensiveAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new AzureVisionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=denseCaptions,objects,tags,people,smartCrops&smartcrops-aspect-ratios=1.0,0.75,1.33,1.78";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new AzureVisionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            return ParseComprehensiveResponse(responseBody);
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<AzureVisionResult> DetectPeopleAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new AzureVisionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=people";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new AzureVisionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            return ParsePeopleResponse(responseBody);
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SmartCropResult> GetSmartCropsAsync(byte[] imageBytes, params double[] aspectRatios)
    {
        try
        {
            if (!IsConfigured)
                return new SmartCropResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var ratiosStr = aspectRatios.Length > 0 
                ? string.Join(",", aspectRatios.Select(r => r.ToString("F2")))
                : "1.0,0.75,1.33,1.78";

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=smartCrops&smartcrops-aspect-ratios={ratiosStr}";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new SmartCropResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            return ParseSmartCropsResponse(responseBody);
        }
        catch (Exception ex)
        {
            return new SmartCropResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<AzureVisionResult> AnalyzeImageAsync(byte[] imageBytes)
    {
        try
        {
            if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_apiKey))
            {
                return new AzureVisionResult
                {
                    Success = false,
                    ErrorMessage = "Azure Vision service not configured. Please set VisionEndpoint and VisionKey in settings."
                };
            }

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=denseCaptions,objects,tags";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new AzureVisionResult
                {
                    Success = false,
                    ErrorMessage = $"Azure Vision API error: {response.StatusCode} - {responseBody}"
                };
            }

            return ParseAnalysisResponse(responseBody);
        }
        catch (Exception ex)
        {
            return new AzureVisionResult
            {
                Success = false,
                ErrorMessage = $"Azure Vision analysis failed: {ex.Message}"
            };
        }
    }

    public async Task<AzureVisionResult> GetDenseCaptionsAsync(byte[] imageBytes)
    {
        try
        {
            if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_apiKey))
                return new AzureVisionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=denseCaptions";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new AzureVisionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            return ParseDenseCaptionsResponse(responseBody);
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<AzureVisionResult> DetectObjectsAsync(byte[] imageBytes)
    {
        try
        {
            if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_apiKey))
                return new AzureVisionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=objects";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new AzureVisionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            return ParseObjectsResponse(responseBody);
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<TextDetectionResult> DetectTextAsync(byte[] imageBytes)
    {
        try
        {
            if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_apiKey))
                return new TextDetectionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=read";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new TextDetectionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            return ParseTextResponse(responseBody);
        }
        catch (Exception ex)
        {
            return new TextDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private AzureVisionResult ParseAnalysisResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var result = new AzureVisionResult { Success = true, Regions = new List<DetectedRegion>() };

            if (root.TryGetProperty("metadata", out var metadata))
            {
                result.ImageWidth = metadata.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                result.ImageHeight = metadata.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
            }

            if (root.TryGetProperty("denseCaptionsResult", out var denseCaptions) &&
                denseCaptions.TryGetProperty("values", out var captionValues))
            {
                foreach (var caption in captionValues.EnumerateArray())
                {
                    var region = ParseCaptionRegion(caption);
                    if (region != null) result.Regions.Add(region);
                }
            }

            if (root.TryGetProperty("objectsResult", out var objects) &&
                objects.TryGetProperty("values", out var objectValues))
            {
                foreach (var obj in objectValues.EnumerateArray())
                {
                    var region = ParseObjectRegion(obj);
                    if (region != null && !IsOverlapping(region, result.Regions))
                        result.Regions.Add(region);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = $"Parse error: {ex.Message}" };
        }
    }

    private AzureVisionResult ParseDenseCaptionsResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new AzureVisionResult { Success = true, Regions = new List<DetectedRegion>() };

            if (root.TryGetProperty("metadata", out var metadata))
            {
                result.ImageWidth = metadata.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                result.ImageHeight = metadata.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
            }

            if (root.TryGetProperty("denseCaptionsResult", out var denseCaptions) &&
                denseCaptions.TryGetProperty("values", out var values))
            {
                foreach (var caption in values.EnumerateArray())
                {
                    var region = ParseCaptionRegion(caption);
                    if (region != null) result.Regions.Add(region);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private AzureVisionResult ParseObjectsResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new AzureVisionResult { Success = true, Regions = new List<DetectedRegion>() };

            if (root.TryGetProperty("metadata", out var metadata))
            {
                result.ImageWidth = metadata.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                result.ImageHeight = metadata.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
            }

            if (root.TryGetProperty("objectsResult", out var objects) &&
                objects.TryGetProperty("values", out var values))
            {
                foreach (var obj in values.EnumerateArray())
                {
                    var region = ParseObjectRegion(obj);
                    if (region != null) result.Regions.Add(region);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private TextDetectionResult ParseTextResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new TextDetectionResult { Success = true, TextRegions = new List<TextRegion>() };

            if (root.TryGetProperty("readResult", out var readResult) &&
                readResult.TryGetProperty("blocks", out var blocks))
            {
                foreach (var block in blocks.EnumerateArray())
                {
                    if (block.TryGetProperty("lines", out var lines))
                    {
                        foreach (var line in lines.EnumerateArray())
                        {
                            var textRegion = ParseTextLine(line);
                            if (textRegion != null) result.TextRegions.Add(textRegion);
                        }
                    }
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            return new TextDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private DetectedRegion? ParseCaptionRegion(JsonElement caption)
    {
        try
        {
            if (!caption.TryGetProperty("boundingBox", out var bbox)) return null;
            var x = bbox.TryGetProperty("x", out var xVal) ? xVal.GetInt32() : 0;
            var y = bbox.TryGetProperty("y", out var yVal) ? yVal.GetInt32() : 0;
            var w = bbox.TryGetProperty("w", out var wVal) ? wVal.GetInt32() : 0;
            var h = bbox.TryGetProperty("h", out var hVal) ? hVal.GetInt32() : 0;
            if (w < 10 || h < 10) return null;

            return new DetectedRegion
            {
                X = x, Y = y, Width = w, Height = h,
                Caption = caption.TryGetProperty("text", out var text) ? text.GetString() : null,
                Confidence = caption.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5
            };
        }
        catch { return null; }
    }

    private DetectedRegion? ParseObjectRegion(JsonElement obj)
    {
        try
        {
            if (!obj.TryGetProperty("boundingBox", out var bbox)) return null;
            var x = bbox.TryGetProperty("x", out var xVal) ? xVal.GetInt32() : 0;
            var y = bbox.TryGetProperty("y", out var yVal) ? yVal.GetInt32() : 0;
            var w = bbox.TryGetProperty("w", out var wVal) ? wVal.GetInt32() : 0;
            var h = bbox.TryGetProperty("h", out var hVal) ? hVal.GetInt32() : 0;
            if (w < 10 || h < 10) return null;

            var tags = new List<string>();
            if (obj.TryGetProperty("tags", out var tagsArr))
                foreach (var tag in tagsArr.EnumerateArray())
                    if (tag.TryGetProperty("name", out var name))
                        tags.Add(name.GetString() ?? "");

            return new DetectedRegion
            {
                X = x, Y = y, Width = w, Height = h,
                Caption = tags.FirstOrDefault(),
                Confidence = obj.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5,
                Tags = tags
            };
        }
        catch { return null; }
    }

    private TextRegion? ParseTextLine(JsonElement line)
    {
        try
        {
            if (!line.TryGetProperty("boundingPolygon", out var polygon)) return null;
            var points = polygon.EnumerateArray().ToList();
            if (points.Count < 4) return null;

            var xs = points.Select(p => p.TryGetProperty("x", out var x) ? x.GetInt32() : 0).ToList();
            var ys = points.Select(p => p.TryGetProperty("y", out var y) ? y.GetInt32() : 0).ToList();

            return new TextRegion
            {
                X = xs.Min(), Y = ys.Min(),
                Width = xs.Max() - xs.Min(), Height = ys.Max() - ys.Min(),
                Text = line.TryGetProperty("text", out var text) ? text.GetString() : null
            };
        }
        catch { return null; }
    }

    private bool IsOverlapping(DetectedRegion newRegion, List<DetectedRegion> existing)
    {
        foreach (var region in existing)
        {
            var overlapX = Math.Max(0, Math.Min(newRegion.X + newRegion.Width, region.X + region.Width) - Math.Max(newRegion.X, region.X));
            var overlapY = Math.Max(0, Math.Min(newRegion.Y + newRegion.Height, region.Y + region.Height) - Math.Max(newRegion.Y, region.Y));
            var overlapArea = overlapX * overlapY;
            var smallerArea = Math.Min(newRegion.Width * newRegion.Height, region.Width * region.Height);
            if (overlapArea > smallerArea * 0.7) return true;
        }
        return false;
    }

    private AzureVisionResult ParseComprehensiveResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new AzureVisionResult { Success = true, Regions = new List<DetectedRegion>() };

            if (root.TryGetProperty("metadata", out var metadata))
            {
                result.ImageWidth = metadata.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                result.ImageHeight = metadata.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
            }

            // Parse people first
            if (root.TryGetProperty("peopleResult", out var peopleResult) &&
                peopleResult.TryGetProperty("values", out var peopleValues))
            {
                int personIndex = 1;
                foreach (var person in peopleValues.EnumerateArray())
                {
                    if (person.TryGetProperty("boundingBox", out var bbox))
                    {
                        var x = bbox.TryGetProperty("x", out var xVal) ? xVal.GetInt32() : 0;
                        var y = bbox.TryGetProperty("y", out var yVal) ? yVal.GetInt32() : 0;
                        var w = bbox.TryGetProperty("w", out var wVal) ? wVal.GetInt32() : 0;
                        var h = bbox.TryGetProperty("h", out var hVal) ? hVal.GetInt32() : 0;
                        if (w >= 10 && h >= 10)
                        {
                            result.Regions.Add(new DetectedRegion
                            {
                                X = x, Y = y, Width = w, Height = h,
                                Caption = $"Person {personIndex++}",
                                Confidence = person.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.8,
                                Tags = new List<string> { "person" },
                                RegionType = "person"
                            });
                        }
                    }
                }
            }

            // Parse objects
            if (root.TryGetProperty("objectsResult", out var objects) &&
                objects.TryGetProperty("values", out var objectValues))
            {
                foreach (var obj in objectValues.EnumerateArray())
                {
                    var region = ParseObjectRegion(obj);
                    if (region != null)
                    {
                        region.RegionType = "object";
                        if (!IsOverlapping(region, result.Regions))
                            result.Regions.Add(region);
                    }
                }
            }

            // Parse dense captions
            if (root.TryGetProperty("denseCaptionsResult", out var denseCaptions) &&
                denseCaptions.TryGetProperty("values", out var captionValues))
            {
                foreach (var caption in captionValues.EnumerateArray())
                {
                    var region = ParseCaptionRegion(caption);
                    if (region != null)
                    {
                        region.RegionType = "caption";
                        if (!IsOverlapping(region, result.Regions))
                            result.Regions.Add(region);
                    }
                }
            }

            // Parse smart crops
            if (root.TryGetProperty("smartCropsResult", out var smartCrops) &&
                smartCrops.TryGetProperty("values", out var cropValues))
            {
                foreach (var crop in cropValues.EnumerateArray())
                {
                    if (crop.TryGetProperty("boundingBox", out var bbox))
                    {
                        var x = bbox.TryGetProperty("x", out var xVal) ? xVal.GetInt32() : 0;
                        var y = bbox.TryGetProperty("y", out var yVal) ? yVal.GetInt32() : 0;
                        var w = bbox.TryGetProperty("w", out var wVal) ? wVal.GetInt32() : 0;
                        var h = bbox.TryGetProperty("h", out var hVal) ? hVal.GetInt32() : 0;
                        var aspectRatio = crop.TryGetProperty("aspectRatio", out var ar) ? ar.GetDouble() : 1.0;

                        var region = new DetectedRegion
                        {
                            X = x, Y = y, Width = w, Height = h,
                            Caption = $"Smart crop {aspectRatio:F2}",
                            Confidence = 0.9,
                            Tags = new List<string> { $"aspect-{aspectRatio:F2}" },
                            RegionType = "smartcrop"
                        };

                        if (!IsOverlapping(region, result.Regions, 0.5))
                            result.Regions.Add(region);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = $"Parse error: {ex.Message}" };
        }
    }

    private bool IsOverlapping(DetectedRegion newRegion, List<DetectedRegion> existing, double threshold)
    {
        foreach (var region in existing)
        {
            var overlapX = Math.Max(0, Math.Min(newRegion.X + newRegion.Width, region.X + region.Width) - Math.Max(newRegion.X, region.X));
            var overlapY = Math.Max(0, Math.Min(newRegion.Y + newRegion.Height, region.Y + region.Height) - Math.Max(newRegion.Y, region.Y));
            var overlapArea = overlapX * overlapY;
            var smallerArea = Math.Min(newRegion.Width * newRegion.Height, region.Width * region.Height);
            if (smallerArea > 0 && overlapArea > smallerArea * threshold) return true;
        }
        return false;
    }

    private AzureVisionResult ParsePeopleResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new AzureVisionResult { Success = true, Regions = new List<DetectedRegion>() };

            if (root.TryGetProperty("metadata", out var metadata))
            {
                result.ImageWidth = metadata.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                result.ImageHeight = metadata.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
            }

            if (root.TryGetProperty("peopleResult", out var peopleResult) &&
                peopleResult.TryGetProperty("values", out var peopleValues))
            {
                int personIndex = 1;
                foreach (var person in peopleValues.EnumerateArray())
                {
                    if (person.TryGetProperty("boundingBox", out var bbox))
                    {
                        var x = bbox.TryGetProperty("x", out var xVal) ? xVal.GetInt32() : 0;
                        var y = bbox.TryGetProperty("y", out var yVal) ? yVal.GetInt32() : 0;
                        var w = bbox.TryGetProperty("w", out var wVal) ? wVal.GetInt32() : 0;
                        var h = bbox.TryGetProperty("h", out var hVal) ? hVal.GetInt32() : 0;
                        if (w >= 10 && h >= 10)
                        {
                            result.Regions.Add(new DetectedRegion
                            {
                                X = x, Y = y, Width = w, Height = h,
                                Caption = $"Person {personIndex++}",
                                Confidence = person.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.8,
                                Tags = new List<string> { "person" },
                                RegionType = "person"
                            });
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new AzureVisionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private SmartCropResult ParseSmartCropsResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new SmartCropResult { Success = true, Crops = new List<SmartCropRegion>() };

            if (root.TryGetProperty("metadata", out var metadata))
            {
                result.ImageWidth = metadata.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                result.ImageHeight = metadata.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
            }

            if (root.TryGetProperty("smartCropsResult", out var smartCrops) &&
                smartCrops.TryGetProperty("values", out var cropValues))
            {
                foreach (var crop in cropValues.EnumerateArray())
                {
                    if (crop.TryGetProperty("boundingBox", out var bbox))
                    {
                        result.Crops.Add(new SmartCropRegion
                        {
                            X = bbox.TryGetProperty("x", out var xVal) ? xVal.GetInt32() : 0,
                            Y = bbox.TryGetProperty("y", out var yVal) ? yVal.GetInt32() : 0,
                            Width = bbox.TryGetProperty("w", out var wVal) ? wVal.GetInt32() : 0,
                            Height = bbox.TryGetProperty("h", out var hVal) ? hVal.GetInt32() : 0,
                            AspectRatio = crop.TryGetProperty("aspectRatio", out var ar) ? ar.GetDouble() : 1.0
                        });
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new SmartCropResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ===== NEW INTERFACE IMPLEMENTATIONS =====

    public async Task<ImageCaptionResult> GetImageCaptionAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new ImageCaptionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=caption";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new ImageCaptionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            var result = new ImageCaptionResult { Success = true };
            if (root.TryGetProperty("captionResult", out var captionResult))
            {
                result.Caption = captionResult.TryGetProperty("text", out var text) ? text.GetString() : null;
                result.Confidence = captionResult.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0;
            }
            return result;
        }
        catch (Exception ex)
        {
            return new ImageCaptionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImageTagsResult> GetImageTagsAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new ImageTagsResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=tags";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new ImageTagsResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            var result = new ImageTagsResult { Success = true, Tags = new List<ImageTag>() };
            if (root.TryGetProperty("tagsResult", out var tagsResult) &&
                tagsResult.TryGetProperty("values", out var values))
            {
                foreach (var tag in values.EnumerateArray())
                {
                    result.Tags.Add(new ImageTag
                    {
                        Name = tag.TryGetProperty("name", out var name) ? name.GetString() : "",
                        Confidence = tag.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0
                    });
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            return new ImageTagsResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<BrandDetectionResult> DetectBrandsAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new BrandDetectionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            // Brands detection uses v3.2 API
            var apiUrl = $"{_endpoint.TrimEnd('/')}/vision/v3.2/analyze?visualFeatures=Brands";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new BrandDetectionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            var result = new BrandDetectionResult { Success = true, Brands = new List<DetectedBrand>() };
            if (root.TryGetProperty("brands", out var brands))
            {
                foreach (var brand in brands.EnumerateArray())
                {
                    var detectedBrand = new DetectedBrand
                    {
                        Name = brand.TryGetProperty("name", out var name) ? name.GetString() : "",
                        Confidence = brand.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0
                    };
                    if (brand.TryGetProperty("rectangle", out var rect))
                    {
                        detectedBrand.X = rect.TryGetProperty("x", out var x) ? x.GetInt32() : 0;
                        detectedBrand.Y = rect.TryGetProperty("y", out var y) ? y.GetInt32() : 0;
                        detectedBrand.Width = rect.TryGetProperty("w", out var w) ? w.GetInt32() : 0;
                        detectedBrand.Height = rect.TryGetProperty("h", out var h) ? h.GetInt32() : 0;
                    }
                    result.Brands.Add(detectedBrand);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            return new BrandDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<CategoryResult> GetImageCategoryAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new CategoryResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/vision/v3.2/analyze?visualFeatures=Categories";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new CategoryResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            var result = new CategoryResult { Success = true, Categories = new List<ImageCategory>() };
            if (root.TryGetProperty("categories", out var categories))
            {
                foreach (var cat in categories.EnumerateArray())
                {
                    result.Categories.Add(new ImageCategory
                    {
                        Name = cat.TryGetProperty("name", out var name) ? name.GetString() : "",
                        Confidence = cat.TryGetProperty("score", out var score) ? score.GetDouble() : 0
                    });
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            return new CategoryResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<AdultContentResult> DetectAdultContentAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new AdultContentResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/vision/v3.2/analyze?visualFeatures=Adult";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new AdultContentResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            var result = new AdultContentResult { Success = true };
            if (root.TryGetProperty("adult", out var adult))
            {
                result.IsAdultContent = adult.TryGetProperty("isAdultContent", out var isAdult) && isAdult.GetBoolean();
                result.IsRacyContent = adult.TryGetProperty("isRacyContent", out var isRacy) && isRacy.GetBoolean();
                result.IsGoryContent = adult.TryGetProperty("isGoryContent", out var isGory) && isGory.GetBoolean();
                result.AdultScore = adult.TryGetProperty("adultScore", out var adultScore) ? adultScore.GetDouble() : 0;
                result.RacyScore = adult.TryGetProperty("racyScore", out var racyScore) ? racyScore.GetDouble() : 0;
                result.GoreScore = adult.TryGetProperty("goreScore", out var goreScore) ? goreScore.GetDouble() : 0;
            }
            return result;
        }
        catch (Exception ex)
        {
            return new AdultContentResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<FaceDetectionResult> DetectFacesAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new FaceDetectionResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/vision/v3.2/analyze?visualFeatures=Faces";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new FaceDetectionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            var result = new FaceDetectionResult { Success = true, Faces = new List<DetectedFace>() };
            if (root.TryGetProperty("faces", out var faces))
            {
                foreach (var face in faces.EnumerateArray())
                {
                    var detectedFace = new DetectedFace
                    {
                        Age = face.TryGetProperty("age", out var age) ? age.GetInt32() : 0,
                        Gender = face.TryGetProperty("gender", out var gender) ? gender.GetString() : ""
                    };
                    if (face.TryGetProperty("faceRectangle", out var rect))
                    {
                        detectedFace.X = rect.TryGetProperty("left", out var x) ? x.GetInt32() : 0;
                        detectedFace.Y = rect.TryGetProperty("top", out var y) ? y.GetInt32() : 0;
                        detectedFace.Width = rect.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                        detectedFace.Height = rect.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                    }
                    result.Faces.Add(detectedFace);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            return new FaceDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImageTypeResult> GetImageTypeAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new ImageTypeResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/vision/v3.2/analyze?visualFeatures=ImageType";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new ImageTypeResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            
            var result = new ImageTypeResult { Success = true };
            if (root.TryGetProperty("imageType", out var imageType))
            {
                var clipArtType = imageType.TryGetProperty("clipArtType", out var clipArt) ? clipArt.GetInt32() : 0;
                var lineDrawType = imageType.TryGetProperty("lineDrawingType", out var lineDraw) ? lineDraw.GetInt32() : 0;
                result.IsClipArt = clipArtType > 0;
                result.IsLineDrawing = lineDrawType > 0;
                result.ClipArtConfidence = clipArtType / 3.0; // Convert 0-3 scale to 0-1
                result.LineDrawingConfidence = lineDrawType / 1.0; // Convert 0-1 scale
                result.ImageType = clipArtType > 0 ? "clipart" : (lineDrawType > 0 ? "linedrawing" : "photo");
            }
            return result;
        }
        catch (Exception ex)
        {
            return new ImageTypeResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<BackgroundRemovalResult> RemoveBackgroundAsync(byte[] imageBytes)
    {
        try
        {
            if (!IsConfigured)
                return new BackgroundRemovalResult { Success = false, ErrorMessage = "Azure Vision not configured" };

            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:segment?api-version=2023-02-01-preview&mode=backgroundRemoval";

            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(apiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                return new BackgroundRemovalResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };
            }

            var resultBytes = await response.Content.ReadAsByteArrayAsync();
            return new BackgroundRemovalResult
            {
                Success = true,
                ImageWithoutBackground = resultBytes,
                Base64Image = Convert.ToBase64String(resultBytes)
            };
        }
        catch (Exception ex)
        {
            return new BackgroundRemovalResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
