using AzureLogin.Web.Components;
using AzureLogin.Shared.Services;
using AzureLogin.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add device-specific services used by the AzureLogin.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Register URL launcher for opening browser
builder.Services.AddScoped<IUrlLauncher, WebUrlLauncher>();

// Configure Azure OpenAI settings from configuration
builder.Services.Configure<AzureOpenAISettings>(
    builder.Configuration.GetSection(AzureOpenAISettings.SectionName));

// Register Azure OpenAI service
builder.Services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();

// Register Azure Vision service for pixel-accurate image detection
builder.Services.AddSingleton<IAzureVisionService, AzureVisionService>();

// Register Image to Code service
builder.Services.AddSingleton<IImageToCodeService, ImageToCodeService>();

// Register Vision service for comprehensive image analysis
builder.Services.AddSingleton<IVisionService, VisionService>();

// Register Image Extraction service for extracting images from composites
builder.Services.AddSingleton<IImageExtractionService, ImageExtractionService>();

// Register Dynamic Razor Renderer for live preview of generated Razor components
builder.Services.AddSingleton<IDynamicRazorRenderer, DynamicRazorRenderer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(AzureLogin.Shared._Imports).Assembly);

app.Run();

