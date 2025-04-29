using HeatMapAPI.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using HeatMapAPI.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DayZ Heatmap API",
        Version = "v1",
        Description = "API for generating death heatmaps for DayZ servers"
    });
    
    // Include XML documentation comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
    
    // Configure enum values to be displayed as strings
    options.SchemaFilter<EnumSchemaFilter>();
});

// Register DeathMap services
builder.Services.AddScoped<IFileProcessingService, FileProcessingService>();
builder.Services.AddScoped<IDeathMapService, DeathMapService>();
builder.Services.AddScoped<IPlayerTraceService, PlayerTraceService>();
builder.Services.AddScoped<IDiscordWebhookService, DiscordWebhookService>(); // Add Discord service

// Add HttpClientFactory for making HTTP requests (used by DiscordWebhookService)
builder.Services.AddHttpClient();

// Add System.Drawing.Common compatibility for Linux
if (OperatingSystem.IsLinux())
{
    // This allows System.Drawing to work on Linux without libgdiplus
    AppContext.SetSwitch("System.Drawing.EnableUnixSupport", true);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Add static files middleware to serve files from wwwroot
app.UseAuthorization();
app.MapControllers();

// Ensure the Maps directory exists in wwwroot
var mapDirectory = Path.Combine(builder.Environment.WebRootPath, "Maps");
if (!Directory.Exists(mapDirectory))
{
    Directory.CreateDirectory(mapDirectory);
}

app.Run();
