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

        // Updated to return AdminLogProcessingResult
        public async Task<AdminLogProcessingResult> ProcessAdminLogFileAsync(Stream fileStream, int mapSize, string outputContent)
        {
            var result = new AdminLogProcessingResult();

            using StreamReader reader = new StreamReader(fileStream);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                result.LinesProcessed++;

                // Categorize the death first
                var deathCategory = CategorizeDeath(line, out float distance);

                // Update furthest kill distance if applicable
                if (deathCategory == DeathCategory.Gun && distance > result.FurthestKillDistance)
                {
                    result.FurthestKillDistance = distance;
                }

                // Determine if the position should be included based on the category and outputContent filter
                if (ShouldIncludePositionForHeatmap(line, outputContent, deathCategory, out float weight))
                {
                    if (TryExtractPosition(line, out float posX, out float posY))
                    {
                        if (IsPositionWithinBounds(posX, posY, mapSize))
                        {
                            result.Positions.Add(new DeathPosition { X = posX, Y = posY, Weight = weight });
                        }
                        else
                        {
                            result.PositionsOutOfBounds++;
                        }
                    }
                }

                // Increment death counters based on category, regardless of heatmap inclusion or bounds
                switch (deathCategory)
                {
                    case DeathCategory.Zombie:
                        result.ZombieDeaths++;
                        break;
                    case DeathCategory.Melee:
                        result.MeleeDeaths++;
                        break;
                    case DeathCategory.Gun:
                        result.GunDeaths++;
                        break;
                    case DeathCategory.SuicideOrOther:
                        result.SuicidesOrOtherDeaths++;
                        break;
                }
            }

            _logger.LogInformation(
                "Processed file for map positions: Added {PositionCount} positions ({OutOfBounds} out of bounds). Deaths - Zombie: {ZombieDeaths}, Melee: {MeleeDeaths}, Gun: {GunDeaths}, Suicide/Other: {SuicideDeaths}. Furthest Kill: {FurthestKill}m. Total Lines: {TotalLines}",
                result.Positions.Count, result.PositionsOutOfBounds, result.ZombieDeaths, result.MeleeDeaths, result.GunDeaths, result.SuicidesOrOtherDeaths, result.FurthestKillDistance, result.LinesProcessed);

            return result;
        }

        // Updated to return and aggregate AdminLogProcessingResult
        public async Task<AdminLogProcessingResult> ProcessAdminLogDirectoryAsync(string directoryPath, int mapSize, string outputContent)
        {
            var aggregatedResult = new AdminLogProcessingResult();

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory doesn't exist: {DirectoryPath}", directoryPath);
                return aggregatedResult;
            }

            string[] files = Directory.GetFiles(directoryPath, "*.ADM", SearchOption.AllDirectories);
            foreach (string fileName in files)
            {
                try
                {
                    using FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    var fileResult = await ProcessAdminLogFileAsync(fileStream, mapSize, outputContent);

                    // Aggregate results
                    aggregatedResult.Positions.AddRange(fileResult.Positions);
                    aggregatedResult.ZombieDeaths += fileResult.ZombieDeaths;
                    aggregatedResult.MeleeDeaths += fileResult.MeleeDeaths;
                    aggregatedResult.GunDeaths += fileResult.GunDeaths;
                    aggregatedResult.SuicidesOrOtherDeaths += fileResult.SuicidesOrOtherDeaths;
                    aggregatedResult.FurthestKillDistance = Math.Max(aggregatedResult.FurthestKillDistance, fileResult.FurthestKillDistance);
                    aggregatedResult.PositionsOutOfBounds += fileResult.PositionsOutOfBounds;
                    aggregatedResult.LinesProcessed += fileResult.LinesProcessed;

                    _logger.LogInformation("Processed file {Filename} for map positions and stats.", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file {Filename} for map positions and stats.", fileName);
                }
            }

            return aggregatedResult;
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
        /// Categorizes the type of death based on the log line content.
        /// </summary>
        /// <param name="line">The log line.</param>
        /// <param name="distance">Outputs the kill distance if it's a gun kill, otherwise 0.</param>
        /// <returns>The category of death.</returns>
        internal DeathCategory CategorizeDeath(string line, out float distance)
        {
            distance = 0.0f;

            // Ignore chat lines immediately
            if (line.Trim().StartsWith("CHAT:", StringComparison.OrdinalIgnoreCase))
            {
                return DeathCategory.None;
            }

            bool isKill = line.Contains("killed by");
            bool isSuicideOrOther = line.Contains("died. Stats>");
            bool isZombieKill = line.Contains("killed by Zmb");
            bool hasDistance = line.Contains(" from ") && line.Contains(" meters");

            // Check for Zombie kill first
            if (isZombieKill)
            {
                return DeathCategory.Zombie;
            }

            // Check for Gun kill (has distance)
            if (isKill && hasDistance)
            {
                try
                {
                    int fromIndex = line.LastIndexOf(" from ");
                    int metersIndex = line.LastIndexOf(" meters");
                    if (fromIndex != -1 && metersIndex > fromIndex)
                    {
                        string distanceString = line.Substring(fromIndex + 6, metersIndex - (fromIndex + 6)).Trim();
                        if (float.TryParse(distanceString, NumberStyles.Any, CultureInfo.InvariantCulture, out distance))
                        {
                            // Successfully parsed distance
                            return DeathCategory.Gun;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing distance for categorization from log line: {LogLine}", line);
                    // Fallback to Melee if distance parsing fails but it's a kill
                    return DeathCategory.Melee;
                }
                // If it looks like a gun kill but parsing failed, categorize as Melee
                return DeathCategory.Melee;
            }

            // Check for Melee kill (player kill without distance)
            if (isKill && !isZombieKill && !hasDistance) // Ensure it's not a zombie kill we missed and no distance
            {
                return DeathCategory.Melee;
            }

            // Check for Suicide or other deaths
            if (isSuicideOrOther)
            {
                return DeathCategory.SuicideOrOther;
            }

            // If none of the above, it's not a death event we track
            return DeathCategory.None;
        }

        // Updated to use DeathCategory and assign weights based on it.
        internal bool ShouldIncludePositionForHeatmap(string line, string outputContent, DeathCategory category, out float weight)
        {
            weight = 0f; // Default to zero weight, meaning don't include

            // Assign weight based on category
            switch (category)
            {
                case DeathCategory.Zombie:
                    weight = 0.25f;
                    break;
                case DeathCategory.Melee:
                    weight = 50.0f; // Standard weight for close-quarters player kill
                    break;
                case DeathCategory.Gun:
                    // Extract distance again for weighting (could optimize later if needed)
                    if (TryExtractDistance(line, out float distance))
                    {
                         weight = (distance > 25.0f) ? 100.0f : 50.0f; // Higher for long range
                    }
                    else
                    {
                        weight = 50.0f; // Fallback if distance extraction fails here
                    }
                    break;
                case DeathCategory.SuicideOrOther:
                    weight = 50.0f; // Same weight as melee/standard kill
                    break;
                case DeathCategory.None:
                default:
                    return false; // Don't include non-death events
            }

            // --- Filter based on outputContent ---
            bool includeBasedOnContent = false;
            string lowerOutputContent = outputContent.ToLower();

            if (lowerOutputContent == "infected")
            {
                includeBasedOnContent = (category == DeathCategory.Zombie);
            }
            else if (lowerOutputContent == "pvp") // Added PvP filter
            {
                 includeBasedOnContent = (category == DeathCategory.Melee || category == DeathCategory.Gun);
            }
            else // Default ("all" or unspecified)
            {
                includeBasedOnContent = (category != DeathCategory.None); // Include any categorized death
            }

            return includeBasedOnContent && weight > 0;
        }

        // Helper to extract distance, used in ShouldIncludePositionForHeatmap
        internal bool TryExtractDistance(string line, out float distance)
        {
            distance = 0.0f;
            try
            {
                int fromIndex = line.LastIndexOf(" from ");
                int metersIndex = line.LastIndexOf(" meters");
                if (fromIndex != -1 && metersIndex > fromIndex)
                {
                    string distanceString = line.Substring(fromIndex + 6, metersIndex - (fromIndex + 6)).Trim();
                    if (float.TryParse(distanceString, NumberStyles.Any, CultureInfo.InvariantCulture, out distance))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                 _logger.LogWarning(ex, "Could not parse distance from line for weighting: {LogLine}", line);
            }
            return false;
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
