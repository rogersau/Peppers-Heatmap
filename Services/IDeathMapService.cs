using HeatMapAPI.Models;

namespace HeatMapAPI.Services
{
    public interface IDeathMapService
    {
        Task<HeatmapResponse> GenerateHeatmapFromFilesAsync(IFormFileCollection files, DeathMapConfiguration? config = null);
        Task<byte[]> GenerateHeatmapImageAsync(List<DeathPosition> positions, DeathMapConfiguration config);
        Task<byte[]> GeneratePixelMapImageAsync(List<DeathPosition> positions, DeathMapConfiguration config);
    }
}
