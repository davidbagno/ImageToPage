# 🌳 Azure AI Services - Complete Reference Guide

**Parent Endpoint:** `https://oai-eus2-nonprod.openai.azure.com/`

---

## 📁 Service Hierarchy

```
Azure AI Services
├── 🤖 Azure OpenAI (GPT-4o)
│   ├── Chat Completions
│   ├── Vision Completions (GPT-4o Vision)
│   └── Image Generation (DALL-E 3)
│
├── 👁️ Azure AI Vision (Computer Vision 4.0 + 3.2)
│   ├── Image Analysis v4.0
│   │   ├── Dense Captions
│   │   ├── Object Detection
│   │   ├── People Detection
│   │   ├── Smart Crops
│   │   ├── Tags
│   │   ├── Caption
│   │   └── Read (OCR)
│   ├── Image Analysis v3.2
│   │   ├── Brands Detection
│   │   ├── Categories
│   │   ├── Adult Content
│   │   ├── Faces
│   │   └── Image Type
│   └── Background Removal
│
├── 🧠 GPT-4o Vision Service (via Azure OpenAI)
│   ├── UI Analysis
│   ├── Code Generation
│   ├── Document Parsing
│   └── Image Understanding
│
├── 🖼️ Image Extraction Service
│   ├── AI Detection Modes
│   ├── Local Processing (SkiaSharp)
│   └── Hybrid Modes
│
└── 🚀 Advanced AI Services
    ├── Document Intelligence
    ├── Custom Vision
    ├── Video Analysis
    └── Image Enhancement
```

---

# 🤖 1. AZURE OPENAI SERVICE

**Interface:** `IAzureOpenAIService`  
**Endpoint:** `https://oai-eus2-nonprod.openai.azure.com/openai/deployments/{deployment}/`

---

## 1.1 Chat Completions

### 📋 Description
Send text messages to GPT-4o and receive AI-generated responses. Supports system prompts for controlling AI behavior and multi-turn conversations.

### 🔧 Function
```csharp
Task<string> GetChatCompletionAsync(string userMessage, string? systemPrompt = null)
```

### 📡 API Command
```http
POST /openai/deployments/gpt-4o/chat/completions?api-version=2024-02-15-preview
Content-Type: application/json
Authorization: Bearer {token}
```

### 📦 Request Body
```json
{
  "messages": [
    { "role": "system", "content": "You are a helpful assistant." },
    { "role": "user", "content": "Explain microservices architecture" }
  ],
  "max_tokens": 4096,
  "temperature": 0.7
}
```

### 💡 Example Prompts
| Use Case | Prompt |
|----------|--------|
| Code Explanation | `"Explain this C# code: {code}"` |
| Technical Writing | `"Write documentation for this API endpoint"` |
| Problem Solving | `"How do I implement caching in .NET?"` |
| Code Review | `"Review this code for security issues: {code}"` |

### 🎯 How to Use
1. Call `GetChatCompletionAsync` with your question
2. Optionally provide a system prompt to set context
3. Receive the AI response as a string

```csharp
var response = await _azureOpenAI.GetChatCompletionAsync(
    "What is dependency injection?",
    "You are a senior .NET developer. Give concise, practical answers."
);
```

---

## 1.2 Vision Completions (GPT-4o Vision)

### 📋 Description
Analyze images using GPT-4o's vision capabilities. Send an image and a prompt to get AI-powered image understanding, descriptions, or analysis.

### 🔧 Function
```csharp
Task<string> GetVisionCompletionAsync(
    string imageBase64, 
    string mimeType, 
    string userPrompt, 
    string? systemPrompt = null)
```

### 📡 API Command
```http
POST /openai/deployments/gpt-4o/chat/completions?api-version=2024-02-15-preview
Content-Type: application/json
```

### 📦 Request Body
```json
{
  "messages": [{
    "role": "user",
    "content": [
      { 
        "type": "image_url", 
        "image_url": { 
          "url": "data:image/png;base64,{base64ImageData}" 
        }
      },
      { "type": "text", "text": "Describe this UI design" }
    ]
  }],
  "max_tokens": 4096
}
```

### 💡 Example Prompts
| Use Case | Prompt |
|----------|--------|
| UI Description | `"Describe all the UI elements in this screenshot"` |
| Code Generation | `"Generate React code that recreates this UI"` |
| Accessibility | `"Identify accessibility issues in this interface"` |
| Color Analysis | `"What colors are used in this design?"` |

### 🎯 How to Use
1. Convert your image to base64
2. Determine the MIME type (image/png, image/jpeg, etc.)
3. Call the function with your analysis prompt

```csharp
var imageBytes = await File.ReadAllBytesAsync("screenshot.png");
var base64 = Convert.ToBase64String(imageBytes);
var result = await _azureOpenAI.GetVisionCompletionAsync(
    base64,
    "image/png",
    "Generate HTML/CSS code for this UI design"
);
```

---

## 1.3 Image Generation (DALL-E 3)

