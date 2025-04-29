using HeatMapAPI.Converters;
using System.Text.Json.Serialization;

namespace HeatMapAPI.Models
{
    public class PlayerTraceResponse
    {
        public string ImageFormat { get; set; } = "png";
        
        [JsonConverter(typeof(Base64ImageConverter))]
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        
        public int TotalPlayers { get; set; }
        public int TotalPositions { get; set; }
        public int LinesProcessed { get; set; } // Added to track processed lines
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
