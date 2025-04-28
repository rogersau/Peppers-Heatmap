using HeatMapAPI.Models;

namespace HeatMapAPI.Services
{
    public interface IPlayerTraceService
    {
        Task<PlayerTraceResponse> GeneratePlayerTraceFromFilesAsync(IFormFileCollection files, PlayerTraceConfiguration? config = null);
        Task<PlayerTraceResponse> GeneratePlayerTraceFromDirectoryAsync(string directoryPath, PlayerTraceConfiguration? config = null);
        Task<byte[]> GeneratePlayerTraceImageAsync(Dictionary<string, List<PlayerPosition>> playerPositions, PlayerTraceConfiguration config);
    }
}