### 📋 Description
Generate new images from text descriptions using DALL-E 3. Create artwork, mockups, icons, or any visual content from natural language prompts.

### 🔧 Function
```csharp
Task<ImageGenerationResult> GenerateImageAsync(string prompt, string size = "1024x1024")
```

### 📡 API Command
```http
POST /openai/deployments/dall-e-3/images/generations?api-version=2024-02-15-preview
Content-Type: application/json
```

### 📦 Request Body
```json
{
  "prompt": "A futuristic city skyline at sunset with flying cars",
  "n": 1,
  "size": "1024x1024",
  "quality": "hd",
  "style": "vivid"
}
```

### 📐 Available Sizes
| Size | Aspect Ratio | Best For |
|------|--------------|----------|
| `1024x1024` | 1:1 Square | Icons, avatars, general use |
| `1792x1024` | 16:9 Landscape | Headers, banners, desktop wallpapers |
| `1024x1792` | 9:16 Portrait | Mobile wallpapers, stories, posters |

### 💡 Example Prompts
| Use Case | Prompt |
|----------|--------|
| App Icon | `"A minimal flat design app icon for a weather app, blue gradient background, white cloud symbol"` |
| Hero Image | `"Professional business team collaborating in modern office, soft lighting, corporate style"` |
| Abstract Art | `"Abstract geometric patterns in purple and gold, digital art style"` |
| Product Mockup | `"Smartphone displaying app interface on wooden desk, lifestyle photography"` |

### 🎯 How to Use
```csharp
var result = await _azureOpenAI.GenerateImageAsync(
    "A modern minimalist logo for a tech startup called 'CloudFlow'",
    "1024x1024"
);

if (result.Success)
{
    var imageUrl = result.ImageUrl;
    var revisedPrompt = result.RevisedPrompt; // DALL-E may modify your prompt
}
```

---

# 👁️ 2. AZURE AI VISION SERVICE

**Interface:** `IAzureVisionService`  
**Endpoint:** `{visionEndpoint}/computervision/`  
**Authentication:** `Ocp-Apim-Subscription-Key: {apiKey}`

---

## 2.1 Vision 4.0 APIs

### 2.1.1 Comprehensive Analysis

#### 📋 Description
Perform complete image analysis combining all Vision 4.0 features in a single API call. Returns objects, people, captions, tags, and smart crop suggestions.

#### 🔧 Function
```csharp
Task<AzureVisionResult> AnalyzeComprehensiveAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:analyze?api-version=2024-02-01
     &features=denseCaptions,objects,tags,people,smartCrops
     &smartcrops-aspect-ratios=1.0,0.75,1.33,1.78
Content-Type: application/octet-stream
Ocp-Apim-Subscription-Key: {apiKey}
Body: [binary image data]
```

#### 📤 Response Structure
```json
{
  "denseCaptionsResult": {
    "values": [
      { "text": "a person sitting at a desk", "confidence": 0.95, "boundingBox": {...} }
    ]
  },
  "objectsResult": {
    "values": [
      { "tags": ["laptop"], "boundingBox": { "x": 100, "y": 50, "w": 200, "h": 150 } }
    ]
  },
  "tagsResult": { "values": [{ "name": "indoor", "confidence": 0.99 }] },
  "peopleResult": { "values": [{ "boundingBox": {...}, "confidence": 0.98 }] },
  "smartCropsResult": { "values": [{ "aspectRatio": 1.0, "boundingBox": {...} }] }
}
```

#### 🎯 How to Use
```csharp
var imageBytes = await File.ReadAllBytesAsync("photo.jpg");
var result = await _visionService.AnalyzeComprehensiveAsync(imageBytes);

foreach (var region in result.Regions)
{
    Console.WriteLine($"Found: {region.Caption} at ({region.X}, {region.Y})");
    Console.WriteLine($"Size: {region.Width}x{region.Height}, Confidence: {region.Confidence:P}");
}
```

---

### 2.1.2 Dense Captions

#### 📋 Description
Generate detailed descriptions for multiple regions within an image. Each region gets its own caption with pixel-accurate bounding boxes.

#### 🔧 Function
```csharp
Task<AzureVisionResult> GetDenseCaptionsAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:analyze?api-version=2024-02-01&features=denseCaptions
```

#### 💡 Use Cases
- **E-commerce**: Auto-generate product descriptions
- **Accessibility**: Create alt-text for images
- **Content moderation**: Understand image content
- **Search**: Index images by their content

#### 🎯 How to Use
```csharp
var result = await _visionService.GetDenseCaptionsAsync(imageBytes);
// Returns: "a red sports car parked in front of a building"
//          "a person walking on the sidewalk"
//          "a street sign showing 'Main St'"
```

---

### 2.1.3 Object Detection

#### 📋 Description
Detect and locate objects within an image with pixel-precise bounding boxes. Identifies common objects like cars, furniture, electronics, etc.

#### 🔧 Function
```csharp
Task<AzureVisionResult> DetectObjectsAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:analyze?api-version=2024-02-01&features=objects
```

