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
        if (!IsConfigured) return new ImageCaptionResult { Success = false, ErrorMessage = "Azure Vision not configured" };
        try
        {
            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=caption";
            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var response = await _httpClient.PostAsync(apiUrl, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return new ImageCaptionResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };
            using var doc = JsonDocument.Parse(body);
            var caption = doc.RootElement.GetProperty("caption");
            return new ImageCaptionResult
            {
                Success = true,
                Caption = caption.GetProperty("text").GetString(),
                Confidence = caption.GetProperty("confidence").GetDouble()
            };
        }
        catch (Exception ex)
        {
            return new ImageCaptionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImageTagsResult> GetImageTagsAsync(byte[] imageBytes)
    {
        if (!IsConfigured) return new ImageTagsResult { Success = false, ErrorMessage = "Azure Vision not configured" };
        try
        {
            var apiUrl = $"{_endpoint.TrimEnd('/')}/computervision/imageanalysis:analyze?api-version=2024-02-01&features=tags";
            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var response = await _httpClient.PostAsync(apiUrl, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return new ImageTagsResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };
            using var doc = JsonDocument.Parse(body);
            var tagArray = doc.RootElement.GetProperty("tags");
            var tags = tagArray.EnumerateArray()
                .Select(t => new ImageTag { Name = t.GetProperty("name").GetString(), Confidence = t.GetProperty("confidence").GetDouble() })
                .ToList();
            return new ImageTagsResult { Success = true, Tags = tags };
        }
        catch (Exception ex)
        {
            return new ImageTagsResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<BrandDetectionResult> DetectBrandsAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeV32Async(imageBytes, "Brands");
        if (doc == null) return new BrandDetectionResult { Success = false, ErrorMessage = "Azure Vision not configured" };
        try
        {
            var brands = doc.RootElement.GetProperty("brands");
            var list = new List<DetectedBrand>();
            foreach (var b in brands.EnumerateArray())
            {
                list.Add(new DetectedBrand
                {
                    Name = b.GetProperty("name").GetString(),
                    Confidence = b.GetProperty("confidence").GetDouble(),
                    X = b.GetProperty("rectangle").GetProperty("x").GetInt32(),
                    Y = b.GetProperty("rectangle").GetProperty("y").GetInt32(),
                    Width = b.GetProperty("rectangle").GetProperty("w").GetInt32(),
                    Height = b.GetProperty("rectangle").GetProperty("h").GetInt32()
                });
            }
            return new BrandDetectionResult { Success = true, Brands = list };
        }
        catch (Exception ex)
        {
            return new BrandDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<CategoryResult> GetImageCategoryAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeV32Async(imageBytes, "Categories");
        if (doc == null) return new CategoryResult { Success = false, ErrorMessage = "Azure Vision not configured" };
        try
        {
            var cats = doc.RootElement.GetProperty("categories");
            var list = cats.EnumerateArray().Select(c => new ImageCategory { Name = c.GetProperty("name").GetString(), Confidence = c.GetProperty("score").GetDouble() }).ToList();
            return new CategoryResult { Success = true, PrimaryCategory = list.FirstOrDefault()?.Name, Categories = list };
        }
        catch (Exception ex)
        {
            return new CategoryResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<AdultContentResult> DetectAdultContentAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeV32Async(imageBytes, "Adult");
        if (doc == null) return new AdultContentResult { Success = false, ErrorMessage = "Azure Vision not configured" };
        try
        {
            var adult = doc.RootElement.GetProperty("adult");
            return new AdultContentResult
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
            return new AdultContentResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<FaceDetectionResult> DetectFacesAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeV32Async(imageBytes, "Faces");
        if (doc == null) return new FaceDetectionResult { Success = false, ErrorMessage = "Azure Vision not configured" };
        try
        {
            var faces = doc.RootElement.GetProperty("faces");
            var list = new List<DetectedFace>();
            foreach (var f in faces.EnumerateArray())
            {
                list.Add(new DetectedFace
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
            return new FaceDetectionResult { Success = true, Faces = list };
        }
        catch (Exception ex)
        {
            return new FaceDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImageTypeResult> GetImageTypeAsync(byte[] imageBytes)
    {
        var doc = await AnalyzeV32Async(imageBytes, "ImageType");
        if (doc == null) return new ImageTypeResult { Success = false, ErrorMessage = "Azure Vision not configured" };
        try
        {
            var type = doc.RootElement.GetProperty("imageType");
            return new ImageTypeResult
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
            return new ImageTypeResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public Task<BackgroundRemovalResult> RemoveBackgroundAsync(byte[] imageBytes)
    {
        return Task.FromResult(new BackgroundRemovalResult { Success = false, ErrorMessage = "Background removal not supported in this build" });
    }

    private async Task<JsonDocument?> AnalyzeV32Async(byte[] imageBytes, string visualFeatures)
    {
        if (!IsConfigured) return null;
        var url = $"{_endpoint.TrimEnd('/')}/vision/v3.2/analyze?visualFeatures={visualFeatures}";
        using var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var response = await _httpClient.PostAsync(url, content);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }
}
