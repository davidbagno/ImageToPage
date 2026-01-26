namespace AzureLogin.Shared.Services;

/// <summary>
/// Interface for rendering dynamic Razor/Blazor component markup to HTML
/// </summary>
public interface IDynamicRazorRenderer
{
    /// <summary>
    /// Renders Razor markup to HTML string
    /// </summary>
    /// <param name="razorMarkup">The Razor component markup</param>
    /// <param name="framework">The target framework (Blazor Razor, Blazor MAUI, ASP.NET Razor)</param>
    /// <returns>Rendered HTML result</returns>
    Task<RenderResult> RenderToHtmlAsync(string razorMarkup, string framework = "Blazor Razor");
    
    /// <summary>
    /// Generates a self-contained HTML page for preview from Razor markup
    /// </summary>
    /// <param name="razorMarkup">The Razor component markup</param>
    /// <param name="framework">The target framework</param>
    /// <returns>Complete HTML page string</returns>
    Task<string> GeneratePreviewPageAsync(string razorMarkup, string framework = "Blazor Razor");
    
    /// <summary>
    /// Checks if the renderer supports the given framework
    /// </summary>
    bool SupportsFramework(string framework);
}

/// <summary>
/// Result of dynamic Razor rendering
/// </summary>
public class RenderResult
{
    public bool Success { get; set; }
    public string? Html { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? Warnings { get; set; }
    public RazorParseInfo? ParseInfo { get; set; }
}

/// <summary>
/// Information extracted from parsing Razor markup
/// </summary>
public class RazorParseInfo
{
    public string? ComponentName { get; set; }
    public List<string>? Usings { get; set; }
    public List<string>? Injects { get; set; }
    public List<RazorParameter>? Parameters { get; set; }
    public string? CodeBlock { get; set; }
    public string? MarkupContent { get; set; }
    public List<string>? ChildComponents { get; set; }
    public List<string>? CssClasses { get; set; }
    public bool HasEventHandlers { get; set; }
    public bool HasBindings { get; set; }
}

/// <summary>
/// Parameter information from Razor component
/// </summary>
public class RazorParameter
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
}