#### 📤 Response Example
```json
{
  "objectsResult": {
    "values": [
      { "tags": [{"name": "laptop", "confidence": 0.95}], 
        "boundingBox": {"x": 120, "y": 80, "w": 300, "h": 200} },
      { "tags": [{"name": "coffee cup", "confidence": 0.89}],
        "boundingBox": {"x": 450, "y": 150, "w": 50, "h": 70} }
    ]
  }
}
```

#### 🎯 How to Use
```csharp
var result = await _visionService.DetectObjectsAsync(imageBytes);
foreach (var obj in result.Regions)
{
    Console.WriteLine($"Detected {obj.Tags[0]}: Box({obj.X}, {obj.Y}, {obj.Width}, {obj.Height})");
}
```

---

### 2.1.4 People Detection

#### 📋 Description
Specifically detect people and their locations in images. Returns bounding boxes around each person detected.

#### 🔧 Function
```csharp
Task<AzureVisionResult> DetectPeopleAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:analyze?api-version=2024-02-01&features=people
```

#### 💡 Use Cases
- **Photo apps**: Find photos containing people
- **Security**: Count people in surveillance footage
- **Cropping**: Auto-crop to focus on people
- **Privacy**: Blur or redact people in images

#### 🎯 How to Use
```csharp
var result = await _visionService.DetectPeopleAsync(imageBytes);
Console.WriteLine($"Found {result.Regions.Count} people in the image");
```

---

### 2.1.5 Smart Crops

#### 📋 Description
Get AI-suggested crop regions for different aspect ratios. Ensures the most important content remains in frame.

#### 🔧 Function
```csharp
Task<SmartCropResult> GetSmartCropsAsync(byte[] imageBytes, params double[] aspectRatios)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:analyze?api-version=2024-02-01
     &features=smartCrops
     &smartcrops-aspect-ratios=1.0,0.75,1.33,1.78
```

#### 📐 Common Aspect Ratios
| Ratio | Value | Use Case |
|-------|-------|----------|
| Square | `1.0` | Profile pictures, thumbnails |
| Portrait | `0.75` | Mobile screens, stories |
| Landscape | `1.33` | 4:3 displays |
| Widescreen | `1.78` | 16:9 video, banners |

#### 🎯 How to Use
```csharp
var crops = await _visionService.GetSmartCropsAsync(imageBytes, 1.0, 1.78);
foreach (var crop in crops.Crops)
{
    Console.WriteLine($"Aspect {crop.AspectRatio}: Crop at ({crop.X}, {crop.Y}) size {crop.Width}x{crop.Height}");
}
```

---

### 2.1.6 Image Tags

#### 📋 Description
Get content tags describing the image (e.g., "outdoor", "nature", "person", "building"). Useful for categorization and search.

#### 🔧 Function
```csharp
Task<ImageTagsResult> GetImageTagsAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:analyze?api-version=2024-02-01&features=tags
```

#### 📤 Response Example
```json
{
  "tagsResult": {
    "values": [
      { "name": "sky", "confidence": 0.99 },
      { "name": "outdoor", "confidence": 0.98 },
      { "name": "building", "confidence": 0.95 },
      { "name": "city", "confidence": 0.89 }
    ]
  }
}
```

#### 🎯 How to Use
```csharp
var tags = await _visionService.GetImageTagsAsync(imageBytes);
var topTags = tags.Tags.Where(t => t.Confidence > 0.8).Select(t => t.Name);
// Returns: ["sky", "outdoor", "building", "city"]
```

---

### 2.1.7 Single Caption

#### 📋 Description
Generate one descriptive sentence for the entire image. Simpler than dense captions when you just need a single description.

#### 🔧 Function
```csharp
Task<ImageCaptionResult> GetImageCaptionAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:analyze?api-version=2024-02-01&features=caption
```

#### 🎯 How to Use
```csharp
var caption = await _visionService.GetImageCaptionAsync(imageBytes);
Console.WriteLine($"Caption: {caption.Caption}"); 
// Output: "a group of people sitting around a conference table"
Console.WriteLine($"Confidence: {caption.Confidence:P}");
// Output: "Confidence: 94.5%"
```

---

### 2.1.8 Text Detection (OCR)

#### 📋 Description
Extract all text visible in an image with position information. Supports multiple languages and handwriting.

#### 🔧 Function
```csharp
Task<TextDetectionResult> DetectTextAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:analyze?api-version=2024-02-01&features=read
```

#### 📤 Response Structure
```json
{
  "readResult": {
    "blocks": [{
      "lines": [{
        "text": "Welcome to Azure",
        "boundingPolygon": [{"x": 10, "y": 5}, {"x": 200, "y": 5}, ...],
        "words": [
          { "text": "Welcome", "confidence": 0.99 },
          { "text": "to", "confidence": 0.99 },
          { "text": "Azure", "confidence": 0.98 }
        ]
      }]
    }]
  }
}
```

