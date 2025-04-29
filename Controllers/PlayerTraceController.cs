using Microsoft.AspNetCore.Mvc;
using HeatMapAPI.Models;
using HeatMapAPI.Services;

namespace HeatMapAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlayerTraceController : ControllerBase
    {
        private readonly IPlayerTraceService _playerTraceService;
        private readonly IDiscordWebhookService _discordWebhookService;
        private readonly ILogger<PlayerTraceController> _logger;

        public PlayerTraceController(IPlayerTraceService playerTraceService, IDiscordWebhookService discordWebhookService, ILogger<PlayerTraceController> logger)
        {
            _playerTraceService = playerTraceService;
            _discordWebhookService = discordWebhookService;
            _logger = logger;
        }
        
        [HttpPost("generate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GeneratePlayerTrace([FromForm] PlayerTraceRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null");
            }

            if (request.AdminLogFiles == null || request.AdminLogFiles.Count == 0)
            {
                return BadRequest("Admin log files must be provided");
            }

            PlayerTraceResponse response = await _playerTraceService.GeneratePlayerTraceFromFilesAsync(
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PlayerTraceResponse>> GeneratePlayerTraceWithMetadata([FromForm] PlayerTraceRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null");
            }

            if (request.AdminLogFiles == null || request.AdminLogFiles.Count == 0)
            {
                return BadRequest("Admin log files must be provided");
            }

            PlayerTraceResponse response = await _playerTraceService.GeneratePlayerTraceFromFilesAsync(
                request.AdminLogFiles, 
                request.Configuration
            );
            
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("generateToDiscord")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DiscordResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(DiscordResponse))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(DiscordResponse))]
        public async Task<ActionResult<DiscordResponse>> GeneratePlayerTraceToDiscord([FromForm] PlayerTraceDiscordRequest request)
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

            // 1. Generate the player trace image
            PlayerTraceResponse playerTraceResponse = await _playerTraceService.GeneratePlayerTraceFromFilesAsync(
                request.AdminLogFiles,
                request.Configuration
            );

            if (!playerTraceResponse.Success || playerTraceResponse.ImageData == null)
            {
                _logger.LogError("Player trace generation failed: {ErrorMessage}", playerTraceResponse.ErrorMessage);
                return BadRequest(new DiscordResponse { Success = false, Message = $"Player trace generation failed: {playerTraceResponse.ErrorMessage}" });
            }

            // 2. Send the image to Discord
            try
            {
                string imageBase64 = Convert.ToBase64String(playerTraceResponse.ImageData);
                string mapName = request.Configuration?.MapName.ToString() ?? "Unknown";
                string title = "Player Trace Generated";
                string description = $"Player trace generated for map: {mapName}. Player ID: {request.Configuration?.PlayerId ?? "N/A"}. Processed {playerTraceResponse.LinesProcessed} log lines and found {playerTraceResponse.TotalPositions} positions.";

                DiscordResponse discordResponse = await _discordWebhookService.SendImageToWebhookAsync(
                    request.DiscordWebhookUrl,
                    imageBase64,
                    mapName,
                    title,
                    description
                );

                return discordResponse.Success ? Ok(discordResponse) : BadRequest(discordResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending player trace to Discord.");
                return StatusCode(StatusCodes.Status500InternalServerError, new DiscordResponse { Success = false, Message = $"An internal error occurred while sending to Discord: {ex.Message}" });
            }
        }
    }
}
