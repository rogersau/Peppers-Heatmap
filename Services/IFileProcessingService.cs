using HeatMapAPI.Models;

namespace HeatMapAPI.Services
{
    public interface IFileProcessingService
    {
        Task<List<DeathPosition>> ProcessAdminLogFileAsync(Stream fileStream, int mapSize, string outputContent);
        Task<List<DeathPosition>> ProcessAdminLogDirectoryAsync(string directoryPath, int mapSize, string outputContent);
        Task<Dictionary<string, List<PlayerPosition>>> ProcessPlayerPositionsFromFileAsync(Stream fileStream, int mapSize);
        Task<Dictionary<string, List<PlayerPosition>>> ProcessPlayerPositionsFromDirectoryAsync(string directoryPath, int mapSize);
    }
}
