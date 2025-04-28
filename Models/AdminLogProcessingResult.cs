using HeatMapAPI.Models;
using System.Collections.Generic;

namespace HeatMapAPI.Models
{
    /// <summary>
    /// Represents the result of processing admin log files, including extracted positions and death metadata.
    /// </summary>
    public class AdminLogProcessingResult
    {
        /// <summary>
        /// List of positions extracted for the heatmap.
        /// </summary>
        public List<DeathPosition> Positions { get; set; } = new List<DeathPosition>();

        /// <summary>
        /// Count of deaths caused by zombies/infected.
        /// </summary>
        public int ZombieDeaths { get; set; } = 0;

        /// <summary>
        /// Count of deaths caused by melee weapons (player vs player, no distance specified).
        /// </summary>
        public int MeleeDeaths { get; set; } = 0;

        /// <summary>
        /// Count of deaths caused by guns (player vs player, distance specified).
        /// </summary>
        public int GunDeaths { get; set; } = 0;

        /// <summary>
        /// Count of suicides or other non-combat deaths.
        /// </summary>
        public int SuicidesOrOtherDeaths { get; set; } = 0;

        /// <summary>
        /// The furthest recorded kill distance in meters.
        /// </summary>
        public float FurthestKillDistance { get; set; } = 0.0f;

        /// <summary>
        /// Total number of death events processed.
        /// </summary>
        public int TotalDeaths => ZombieDeaths + MeleeDeaths + GunDeaths + SuicidesOrOtherDeaths;

        /// <summary>
        /// Number of positions that were outside the map bounds.
        /// </summary>
        public int PositionsOutOfBounds { get; set; } = 0;

        /// <summary>
        /// Total number of lines processed from the logs.
        /// </summary>
        public int LinesProcessed { get; set; } = 0;
    }

    /// <summary>
    /// Enum to categorize death types.
    /// </summary>
    internal enum DeathCategory
    {
        None,
        Zombie,
        Melee,
        Gun,
        SuicideOrOther
    }
}
