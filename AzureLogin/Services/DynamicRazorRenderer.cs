using System.Text;
using System.Text.RegularExpressions;
using AzureLogin.Shared.Services;
using Microsoft.Extensions.Logging;

namespace AzureLogin.Services;

/// <summary>
/// Service for rendering dynamic Razor/Blazor component markup to HTML for preview.
/// Uses a transpilation approach to convert Razor syntax to valid HTML for display.
/// </summary>
public class DynamicRazorRenderer : IDynamicRazorRenderer
{
    private readonly ILogger<DynamicRazorRenderer>? _logger;
    
    private static readonly string[] SupportedFrameworks = 
    {
        "Blazor Razor",
        "Blazor MAUI", 
        "ASP.NET Razor"
    };

    public DynamicRazorRenderer(ILogger<DynamicRazorRenderer>? logger = null)
    {
        _logger = logger;
    }

    public bool SupportsFramework(string framework)
    {
        return SupportedFrameworks.Contains(framework, StringComparer.OrdinalIgnoreCase);
    }

    public Task<RenderResult> RenderToHtmlAsync(string razorMarkup, string framework = "Blazor Razor")
    {
        if (string.IsNullOrWhiteSpace(razorMarkup))
        {
            return Task.FromResult(new RenderResult
            {
                Success = false,
                ErrorMessage = "No Razor markup provided"
            });
        }

        try
        {
            var parseInfo = ParseRazorMarkup(razorMarkup);
            var html = TranspileToHtml(razorMarkup, parseInfo, framework);
            
            return Task.FromResult(new RenderResult
            {
                Success = true,
                Html = html,
                ParseInfo = parseInfo,
                Warnings = GetRenderWarnings(parseInfo)
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to render Razor markup");
            return Task.FromResult(new RenderResult
            {
                Success = false,
                ErrorMessage = $"Rendering failed: {ex.Message}"
            });
        }
    }

    public async Task<string> GeneratePreviewPageAsync(string razorMarkup, string framework = "Blazor Razor")
    {
        var result = await RenderToHtmlAsync(razorMarkup, framework);
        
        if (!result.Success || string.IsNullOrEmpty(result.Html))
        {
            return GenerateErrorPage(result.ErrorMessage ?? "Unknown error");
        }

        return GeneratePreviewHtmlPage(result.Html, result.ParseInfo, framework);
    }

    private RazorParseInfo ParseRazorMarkup(string markup)
    {
        var info = new RazorParseInfo
        {
            Usings = new List<string>(),
            Injects = new List<string>(),
            Parameters = new List<RazorParameter>(),
            ChildComponents = new List<string>(),
            CssClasses = new List<string>()
        };

        // Extract @using directives
        var usingMatches = Regex.Matches(markup, @"@using\s+([\w\.]+)", RegexOptions.Multiline);
        foreach (Match match in usingMatches)
        {
            info.Usings.Add(match.Groups[1].Value);
        }

        // Extract @inject directives
        var injectMatches = Regex.Matches(markup, @"@inject\s+(\w+)\s+(\w+)", RegexOptions.Multiline);
        foreach (Match match in injectMatches)
        {
            info.Injects.Add($"{match.Groups[1].Value} {match.Groups[2].Value}");
        }

        // Extract @code block
        var codeMatch = Regex.Match(markup, @"@code\s*\{([\s\S]*?)\}(?=\s*$|\s*@|\s*<)", RegexOptions.Multiline);
        if (codeMatch.Success)
        {
            info.CodeBlock = codeMatch.Groups[1].Value.Trim();
            
            // Extract [Parameter] properties
            var paramMatches = Regex.Matches(info.CodeBlock, @"\[Parameter\]\s*(?:\[[\w\(\)]+\]\s*)*public\s+(\w+[\?]?)\s+(\w+)\s*\{");
            foreach (Match match in paramMatches)
            {
                info.Parameters.Add(new RazorParameter
                {
                    Type = match.Groups[1].Value,
                    Name = match.Groups[2].Value
                });
            }
        }

        // Extract @page directive for component name hint
        var pageMatch = Regex.Match(markup, @"@page\s+""([^""]+)""");
        if (pageMatch.Success)
        {
            var route = pageMatch.Groups[1].Value;
            info.ComponentName = route.Split('/').LastOrDefault()?.Replace("-", "") ?? "Component";
        }

        // Extract markup content (remove directives and code blocks)
        var markupContent = markup;
        markupContent = Regex.Replace(markupContent, @"@page\s+""[^""]+""", "");
        markupContent = Regex.Replace(markupContent, @"@using\s+[\w\.]+", "");
        markupContent = Regex.Replace(markupContent, @"@inject\s+\w+\s+\w+", "");
        markupContent = Regex.Replace(markupContent, @"@inherits\s+\w+", "");
        markupContent = Regex.Replace(markupContent, @"@implements\s+[\w\<\>]+", "");
        markupContent = Regex.Replace(markupContent, @"@code\s*\{[\s\S]*?\}(?=\s*$|\s*@|\s*<)", "", RegexOptions.Multiline);
        info.MarkupContent = markupContent.Trim();

        // Find child components (PascalCase tags)
        var componentMatches = Regex.Matches(markup, @"<([A-Z][a-zA-Z0-9]+)[\s/>]");
        foreach (Match match in componentMatches)
        {
            var componentName = match.Groups[1].Value;
            if (!info.ChildComponents.Contains(componentName))
            {
                info.ChildComponents.Add(componentName);
            }
        }

        // Extract CSS classes
        var classMatches = Regex.Matches(markup, @"class=""([^""]+)""");
        foreach (Match match in classMatches)
        {
            var classes = match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var cls in classes)
            {
                if (!info.CssClasses.Contains(cls))
                {
                    info.CssClasses.Add(cls);
                }
            }
        }

        // Check for event handlers
        info.HasEventHandlers = Regex.IsMatch(markup, @"@on\w+=""");
        
        // Check for bindings
        info.HasBindings = Regex.IsMatch(markup, @"@bind[-\w]*=""");

        return info;
    }

    private string TranspileToHtml(string markup, RazorParseInfo parseInfo, string _ = "Blazor Razor")
    {
        var html = parseInfo.MarkupContent ?? markup;

        // Remove remaining Razor directives at the top
        html = Regex.Replace(html, @"^@\w+.*$", "", RegexOptions.Multiline);
        
        // Convert Razor expressions to placeholder spans
        // @Variable -> <span class="razor-var">Variable</span>
        html = Regex.Replace(html, @"@(\w+)(?![<\w])", m => 
            $"<span class=\"razor-var\" data-binding=\"{m.Groups[1].Value}\">[{m.Groups[1].Value}]</span>");
        
        // @Model.Property -> placeholder
        html = Regex.Replace(html, @"@([\w\.]+)", m =>
            $"<span class=\"razor-var\" data-binding=\"{m.Groups[1].Value}\">[{m.Groups[1].Value}]</span>");

        // Convert @if blocks to visible divs with condition indicator
        html = Regex.Replace(html, @"@if\s*\(([^)]+)\)\s*\{([^}]*)\}", m =>
            $"<div class=\"razor-conditional\" data-condition=\"{EscapeHtml(m.Groups[1].Value)}\">{m.Groups[2].Value}</div>");

        // Convert @foreach to repeated placeholder
        html = Regex.Replace(html, @"@foreach\s*\([^)]+\)\s*\{([^}]*)\}", m =>
            $"<div class=\"razor-loop\" data-loop=\"foreach\">{m.Groups[1].Value}<div class=\"razor-loop-hint\">... (repeated items)</div></div>");

        // Convert @for loops similarly
        html = Regex.Replace(html, @"@for\s*\([^)]+\)\s*\{([^}]*)\}", m =>
            $"<div class=\"razor-loop\" data-loop=\"for\">{m.Groups[1].Value}<div class=\"razor-loop-hint\">... (repeated items)</div></div>");

        // Handle event handlers - convert to data attributes for visualization
        html = Regex.Replace(html, @"@(on\w+)=""([^""]+)""", m =>
            $"data-event-{m.Groups[1].Value}=\"{EscapeHtml(m.Groups[2].Value)}\" onclick=\"handleRazorEvent(this, '{m.Groups[1].Value}')\"");

        // Handle @bind directives
        html = Regex.Replace(html, @"@bind(-[\w]+)?=""([^""]+)""", m =>
            $"data-bind=\"{m.Groups[2].Value}\" class=\"razor-bound\"");

        // Convert Blazor components to div placeholders with styling
        foreach (var component in parseInfo.ChildComponents ?? new List<string>())
        {
            // Self-closing components
            html = Regex.Replace(html, $@"<{component}\s*([^>]*)/\s*>", m =>
                $"<div class=\"razor-component\" data-component=\"{component}\"><div class=\"component-header\">🧩 {component}</div><div class=\"component-props\">{FormatComponentProps(m.Groups[1].Value)}</div></div>");
            
            // Components with content
            html = Regex.Replace(html, $@"<{component}\s*([^>]*)>([\s\S]*?)</{component}>", m =>
                $"<div class=\"razor-component\" data-component=\"{component}\"><div class=\"component-header\">🧩 {component}</div><div class=\"component-props\">{FormatComponentProps(m.Groups[1].Value)}</div><div class=\"component-content\">{m.Groups[2].Value}</div></div>");
        }

        // Clean up any remaining @ symbols that weren't processed
        html = Regex.Replace(html, @"@\{[^}]*\}", "<span class=\"razor-code-block\">[code block]</span>");
        
        // Remove empty lines and trim
        html = Regex.Replace(html, @"^\s*$\n", "", RegexOptions.Multiline);
        html = html.Trim();

        return html;
    }

    private string FormatComponentProps(string propsString)
    {
        if (string.IsNullOrWhiteSpace(propsString)) return "";
        
        var sb = new StringBuilder("<ul class=\"prop-list\">");
        var propMatches = Regex.Matches(propsString, @"(\w+)=""([^""]*)""");
        foreach (Match match in propMatches)
        {
            sb.Append($"<li><strong>{match.Groups[1].Value}:</strong> {EscapeHtml(match.Groups[2].Value)}</li>");
        }
        sb.Append("</ul>");
        return sb.ToString();
    }

    private List<string> GetRenderWarnings(RazorParseInfo parseInfo)
    {
        var warnings = new List<string>();
        
        if (parseInfo.HasEventHandlers)
        {
            warnings.Add("Event handlers (@onclick, @onchange, etc.) are shown as indicators but won't execute");
        }
        
        if (parseInfo.HasBindings)
        {
            warnings.Add("Data bindings (@bind) are visualized but won't be interactive");
        }
        
        if (parseInfo.ChildComponents?.Count > 0)
        {
            warnings.Add($"Child components ({string.Join(", ", parseInfo.ChildComponents)}) are shown as placeholders");
        }
        
        if (parseInfo.Injects?.Count > 0)
        {
            warnings.Add("Injected services are not available in preview");
        }

        return warnings;
    }

    private string GeneratePreviewHtmlPage(string html, RazorParseInfo? parseInfo, string framework)
    {
        var warningsHtml = "";
        if (parseInfo != null)
        {
            var warnings = GetRenderWarnings(parseInfo);
            if (warnings.Count > 0)
            {
                warningsHtml = $@"
                <div class=""preview-warnings"">
                    <div class=""warning-header"">⚠️ Preview Limitations</div>
                    <ul>
                        {string.Join("", warnings.Select(w => $"<li>{w}</li>"))}
                    </ul>
                </div>";
            }
        }

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{framework} Preview</title>
    <script src=""https://cdn.tailwindcss.com""></script>
    <link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css"" rel=""stylesheet"">
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"" rel=""stylesheet"">
    <style>
        * {{ box-sizing: border-box; }}
        body {{ 
            margin: 0; 
            padding: 16px; 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            min-height: 100vh;
            background: #ffffff;
        }}
        
        /* Razor Preview Styles */
        .preview-warnings {{
            background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);
            border: 1px solid #f59e0b;
            border-radius: 8px;
            padding: 12px 16px;
            margin-bottom: 16px;
            font-size: 13px;
        }}
        .preview-warnings .warning-header {{
            font-weight: 600;
            color: #92400e;
            margin-bottom: 8px;
        }}
        .preview-warnings ul {{
            margin: 0;
            padding-left: 20px;
            color: #78350f;
        }}
        .preview-warnings li {{
            margin: 4px 0;
        }}
        
        .razor-var {{
            background: linear-gradient(135deg, #ddd6fe 0%, #c4b5fd 100%);
            color: #5b21b6;
            padding: 2px 6px;
            border-radius: 4px;
            font-family: 'SF Mono', Monaco, monospace;
            font-size: 0.85em;
            font-weight: 500;
        }}
        
        .razor-conditional {{
            border-left: 3px solid #3b82f6;
            background: #eff6ff;
            padding: 8px 12px;
            margin: 8px 0;
            border-radius: 0 6px 6px 0;
        }}
        .razor-conditional::before {{
            content: '🔀 @if condition';
            display: block;
            font-size: 11px;
            color: #1d4ed8;
            font-weight: 600;
            margin-bottom: 6px;
            font-family: monospace;
        }}
        
        .razor-loop {{
            border-left: 3px solid #10b981;
            background: #ecfdf5;
            padding: 8px 12px;
            margin: 8px 0;
            border-radius: 0 6px 6px 0;
        }}
        .razor-loop::before {{
            content: '🔄 @foreach loop';
            display: block;
            font-size: 11px;
            color: #047857;
            font-weight: 600;
            margin-bottom: 6px;
            font-family: monospace;
        }}
        .razor-loop-hint {{
            font-size: 11px;
            color: #059669;
            font-style: italic;
            margin-top: 4px;
        }}
        
        .razor-component {{
            border: 2px dashed #8b5cf6;
            background: linear-gradient(135deg, #faf5ff 0%, #f3e8ff 100%);
            border-radius: 8px;
            padding: 12px;
            margin: 8px 0;
        }}
        .razor-component .component-header {{
            font-weight: 700;
            color: #6d28d9;
            font-size: 14px;
            margin-bottom: 8px;
            padding-bottom: 6px;
            border-bottom: 1px solid #c4b5fd;
        }}
        .razor-component .component-props {{
            font-size: 12px;
        }}
        .razor-component .prop-list {{
            margin: 4px 0;
            padding-left: 16px;
            color: #7c3aed;
        }}
        .razor-component .component-content {{
            margin-top: 8px;
            padding-top: 8px;
            border-top: 1px dashed #c4b5fd;
        }}
        
        .razor-bound {{
            outline: 2px solid #f59e0b !important;
            outline-offset: 2px;
        }}
        .razor-bound::after {{
            content: ' 🔗';
            font-size: 10px;
        }}
        
        .razor-code-block {{
            background: #374151;
            color: #e5e7eb;
            padding: 2px 8px;
            border-radius: 4px;
            font-family: monospace;
            font-size: 12px;
        }}
        
        [data-event-onclick] {{
            cursor: pointer;
            position: relative;
        }}
        [data-event-onclick]::after {{
            content: ' ⚡';
            font-size: 10px;
        }}
        
        .preview-badge {{
            position: fixed;
            top: 8px;
            right: 8px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 6px 12px;
            border-radius: 20px;
            font-size: 11px;
            font-weight: 600;
            z-index: 1000;
            box-shadow: 0 2px 10px rgba(102, 126, 234, 0.3);
        }}
    </style>
</head>
<body>
    <div class=""preview-badge"">🔮 {framework} Preview</div>
    {warningsHtml}
    <div class=""preview-content"">
        {html}
    </div>
    <script>
        function handleRazorEvent(element, eventType) {{
            const handler = element.getAttribute('data-event-' + eventType);
            console.log('Razor event triggered:', eventType, handler);
            
            // Visual feedback
            element.style.transform = 'scale(0.98)';
            setTimeout(() => element.style.transform = '', 100);
            
            // Show toast notification
            const toast = document.createElement('div');
            toast.style.cssText = 'position:fixed;bottom:20px;left:50%;transform:translateX(-50%);background:#374151;color:white;padding:8px 16px;border-radius:8px;font-size:13px;z-index:10000;box-shadow:0 4px 12px rgba(0,0,0,0.15);';
            toast.textContent = '⚡ ' + eventType + ': ' + handler + ' (preview mode)';
            document.body.appendChild(toast);
            setTimeout(() => toast.remove(), 2000);
        }}
    </script>
</body>
</html>";
    }

    private string GenerateErrorPage(string errorMessage)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ 
            font-family: -apple-system, sans-serif; 
            padding: 40px; 
            background: #fef2f2;
            color: #991b1b;
        }}
        .error-box {{
            background: white;
            border: 1px solid #fca5a5;
            border-radius: 12px;
            padding: 24px;
            max-width: 500px;
            margin: 0 auto;
            box-shadow: 0 4px 12px rgba(239, 68, 68, 0.1);
        }}
        h2 {{ color: #dc2626; margin-top: 0; }}
    </style>
</head>
<body>
    <div class=""error-box"">
        <h2>⚠️ Preview Error</h2>
        <p>{EscapeHtml(errorMessage)}</p>
        <p style=""font-size: 14px; color: #6b7280;"">The Razor markup could not be rendered. Please check the syntax and try again.</p>
    </div>
</body>
</html>";
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
