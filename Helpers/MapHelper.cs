using HeatMapAPI.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HeatMapAPI.Helpers
{
    public static class MapHelper
    {
        private static readonly Dictionary<MapName, string> _mapFileNames = new Dictionary<MapName, string>
        {
            { MapName.ChernarusPlus, "chernarusplus.png" },
            { MapName.Livonia, "livonia.png" },
            { MapName.Namalsk, "namalsk.png" },
            { MapName.Sakhal, "sakhal.png" },
            { MapName.Deadfall, "deadfall.png" }
        };

        public static string GetMapDirectory(IWebHostEnvironment environment)
        {
            // Changed to look in wwwroot/Maps instead of ContentRootPath/Maps
            return Path.Combine(environment.WebRootPath, "Maps");
        }

        public static string GetMapFilePath(IWebHostEnvironment environment, MapName mapName)
        {
            if (!_mapFileNames.ContainsKey(mapName))
                return string.Empty;
                
            return Path.Combine(GetMapDirectory(environment), _mapFileNames[mapName]);
        }

        public static bool MapImageExists(IWebHostEnvironment environment, MapName mapName)
        {
            string filePath = GetMapFilePath(environment, mapName);
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        }

        public static async Task<Image<Rgba32>?> LoadMapImageAsync(IWebHostEnvironment environment, MapName mapName)
        {
            string filePath = GetMapFilePath(environment, mapName);
            
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            return await Image.LoadAsync<Rgba32>(filePath);
        }

        public static string GetMapNameString(MapName mapName)
        {
            return mapName.ToString().ToLower();
        }

        public static int GetDefaultOutputSize(MapName mapName)
        {
            return mapName switch
            {
                MapName.Livonia or MapName.Namalsk => 1000,
                MapName.ChernarusPlus => 1200,
                _ => 1000
            };
        }
    }
}
