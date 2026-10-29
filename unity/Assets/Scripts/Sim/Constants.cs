namespace EmberVale.Sim
{
    /// <summary>Tuning values shared with ember-vale.html. Keep names and numbers in step with the JS.</summary>
    public static class K
    {
        public const int CS = 16;            // chunk size in tiles
        public const int DAY = 12;           // game seconds per day
        public const int SEASON_DAYS = 8;
        public const int YEAR = 32;          // days
        public const double BAND_SPEED = 0.8; // tiles per game second
        public const double SPEED = 2.1;     // colonists, tiles per game second
        public const int CAP = 100;          // what a village can store before it builds storehouses

        public static readonly int[] PopCap = { 25, 50, 100, 170, 260, 320 };
        public static readonly int[] Radius = { 5, 6, 8, 9, 11, 13 };
        public static readonly string[] AgeNames = { "Settlement", "Village", "Town", "City", "Kingdom", "Empire" };
        public static readonly int[] AgePop = { 0, 12, 30, 60, 110, 180 };   // people needed to reach each age
        public static readonly string[] AgeMark = { null, "chapel", "market", "cathedral", "castle", "palace" };   // landmark each age needs
        public const int MaxAge = 2;         // ported so far: Settlement, Village, Town

        public static readonly string[] Colors =
        {
            "#e3a246", "#3d7fd1", "#a24bc0", "#2f9a63", "#d0453a", "#3fa3a3", "#6a5acd",
            "#c9b03a", "#d4608f", "#8a5a2b", "#5b8c2a", "#c46b2e", "#7a8794", "#b03a6a",
        };

        public static readonly string[] FactionNames =
        {
            "Thornbury", "Greywater", "Oakhurst", "Larkspur", "Ravensby", "Millhaven", "Stonebridge", "Kestrel Hill",
            "Ashford", "Hollowell", "Wyndham", "Fallowmere", "Corrowmere", "Brackenhold", "Northcombe", "Eastwick",
            "Dunhallow", "Ironbrook", "Westmarch", "Highgate", "Saltmere", "Redcliff", "Elmstead", "Brightwater",
            "Coldharbour", "Ambervale", "Sunmere", "Blackmoor", "Windrush", "Copperhill", "Pinecrest", "Harrowgate",
        };
    }

    /// <summary>Ground kinds (not named Terrain: that clashes with UnityEngine.Terrain), numbered as in the JS (Tile.t).</summary>
    public enum Ground : byte { Meadow = 0, Forest = 1, Rock = 2, Water = 3 }
}
