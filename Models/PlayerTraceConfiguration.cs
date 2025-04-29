using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HeatMapAPI.Models
{
    public class PlayerTraceConfiguration
    {
        /// <summary>
        /// The map name to use for the player trace map.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [DefaultValue(MapName.Namalsk)]
        public MapName MapName { get; set; } = MapName.Namalsk;

        /// <summary>
        /// The output size in pixels. If not specified, it will be determined from the selected map's background image.
        /// </summary>
        [DefaultValue(1024)]
        public int? OutputSize { get; set; } = null;

        /// <summary>
        /// Optional custom background image. If not provided, the default map image will be used based on MapName.
        /// </summary>
        public IFormFile? BackgroundImage { get; set; }

        /// <summary>
        /// The ID of the player to trace.
        /// </summary>
        [DefaultValue("")]
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// Line thickness for player traces.
        /// </summary>
        [DefaultValue(1)]
        public float LineThickness { get; set; } = 1;
    }
}