#### 🎯 How to Use
```csharp
var textResult = await _visionService.DetectTextAsync(imageBytes);
Console.WriteLine($"Full text: {textResult.FullText}");
foreach (var region in textResult.TextRegions)
{
    Console.WriteLine($"Text '{region.Text}' at ({region.X}, {region.Y})");
}
```

---

### 2.1.9 Background Removal

#### 📋 Description
Remove the background from an image, returning a PNG with transparent background. Perfect for product photos or profile pictures.

#### 🔧 Function
```csharp
Task<BackgroundRemovalResult> RemoveBackgroundAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /computervision/imageanalysis:segment?api-version=2023-02-01-preview&mode=backgroundRemoval
Content-Type: application/octet-stream
Returns: image/png (with transparency)
```

#### 🎯 How to Use
```csharp
var result = await _visionService.RemoveBackgroundAsync(imageBytes);
if (result.Success)
{
    await File.WriteAllBytesAsync("output_transparent.png", result.ImageWithoutBackground);
    // Or use base64 for web display:
    var imgSrc = $"data:image/png;base64,{result.Base64Image}";
}
```

---

## 2.2 Vision 3.2 APIs (Legacy)

### 2.2.1 Brand Detection

#### 📋 Description
Detect commercial brand logos in images. Identifies thousands of global brands with bounding boxes.

#### 🔧 Function
```csharp
Task<BrandDetectionResult> DetectBrandsAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /vision/v3.2/analyze?visualFeatures=Brands
```

#### 🎯 How to Use
```csharp
var brands = await _visionService.DetectBrandsAsync(imageBytes);
foreach (var brand in brands.Brands)
{
    Console.WriteLine($"Found {brand.Name} logo at ({brand.X}, {brand.Y})");
}
// Output: "Found Microsoft logo at (150, 30)"
```

---

### 2.2.2 Category Detection

#### 📋 Description
Classify images into predefined categories like "building", "people", "outdoor", "food", etc.

#### 🔧 Function
```csharp
Task<CategoryResult> GetImageCategoryAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /vision/v3.2/analyze?visualFeatures=Categories
```

#### 📤 Category Examples
| Category | Subcategories |
|----------|--------------|
| `building_` | building_commercial, building_house |
| `outdoor_` | outdoor_city, outdoor_mountain, outdoor_beach |
| `people_` | people_group, people_portrait |
| `food_` | food_bread, food_meat |

#### 🎯 How to Use
```csharp
var category = await _visionService.GetImageCategoryAsync(imageBytes);
Console.WriteLine($"Primary: {category.PrimaryCategory}");
// Output: "Primary: outdoor_city"
```

---

### 2.2.3 Adult Content Detection

#### 📋 Description
Detect adult, racy, or gory content in images. Returns scores for each category.

#### 🔧 Function
```csharp
Task<AdultContentResult> DetectAdultContentAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /vision/v3.2/analyze?visualFeatures=Adult
```

#### 📤 Response Structure
```json
{
  "adult": {
    "isAdultContent": false,
    "isRacyContent": false,
    "isGoryContent": false,
    "adultScore": 0.02,
    "racyScore": 0.05,
    "goreScore": 0.01
  }
}
```

#### 🎯 How to Use
```csharp
var result = await _visionService.DetectAdultContentAsync(imageBytes);
if (result.IsAdultContent || result.AdultScore > 0.5)
{
    // Flag or block content
}
```

---

### 2.2.4 Face Detection

#### 📋 Description
Detect faces in images with estimated age and gender (legacy API).

#### 🔧 Function
```csharp
Task<FaceDetectionResult> DetectFacesAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /vision/v3.2/analyze?visualFeatures=Faces
```

#### 🎯 How to Use
```csharp
var faces = await _visionService.DetectFacesAsync(imageBytes);
foreach (var face in faces.Faces)
{
    Console.WriteLine($"Face at ({face.X}, {face.Y}), Age: {face.Age}, Gender: {face.Gender}");
}
```

---

### 2.2.5 Image Type Detection

#### 📋 Description
Determine if an image is a photograph, clipart, or line drawing.

#### 🔧 Function
```csharp
Task<ImageTypeResult> GetImageTypeAsync(byte[] imageBytes)
```

#### 📡 API Command
```http
POST /vision/v3.2/analyze?visualFeatures=ImageType
```

#### 📤 Response Values
| ClipArtType | Meaning |
|-------------|---------|
| 0 | Not clipart |
| 1 | Ambiguous |
| 2 | Normal clipart |
| 3 | Good clipart |

#### 🎯 How to Use
```csharp
var type = await _visionService.GetImageTypeAsync(imageBytes);
Console.WriteLine($"Type: {type.ImageType}"); // "photo", "clipart", or "linedrawing"
Console.WriteLine($"Is Clipart: {type.IsClipArt}");
```

---

# 🧠 3. GPT-4o VISION SERVICE

**Interface:** `IVisionService`  
**Powered by:** Azure OpenAI GPT-4o with vision capabilities

---

## 3.1 UI Analysis

### 3.1.1 Describe Design

