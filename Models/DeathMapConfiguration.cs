using System.ComponentModel;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace HeatMapAPI.Models
{
    public class DeathMapConfiguration
    {
        /// <summary>
        /// The map name to use for the death map.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [DefaultValue(MapName.Namalsk)]
        public MapName MapName { get; set; } = MapName.Namalsk;

        /// <summary>
        /// The output size in pixels. If not specified, it will be determined from the selected map's background image.
        /// </summary>
        [DefaultValue(1024)]
        public int? OutputSize { get; set; } = 1024;

        /// <summary>
        /// The output type (heat or pixel).
        /// </summary>
        [DefaultValue("heat")]
        public string OutputType { get; set; } = "heat"; // heat or pixel

        /// <summary>
        /// Optional custom background image. If not provided, the default map image will be used based on MapName.
        /// </summary>
        public IFormFile? BackgroundImage { get; set; }

        /// <summary>
        /// Determines whether to apply weighting to death positions. Defaults to false (unweighted).
        /// </summary>
        [DefaultValue(false)]
        public bool UseWeighting { get; set; } = false;
    }

    /// <summary>
    /// Available map names for DayZ heatmap generation
    /// </summary>
    public enum MapName
    {        
        [Display(Name = "Chernarus Plus")]
        ChernarusPlus,
        
        [Display(Name = "Livonia")]
        Livonia,
                
        [Display(Name = "Namalsk")]
        Namalsk,

        [Display(Name = "Sakhal")]
        Sakhal,

        [Display(Name = "Deadfall")]
        Deadfall,
    }
}
