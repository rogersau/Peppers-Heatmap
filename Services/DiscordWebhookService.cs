using HeatMapAPI.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace HeatMapAPI.Services
{
    public class DiscordWebhookService : IDiscordWebhookService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DiscordWebhookService> _logger;

        public DiscordWebhookService(IHttpClientFactory httpClientFactory, ILogger<DiscordWebhookService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<DiscordResponse> SendImageToWebhookAsync(string webhookUrl, string imageBase64, string mapName, string title, string description)
        {
            var client = _httpClientFactory.CreateClient();

            // Discord expects the image data as an attachment.
            // We need to send a multipart/form-data request.

            try
            {
                using var multipartFormContent = new MultipartFormDataContent();

                // Add JSON payload for embed
                var payload = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = title,
                            description = description,
                            image = new { url = "attachment://image.png" }, // Reference the attached image
                            fields = new[]
                            {
                                new { name = "Map", value = mapName, inline = true }
                            },
                            color = 0x00ff00 // Green color, you can customize this
                        }
                    }
                };
                var jsonPayload = JsonSerializer.Serialize(payload);
                multipartFormContent.Add(new StringContent(jsonPayload, Encoding.UTF8, "application/json"), "payload_json");

                // Add image file
                var imageBytes = Convert.FromBase64String(imageBase64);
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                multipartFormContent.Add(imageContent, "file", "image.png");

                var response = await client.PostAsync(webhookUrl, multipartFormContent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent image to Discord webhook: {WebhookUrl}", webhookUrl);
                    return new DiscordResponse { Success = true, Message = "Image successfully sent to Discord." };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send image to Discord webhook: {WebhookUrl}. Status: {StatusCode}. Response: {ErrorContent}", webhookUrl, response.StatusCode, errorContent);
                    return new DiscordResponse { Success = false, Message = $"Failed to send image to Discord. Status code: {response.StatusCode}. Error: {errorContent}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending image to Discord webhook: {WebhookUrl}", webhookUrl);
                return new DiscordResponse { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }
    }
}