#### 📋 Description
Get a comprehensive description of a UI design including layout, colors, components, and overall style.

#### 🔧 Function
```csharp
Task<VisionResult> DescribeImageAsync(string base64Image, string mimeType)
```

#### 💬 Prompt Used
```
Provide a comprehensive description of this UI design image. Include:
1. Overall Purpose: What is this UI for?
2. Visual Style: Describe the design aesthetic
3. Layout Structure: How is the content organized?
4. Key Components: List all major UI elements
5. Color Scheme: Describe the colors used
6. Typography: Describe fonts and text styles
7. Interactive Elements: Identify buttons, links, inputs
8. Notable Design Choices: Any unique design decisions
```

#### 🎯 How to Use
```csharp
var description = await _visionService.DescribeImageAsync(base64Image, "image/png");
Console.WriteLine(description.Content);
// Output: "This is a login page with a modern, minimal design..."
```

---

### 3.1.2 Generate Code from Image

#### 📋 Description
Convert a UI screenshot into working code for your chosen framework.

#### 🔧 Function
```csharp
Task<VisionResult> GenerateCodeFromImageAsync(
    string base64Image, 
    string mimeType, 
    string framework = "HTML/CSS")
```

#### 🎨 Supported Frameworks
| Framework | Output |
|-----------|--------|
| `HTML/CSS` | Vanilla HTML with CSS |
| `Tailwind CSS` | HTML with Tailwind classes |
| `React` | JSX component with CSS |
| `Vue` | Vue SFC (.vue file) |
| `Blazor Razor` | .razor component |
| `SwiftUI` | Swift View code |
| `MAUI XAML` | XAML with C# |
| `Angular` | TypeScript component |

#### 💬 Prompt Used
```
Analyze this UI design image and generate clean, production-ready {framework} code.
Include:
- Semantic HTML structure
- Complete CSS styling using Flexbox/Grid
- Responsive design considerations
- Accessibility attributes (ARIA labels, alt text)
- Proper component organization
Return ONLY the code, no explanations.
```

#### 🎯 How to Use
```csharp
var code = await _visionService.GenerateCodeFromImageAsync(
    base64Image, 
    "image/png", 
    "React"
);
// Returns complete React component code
```

---

### 3.1.3 Extract Color Palette

#### 📋 Description
Extract all colors used in a design with their purposes (primary, secondary, background, etc.).

#### 🔧 Function
```csharp
Task<ColorPaletteResult> ExtractColorPaletteAsync(string base64Image, string mimeType)
```

#### 💬 Prompt Used
```
Analyze this UI design and extract the complete color palette. Return JSON:
{
  "primaryColor": "#hex",
  "secondaryColor": "#hex",
  "accentColor": "#hex",
  "backgroundColor": "#hex",
  "textColor": "#hex",
  "textSecondaryColor": "#hex",
  "borderColor": "#hex",
  "successColor": "#hex",
  "warningColor": "#hex",
  "errorColor": "#hex",
  "gradients": ["linear-gradient(...)"],
  "colorScheme": "light|dark|mixed"
}
```

#### 🎯 How to Use
```csharp
var colors = await _visionService.ExtractColorPaletteAsync(base64Image, "image/png");
Console.WriteLine($"Primary: {colors.PrimaryColor}");     // #667eea
Console.WriteLine($"Background: {colors.BackgroundColor}"); // #ffffff
Console.WriteLine($"Scheme: {colors.ColorScheme}");       // light
```

---

### 3.1.4 Extract Typography

#### 📋 Description
Identify fonts, sizes, weights, and text styles used in the design.

#### 🔧 Function
```csharp
Task<TypographyResult> ExtractTypographyAsync(string base64Image, string mimeType)
```

#### 📤 Returns
```json
{
  "primaryFont": "Inter",
  "secondaryFont": "Roboto Mono",
  "headingFont": "Inter",
  "bodyFont": "Inter",
  "fontSizes": {
    "h1": "48px",
    "h2": "36px",
    "body": "16px",
    "small": "14px"
  },
  "fontWeights": ["400", "500", "600", "700"],
  "lineHeight": "1.5",
  "letterSpacing": "0.02em"
}
```

#### 🎯 How to Use
```csharp
var typography = await _visionService.ExtractTypographyAsync(base64Image, "image/png");
Console.WriteLine($"Primary Font: {typography.PrimaryFont}");
Console.WriteLine($"Heading Size: {typography.FontSizes["h1"]}");
```

---

### 3.1.5 Extract Layout

#### 📋 Description
Analyze the layout structure, grid system, spacing, and section organization.

#### 🔧 Function
```csharp
Task<LayoutResult> ExtractLayoutAsync(string base64Image, string mimeType)
```

#### 📤 Returns
```json
{
  "layoutType": "grid",
  "containerWidth": "1200px",
  "containerPadding": "24px",
  "columns": 12,
  "gap": "24px",
  "sections": [
    { "name": "header", "type": "header", "layout": "flex", "height": "80px" },
    { "name": "hero", "type": "hero", "layout": "flex", "height": "500px" },
    { "name": "features", "type": "content", "layout": "grid", "columns": 3 }
  ],
  "spacing": {
    "baseUnit": "8px",
    "scale": { "xs": "4px", "sm": "8px", "md": "16px", "lg": "24px", "xl": "48px" }
  }
}
```

