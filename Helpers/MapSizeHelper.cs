namespace HeatMapAPI.Helpers
{
    public static class MapSizeHelper
    {
        public static int GetMapSize(string mapName)
        {
            return mapName.ToLower() switch
            {
                "livonia" => 12800,
                "chernarusplus" => 15360,
                "namalsk" => 12800,
                "sakhal" => 15360,
                "deafall" => 10280,

                _ => int.TryParse(mapName, out int size) ? size : -1
            };
        }
    }
}
