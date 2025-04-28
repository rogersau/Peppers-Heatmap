using HeatMapAPI.Helpers;
using HeatMapAPI.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace HeatMapAPI.Services
{
    public class DeathMapService : IDeathMapService
    {
        private readonly IFileProcessingService _fileProcessingService;
        private readonly ILogger<DeathMapService> _logger;
        private readonly IWebHostEnvironment _environment;

        public DeathMapService(
            IFileProcessingService fileProcessingService, 
            ILogger<DeathMapService> logger,
            IWebHostEnvironment environment)
        {
            _fileProcessingService = fileProcessingService ?? throw new ArgumentNullException(nameof(fileProcessingService));
            _logger = logger;
            _environment = environment;
        }

        public async Task<HeatmapResponse> GenerateHeatmapFromFilesAsync(IFormFileCollection files, DeathMapConfiguration? config = null)
        {
            if (files == null || !files.Any())
            {
                return new HeatmapResponse 
                { 
                    Success = false, 
                    ErrorMessage = "No files were provided" 
                };
            }

            config ??= new DeathMapConfiguration();
            
            // Validate OutputType
            if (config.OutputType.ToLower() != "heat" && config.OutputType.ToLower() != "pixel")
            {
                return new HeatmapResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid output type specified. Must be 'heat' or 'pixel'"
                };
            }
            
            // Get map size from the MapName enum
            string mapNameString = MapHelper.GetMapNameString(config.MapName);
            int resolvedMapSize = MapSizeHelper.GetMapSize(mapNameString);
            
            if (resolvedMapSize == -1)
            {
                return new HeatmapResponse 
                { 
                    Success = false, 
                    ErrorMessage = "Invalid map name specified" 
                };
            }

            // Handle output size based on background image
            if (!config.OutputSize.HasValue)
            {
                if (config.BackgroundImage == null && MapHelper.MapImageExists(_environment, config.MapName))
                {
                    using var mapImage = await MapHelper.LoadMapImageAsync(_environment, config.MapName);
                    if (mapImage != null)
                    {
                        config.OutputSize = mapImage.Width;
                    }
                    else
                    {
                        config.OutputSize = MapHelper.GetDefaultOutputSize(config.MapName);
                    }
                }
                else
                {
                    config.OutputSize = 1000;
                }
            }

            List<DeathPosition> positions = new List<DeathPosition>();
            int totalActualDeaths = 0;
            int outOfBoundsPositions = 0;

            var fileProcessor = _fileProcessingService as FileProcessingService;
            if (fileProcessor == null)
            {
                _logger.LogError("Could not cast IFileProcessingService to FileProcessingService to access internal methods.");
                return new HeatmapResponse { Success = false, ErrorMessage = "Internal server error during file processing setup." };
            }

            foreach (var file in files)
            {
                if (file.FileName.EndsWith(".ADM", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var stream = file.OpenReadStream();
                        using var streamReader = new StreamReader(stream);
                        string? line;
                        int fileDeaths = 0;
                        List<DeathPosition> filePositions = new List<DeathPosition>();

                        while ((line = await streamReader.ReadLineAsync()) != null)
                        {
                            if (fileProcessor.IsActualDeathEvent(line))
                            {
                                fileDeaths++;
                            }

                            if (fileProcessor.ShouldIncludePositionForHeatmap(line, "all", out float weight))
                            {
                                if (fileProcessor.TryExtractPosition(line, out float posX, out float posY))
                                {
                                    if (fileProcessor.IsPositionWithinBounds(posX, posY, resolvedMapSize))
                                    {
                                        filePositions.Add(new DeathPosition { X = posX, Y = posY, Weight = weight });
                                    }
                                    else
                                    {
                                        outOfBoundsPositions++;
                                    }
                                }
                            }
                        }
                        
                        positions.AddRange(filePositions);
                        totalActualDeaths += fileDeaths;
                        _logger.LogInformation("Processed file {FileName}: Found {FileDeaths} deaths, added {FilePositionsCount} positions for map.", 
                            file.FileName, fileDeaths, filePositions.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file {FileName}", file.FileName);
                    }
                }
            }

            if (positions.Count == 0 && totalActualDeaths == 0)
            {
                return new HeatmapResponse 
                { 
                    Success = false, 
                    ErrorMessage = "No valid death positions or death events found in the provided files" 
                };
            }
            else if (positions.Count == 0)
            {
                _logger.LogWarning("No positions were added for map generation, although {ActualDeaths} death events were counted.", totalActualDeaths);
                return new HeatmapResponse { Success = false, ErrorMessage = "No valid positions found for map generation." };
            }
            else if (totalActualDeaths == 0)
            {
                _logger.LogWarning("No actual death events were counted, although {PositionCount} positions were added for map generation.", positions.Count);
            }

            byte[] imageData;
            if (config.OutputType.ToLower() == "heat")
            {
                imageData = await GenerateHeatmapImageAsync(positions, config);
            }
            else
            {
                imageData = await GeneratePixelMapImageAsync(positions, config);
            }

            return new HeatmapResponse 
            { 
                Success = true, 
                ImageData = imageData,
                TotalDeaths = totalActualDeaths,
                OutOfBoundsDeaths = outOfBoundsPositions,
                ImageFormat = "png"
            };
        }

        public async Task<HeatmapResponse> GenerateHeatmapFromDirectoryAsync(string directoryPath, DeathMapConfiguration? config = null)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return new HeatmapResponse 
                { 
                    Success = false, 
                    ErrorMessage = $"Directory not found: {directoryPath}" 
                };
            }

            config ??= new DeathMapConfiguration();
            
            // Validate OutputType
            if (config.OutputType.ToLower() != "heat" && config.OutputType.ToLower() != "pixel")
            {
                return new HeatmapResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid output type specified. Must be 'heat' or 'pixel'"
                };
            }
            
            int resolvedMapSize = MapSizeHelper.GetMapSize(config.MapName.ToString());
            
            if (resolvedMapSize == -1)
            {
                return new HeatmapResponse 
                { 
                    Success = false, 
                    ErrorMessage = "Invalid map name specified" 
                };
            }

            var positions = await _fileProcessingService.ProcessAdminLogDirectoryAsync(
                directoryPath, resolvedMapSize, "all");

            if (positions.Count == 0)
            {
                return new HeatmapResponse 
                { 
                    Success = false, 
                    ErrorMessage = "No valid death positions found in the directory" 
                };
            }

            byte[] imageData;
            if (config.OutputType.ToLower() == "heat")
            {
                imageData = await GenerateHeatmapImageAsync(positions, config);
            }
            else
            {
                imageData = await GeneratePixelMapImageAsync(positions, config);
            }

            return new HeatmapResponse 
            { 
                Success = true, 
                ImageData = imageData,
                TotalDeaths = positions.Count,
                ImageFormat = "png"
            };
        }

        public async Task<byte[]> GenerateHeatmapImageAsync(List<DeathPosition> positions, DeathMapConfiguration config)
        {
            return await Task.Run(() => {
                int outputSize = config.OutputSize ?? 1000;
                string mapNameString = MapHelper.GetMapNameString(config.MapName);
                int mapSize = MapSizeHelper.GetMapSize(mapNameString);

                Image<Rgba32> outputHeatMap = new Image<Rgba32>(outputSize, outputSize);
                
                float[,] intensityGrid = new float[outputSize, outputSize];
                
                foreach (var position in positions)
                {
                    int posX = (int)((position.X / mapSize) * outputSize);
                    int posY = (outputSize - 1) - (int)((position.Y / mapSize) * outputSize);

                    int sizeOfDot = (int)(0.075 * outputSize);
                    if (sizeOfDot % 2 != 0)
                    {
                        sizeOfDot -= 1;
                    }
                    
                    int bxmax = Math.Min(posX + (sizeOfDot / 2) + 1, outputSize);
                    int bxmin = Math.Max(0, posX - (sizeOfDot / 2));
                    int bymax = Math.Min(posY + (sizeOfDot / 2) + 1, outputSize);
                    int bymin = Math.Max(0, posY - (sizeOfDot / 2));

                    float sigma = sizeOfDot / 7.0f;
                    for (int x = bxmin; x < bxmax; x++)
                    {
                        for (int y = bymin; y < bymax; y++)
                        {
                            float weightToUse = config.UseWeighting ? position.Weight : 1.0f;
                            float weightFactor = (float)Math.Pow(weightToUse, 2);
                            intensityGrid[x, y] += (float)(Math.Exp(-1 * ((x - posX) * (x - posX) + (y - posY) * (y - posY)) / 2.0 / sigma / sigma) / (Math.Sqrt(sigma * sigma / 2.0 / Math.PI))) * weightFactor;
                        }
                    }
                }

                float max = 0.0f;
                foreach (float e in intensityGrid)
                {
                    if (e > max)
                        max = e;
                }
                
                Rgba32[] palette = new Rgba32[256];
                for (int i = 0; i < 256; i++)
                {
                    palette[i] = GetBlackBlueRedWhiteColor(i / 255.0f);
                }

                if (config.BackgroundImage != null)
                {
                    try
                    {
                        using (var stream = config.BackgroundImage.OpenReadStream())
                        {
                            outputHeatMap = Image.Load<Rgba32>(stream);
                            outputHeatMap.Mutate(x => x.Resize(outputSize, outputSize));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading custom background image.");
                        outputHeatMap = new Image<Rgba32>(outputSize, outputSize); 
                    }
                }
                else if (MapHelper.MapImageExists(_environment, config.MapName))
                {
                    try
                    {
                        var loadedImage = MapHelper.LoadMapImageAsync(_environment, config.MapName).GetAwaiter().GetResult();
                        if (loadedImage != null)
                        {
                            outputHeatMap = loadedImage;
                            outputHeatMap.Mutate(x => x.Resize(outputSize, outputSize));
                        }
                        else
                        {
                            string filePath = MapHelper.GetMapFilePath(_environment, config.MapName);
                            _logger.LogWarning("Map image file not loaded for {MapName}, path checked: {FilePath}. Using blank background.", config.MapName, filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        string filePath = MapHelper.GetMapFilePath(_environment, config.MapName);
                        _logger.LogError(ex, "Error loading map image for {MapName} from path {FilePath}. Using blank background.", config.MapName, filePath);
                    }
                }

                using (Image<Rgba32> heatmapOverlay = new Image<Rgba32>(outputSize, outputSize))
                {
                    for (int i = 0; i < outputSize; i++)
                    {
                        for (int j = 0; j < outputSize; j++)
                        {
                            int colorIndex = (int)((intensityGrid[i, j] / max) * 255);
                            colorIndex = Math.Min(255, Math.Max(0, colorIndex));
                            
                            var color = palette[colorIndex];
                            heatmapOverlay[i, j] = new Rgba32(color.R, color.G, color.B, (byte)(255 * 0.9f));
                        }
                    }
                    
                    outputHeatMap.Mutate(x => x.DrawImage(heatmapOverlay, new Point(0, 0), 0.9f));
                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        outputHeatMap.Save(ms, new PngEncoder());
                        return ms.ToArray();
                    }
                }
            });
        }

        public async Task<byte[]> GeneratePixelMapImageAsync(List<DeathPosition> positions, DeathMapConfiguration config)
        {
            return await Task.Run(() => {
                int outputSize = config.OutputSize ?? 1000;
                string mapNameString = MapHelper.GetMapNameString(config.MapName);
                int mapSize = MapSizeHelper.GetMapSize(mapNameString);

                Image<Rgba32> outputPixelMap = new Image<Rgba32>(outputSize, outputSize);

                int dataGridSize = 100;
                float[] dataGrid = new float[dataGridSize * dataGridSize];
                
                foreach (var position in positions)
                {
                    int posX = (int)((position.X / mapSize) * dataGridSize);
                    int posY = (dataGridSize - 1) - (int)((position.Y / mapSize) * dataGridSize);
                    
                    if (posX >= 0 && posX < dataGridSize && posY >= 0 && posY < dataGridSize)
                    {
                        float valueToAdd = config.UseWeighting ? position.Weight : 1.0f;
                        dataGrid[posY * dataGridSize + posX] += valueToAdd;
                    }
                }

                float max = dataGrid.Max();

                if (config.BackgroundImage != null)
                {
                    try
                    {
                        using (var stream = config.BackgroundImage.OpenReadStream())
                        {
                            outputPixelMap = Image.Load<Rgba32>(stream);
                            outputPixelMap.Mutate(x => x.Resize(outputSize, outputSize));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading custom background image.");
                        outputPixelMap = new Image<Rgba32>(outputSize, outputSize);
                    }
                }
                else if (MapHelper.MapImageExists(_environment, config.MapName))
                {
                    try
                    {
                        var loadedImage = MapHelper.LoadMapImageAsync(_environment, config.MapName).GetAwaiter().GetResult();
                        if (loadedImage != null)
                        {
                            outputPixelMap = loadedImage;
                            outputPixelMap.Mutate(x => x.Resize(outputSize, outputSize));
                        }
                        else
                        {
                            string filePath = MapHelper.GetMapFilePath(_environment, config.MapName);
                            _logger.LogWarning("Map image file not loaded for {MapName}, path checked: {FilePath}. Using blank background.", config.MapName, filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading map image for {MapName}. Using blank background.", config.MapName);
                    }
                }

                using (Image<Rgba32> pixelOverlay = new Image<Rgba32>(outputSize, outputSize))
                {
                    int truePixelSize = outputSize / dataGridSize;
                    
                    Rgba32 brown = new Rgba32(165, 42, 42, 255);
                    Rgba32 orange = new Rgba32(255, 165, 0, 255);
                    Rgba32 yellow = new Rgba32(255, 255, 0, 255);
                    Rgba32 white = new Rgba32(255, 255, 255, 255);
                    Rgba32 transparent = new Rgba32(0, 0, 0, 0);

                    for (int i = 0; i < dataGrid.Length; i++)
                    {
                        int picPosX = i % dataGridSize, picPosY = i / dataGridSize;

                        Rgba32 pixelColor;
                        if (dataGrid[i] != 0)
                        {
                            pixelColor = new Rgba32(brown.R, brown.G, brown.B, 204);
                            if (dataGrid[i] > (max / 6))
                                pixelColor = new Rgba32(orange.R, orange.G, orange.B, 204);
                            if (dataGrid[i] > (max / 3))
                                pixelColor = new Rgba32(yellow.R, yellow.G, yellow.B, 204);
                            if (dataGrid[i] >= max)
                                pixelColor = new Rgba32(white.R, white.G, white.B, 204);
                        }
                        else
                        {
                            pixelColor = transparent;
                        }

                        for (int j = 0; j < truePixelSize; j++)
                        {
                            for (int k = 0; k < truePixelSize; k++)
                            {
                                int x = (picPosX * truePixelSize) + j;
                                int y = (picPosY * truePixelSize) + k;
                                
                                if (x < outputSize && y < outputSize)
                                {
                                    pixelOverlay[x, y] = pixelColor;
                                }
                            }
                        }
                    }

                    outputPixelMap.Mutate(x => x.DrawImage(pixelOverlay, new Point(0, 0), 0.8f));
                    
                    using (MemoryStream ms = new MemoryStream())
                    {
                        outputPixelMap.Save(ms, new PngEncoder());
                        return ms.ToArray();
                    }
                }
            });
        }

        private Rgba32 GetBlackBlueRedWhiteColor(float value)
        {
            value = Math.Clamp(value, 0.0f, 1.0f);

            if (value <= 1.0f / 3.0f)
            {
                float t = value * 3.0f;
                byte blue = (byte)(255 * t);
                return new Rgba32(0, 0, blue, 255);
            }
            else if (value <= 2.0f / 3.0f)
            {
                float t = (value - 1.0f / 3.0f) * 3.0f;
                byte red = (byte)(255 * t);
                byte blue = (byte)(255 * (1.0f - t));
                return new Rgba32(red, 0, blue, 255);
            }
            else
            {
                float t = (value - 2.0f / 3.0f) * 3.0f;
                byte green = (byte)(255 * t);
                byte blue = (byte)(255 * t);
                return new Rgba32(255, green, blue, 255);
            }
        }

        private Rgba32 GetBlackToRedToWhiteColor(float value)
        {
            if (value <= 0.0f)
                return new Rgba32(0, 0, 0, 255);
            else if (value <= 0.5f)
            {
                byte red = (byte)(255 * (value * 2));
                return new Rgba32(red, 0, 0, 255);
            }
            else
            {
                byte green = (byte)(255 * ((value - 0.5f) * 2));
                byte blue = (byte)(255 * ((value - 0.5f) * 2));
                return new Rgba32(255, green, blue, 255);
            }
        }
    }
}
