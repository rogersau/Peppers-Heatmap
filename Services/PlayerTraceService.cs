using HeatMapAPI.Helpers;
using HeatMapAPI.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace HeatMapAPI.Services
{
    public class PlayerTraceService : IPlayerTraceService
    {
        private readonly IFileProcessingService _fileProcessingService;
        private readonly ILogger<PlayerTraceService> _logger;
        private readonly IWebHostEnvironment _environment;

        public PlayerTraceService(
            IFileProcessingService fileProcessingService,
            ILogger<PlayerTraceService> logger,
            IWebHostEnvironment environment)
        {
            _fileProcessingService = fileProcessingService;
            _logger = logger;
            _environment = environment;
        }

        public async Task<PlayerTraceResponse> GeneratePlayerTraceFromFilesAsync(IFormFileCollection files, PlayerTraceConfiguration? config = null)
        {
            if (files == null || !files.Any())
            {
                return new PlayerTraceResponse
                {
                    Success = false,
                    ErrorMessage = "No files were provided"
                };
            }

            config ??= new PlayerTraceConfiguration();

            // Get map size from the MapName enum
            string mapNameString = MapHelper.GetMapNameString(config.MapName);
            int resolvedMapSize = MapSizeHelper.GetMapSize(mapNameString);

            if (resolvedMapSize == -1)
            {
                return new PlayerTraceResponse
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
                        // Set output size based on map image dimensions (use width)
                        config.OutputSize = mapImage.Width;
                    }
                    else
                    {
                        // Use default output size for the map
                        config.OutputSize = MapHelper.GetDefaultOutputSize(config.MapName);
                    }
                }
                else
                {
                    // Default output size if no specific image available
                    config.OutputSize = 1000;
                }
            }

            Dictionary<string, List<PlayerPosition>> playerPositions = new();
            int totalPositions = 0;

            foreach (var file in files)
            {
                if (file.FileName.EndsWith(".ADM", StringComparison.OrdinalIgnoreCase))
                {
                    var filePlayerPositions = await _fileProcessingService.ProcessPlayerPositionsFromFileAsync(
                        file.OpenReadStream(), resolvedMapSize);

                    foreach (var player in filePlayerPositions)
                    {
                        if (!playerPositions.ContainsKey(player.Key))
                        {
                            playerPositions[player.Key] = new List<PlayerPosition>();
                        }
                        playerPositions[player.Key].AddRange(player.Value);
                        totalPositions += player.Value.Count;
                    }
                }
            }

            if (playerPositions.Count == 0)
            {
                return new PlayerTraceResponse
                {
                    Success = false,
                    ErrorMessage = "No valid player positions found in the provided files"
                };
            }

            byte[] imageData = await GeneratePlayerTraceImageAsync(playerPositions, config);

            return new PlayerTraceResponse
            {
                Success = true,
                ImageData = imageData,
                TotalPlayers = playerPositions.Count,
                TotalPositions = totalPositions,
                ImageFormat = "png"
            };
        }

        public async Task<PlayerTraceResponse> GeneratePlayerTraceFromDirectoryAsync(string directoryPath, PlayerTraceConfiguration? config = null)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return new PlayerTraceResponse
                {
                    Success = false,
                    ErrorMessage = $"Directory not found: {directoryPath}"
                };
            }

            config ??= new PlayerTraceConfiguration();

            int resolvedMapSize = MapSizeHelper.GetMapSize(config.MapName.ToString());

            if (resolvedMapSize == -1)
            {
                return new PlayerTraceResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid map name specified"
                };
            }

            var playerPositions = await _fileProcessingService.ProcessPlayerPositionsFromDirectoryAsync(
                directoryPath, resolvedMapSize);

            if (playerPositions.Count == 0)
            {
                return new PlayerTraceResponse
                {
                    Success = false,
                    ErrorMessage = "No valid player positions found in the directory"
                };
            }

            int totalPositions = playerPositions.Sum(p => p.Value.Count);
            byte[] imageData = await GeneratePlayerTraceImageAsync(playerPositions, config);

            return new PlayerTraceResponse
            {
                Success = true,
                ImageData = imageData,
                TotalPlayers = playerPositions.Count,
                TotalPositions = totalPositions,
                ImageFormat = "png"
            };
        }

        public async Task<byte[]> GeneratePlayerTraceImageAsync(Dictionary<string, List<PlayerPosition>> playerPositions, PlayerTraceConfiguration config)
        {
            return await Task.Run(() =>
            {
                Image<Rgba32>? outputImage = null;
                int outputSize = config.OutputSize ?? 1000;
                string mapNameString = MapHelper.GetMapNameString(config.MapName);
                int mapSize = MapSizeHelper.GetMapSize(mapNameString);

                // Create image with background
                if (config.BackgroundImage != null)
                {
                    // User provided a custom background image
                    using (var stream = config.BackgroundImage.OpenReadStream())
                    {
                        outputImage = Image.Load<Rgba32>(stream);
                        outputImage.Mutate(x => x.Resize(outputSize, outputSize));
                    }
                }
                else
                {
                    // Try to load the background image from the server based on MapName
                    bool mapImageLoaded = false;
                    if (MapHelper.MapImageExists(_environment, config.MapName))
                    {
                        try
                        {
                            outputImage = MapHelper.LoadMapImageAsync(_environment, config.MapName).GetAwaiter().GetResult();
                            if (outputImage != null)
                            {
                                outputImage.Mutate(x => x.Resize(outputSize, outputSize));
                                mapImageLoaded = true;
                            }
                            else
                            {
                                string filePath = MapHelper.GetMapFilePath(_environment, config.MapName);
                                _logger.LogWarning("Map image file not loaded for {MapName}, path checked: {FilePath}", config.MapName, filePath);
                                mapImageLoaded = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            string filePath = MapHelper.GetMapFilePath(_environment, config.MapName);
                            _logger.LogError(ex, "Error loading map image for {MapName} from path {FilePath}", config.MapName, filePath);
                            mapImageLoaded = false;
                        }
                    }

                    if (!mapImageLoaded)
                    {
                        // Create a blank image if no background available
                        outputImage = new Image<Rgba32>(outputSize, outputSize);
                    }
                }

                // Create trace overlay
                using (Image<Rgba32> traceOverlay = new Image<Rgba32>(outputSize, outputSize))
                {
                    // Define the pen for drawing lines
                    var redColor = new Rgba32(255, 0, 0, 128); // Red with 50% transparency
                    
                    // Draw lines for each player
                    foreach (var player in playerPositions)
                    {
                        var positions = player.Value.OrderBy(p => p.Timestamp).ToList();
                        if (positions.Count < 2)
                            continue;

                        for (int i = 0; i < positions.Count - 1; i++)
                        {
                            // Convert map coordinates to image coordinates
                            var startX = (int)((positions[i].X / mapSize) * outputSize);
                            var startY = (outputSize - 1) - (int)((positions[i].Y / mapSize) * outputSize);
                            var endX = (int)((positions[i + 1].X / mapSize) * outputSize);
                            var endY = (outputSize - 1) - (int)((positions[i + 1].Y / mapSize) * outputSize);

                            // Draw line
                            // Replace the line causing the error:

                            // With the following corrected code:
                            traceOverlay.Mutate(ctx => ctx.DrawLine(
                                color: redColor,
                                thickness: config.LineThickness,
                                points: new PointF[] { new PointF(startX, startY), new PointF(endX, endY) }
                            ));
                        }
                    }

                    // Apply the trace overlay on top of the background
                    outputImage.Mutate(x => x.DrawImage(traceOverlay, new Point(0, 0), 1.0f));

                    using (MemoryStream ms = new MemoryStream())
                    {
                        outputImage.Save(ms, new PngEncoder());
                        return ms.ToArray();
                    }
                }
            });
        }
    }
}
