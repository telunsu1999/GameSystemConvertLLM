using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameLoop
{
    // ================================================================
    // Grid Map Configuration (JSON)
    // ================================================================

    /// <summary>
    /// Top-level grid map config loaded from JSON.
    /// Defines terrain types, the grid string array, and POI node placements.
    /// </summary>
    public class MapGridConfig
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        /// <summary>Character → TerrainDef mapping. Characters appear in the grid strings.</summary>
        [JsonProperty("terrain_defs")]
        public Dictionary<char, TerrainDef> TerrainDefs { get; set; } = new Dictionary<char, TerrainDef>();

        /// <summary>Grid rows, top to bottom. Each string has Width characters.</summary>
        [JsonProperty("grid")]
        public List<string> Grid { get; set; } = new List<string>();

        /// <summary>Points of interest placed on the grid. These become navigation graph nodes.</summary>
        [JsonProperty("poi_nodes")]
        public List<PoiNodeDef> PoiNodes { get; set; } = new List<PoiNodeDef>();

        /// <summary>Ticks per unit of abstract distance. Used to convert grid path cost → game ticks.</summary>
        [JsonProperty("ticks_per_unit_distance")]
        public double TicksPerUnitDistance { get; set; } = 1.0;
    }

    // ================================================================
    // Terrain
    // ================================================================

    public class TerrainDef
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("walkable")]
        public bool Walkable { get; set; } = true;

        /// <summary>
        /// Speed modifier. >1 = faster (roads), &lt;1 = slower (forest, swamp), 0 = impassable.
        /// Movement cost = base_step_cost / speed_mod.
        /// </summary>
        [JsonProperty("speed_mod")]
        public double SpeedMod { get; set; } = 1.0;
    }

    // ================================================================
    // POI Node Definition (from config)
    // ================================================================

    public class PoiNodeDef
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("grid_x")]
        public int GridX { get; set; }

        [JsonProperty("grid_y")]
        public int GridY { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = "poi";

        [JsonProperty("tags")]
        public string[] Tags { get; set; } = new string[0];
    }

    // ================================================================
    // Navigation Graph (generated from grid + POIs)
    // ================================================================

    public class NavNode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int GridX { get; set; }
        public int GridY { get; set; }
        public string Type { get; set; } = "poi";     // "poi" or "intersection"
        public string[] Tags { get; set; } = new string[0];
    }

    public class NavEdge
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;

        /// <summary>Abstract distance (terrain-weighted).</summary>
        public double Distance { get; set; }

        /// <summary>Game ticks to traverse this edge.</summary>
        public int TimeCostTicks { get; set; }

        /// <summary>Grid cell sequence for this edge (from.x,from.y → ... → to.x,to.y).</summary>
        public List<(int x, int y)> GridPath { get; set; } = new List<(int x, int y)>();
    }

    // ================================================================
    // Pathfinding Result
    // ================================================================

    public class PathResult
    {
        public bool Reachable { get; set; }
        public List<string> NodePath { get; set; } = new List<string>();   // node IDs in order
        public int TotalTicks { get; set; }
        public double TotalDistance { get; set; }
        public List<(int x, int y)> GridPath { get; set; } = new List<(int x, int y)>(); // raw grid coords
    }
}
