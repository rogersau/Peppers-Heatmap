using System.Text.Json.Serialization;
using HeatMapAPI.Converters;

namespace HeatMapAPI.Models
{
    public class HeatmapResponse
    {
        public string ImageFormat { get; set; } = "png";
        
        [JsonConverter(typeof(Base64ImageConverter))]
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        
        public int TotalDeaths { get; set; }
        public int OutOfBoundsDeaths { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
