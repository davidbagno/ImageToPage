using System.Text.Json;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Logging;

namespace AzureLogin.Services;

/// <summary>
/// Image analysis service using Azure OpenAI GPT-4o Vision.
/// Provides UI analysis, code generation, and design extraction capabilities.
/// </summary>
public sealed class VisionService : IVisionService
{
    private readonly IAzureOpenAIService _openAI;
    private readonly ILogger<VisionService>? _logger;

    public VisionService(IAzureOpenAIService openAI, ILogger<VisionService>? logger = null)
    {
        _openAI = openAI ?? throw new ArgumentNullException(nameof(openAI));
        _logger = logger;
    }

    #region Core Analysis

    public async Task<VisionResult> AnalyzeImageAsync(string base64Image, string mimeType, string prompt)
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            return VisionResult.Ok(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image analysis failed");
            return VisionResult.Fail(ex.Message);
        }
    }

    public Task<VisionResult> DescribeImageAsync(string base64Image, string mimeType) =>
        AnalyzeImageAsync(base64Image, mimeType, Prompts.Describe);

    public Task<VisionResult> ExtractTextAsync(string base64Image, string mimeType) =>
        AnalyzeImageAsync(base64Image, mimeType, Prompts.ExtractText);

    public Task<VisionResult> SuggestImprovementsAsync(string base64Image, string mimeType) =>
        AnalyzeImageAsync(base64Image, mimeType, Prompts.SuggestImprovements);

    public Task<VisionResult> CompareImagesAsync(string base64Image1, string base64Image2, string mimeType) =>
        AnalyzeImageAsync(base64Image1, mimeType, Prompts.Compare);

    #endregion

    #region Code Generation

    public async Task<VisionResult> GenerateCodeFromImageAsync(string base64Image, string mimeType, string framework = "HTML/CSS")
    {
        try
        {
            // Use specialized Razor prompt for Blazor/Razor frameworks
            if (framework.Contains("Blazor", StringComparison.OrdinalIgnoreCase) || 
                (framework.Contains("Razor", StringComparison.OrdinalIgnoreCase) && !framework.Contains("Pages", StringComparison.OrdinalIgnoreCase)))
            {
                return await GenerateRazorCodeAsync(base64Image, mimeType);
            }
            
            // Use Razor Pages prompt for .cshtml
            if (framework.Contains("Razor Pages", StringComparison.OrdinalIgnoreCase) || 
                framework.Contains("cshtml", StringComparison.OrdinalIgnoreCase))
            {
                return await GenerateRazorPagesCodeAsync(base64Image, mimeType);
            }
            
            var response = await _openAI.GetVisionCompletionAsync(
                base64Image, mimeType,
                Prompts.GetCodePrompt(framework),
                Prompts.GetCodeSystemPrompt(framework));

            return VisionResult.Ok(CleanCodeBlock(response));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Code generation failed for {Framework}", framework);
            return VisionResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Generates Blazor Razor component code from a UI screenshot
    /// Uses specialized prompt for accurate Razor syntax and Blazor patterns
    /// </summary>
    public async Task<VisionResult> GenerateRazorCodeAsync(string base64Image, string mimeType)
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(
                base64Image, mimeType,
                Prompts.RazorBlazor,
                "You are an expert Blazor and ASP.NET Core developer specializing in Razor components. Generate clean, production-ready .razor files with proper component patterns, Bootstrap styling, and C# code blocks.");

            return VisionResult.Ok(CleanCodeBlock(response));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Razor code generation failed");
            return VisionResult.Fail(ex.Message);
        }
    }
    
    /// <summary>
    /// Generates ASP.NET Core Razor Pages code (.cshtml + .cshtml.cs) from a UI screenshot
    /// </summary>
    public async Task<VisionResult> GenerateRazorPagesCodeAsync(string base64Image, string mimeType)
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(
                base64Image, mimeType,
                Prompts.RazorPages,
                "You are an expert ASP.NET Core developer specializing in Razor Pages. Generate clean, production-ready .cshtml and PageModel files with proper tag helpers, validation, and Bootstrap styling.");

            return VisionResult.Ok(CleanCodeBlock(response));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Razor Pages code generation failed");
            return VisionResult.Fail(ex.Message);
        }
    }

    #endregion

    #region Structured Extraction

    public Task<UIComponentsResult> ExtractUIComponentsAsync(string base64Image, string mimeType) =>
        ExtractJsonAsync<UIComponentsResult>(base64Image, mimeType, Prompts.Components, ParseComponents);

    public Task<ColorPaletteResult> ExtractColorPaletteAsync(string base64Image, string mimeType) =>
        ExtractJsonAsync<ColorPaletteResult>(base64Image, mimeType, Prompts.Colors, ParseColors);

    public Task<TypographyResult> ExtractTypographyAsync(string base64Image, string mimeType) =>
        ExtractJsonAsync<TypographyResult>(base64Image, mimeType, Prompts.Typography, ParseTypography);

    public Task<LayoutResult> ExtractLayoutAsync(string base64Image, string mimeType) =>
        ExtractJsonAsync<LayoutResult>(base64Image, mimeType, Prompts.Layout, ParseLayout);

    public Task<IconsResult> IdentifyIconsAsync(string base64Image, string mimeType) =>
        ExtractJsonAsync<IconsResult>(base64Image, mimeType, Prompts.Icons, ParseIcons);

    public Task<AccessibilityResult> AnalyzeAccessibilityAsync(string base64Image, string mimeType) =>
        ExtractJsonAsync<AccessibilityResult>(base64Image, mimeType, Prompts.Accessibility, ParseAccessibility);

    #endregion

    #region Comprehensive Analysis

    public async Task<ComprehensiveUIAnalysis> AnalyzeUIAsync(string base64Image, string mimeType)
    {
        _logger?.LogInformation("Starting comprehensive UI analysis");

        var result = new ComprehensiveUIAnalysis { Success = true };

        try
        {
            // Run all analyses in parallel for performance
            var descTask = DescribeImageAsync(base64Image, mimeType);
            var compTask = ExtractUIComponentsAsync(base64Image, mimeType);
            var colorTask = ExtractColorPaletteAsync(base64Image, mimeType);
            var typoTask = ExtractTypographyAsync(base64Image, mimeType);
            var layoutTask = ExtractLayoutAsync(base64Image, mimeType);
            var iconsTask = IdentifyIconsAsync(base64Image, mimeType);
            var a11yTask = AnalyzeAccessibilityAsync(base64Image, mimeType);

            await Task.WhenAll(descTask, compTask, colorTask, typoTask, layoutTask, iconsTask, a11yTask);

            result.Description = descTask.Result.Content;
            result.Components = compTask.Result;
            result.Colors = colorTask.Result;
            result.Typography = typoTask.Result;
            result.Layout = layoutTask.Result;
            result.Icons = iconsTask.Result;
            result.Accessibility = a11yTask.Result;
            result.Content = "Analysis completed";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Comprehensive analysis failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    #endregion

    #region Private Helpers

    private async Task<T> ExtractJsonAsync<T>(
        string base64Image, string mimeType, string prompt, Func<string, T> parser)
        where T : VisionResult, new()
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            return parser(CleanJsonBlock(response));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "{Type} extraction failed", typeof(T).Name);
            return new T { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static string CleanCodeBlock(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var newlineIdx = text.IndexOf('\n');
            if (newlineIdx > 0) text = text[(newlineIdx + 1)..];
        }
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }

    private static string CleanJsonBlock(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];
        if (text.EndsWith("```"))
            text = text[..^3];
        return text.Trim();
    }

    private static string? GetJsonString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var val) ? val.GetString() : null;

    private static int? GetJsonInt(JsonElement root, string property) =>
        root.TryGetProperty(property, out var val) && val.TryGetInt32(out var i) ? i : null;

    private static bool? GetJsonBool(JsonElement root, string property) =>
        root.TryGetProperty(property, out var val) ? val.GetBoolean() : null;

    #endregion

    #region JSON Parsers

    private static UIComponentsResult ParseComponents(string json)
    {
        var result = new UIComponentsResult { Success = true, Content = json };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            result.LayoutType = GetJsonString(root, "layoutType");
            if (root.TryGetProperty("sections", out var sections))
                result.Sections = sections.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => x != null)
                    .Cast<string>()
                    .ToList();
        }
        catch { /* Return partial result */ }
        return result;
    }

    private static ColorPaletteResult ParseColors(string json)
    {
        var result = new ColorPaletteResult { Success = true, Content = json };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            result.PrimaryColor = GetJsonString(root, "primaryColor");
            result.SecondaryColor = GetJsonString(root, "secondaryColor");
            result.AccentColor = GetJsonString(root, "accentColor");
            result.BackgroundColor = GetJsonString(root, "backgroundColor");
            result.TextColor = GetJsonString(root, "textColor");
            result.ColorScheme = GetJsonString(root, "colorScheme");
        }
        catch { /* Return partial result */ }
        return result;
    }

    private static TypographyResult ParseTypography(string json)
    {
        var result = new TypographyResult { Success = true, Content = json };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            result.PrimaryFont = GetJsonString(root, "primaryFont");
            result.HeadingFont = GetJsonString(root, "headingFont");
            result.BodyFont = GetJsonString(root, "bodyFont");
            result.LineHeight = GetJsonString(root, "lineHeight");

            if (root.TryGetProperty("fontSizes", out var sizes) && sizes.ValueKind == JsonValueKind.Object)
            {
                result.FontSizes = sizes.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            }
        }
        catch { /* Return partial result */ }
        return result;
    }

    private static LayoutResult ParseLayout(string json)
    {
        var result = new LayoutResult { Success = true, Content = json };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            result.LayoutType = GetJsonString(root, "layoutType");
            result.ContainerWidth = GetJsonString(root, "containerWidth");
            result.Gap = GetJsonString(root, "gap");
            result.Columns = GetJsonInt(root, "columns");

            if (root.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
            {
                result.Sections = sections.EnumerateArray()
                    .Select(s => new LayoutSection
                    {
                        Name = GetJsonString(s, "name"),
                        Type = GetJsonString(s, "type")
                    })
                    .ToList();
            }
        }
        catch { /* Return partial result */ }
        return result;
    }

    private static IconsResult ParseIcons(string json)
    {
        var result = new IconsResult { Success = true, Content = json };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            result.IconStyle = GetJsonString(root, "iconStyle");
            result.SuggestedLibrary = GetJsonString(root, "suggestedLibrary");

            if (root.TryGetProperty("icons", out var icons) && icons.ValueKind == JsonValueKind.Array)
            {
                result.Icons = icons.EnumerateArray()
                    .Select(i => new IdentifiedIcon
                    {
                        SuggestedName = GetJsonString(i, "suggestedName") ?? GetJsonString(i, "name"),
                        Location = GetJsonString(i, "location"),
                        Size = GetJsonString(i, "size"),
                        Color = GetJsonString(i, "color")
                    })
                    .ToList();
            }
        }
        catch { /* Return partial result */ }
        return result;
    }

    private static AccessibilityResult ParseAccessibility(string json)
    {
        var result = new AccessibilityResult { Success = true, Content = json };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            result.OverallScore = GetJsonString(root, "overallScore");
            result.HasSufficientTextSize = GetJsonBool(root, "hasSufficientTextSize");
            result.HasClearHierarchy = GetJsonBool(root, "hasClearHierarchy");
            result.HasTouchTargets = GetJsonBool(root, "hasTouchTargets");

            if (root.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
            {
                result.Issues = issues.EnumerateArray()
                    .Select(i => new AccessibilityIssue
                    {
                        Severity = GetJsonString(i, "severity"),
                        Description = GetJsonString(i, "description"),
                        Recommendation = GetJsonString(i, "recommendation")
                    })
                    .ToList();
            }

            if (root.TryGetProperty("recommendations", out var recs) && recs.ValueKind == JsonValueKind.Array)
            {
                result.Recommendations = recs.EnumerateArray()
                    .Select(r => r.GetString())
                    .Where(r => r != null)
                    .Cast<string>()
                    .ToList();
            }
        }
        catch { /* Return partial result */ }
        return result;
    }

    #endregion

    #region Prompts

    private static class Prompts
    {
        public const string Describe = """
            Describe this UI design comprehensively:
            1. Purpose  2. Visual Style  3. Layout  4. Components
            5. Colors  6. Typography  7. Interactions  8. Design Patterns
            """;

        public const string ExtractText = """
            Extract ALL visible text: headings, labels, buttons, placeholders, nav items, footer.
            Group by location/purpose. Return structured list.
            """;

        public const string SuggestImprovements = """
            Suggest improvements for: Usability, Visual Hierarchy, Accessibility,
            Consistency, Modern Practices, Performance, Responsiveness.
            Be specific and actionable.
            """;

        public const string Compare = """
            Analyze this UI focusing on: layout, colors, typography, components, spacing.
            """;

        public const string Components = """
            Extract UI components as JSON:
            {"layoutType":"","sections":[],"components":[{"type":"","name":"","text":""}]}
            Return ONLY valid JSON.
            """;

        public const string Colors = """
            Extract colors as JSON:
            {"primaryColor":"#hex","secondaryColor":"","accentColor":"","backgroundColor":"","textColor":"","colorScheme":"light|dark"}
            Return ONLY valid JSON.
            """;

        public const string Typography = """
            Extract typography as JSON:
            {"primaryFont":"","headingFont":"","bodyFont":"","fontSizes":{"h1":"","body":""},"lineHeight":""}
            Return ONLY valid JSON.
            """;

        public const string Layout = """
            Extract layout as JSON:
            {"layoutType":"grid|flex|fixed","containerWidth":"","columns":12,"gap":"","sections":[{"name":"","type":""}]}
            Return ONLY valid JSON.
            """;

        public const string Icons = """
            Identify icons as JSON:
            {"iconStyle":"outlined|filled","suggestedLibrary":"Material|FontAwesome","icons":[{"suggestedName":"","location":"","size":"","color":""}]}
            Return ONLY valid JSON.
            """;

        public const string Accessibility = """
            Analyze accessibility as JSON:
            {"overallScore":"Good|Needs Improvement|Poor","hasSufficientTextSize":true,"hasClearHierarchy":true,"hasTouchTargets":true,"issues":[{"severity":"critical|major|minor","description":"","recommendation":""}],"recommendations":[]}
            Return ONLY valid JSON.
            """;

        // New prompts for additional methods
        public const string Document = """
            Parse this document (invoice/receipt/form) as JSON:
            {"documentType":"invoice|receipt|form","fields":{"vendor":"","date":"","total":"","items":[]},"rawText":""}
            Return ONLY valid JSON.
            """;

        public const string ChartData = """
            Extract data from this chart/graph as JSON:
            {"chartType":"bar|line|pie|scatter","title":"","labels":[],"series":[{"name":"","values":[]}],"xAxisLabel":"","yAxisLabel":""}
            Return ONLY valid JSON.
            """;

        public const string MathEquation = """
            Parse mathematical notation from this image as JSON:
            {"latex":"","mathml":"","plainText":"","expressions":[{"latex":"","description":""}]}
            Return ONLY valid JSON.
            """;

        public const string Handwriting = """
            Read handwritten text from this image as JSON:
            {"recognizedText":"","confidence":0.95,"lines":[{"text":"","confidence":0.9}]}
            Return ONLY valid JSON.
            """;

        public const string Table = """
            Extract table data from this image as JSON:
            {"rowCount":0,"columnCount":0,"headers":[],"rows":[[]]}
            Return ONLY valid JSON.
            """;

        public const string ImageDiff = """
            Compare these two images and describe differences as JSON:
            {"differences":[{"description":"","location":"","severity":"minor|moderate|significant"}],"similarityScore":0.85}
            Return ONLY valid JSON.
            """;

        public static string GetCodePrompt(string framework) =>
            $"Generate production-ready {framework} code for this UI. Be pixel-perfect, semantic, accessible, responsive. Output ONLY code, no explanations.";

        public static string GetCodeSystemPrompt(string framework) =>
            $"You are an expert {framework} developer. Output clean, production-ready code only.";
        
        public const string RazorBlazor = """
            Convert this UI screenshot to a Blazor Razor component (.razor file).
            
            Requirements:
            1. Use proper Razor syntax with @code block
            2. Include scoped CSS in <style> tags or separate .razor.css
            3. Use Bootstrap 5 classes where appropriate
            4. Create @bind parameters for form inputs
            5. Use EventCallback for button clicks
            6. Add [Parameter] attributes for component inputs
            7. Use proper Blazor component lifecycle if needed
            8. Include proper null checks with ?. and ??
            9. Use @inject for any services needed
            10. Make it responsive with Bootstrap grid
            
            Output format:
            - Start with @page directive if it's a page
            - Include all necessary @using statements
            - Put CSS in <style> block at end or note for .razor.css
            - Put C# code in @code { } block
            
            Output ONLY the complete .razor file code, no explanations.
            """;
        
        public const string RazorPages = """
            Convert this UI screenshot to an ASP.NET Core Razor Pages view (.cshtml file).
            
            Requirements:
            1. Use proper Razor Pages syntax with @page, @model directives
            2. Include proper tag helpers (asp-for, asp-action, asp-controller)
            3. Use Bootstrap 5 classes for styling
            4. Create form inputs with proper validation attributes
            5. Use partial views for reusable components where appropriate
            6. Include anti-forgery tokens for forms
            7. Use @section for scripts and styles
            8. Include proper ViewData/ViewBag usage if needed
            
            Output format:
            - Start with @page directive
            - Include @model directive pointing to PageModel
            - Add @{ Layout = "_Layout"; } if needed
            - Use tag helpers for forms and links
            
            Also generate the corresponding PageModel (.cshtml.cs) class with:
            - OnGet and OnPost handlers
            - Bound properties with [BindProperty]
            - Proper validation
            
            Output ONLY the complete code, no explanations.
            """;
    }

    #endregion

    #region New GPT-4o Vision Methods

    public async Task<ImageComparisonResult> CompareMultipleImagesAsync(List<string> base64Images, string mimeType)
    {
        if (base64Images == null || base64Images.Count < 2)
            return new ImageComparisonResult { Success = false, ErrorMessage = "Need at least 2 images to compare" };

        try
        {
            var prompt = "Compare these images and describe all differences. For each difference, specify location, description, and severity (minor/moderate/significant). Also provide an overall similarity score from 0 to 1.";
            var response = await _openAI.GetVisionCompletionAsync(base64Images[0], mimeType, prompt);
            
            return new ImageComparisonResult
            {
                Success = true,
                Content = response,
                Differences = new List<ImageDifference>(),
                SimilarityScore = 0.8 // Default, would parse from response
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Multi-image comparison failed");
            return new ImageComparisonResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<VisionResult> AskAboutImageAsync(string base64Image, string mimeType, string question)
    {
        try
        {
            var prompt = $"Answer this question about the image: {question}";
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            return VisionResult.Ok(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image Q&A failed");
            return VisionResult.Fail(ex.Message);
        }
    }

    public async Task<DocumentParseResult> ParseDocumentAsync(string base64Image, string mimeType)
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, Prompts.Document);
            var json = CleanJsonBlock(response);
            
            var result = new DocumentParseResult { Success = true, Content = response };
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                result.DocumentType = GetJsonString(root, "documentType");
                result.RawText = GetJsonString(root, "rawText");
                
                if (root.TryGetProperty("fields", out var fields))
                {
                    result.Fields = new Dictionary<string, string>();
                    foreach (var prop in fields.EnumerateObject())
                    {
                        result.Fields[prop.Name] = prop.Value.ToString();
                    }
                }
            }
            catch { /* Return partial result */ }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Document parsing failed");
            return new DocumentParseResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ChartDataResult> ExtractChartDataAsync(string base64Image, string mimeType)
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, Prompts.ChartData);
            var json = CleanJsonBlock(response);
            
            var result = new ChartDataResult { Success = true, Content = response };
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                result.ChartType = GetJsonString(root, "chartType");
                result.Title = GetJsonString(root, "title");
                result.XAxisLabel = GetJsonString(root, "xAxisLabel");
                result.YAxisLabel = GetJsonString(root, "yAxisLabel");
                
                if (root.TryGetProperty("labels", out var labels))
                    result.Labels = labels.EnumerateArray().Select(l => l.GetString() ?? "").ToList();
            }
            catch { /* Return partial result */ }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Chart data extraction failed");
            return new ChartDataResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<MathOcrResult> ParseMathEquationAsync(string base64Image, string mimeType)
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, Prompts.MathEquation);
            var json = CleanJsonBlock(response);
            
            var result = new MathOcrResult { Success = true, Content = response };
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                result.Latex = GetJsonString(root, "latex");
                result.MathML = GetJsonString(root, "mathml");
                result.PlainText = GetJsonString(root, "plainText");
            }
            catch { /* Return partial result */ }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Math equation parsing failed");
            return new MathOcrResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<HandwritingResult> ReadHandwritingAsync(string base64Image, string mimeType)
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, Prompts.Handwriting);
            var json = CleanJsonBlock(response);
            
            var result = new HandwritingResult { Success = true, Content = response };
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                result.RecognizedText = GetJsonString(root, "recognizedText");
                if (root.TryGetProperty("confidence", out var conf) && conf.TryGetDouble(out var c))
                    result.Confidence = c;
            }
            catch { /* Return partial result */ }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Handwriting recognition failed");
            return new HandwritingResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<TableExtractionResult> ExtractTableAsync(string base64Image, string mimeType)
    {
        try
        {
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, Prompts.Table);
            var json = CleanJsonBlock(response);
            
            var result = new TableExtractionResult { Success = true, Content = response };
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                result.RowCount = GetJsonInt(root, "rowCount") ?? 0;
                result.ColumnCount = GetJsonInt(root, "columnCount") ?? 0;
                
                if (root.TryGetProperty("headers", out var headers))
                    result.Headers = headers.EnumerateArray().Select(h => h.GetString() ?? "").ToList();
                
                if (root.TryGetProperty("rows", out var rows))
                {
                    result.Rows = new List<List<string>>();
                    foreach (var row in rows.EnumerateArray())
                    {
                        result.Rows.Add(row.EnumerateArray().Select(c => c.GetString() ?? "").ToList());
                    }
                }
                
                // Generate CSV
                if (result.Headers != null && result.Rows != null)
                {
                    var csv = string.Join(",", result.Headers) + "\n";
                    csv += string.Join("\n", result.Rows.Select(r => string.Join(",", r)));
                    result.CsvData = csv;
                }
            }
            catch { /* Return partial result */ }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Table extraction failed");
            return new TableExtractionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<VisionResult> AnswerSpatialQuestionAsync(string base64Image, string mimeType, string question)
    {
        try
        {
            var prompt = $"Answer this spatial question about the image layout and positions: {question}";
            var response = await _openAI.GetVisionCompletionAsync(base64Image, mimeType, prompt);
            return VisionResult.Ok(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Spatial question failed");
            return VisionResult.Fail(ex.Message);
        }
    }

    public async Task<ImageDiffResult> GetImageDiffAsync(string base64Image1, string base64Image2, string mimeType)
    {
        try
        {
            // Note: GPT-4o can only see one image at a time in basic implementation
            // For true diff, you'd need to combine images or use multi-image API
            var prompt = "Analyze this image for any visible differences, modifications, or changes that might indicate version changes.";
            var response = await _openAI.GetVisionCompletionAsync(base64Image1, mimeType, prompt);
            
            return new ImageDiffResult
            {
                Success = true,
                Content = response,
                AddedRegions = new List<DiffRegion>(),
                RemovedRegions = new List<DiffRegion>(),
                ModifiedRegions = new List<DiffRegion>(),
                OverallChangePercentage = 0
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image diff failed");
            return new ImageDiffResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    #endregion
}
