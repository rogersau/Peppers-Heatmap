using System.Globalization;
using HeatMapAPI.Models;

namespace HeatMapAPI.Services
{
    public class FileProcessingService : IFileProcessingService
    {
        private readonly ILogger<FileProcessingService> _logger;

        public FileProcessingService(ILogger<FileProcessingService> logger)
        {
            _logger = logger;
        }

        // This method now primarily focuses on returning positions for the map.
        // The accurate death count logic is handled separately or within the calling service.
        public async Task<List<DeathPosition>> ProcessAdminLogFileAsync(Stream fileStream, int mapSize, string outputContent)
        {
            List<DeathPosition> positions = new List<DeathPosition>();
            int outsideBounds = 0;
            int linesProcessedForPosition = 0;

            using StreamReader reader = new StreamReader(fileStream);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                // Use ShouldIncludePositionForHeatmap to decide if a position should be added for the map
                if (ShouldIncludePositionForHeatmap(line, outputContent, out float weight))
                {
                    if (TryExtractPosition(line, out float posX, out float posY))
                    {
                        linesProcessedForPosition++;
                        if (IsPositionWithinBounds(posX, posY, mapSize))
                        {
                            positions.Add(new DeathPosition { X = posX, Y = posY, Weight = weight });
                        }
                        else
                        {
                            outsideBounds++;
                        }
                    }
                }
            }

            _logger.LogInformation("Processed file for map positions: Added {PositionCount} positions ({OutOfBounds} out of bounds).", 
                positions.Count, outsideBounds);
            
            return positions;
        }

        public async Task<List<DeathPosition>> ProcessAdminLogDirectoryAsync(string directoryPath, int mapSize, string outputContent)
        {
            List<DeathPosition> allPositions = new List<DeathPosition>(); // Renamed to avoid confusion
            
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory doesn't exist: {DirectoryPath}", directoryPath);
                return allPositions;
            }
            
            string[] files = Directory.GetFiles(directoryPath, "*.ADM", SearchOption.AllDirectories);
            foreach (string fileName in files)
            {
                try
                {
                    using FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    // ProcessAdminLogFileAsync now only returns positions for the map
                    var filePositions = await ProcessAdminLogFileAsync(fileStream, mapSize, outputContent);
                    allPositions.AddRange(filePositions);
                    _logger.LogInformation("Processed file {Filename} for map positions.", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file {Filename} for map positions.", fileName);
                }
            }
            
            return allPositions;
        }

        public async Task<Dictionary<string, List<PlayerPosition>>> ProcessPlayerPositionsFromFileAsync(Stream fileStream, int mapSize)
        {
            Dictionary<string, List<PlayerPosition>> playerPositions = new Dictionary<string, List<PlayerPosition>>();

            using StreamReader reader = new StreamReader(fileStream);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (TryExtractPlayerPosition(line, out string playerId, out float posX, out float posY, out DateTime timestamp))
                {
                    if (!playerPositions.ContainsKey(playerId))
                    {
                        playerPositions[playerId] = new List<PlayerPosition>();
                    }

                    if (IsPositionWithinBounds(posX, posY, mapSize))
                    {
                        playerPositions[playerId].Add(new PlayerPosition
                        {
                            X = posX,
                            Y = posY,
                            PlayerId = playerId,
                            Timestamp = timestamp
                        });
                    }
                }
            }

