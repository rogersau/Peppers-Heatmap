namespace HeatMapAPI.Models
{
    public class PlayerTraceRequest
    {
        /// <summary>
        /// Collection of admin log files to be processed.
        /// </summary>
        public IFormFileCollection? AdminLogFiles { get; set; }

        public PlayerTraceConfiguration? Configuration { get; set; }
    }
}
