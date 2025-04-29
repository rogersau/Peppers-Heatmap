using System.ComponentModel.DataAnnotations;

namespace HeatMapAPI.Models
{
    public class PlayerTraceDiscordRequest : PlayerTraceRequest
    {
        [Required]
        [Url]
        public string DiscordWebhookUrl { get; set; } = string.Empty; // Initialize
    }
}