            return playerPositions;
        }

        public async Task<Dictionary<string, List<PlayerPosition>>> ProcessPlayerPositionsFromDirectoryAsync(string directoryPath, int mapSize)
        {
            Dictionary<string, List<PlayerPosition>> playerPositions = new Dictionary<string, List<PlayerPosition>>();

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory doesn't exist: {DirectoryPath}", directoryPath);
                return playerPositions;
            }

            string[] files = Directory.GetFiles(directoryPath, "*.ADM", SearchOption.AllDirectories);
            foreach (string fileName in files)
            {
                using FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                var filePlayerPositions = await ProcessPlayerPositionsFromFileAsync(fileStream, mapSize);

                foreach (var kvp in filePlayerPositions)
                {
                    if (!playerPositions.ContainsKey(kvp.Key))
                    {
                        playerPositions[kvp.Key] = new List<PlayerPosition>();
                    }
                    playerPositions[kvp.Key].AddRange(kvp.Value);
                }

                _logger.LogInformation("Processed file {Filename}", fileName);
            }

            return playerPositions;
        }

        private bool TryExtractPlayerPosition(string line, out string playerId, out float posX, out float posY, out DateTime timestamp)
        {
            playerId = string.Empty;
            posX = 0;
            posY = 0;
            timestamp = DateTime.MinValue;

            try
            {
                // Example: 12:05:03 | Player "Billy Bob Jane" (id=f88ophdx98bI3BP4aj-JXspPK1NHITcETtDvIdkh6yg= pos=<7210.9, 10886.3, 12.5>)

                // Extract timestamp
                int timestampEnd = line.IndexOf(" | ");
                if (timestampEnd == -1) return false;

                string timeString = line.Substring(0, timestampEnd).Trim();
                if (!DateTime.TryParseExact(timeString, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
                {
                    return false;
                }

                // Extract player id
                int idStart = line.IndexOf("(id=");
                int idEnd = line.IndexOf(" pos=<", idStart);
                if (idStart == -1 || idEnd == -1) return false;

                playerId = line.Substring(idStart + 4, idEnd - idStart - 4);

                // Extract position
                int posStart = line.IndexOf("pos=<");
                int posEnd = line.IndexOf('>', posStart);
                if (posStart == -1 || posEnd == -1) return false;

                string positionString = line.Substring(posStart + 5, posEnd - posStart - 5);
                string[] positionParts = positionString.Split(',');

                if (positionParts.Length >= 2 &&
                    float.TryParse(positionParts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out posX) &&
                    float.TryParse(positionParts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out posY))
                {
                    // Successfully parsed position
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                // Log exception but don't throw it to avoid breaking the processing
                return false;
            }
        }

        /// <summary>
        /// Checks if a log line represents an actual player death event (killed or died).
        /// More specific than ShouldProcessLine, intended only for accurate death counting.
        /// Made internal to be accessible by DeathMapService.
        /// </summary>
        internal bool IsActualDeathEvent(string line)
        {
            // Ignore chat lines
            if (line.Trim().StartsWith("CHAT:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check for specific death patterns
            bool isKill = line.Contains(" killed by "); // Covers player and zombie kills
            bool isSuicideOrOther = line.Contains(" died. Stats>"); 

            // Ensure it looks like a player event line (often contains "Player \"...")
            // This helps avoid accidental matches in other log types.
            bool looksLikePlayerEvent = line.Contains("Player \""); 

            return looksLikePlayerEvent && (isKill || isSuicideOrOther);
        }

        // Renamed from ShouldProcessLine for clarity - this determines if a line contributes a position to the heatmap
        // Made internal to be accessible by DeathMapService.
        internal bool ShouldIncludePositionForHeatmap(string line, string outputContent, out float weight)
        {
            // Default weight
            weight = 1.00f;
    
            // Check for zombie/infected kills first (weight 0.25)
            if (line.Contains("killed by Zmb"))
            {
                weight = 0.25f;
            }
            // Then check for kills involving meters
            else if (line.Contains("killed by") && line.Contains(" from ") && line.Contains(" meters"))
            {
                try
                {
                    int fromIndex = line.LastIndexOf(" from ");
                    int metersIndex = line.LastIndexOf(" meters");
                    if (fromIndex != -1 && metersIndex > fromIndex)
                    {
                        string distanceString = line.Substring(fromIndex + 6, metersIndex - (fromIndex + 6)).Trim();
                        if (float.TryParse(distanceString, NumberStyles.Any, CultureInfo.InvariantCulture, out float distance))
                        {
                            if (distance > 25.0f)
                            {
                                weight = 100.0f; // Higher weight for long-distance kills
                            }
                            else
                            {
                                weight = 50.0f; // Lower weight for short-distance kills
                            }
                        }
                        else
                        {
                            // Couldn't parse distance, use default player kill weight
                            weight = 50.0f; // Changed from 3.0f to be consistent
                        }
                    }
                    else
                    {
                        // Fallback if parsing fails, treat as a regular player kill
                        weight = 50.0f;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing distance from log line: {LogLine}", line);
                    weight = 50.0f; // Fallback weight on error
                }
            }
            // Handle other player kills or deaths not involving specific distance
            else if (line.Contains("killed by") || line.Contains("died. Stats>"))
            {
                // Use a moderate weight for unspecified player kills or suicides/other deaths
                weight = 50.0f; 
            }
            else 
            {
                // If none of the above death-related keywords match, assign zero weight 
                // and don't include it based on keywords alone.
                weight = 0;
                return false; 
            }
    
            // --- Filter based on outputContent --- (Note: outputContent is less relevant now for position inclusion)
            // This filtering might need reconsideration based on desired map output.
            // For now, let's assume 'all' includes any line that got a non-zero weight above.
            if (outputContent.ToLower() == "infected")
            {
                // Only include zombie kills in infected mode
                return line.Contains("killed by Zmb");
            }
            else // Default ("all" or unspecified)
            {
                // Include if any weight was assigned (meaning it matched one of the death keywords)
                return weight > 0; 
            }
        }

        // Made internal
        internal bool TryExtractPosition(string line, out float posX, out float posY)
        {
            posX = 0;
            posY = 0;
            
            int posIndex = line.IndexOf("pos=<");
            if (posIndex == -1) return false;
            
            int closeIndex = line.IndexOf('>', posIndex);
            if (closeIndex == -1) return false;

            string positionString = line.Substring(posIndex + 5, closeIndex - posIndex - 5);
            string[] positionParts = positionString.Split(',');
            
            if (positionParts.Length >= 2 &&
                float.TryParse(positionParts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out posX) &&
                float.TryParse(positionParts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out posY))
            {
                return true;
            }
            
            _logger.LogWarning("Failed to parse position from: {PositionString}", positionString);
            return false;
        }

        // Made internal
        internal bool IsPositionWithinBounds(float x, float y, int mapSize)
        {
            return x >= 0 && x < mapSize && y >= 0 && y < mapSize;
        }
    }
}