---

### 3.1.6 Extract UI Components

#### 📋 Description
Identify all UI components in the design with their properties and hierarchy.

#### 🔧 Function
```csharp
Task<UIComponentsResult> ExtractUIComponentsAsync(string base64Image, string mimeType)
```

#### 📤 Returns
```json
{
  "components": [
    {
      "type": "button",
      "name": "submitButton",
      "text": "Sign In",
      "style": {
        "backgroundColor": "#667eea",
        "textColor": "#ffffff",
        "borderRadius": "8px",
        "padding": "12px 24px"
      },
      "position": { "layout": "flex", "alignment": "center" }
    },
    {
      "type": "input",
      "name": "emailInput",
      "text": "placeholder: Enter your email"
    }
  ],
  "layoutType": "flex",
  "sections": ["header", "form", "footer"]
}
```

---

### 3.1.7 Identify Icons

#### 📋 Description
Detect icons in the UI and suggest matching icons from popular icon libraries.

#### 🔧 Function
```csharp
Task<IconsResult> IdentifyIconsAsync(string base64Image, string mimeType)
```

#### 📤 Returns
```json
{
  "iconStyle": "outlined",
  "suggestedLibrary": "Heroicons",
  "icons": [
    { "description": "home icon", "suggestedName": "home", "location": "navbar", "size": "24px" },
    { "description": "search magnifying glass", "suggestedName": "magnifying-glass", "size": "20px" },
    { "description": "user avatar circle", "suggestedName": "user-circle", "size": "32px" }
  ]
}
```

#### 💡 Suggested Libraries
- **Heroicons** - Tailwind's icon set
- **Material Icons** - Google's icon set
- **Font Awesome** - Popular web icons
- **Lucide** - Fork of Feather icons
- **Phosphor** - Flexible icon family

---

### 3.1.8 Accessibility Audit

#### 📋 Description
Analyze the UI for accessibility issues and WCAG compliance.

#### 🔧 Function
```csharp
Task<AccessibilityResult> AnalyzeAccessibilityAsync(string base64Image, string mimeType)
```

#### 💬 Prompt Used
```
Analyze this UI for accessibility issues. Return JSON:
{
  "overallScore": "Good|Needs Improvement|Poor",
  "issues": [
    {
      "type": "contrast|text-size|touch-target|color-only",
      "severity": "critical|major|minor",
      "description": "...",
      "location": "...",
      "recommendation": "...",
      "wcagCriteria": "1.4.3"
    }
  ],
  "contrastAnalysis": {
    "passesAANormal": true,
    "passesAALarge": true,
    "passesAAA": false
  },
  "recommendations": ["..."]
}
```

#### 🎯 How to Use
```csharp
var a11y = await _visionService.AnalyzeAccessibilityAsync(base64Image, "image/png");
Console.WriteLine($"Score: {a11y.OverallScore}");
foreach (var issue in a11y.Issues.Where(i => i.Severity == "critical"))
{
    Console.WriteLine($"CRITICAL: {issue.Description}");
    Console.WriteLine($"Fix: {issue.Recommendation}");
}
```

---

## 3.2 Document & Text Capabilities

### 3.2.1 Extract Text (OCR)

#### 📋 Description
Extract all visible text from an image with formatting preservation.

#### 🔧 Function
```csharp
Task<VisionResult> ExtractTextAsync(string base64Image, string mimeType)
```

#### 💬 Prompt Used
```
Extract ALL text content visible in this image. Include:
- Headlines and titles
- Body text and paragraphs
- Button labels
- Form labels and placeholder text
- Navigation items
- Footer text
Preserve the hierarchical structure of the text.
```

---

### 3.2.2 Parse Document

#### 📋 Description
Extract structured data from documents like invoices, receipts, or forms.

#### 🔧 Function
```csharp
Task<DocumentParseResult> ParseDocumentAsync(string base64Image, string mimeType)
```

#### 💬 Prompt Used
```
Parse this document image and extract structured data. Return JSON:
{
  "documentType": "invoice|receipt|form|other",
  "fields": [
    { "name": "field_name", "value": "extracted_value", "confidence": 0.95 }
  ],
  "tables": [
    { "headers": ["col1", "col2"], "rows": [["val1", "val2"]] }
  ],
  "summary": "Brief description of the document"
}
```

---

### 3.2.3 Extract Table Data

#### 📋 Description
Extract tabular data from images into structured format.

#### 🔧 Function
```csharp
Task<TableExtractionResult> ExtractTableAsync(string base64Image, string mimeType)
```

#### 📤 Returns
```json
{
  "headers": ["Product", "Quantity", "Price"],
  "rows": [
    ["Widget A", "10", "$5.00"],
    ["Widget B", "5", "$8.00"]
  ],
  "rowCount": 2,
  "columnCount": 3
}
```

