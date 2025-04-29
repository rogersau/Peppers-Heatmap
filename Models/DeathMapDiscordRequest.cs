using System.ComponentModel.DataAnnotations;

namespace HeatMapAPI.Models
{
    public class DeathMapDiscordRequest : HeatmapRequest
    {
        [Required]
        [Url]
        public string DiscordWebhookUrl { get; set; } = string.Empty; // Initialize
    }
}
