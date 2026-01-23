# Version History

## v1.1.0 - Image to Code Feature (January 21, 2026)

### New Features
- **Image to Code Conversion** - Upload a UI screenshot and generate code using GPT-4o Vision
- **Multiple Framework Support** - Generate code for:
  - HTML/CSS
  - Tailwind CSS
  - React
  - Vue.js
  - Blazor
  - SwiftUI
  - .NET MAUI XAML
- **Code Preview** - Syntax-highlighted code display with dark theme
- **Copy to Clipboard** - One-click code copying
- **Three-Tab Interface** - Chat, Image Generation, and Image to Code tabs

### Configuration
```json
{
  "AzureOpenAI": {
    "SubscriptionId": "bad3b0ce-f7a6-49c5-b66e-43fe19f4b800",
    "ResourceGroup": "rg-scus-shared",
    "AccountName": "OAI-EUS2-NonProd",
    "Endpoint": "https://oai-eus2-nonprod.openai.azure.com/",
    "DefaultDeployment": "gpt-5.2",
    "ImageDeployment": "gpt-image-1.5",
    "VisionDeployment": "gpt-4o"
  }
}
```

### New Files
- `AzureLogin.Shared/Services/IImageToCodeService.cs` - Interface for image-to-code conversion
- `AzureLogin/Services/ImageToCodeService.cs` - MAUI implementation
- `AzureLogin.Web/Services/ImageToCodeService.cs` - Web implementation

---

## v1.0.0 - Working Version (January 21, 2026)

### Features
- **Azure AD Authentication** - Device Code Flow authentication with Microsoft accounts
- **Chat with GPT** - Send messages to GPT-5.2 and receive responses
- **Image Generation** - Generate images using gpt-image-1.5
- **Image Upload** - Upload reference images for future processing
- **Image Download** - Save generated images locally
- **Two-Pane Image Studio** - Polished UI with input/upload pane and preview pane

### Platforms
- macOS (MacCatalyst)
- iOS
- Web (Blazor)

### Key Files
- `AzureLogin.Shared/Pages/Home.razor` - Main UI with chat and image generation
- `AzureLogin/Services/AzureOpenAIService.cs` - MAUI Azure OpenAI service
- `AzureLogin.Web/Services/AzureOpenAIService.cs` - Web Azure OpenAI service
- `AzureLogin.Shared/Services/IAzureOpenAIService.cs` - Service interface

### Entitlements (MacCatalyst)
- App Sandbox
- Network Client/Server
- File access (user-selected, downloads, pictures, documents)
- Photos Library

### Privacy Descriptions (iOS/MacCatalyst)
- Photo Library (read/write)
- Camera access
- Downloads folder
- Documents folder
- Desktop folder

---
**Status: ✅ Working**