---

### 3.2.4 Read Handwriting

#### 📋 Description
Transcribe handwritten text from images.

#### 🔧 Function
```csharp
Task<HandwritingResult> ReadHandwritingAsync(string base64Image, string mimeType)
```

#### 💬 Prompt Used
```
Read and transcribe the handwritten text in this image. Return JSON:
{
  "text": "The full transcribed text",
  "lines": ["line 1", "line 2"],
  "confidence": 0.85,
  "language": "en"
}
```

---

### 3.2.5 Parse Math Equations

#### 📋 Description
Convert mathematical notation in images to LaTeX format.

#### 🔧 Function
```csharp
Task<MathOcrResult> ParseMathEquationAsync(string base64Image, string mimeType)
```

#### 📤 Returns
```json
{
  "equations": [
    { "latex": "\\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}", "text": "quadratic formula" }
  ],
  "variables": ["a", "b", "c", "x"],
  "operators": ["+", "-", "±", "√"]
}
```

---

### 3.2.6 Extract Chart Data

#### 📋 Description
Extract numerical data from charts and graphs.

#### 🔧 Function
```csharp
Task<ChartDataResult> ExtractChartDataAsync(string base64Image, string mimeType)
```

#### 💬 Prompt Used
```
Analyze this chart/graph image and extract the data. Return JSON:
{
  "chartType": "bar|line|pie|scatter|other",
  "title": "Chart title if visible",
  "labels": ["Jan", "Feb", "Mar"],
  "datasets": [
    { "name": "Sales", "values": [100, 150, 200] }
  ],
  "axes": { "x": "Month", "y": "Revenue ($)" }
}
```

---

## 3.3 Image Q&A

### 3.3.1 Ask About Image

#### 📋 Description
Ask any question about an image and get an AI-powered answer.

#### 🔧 Function
```csharp
Task<VisionResult> AskAboutImageAsync(string base64Image, string mimeType, string question)
```

#### 💡 Example Questions
| Question Type | Example |
|---------------|---------|
| Identification | `"What brand is the laptop in this image?"` |
| Counting | `"How many people are in this photo?"` |
| Reading | `"What does the sign say?"` |
| Analysis | `"Is this a professional or casual setting?"` |
| Comparison | `"Which button is larger?"` |

#### 🎯 How to Use
```csharp
var answer = await _visionService.AskAboutImageAsync(
    base64Image, 
    "image/png", 
    "What is the main call-to-action on this landing page?"
);
// Output: "The main call-to-action is a blue button labeled 'Start Free Trial' 
//          located in the center of the hero section."
```

---

### 3.3.2 Spatial Questions

#### 📋 Description
Ask questions about spatial relationships between elements in an image.

#### 🔧 Function
```csharp
Task<VisionResult> AnswerSpatialQuestionAsync(string base64Image, string mimeType, string question)
```

#### 💡 Example Questions
- `"What is to the left of the search bar?"`
- `"What elements are above the footer?"`
- `"Which button is closest to the logo?"`
- `"What is positioned between the header and the main content?"`

---

### 3.3.3 Compare Images

#### 📋 Description
Compare two images and describe the differences.

#### 🔧 Function
```csharp
Task<VisionResult> CompareImagesAsync(string base64Image1, string base64Image2, string mimeType)
```

#### 💬 Prompt Used
```
Compare these two images and describe all differences:
- Visual changes (colors, sizes, positions)
- Content changes (text, images, components)
- Layout changes
- Added or removed elements
Be specific about locations and describe changes clearly.
```

---

# 🖼️ 4. IMAGE EXTRACTION SERVICE

**Interface:** `IImageExtractionService`  
**Technology:** GPT-4o + SkiaSharp + Azure Vision

---

## 4.1 AI Detection Modes

### 4.1.1 Smart Detect (GPT-4o + Edge Refinement)

#### 📋 Description
Uses GPT-4o to identify image regions, then refines bounding boxes using edge detection for pixel-perfect extraction.

#### 🔧 Function
```csharp
Task<ImageExtractionResult> ExtractImagesAsync(byte[] imageBytes, string mimeType)
```

#### ⚙️ Options
```csharp
var options = new ExtractionOptions
{
    Mode = CropMode.SmartDetect,
    RefineWithEdgeDetection = true,
    EdgeDetectionThreshold = 30,
    Padding = 2
};
```

#### 🎯 How to Use
```csharp
var result = await _extractionService.ExtractImagesAsync(imageBytes, "image/png");
foreach (var image in result.Images)
{
    Console.WriteLine($"Found: {image.Description}");
    Console.WriteLine($"Type: {image.ImageType}");
    Console.WriteLine($"Size: {image.Width}x{image.Height}");
    await File.WriteAllBytesAsync(image.SuggestedFilename, image.ImageData);
}
```

---

### 4.1.2 Azure Vision Mode

#### 📋 Description
Uses Azure AI Vision 4.0 for pixel-accurate object detection with dense captioning.

