using Microsoft.AspNetCore.Mvc;
using HeatMapAPI.Models;
using HeatMapAPI.Services;

namespace HeatMapAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeathMapController : ControllerBase
    {
        private readonly IDeathMapService _deathMapService;
        private readonly IDiscordWebhookService _discordWebhookService; // Inject Discord service
        private readonly ILogger<DeathMapController> _logger;

        public DeathMapController(IDeathMapService deathMapService, IDiscordWebhookService discordWebhookService, ILogger<DeathMapController> logger)
        {
            _deathMapService = deathMapService;
            _discordWebhookService = discordWebhookService; // Assign Discord service
            _logger = logger;
        }
        
        [HttpPost("generate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GenerateHeatmap([FromForm] HeatmapRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null");
            }

            if (request.AdminLogFiles == null || request.AdminLogFiles.Count == 0)
            {
                return BadRequest("Admin log files must be provided");
            }

            HeatmapResponse response = await _deathMapService.GenerateHeatmapFromFilesAsync(
                request.AdminLogFiles, 
                request.Configuration
            );

            if (!response.Success)
            {
                return BadRequest(response.ErrorMessage);
            }

            return File(response.ImageData, "image/png");
        }
        
        [HttpPost("generateWithMetadata")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HeatmapResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(HeatmapResponse))]
        public async Task<ActionResult<HeatmapResponse>> GenerateHeatmapWithMetadata([FromForm] HeatmapRequest request)
        {
            if (request == null)
            {
                return BadRequest(new HeatmapResponse { Success = false, ErrorMessage = "Request cannot be null" });
            }

            if (request.AdminLogFiles == null || request.AdminLogFiles.Count == 0)
            {
                return BadRequest(new HeatmapResponse { Success = false, ErrorMessage = "Admin log files must be provided" });
            }

            HeatmapResponse response = await _deathMapService.GenerateHeatmapFromFilesAsync(
                request.AdminLogFiles,
                request.Configuration
            );

            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("generateToDiscord")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DiscordResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(DiscordResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(DiscordResponse))]
        public async Task<ActionResult<DiscordResponse>> GenerateHeatmapToDiscord([FromForm] DeathMapDiscordRequest request)
        {
            if (request == null)
            {
                return BadRequest(new DiscordResponse { Success = false, Message = "Request cannot be null" });
            }

            if (request.AdminLogFiles == null || request.AdminLogFiles.Count == 0)
            {
                return BadRequest(new DiscordResponse { Success = false, Message = "Admin log files must be provided" });
            }

            if (string.IsNullOrWhiteSpace(request.DiscordWebhookUrl))
            {
                return BadRequest(new DiscordResponse { Success = false, Message = "Discord webhook URL must be provided" });
            }

            // 1. Generate the heatmap image
            HeatmapResponse heatmapResponse = await _deathMapService.GenerateHeatmapFromFilesAsync(
                request.AdminLogFiles,
                request.Configuration
            );

            if (!heatmapResponse.Success || heatmapResponse.ImageData == null)
            {
                _logger.LogError("Heatmap generation failed: {ErrorMessage}", heatmapResponse.ErrorMessage);
                return BadRequest(new DiscordResponse { Success = false, Message = $"Heatmap generation failed: {heatmapResponse.ErrorMessage}" });
            }

            // 2. Send the image to Discord
            try
            {
                string imageBase64 = Convert.ToBase64String(heatmapResponse.ImageData);
                string mapName = request.Configuration?.MapName.ToString() ?? "Unknown";
                string title = "Death Heatmap Generated";
                // Updated description format with Discord timestamp
                long unixTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
                string description = $"**Map:** {mapName}\n" +
                                     $"**Total Deaths:** {heatmapResponse.TotalDeaths}\n" +
                                     $"**Generated:** <t:{unixTimestamp}:D>";

                DiscordResponse discordResponse = await _discordWebhookService.SendImageToWebhookAsync(
                    request.DiscordWebhookUrl,
                    imageBase64,
                    mapName, // Keep mapName for potential filename use in the service
                    title,
                    description
                );

                return discordResponse.Success ? Ok(discordResponse) : BadRequest(discordResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heatmap to Discord.");
                return StatusCode(StatusCodes.Status500InternalServerError, new DiscordResponse { Success = false, Message = $"An internal error occurred while sending to Discord: {ex.Message}" });
            }
        }
    }
}
