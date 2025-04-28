using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Attributes;

namespace HeatMapAPI.Models
{
    public class HeatmapRequest
    {
        /// <summary>
        /// Collection of admin log files to be processed.
        /// </summary>

        public IFormFileCollection? AdminLogFiles { get; set; }

        public DeathMapConfiguration? Configuration { get; set; }
    }
}
