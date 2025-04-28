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
        private readonly ILogger<DeathMapController> _logger;

        public DeathMapController(IDeathMapService deathMapService, ILogger<DeathMapController> logger)
        {
            _deathMapService = deathMapService;
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<HeatmapResponse>> GenerateHeatmapWithMetadata([FromForm] HeatmapRequest request)
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
            
            return response.Success ? Ok(response) : BadRequest(response);
        }
    }
}
