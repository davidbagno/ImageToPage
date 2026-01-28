using AzureLogin.Shared.Services;
using Microsoft.JSInterop;

namespace AzureLogin.Web.Services;

/// <summary>
/// Web implementation of IUrlLauncher - opens URL in a centered popup window
/// Injects device code directly into the form field with robust retry logic
/// </summary>
public sealed class WebUrlLauncher(IJSRuntime jsRuntime) : IUrlLauncher
{
    private const int WindowWidth = 500;
    private const int WindowHeight = 700;

    public async Task OpenUrlAsync(string url, string? code = null)
    {
        try
        {
            // Copy code to clipboard first as backup
            if (!string.IsNullOrEmpty(code))
            {
                try { await jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", code); } catch { }
            }
            
            // Use URL with otc parameter
            var finalUrl = url;
            if (!string.IsNullOrEmpty(code))
            {
                var separator = url.Contains('?') ? "&" : "?";
                finalUrl = $"{url}{separator}otc={Uri.EscapeDataString(code)}";
            }
            
            var escapedCode = code?.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") ?? "";
            
            var script = $@"
(function() {{
    const w = {WindowWidth}, h = {WindowHeight};
    const left = window.screenLeft + Math.round((window.innerWidth - w) / 2);
    const top = window.screenTop + Math.round((window.innerHeight - h) / 2);
    const features = `width=${{w}},height=${{h}},left=${{left}},top=${{top}},toolbar=no,menubar=no,scrollbars=no,resizable=yes,location=no,status=no`;
    const win = window.open('{finalUrl}', 'AzureLogin_' + Date.now(), features);
    
    if (!win) {{
        alert('Please allow popups for this site to complete sign-in.');
        return;
    }}
    
    win.focus();
    
    const deviceCode = '{escapedCode}';
    let filled = false;
    let lastUrl = '';
    
    function injectCode() {{
        if (filled) return true;
        
        try {{
            const doc = win.document;
            if (!doc || !doc.body) return false;
            
            // Hide scrollbars
            if (!doc.getElementById('_appStyles')) {{
                const s = doc.createElement('style');
                s.id = '_appStyles';
                s.textContent = 'html,body{{overflow:hidden!important;scrollbar-width:none!important;-ms-overflow-style:none!important}}::-webkit-scrollbar{{display:none!important}}';
                doc.head.appendChild(s);
            }}
            
            if (!deviceCode) return false;
            
            // Find input field - try many selectors
            const input = doc.querySelector('input[name=""otc""]') ||
                          doc.querySelector('input#otc') ||
                          doc.querySelector('input#i0116') ||
                          doc.querySelector('input[type=""text""][maxlength]') ||
                          doc.querySelector('input[type=""text""]') ||
                          doc.querySelector('input[placeholder*=""code"" i]') ||
                          doc.querySelector('input[aria-label*=""code"" i]') ||
                          doc.querySelector('input.form-control') ||
                          doc.querySelector('input:not([type=""hidden""]):not([type=""submit""]):not([type=""password""]):not([type=""checkbox""])');
            
            if (!input || input.offsetParent === null) return false;
            
            // Clear and focus
            input.focus();
            input.select();
            input.value = '';
            
            // Set value using native setter for React/Angular compatibility
            const setter = Object.getOwnPropertyDescriptor(win.HTMLInputElement.prototype, 'value').set;
            setter.call(input, deviceCode);
            
            // Trigger all possible events
            ['input', 'change', 'keydown', 'keyup', 'keypress'].forEach(function(evt) {{
                input.dispatchEvent(new win.Event(evt, {{ bubbles: true, cancelable: true }}));
            }});
            
            // Also dispatch keyboard events with key data
            input.dispatchEvent(new win.KeyboardEvent('keydown', {{ bubbles: true, key: 'a', keyCode: 65 }}));
            input.dispatchEvent(new win.KeyboardEvent('keyup', {{ bubbles: true, key: 'a', keyCode: 65 }}));
            
            filled = true;
            
            // Focus submit button after short delay
            setTimeout(function() {{
                const btn = doc.querySelector('input[type=""submit""]') ||
                            doc.querySelector('button[type=""submit""]') ||
                            doc.querySelector('button#idSIButton9') ||
                            doc.querySelector('button.primary') ||
                            doc.querySelector('button[data-testid]');
                if (btn) btn.focus();
            }}, 150);
            
            return true;
        }} catch(e) {{
            // Cross-origin - can't access
            return false;
        }}
    }}
    
    // Monitor loop - handles code injection, page changes, and success detection
    let attempts = 0;
    const checkInterval = setInterval(function() {{
        try {{
            if (win.closed) {{
                clearInterval(checkInterval);
                return;
            }}
            
            try {{
                const currentUrl = win.location.href;
                
                // Re-inject if page changed
                if (currentUrl !== lastUrl) {{
                    lastUrl = currentUrl;
                    filled = false;
                    attempts = 0;
                }}
                
                // Try to inject code
                if (!filled && attempts < 50) {{
                    attempts++;
                    injectCode();
                }}
                
                // Check for success message
                const doc = win.document;
                const bodyText = doc.body ? doc.body.innerText.toLowerCase() : '';
                if (bodyText.includes('you have signed in') || 
                    bodyText.includes('you are signed in') ||
                    bodyText.includes('you can close this') ||
                    bodyText.includes('successfully signed in')) {{
                    clearInterval(checkInterval);
                    setTimeout(function() {{ try {{ win.close(); }} catch(e) {{}} }}, 1200);
                }}
            }} catch(e) {{
                // Cross-origin - can't access, just wait
            }}
        }} catch(e) {{
            clearInterval(checkInterval);
        }}
    }}, 300);
    
    // Stop checking after 3 minutes
    setTimeout(function() {{ clearInterval(checkInterval); }}, 180000);
}})();
            ";
            
            await jsRuntime.InvokeVoidAsync("eval", script);
        }
        catch (JSException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Opens HTML content in a new browser window
    /// </summary>
    public async Task<bool> OpenHtmlPreviewAsync(string htmlContent, string title = "Preview")
    {
        try
        {
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(htmlContent));
            var escapedTitle = title.Replace("'", "\\'").Replace("\"", "\\\"");
            
            await jsRuntime.InvokeVoidAsync("eval", $@"
                (function() {{
                    var win = window.open('', '_blank', 'width=1200,height=800,scrollbars=yes,resizable=yes');
                    if (win) {{
                        win.document.open();
                        win.document.write(atob('{base64}'));
                        win.document.close();
                        win.document.title = '{escapedTitle}';
                    }} else {{
                        alert('Please allow popups for this site to view the preview.');
                    }}
                }})();
            ");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebUrlLauncher] OpenHtmlPreviewAsync error: {ex.Message}");
            return false;
        }
    }
}