#### 🔧 Function
```csharp
Task<ImageExtractionResult> ExtractWithAzureVisionAsync(byte[] imageBytes, string mimeType)
```

#### 💡 Advantages
- Pixel-accurate bounding boxes
- Faster than GPT-4o
- Better for photos and real-world images
- Returns confidence scores

---

### 4.1.3 Hybrid Pixel-Perfect

#### 📋 Description
Combines all detection methods (GPT-4o, Azure Vision, edge detection) for maximum accuracy.

#### 🔧 Function
```csharp
Task<ImageExtractionResult> ExtractHybridPixelPerfectAsync(byte[] imageBytes, string mimeType)
```

#### 🎯 Best For
- Complex UI screenshots
- Mixed content (photos + graphics)
- When accuracy is critical

---

## 4.2 Local Processing Modes (SkiaSharp)

### 4.2.1 Grid Extraction

#### 📋 Description
Divide the image into a uniform grid and extract each cell.

#### 🔧 Function
```csharp
Task<ImageExtractionResult> ExtractGridAsync(byte[] imageBytes, string mimeType, int rows, int columns)
```

#### 🎯 How to Use
```csharp
// Split into 3x3 grid (9 images)
var result = await _extractionService.ExtractGridAsync(imageBytes, "image/png", 3, 3);
```

---

### 4.2.2 Contour Detection

#### 📋 Description
Find image boundaries using edge/contour analysis. No AI required.

#### 🔧 Function
```csharp
Task<ImageExtractionResult> ExtractByContourDetectionAsync(
    byte[] imageBytes, 
    string mimeType, 
    int minSize = 20, 
    int colorThreshold = 25)
```

---

### 4.2.3 Flood Fill Regions

#### 📋 Description
Detect distinct colored regions using flood-fill algorithm.

#### 🔧 Function
```csharp
Task<ImageExtractionResult> ExtractByFloodFillAsync(
    byte[] imageBytes, 
    string mimeType, 
    int minSize = 30, 
    int colorTolerance = 15)
```

---

### 4.2.4 UI Card Detection

#### 📋 Description
Smart detection of UI cards and widgets with rounded corners and distinct backgrounds.

#### 🔧 Function
```csharp
Task<ImageExtractionResult> ExtractUICardsAsync(byte[] imageBytes, string mimeType, int minSize = 40)
```

---

# 🚀 5. ADVANCED AI SERVICES

**Interface:** `IAdvancedAIService`

---

## 5.1 Document Intelligence

### 5.1.1 General Document Analysis

#### 🔧 Function
```csharp
Task<DocumentAnalysisResult> AnalyzeDocumentAsync(byte[] documentBytes, string modelId = "prebuilt-document")
```

### 5.1.2 Invoice Analysis

#### 🔧 Function
```csharp
Task<InvoiceResult> AnalyzeInvoiceAsync(byte[] documentBytes)
```

#### 📤 Returns
- Vendor name, address
- Invoice number, date, due date
- Line items with quantities and prices
- Subtotal, tax, total

### 5.1.3 Receipt Analysis

#### 🔧 Function
```csharp
Task<ReceiptResult> AnalyzeReceiptAsync(byte[] documentBytes)
```

### 5.1.4 ID Document Analysis

#### 🔧 Function
```csharp
Task<IdDocumentResult> AnalyzeIdDocumentAsync(byte[] documentBytes)
```

---

## 5.2 Image Operations

### 5.2.1 Edit Image (DALL-E)

#### 📋 Description
Edit specific regions of an image using a mask and text prompt.

#### 🔧 Function
```csharp
Task<ImageEditResult> EditImageAsync(byte[] imageBytes, byte[] maskBytes, string prompt)
```

### 5.2.2 Create Variations

#### 🔧 Function
```csharp
Task<ImageVariationResult> CreateImageVariationAsync(byte[] imageBytes, int count = 1)
```

### 5.2.3 Upscale Image

#### 🔧 Function
```csharp
Task<ImageEnhanceResult> UpscaleImageAsync(byte[] imageBytes, int scaleFactor = 2)
```

---

## 📊 Quick Reference Summary

| Service | Function Count | Status |
|---------|---------------|--------|
| Azure OpenAI | 3 | ✅ Ready |
| Azure Vision 4.0 | 10 | ✅ Ready |
| Azure Vision 3.2 | 5 | ✅ Ready |
| GPT-4o Vision | 20 | ✅ Ready |
| Image Extraction | 10 | ✅ Ready |
| Advanced Services | 16 | ✅ Ready |
| **Total** | **64** | **~95%** |

---

## 🔑 Configuration

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://oai-eus2-nonprod.openai.azure.com/",
    "DefaultDeployment": "gpt-4o",
    "VisionDeployment": "gpt-4o",
    "ImageDeployment": "dall-e-3",
    "VisionEndpoint": "https://your-vision.cognitiveservices.azure.com/",
    "VisionKey": "your-vision-key"
  }
}
```

---

*Last Updated: January 26, 2026*
