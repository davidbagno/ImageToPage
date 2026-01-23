using AzureLogin.Shared.Services;
using Microsoft.Maui.Controls.Shapes;

namespace AzureLogin.Services;

/// <summary>
/// MAUI implementation of IUrlLauncher - opens URL in a centered popup window with WebView
/// Injects device code directly into the form field with robust retry logic
/// </summary>
public class MauiUrlLauncher : IUrlLauncher
{
    public async Task OpenUrlAsync(string url, string? code = null)
    {
        try
        {
            // Copy code to clipboard first as backup
            if (!string.IsNullOrEmpty(code))
            {
                try { await Clipboard.Default.SetTextAsync(code); } catch { }
            }
            
            // Use URL with otc parameter
            var finalUrl = url;
            if (!string.IsNullOrEmpty(code))
            {
                var separator = url.Contains('?') ? "&" : "?";
                finalUrl = $"{url}{separator}otc={Uri.EscapeDataString(code)}";
            }
            
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var currentWindow = Application.Current?.Windows.FirstOrDefault();
                if (currentWindow?.Page == null) return;

                ContentPage? popup = null;
                var deviceCode = code ?? "";

                var webView = new WebView
                {
                    Source = new UrlWebViewSource { Url = finalUrl },
                    HeightRequest = 620,
                    WidthRequest = 480,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                // Start code injection after WebView is ready
                webView.Navigated += async (sender, e) =>
                {
                    if (sender is not WebView wv) return;
                    
                    // Inject styles and code with proper timing
                    await Task.Delay(800); // Wait for page to fully render
                    await InjectStylesAndCodeAsync(wv, deviceCode);
                    await CheckForSuccessAsync(e.Url, popup, currentWindow);
                };

                var closeButton = new Button
                {
                    Text = "✕ Close",
                    BackgroundColor = Color.FromArgb("#667eea"),
                    TextColor = Colors.White,
                    HeightRequest = 38,
                    WidthRequest = 120,
                    CornerRadius = 10,
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalOptions = LayoutOptions.Center,
                    FontAttributes = FontAttributes.Bold
                };

                var content = new VerticalStackLayout
                {
                    Padding = new Thickness(10),
                    BackgroundColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    WidthRequest = 500,
                    HeightRequest = 700,
                    Children = { webView, closeButton }
                };

                var border = new Border
                {
                    Content = content,
                    BackgroundColor = Colors.White,
                    Stroke = Color.FromArgb("#e0e0e0"),
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Padding = 0,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Shadow = new Shadow
                    {
                        Brush = new SolidColorBrush(Color.FromArgb("#333333")),
                        Offset = new Point(0, 4),
                        Radius = 20,
                        Opacity = 0.25f
                    }
                };

                var overlay = new Grid
                {
                    BackgroundColor = new Color(0, 0, 0, 0.6f),
                    Children = { border }
                };

                popup = new ContentPage
                {
                    Content = overlay,
                    BackgroundColor = Colors.Transparent
                };

                closeButton.Clicked += async (s, e) =>
                {
                    await currentWindow.Page.Navigation.PopModalAsync();
                };

                await currentWindow.Page.Navigation.PushModalAsync(popup);
                
                // Start background monitoring for success
                _ = MonitorAndInjectAsync(webView, popup, currentWindow, deviceCode);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open WebView: {ex.Message}");
            try { await Browser.Default.OpenAsync(new Uri(url), BrowserLaunchMode.SystemPreferred); } catch { }
        }
    }
    
    private static async Task InjectStylesAndCodeAsync(WebView webView, string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        
        try
        {
            var escapedCode = code.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
            
            var script = $@"
(function() {{
    // Hide scrollbars
    if (!document.getElementById('_appStyles')) {{
        var s = document.createElement('style');
        s.id = '_appStyles';
        s.textContent = 'html,body{{overflow:hidden!important;scrollbar-width:none!important;-ms-overflow-style:none!important}}::-webkit-scrollbar{{display:none!important}}';
        document.head.appendChild(s);
    }}
    
    var code = '{escapedCode}';
    var filled = false;
    
    function fillCode() {{
        if (filled) return true;
        
        // Find input field - try many selectors
        var input = document.querySelector('input[name=""otc""]') ||
                    document.querySelector('input#otc') ||
                    document.querySelector('input#i0116') ||
                    document.querySelector('input[type=""text""][maxlength]') ||
                    document.querySelector('input[type=""text""]') ||
                    document.querySelector('input[placeholder*=""code"" i]') ||
                    document.querySelector('input[aria-label*=""code"" i]') ||
                    document.querySelector('input.form-control') ||
                    document.querySelector('input:not([type=""hidden""]):not([type=""submit""]):not([type=""password""]):not([type=""checkbox""])');
        
        if (!input || input.offsetParent === null) return false;
        
        // Clear and focus
        input.focus();
        input.select();
        input.value = '';
        
        // Set value using native setter for React/Angular compatibility
        var setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
        setter.call(input, code);
        
        // Trigger all possible events
        ['input', 'change', 'keydown', 'keyup', 'keypress'].forEach(function(evt) {{
            input.dispatchEvent(new Event(evt, {{ bubbles: true, cancelable: true }}));
        }});
        
        // Also dispatch keyboard events with key data
        input.dispatchEvent(new KeyboardEvent('keydown', {{ bubbles: true, key: 'a', keyCode: 65 }}));
        input.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true, key: 'a', keyCode: 65 }}));
        
        filled = true;
        
        // Focus submit button after short delay
        setTimeout(function() {{
            var btn = document.querySelector('input[type=""submit""]') ||
                      document.querySelector('button[type=""submit""]') ||
                      document.querySelector('button#idSIButton9') ||
                      document.querySelector('button.primary') ||
                      document.querySelector('button[data-testid]');
            if (btn) btn.focus();
        }}, 150);
        
        return true;
    }}
    
    // Try immediately
    if (!fillCode()) {{
        // Retry with intervals
        var attempts = 0;
        var interval = setInterval(function() {{
            attempts++;
            if (fillCode() || attempts > 30) {{
                clearInterval(interval);
            }}
        }}, 200);
    }}
}})();";
            
            await webView.EvaluateJavaScriptAsync(script);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to inject code: {ex.Message}");
        }
    }
    
    private static async Task MonitorAndInjectAsync(WebView webView, ContentPage? popup, Window window, string code)
    {
        var lastUrl = "";
        
        for (int i = 0; i < 180; i++)
        {
            await Task.Delay(1000);
            
            try
            {
                var result = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        // Check current URL and re-inject if page changed
                        var currentUrl = await webView.EvaluateJavaScriptAsync("window.location.href") ?? "";
                        if (currentUrl != lastUrl && !string.IsNullOrEmpty(code))
                        {
                            lastUrl = currentUrl;
                            // Re-inject on page change
                            await Task.Delay(500);
                            await InjectStylesAndCodeAsync(webView, code);
                        }
                        
                        // Check for success
                        return await webView.EvaluateJavaScriptAsync(@"
                            (function() {
                                var t = (document.body ? document.body.innerText : '').toLowerCase();
                                return (t.includes('you have signed in') || t.includes('you are signed in') || 
                                        t.includes('you can close this') || t.includes('successfully signed in')) ? 'SUCCESS' : 'PENDING';
                            })();
                        ");
                    }
                    catch { return "ERROR"; }
                });
                
                if (result?.Contains("SUCCESS") == true)
                {
                    await Task.Delay(1200);
                    await ClosePopupAsync(popup, window);
                    return;
                }
            }
            catch { }
        }
    }
    
    private static async Task CheckForSuccessAsync(string? url, ContentPage? popup, Window window)
    {
        if (string.IsNullOrEmpty(url)) return;
        var u = url.ToLower();
        if (!u.Contains("devicelogin") && !u.Contains("login.microsoftonline") && !u.Contains("oauth") &&
            (u.Contains("success") || u.Contains("done") || u.Contains("complete")))
            await ClosePopupAsync(popup, window);
    }
    
    private static async Task ClosePopupAsync(ContentPage? popup, Window window)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { if (popup != null) await window.Page?.Navigation?.PopModalAsync()!; }
            catch { }
        });
    }
}
