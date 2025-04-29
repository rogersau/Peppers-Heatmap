using HeatMapAPI.Models;

namespace HeatMapAPI.Services
{
    public interface IDiscordWebhookService
    {
        Task<DiscordResponse> SendImageToWebhookAsync(string webhookUrl, string imageBase64, string mapName, string title, string description);
    }
}
