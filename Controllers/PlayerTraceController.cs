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
        private readonly ILogger<PlayerTraceController> _logger;

        public PlayerTraceController(IPlayerTraceService playerTraceService, ILogger<PlayerTraceController> logger)
        {
            _playerTraceService = playerTraceService;
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
    }
}
