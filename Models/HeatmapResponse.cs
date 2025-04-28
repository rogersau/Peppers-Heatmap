using System.Text.Json.Serialization;
using HeatMapAPI.Converters;

namespace HeatMapAPI.Models
{
    public class HeatmapResponse
    {
        public string ImageFormat { get; set; } = "png";
        
        [JsonConverter(typeof(Base64ImageConverter))]
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        // Metadata fields
        public int ZombieDeaths { get; set; }
        public int MeleeDeaths { get; set; }
        public int GunDeaths { get; set; }
        public int SuicidesOrOtherDeaths { get; set; }
        public float FurthestKillDistance { get; set; }
        public int TotalDeaths { get; set; } // This will be calculated sum
        public int PositionsOutOfBounds { get; set; } // Renamed for clarity
        public int LinesProcessed { get; set; }

        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
