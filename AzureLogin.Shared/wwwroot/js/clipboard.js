// Clipboard helper - works in all browsers
window.copyToClipboard = function (text) {
    // Method 1: Modern Clipboard API
    if (navigator.clipboard && window.isSecureContext) {
        return navigator.clipboard.writeText(text)
            .then(function() { return true; })
            .catch(function() { return fallbackCopy(text); });
    }
    // Method 2: Fallback for older browsers or non-secure contexts
    return Promise.resolve(fallbackCopy(text));
};

function fallbackCopy(text) {
    var textArea = document.createElement("textarea");
    textArea.value = text;
    
    // Avoid scrolling to bottom
    textArea.style.top = "0";
    textArea.style.left = "0";
    textArea.style.position = "fixed";
    textArea.style.width = "2em";
    textArea.style.height = "2em";
    textArea.style.padding = "0";
    textArea.style.border = "none";
    textArea.style.outline = "none";
    textArea.style.boxShadow = "none";
    textArea.style.background = "transparent";
    
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    
    var success = false;
    try {
        success = document.execCommand('copy');
    } catch (err) {
        console.error('Fallback copy failed:', err);
    }
    
    document.body.removeChild(textArea);
    return success;
}

// Open popup window
window.openLoginPopup = function(url) {
    var width = 500;
    var height = 700;
    var left = (screen.width - width) / 2;
    var top = (screen.height - height) / 2;
    window.open(url, 'AzureLogin', 'width=' + width + ',height=' + height + ',left=' + left + ',top=' + top + ',scrollbars=yes,resizable=yes');
};

// Load HTML content into iframe using srcdoc (MAUI WebView compatible)
window.loadIframeContent = function(iframeIds, htmlContent) {
    var ids = iframeIds.split(',');
    ids.forEach(function(id) {
        var iframe = document.getElementById(id.trim());
        if (iframe) {
            // Method 1: Use srcdoc attribute (most reliable for MAUI)
            iframe.srcdoc = htmlContent;
            
            // Method 2: Fallback - try blob URL if srcdoc doesn't work
            iframe.onerror = function() {
                try {
                    var blob = new Blob([htmlContent], { type: 'text/html' });
                    iframe.src = URL.createObjectURL(blob);
                } catch (e) {
                    console.error('Failed to load iframe content:', e);
                }
            };
        }
    });
    return true;
};

// Clear iframe content
window.clearIframeContent = function(iframeIds) {
    var ids = iframeIds.split(',');
    ids.forEach(function(id) {
        var iframe = document.getElementById(id.trim());
        if (iframe) {
            iframe.srcdoc = '';
            iframe.src = 'about:blank';
        }
    });
    return true;
};

