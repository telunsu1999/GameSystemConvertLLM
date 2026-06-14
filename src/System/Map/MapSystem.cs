using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// Global map system — NOT an entity component.
    /// Held by GameLoop. Provides:
    /// 1. Grid map with terrain data
    /// 2. Auto-generated navigation graph from grid + POIs
    /// 3. A* pathfinding on the grid
    /// 4. Dijkstra pathfinding on the navigation graph
    /// 5. Entity position registry (who is where)
    /// 6. Spatial queries (nearby entities, reachable locations)
    /// </summary>
    public class MapSystem
    {
        // ===== Grid Layer =====
        private TerrainDef[,] _grid;
        private int _width;
        private int _height;
        private Dictionary<char, TerrainDef> _terrainDefs;
        private double _ticksPerUnitDistance = 1.0;

        // ===== Graph Layer =====
        private Dictionary<string, NavNode> _nodes = new Dictionary<string, NavNode>();
        private Dictionary<string, List<(string neighborId, NavEdge edge)>> _adjacency
            = new Dictionary<string, List<(string, NavEdge)>>();

        // ===== Entity Position Index =====
        private Dictionary<string, HashSet<string>> _locationEntities
            = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, string> _entityLocation
            = new Dictionary<string, string>();

        // ===== Properties =====
        public string MapId { get; private set; }
        public string MapName { get; private set; }
        public int Width => _width;
        public int Height => _height;
        public IReadOnlyDictionary<string, NavNode> Nodes => _nodes;
        public IReadOnlyDictionary<string, string> EntityLocations => _entityLocation;

        // ================================================================
        // Loading
        // ================================================================

        public void LoadFromFile(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var config = JsonConvert.DeserializeObject<MapGridConfig>(json);
            if (config == null)
                throw new InvalidOperationException($"Failed to load map config: {jsonPath}");
            Load(config);
        }

        public void Load(MapGridConfig config)
        {
            MapId = config.Id;
            MapName = config.Name;
            _width = config.Width;
            _height = config.Height;
            _terrainDefs = config.TerrainDefs;
            _ticksPerUnitDistance = config.TicksPerUnitDistance;

            // 1. Parse grid
            _grid = new TerrainDef[_width, _height];
            for (int y = 0; y < _height; y++)
            {
                var row = config.Grid[y];
                for (int x = 0; x < _width; x++)
                {
                    char ch = x < row.Length ? row[x] : 'G'; // default to grassland
                    _grid[x, y] = _terrainDefs.TryGetValue(ch, out var t) ? t : new TerrainDef { Name = "未知", Walkable = true, SpeedMod = 1.0 };
                }
            }

            // 2. Register POI nodes
            _nodes = new Dictionary<string, NavNode>();
            foreach (var poi in config.PoiNodes)
            {
                _nodes[poi.Id] = new NavNode
                {
                    Id = poi.Id,
                    Name = poi.Name,
                    GridX = poi.GridX,
                    GridY = poi.GridY,
                    Type = poi.Type ?? "poi",
                    Tags = poi.Tags ?? new string[0]
                };
            }

            // 3. Generate navigation graph
            GenerateNavigationGraph();

            Logger.Info("MapSystem", $"Loaded map '{MapName}' ({_width}x{_height}), {_nodes.Count} nodes, {_adjacency.Sum(kv => kv.Value.Count)} edges");
        }

        // ================================================================
        // Navigation Graph Generation
        // ================================================================

        /// <summary>
        /// Run A* between every pair of POI nodes on the grid.
        /// If a path exists, create a bidirectional edge in the navigation graph.
        /// </summary>
        private void GenerateNavigationGraph()
        {
            _adjacency = new Dictionary<string, List<(string, NavEdge)>>();
            var nodeIds = _nodes.Keys.ToList();

            for (int i = 0; i < nodeIds.Count; i++)
            {
                for (int j = i + 1; j < nodeIds.Count; j++)
                {
                    var from = _nodes[nodeIds[i]];
                    var to = _nodes[nodeIds[j]];

                    var gridPath = FindGridPath(from.GridX, from.GridY, to.GridX, to.GridY);
                    if (gridPath == null) continue;

                    // Calculate accumulated cost (terrain-weighted)
                    double totalCost = 0;
                    for (int k = 1; k < gridPath.Count; k++)
                    {
                        var (x1, y1) = gridPath[k - 1];
                        var (x2, y2) = gridPath[k];
                        double dx = x2 - x1;
                        double dy = y2 - y1;
                        double stepDist = Math.Sqrt(dx * dx + dy * dy);
                        double speedMod = _grid[x2, y2].SpeedMod;
                        if (speedMod <= 0) speedMod = 0.1; // safety
                        totalCost += stepDist / speedMod;
                    }

                    int ticks = Math.Max(1, (int)Math.Ceiling(totalCost * _ticksPerUnitDistance));

                    // Forward edge
                    var edgeFwd = new NavEdge
                    {
                        From = from.Id,
                        To = to.Id,
                        Distance = totalCost,
                        TimeCostTicks = ticks,
                        GridPath = new List<(int, int)>(gridPath)
                    };
                    AddEdge(from.Id, to.Id, edgeFwd);

                    // Reverse edge (same grid path but reversed)
                    var revPath = new List<(int, int)>(gridPath);
                    revPath.Reverse();
                    var edgeRev = new NavEdge
                    {
                        From = to.Id,
                        To = from.Id,
                        Distance = totalCost,
                        TimeCostTicks = ticks,
                        GridPath = revPath
                    };
                    AddEdge(to.Id, from.Id, edgeRev);
                }
            }
        }

        private void AddEdge(string fromId, string toId, NavEdge edge)
        {
            if (!_adjacency.ContainsKey(fromId))
                _adjacency[fromId] = new List<(string, NavEdge)>();
            _adjacency[fromId].Add((toId, edge));
        }

        // ================================================================
        // A* Grid Pathfinding (8-directional)
        // ================================================================

        /// <summary>
        /// A* pathfinding on the grid. Returns list of grid coordinates from start to goal,
        /// or null if no path exists.
        ///
        /// Cost model:
        ///   - Straight step = 1 / terrain.speedMod
        ///   - Diagonal step = √2 / terrain.speedMod
        ///   - Unwalkable cells are not traversed
        /// </summary>
        public List<(int x, int y)> FindGridPath(int startX, int startY, int goalX, int goalY)
        {
            if (!IsWalkable(startX, startY) || !IsWalkable(goalX, goalY))
                return null;

            // 8-direction neighbors with costs
            var dirs = new (int dx, int dy, double baseCost)[]
            {
                (0, -1, 1.0), (1, -1, 1.41421356), (1, 0, 1.0), (1, 1, 1.41421356),
                (0, 1, 1.0), (-1, 1, 1.41421356), (-1, 0, 1.0), (-1, -1, 1.41421356)
            };

            // Priority queue: (fScore, x, y)
            var openSet = new SortedSet<(double f, int x, int y)>(Comparer<(double f, int x, int y)>.Create(
                (a, b) => {
                    int cmp = a.f.CompareTo(b.f);
                    if (cmp != 0) return cmp;
                    cmp = a.x.CompareTo(b.x);
                    if (cmp != 0) return cmp;
                    return a.y.CompareTo(b.y);
                }));

            var cameFrom = new Dictionary<(int, int), (int, int)>();
            var gScore = new Dictionary<(int, int), double>();
            var startKey = (startX, startY);
            var goalKey = (goalX, goalY);

            gScore[startKey] = 0;
            openSet.Add((Heuristic(startX, startY, goalX, goalY), startX, startY));

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);
                int cx = current.x;
                int cy = current.y;

                if (cx == goalX && cy == goalY)
                    return ReconstructPath(cameFrom, goalKey);

                for (int d = 0; d < 8; d++)
                {
                    var (dx, dy, baseCost) = dirs[d];
                    int nx = cx + dx;
                    int ny = cy + dy;

                    if (nx < 0 || nx >= _width || ny < 0 || ny >= _height) continue;
                    if (!IsWalkable(nx, ny)) continue;

                    // For diagonal moves, check that both adjacent cardinal cells are walkable
                    // (prevents cutting corners through walls)
                    if (dx != 0 && dy != 0)
                    {
                        if (!IsWalkable(cx + dx, cy) || !IsWalkable(cx, cy + dy))
                            continue;
                    }

                    double terrainSpeed = _grid[nx, ny].SpeedMod;
                    if (terrainSpeed <= 0) terrainSpeed = 0.1;
                    double stepCost = baseCost / terrainSpeed;

                    double tentativeG = gScore[(cx, cy)] + stepCost;
                    var neighborKey = (nx, ny);

                    if (!gScore.ContainsKey(neighborKey) || tentativeG < gScore[neighborKey])
                    {
                        cameFrom[neighborKey] = (cx, cy);
                        gScore[neighborKey] = tentativeG;
                        double f = tentativeG + Heuristic(nx, ny, goalX, goalY);
                        openSet.Add((f, nx, ny));
                    }
                }
            }

            return null; // no path
        }

        private double Heuristic(int x, int y, int gx, int gy)
        {
            double dx = Math.Abs(x - gx);
            double dy = Math.Abs(y - gy);
            // Octile distance: D * max(dx,dy) + (D2-D) * min(dx,dy)
            // D = 1.0, D2 = sqrt(2)
            return Math.Max(dx, dy) + 0.41421356 * Math.Min(dx, dy);
        }

        private List<(int, int)> ReconstructPath(Dictionary<(int, int), (int, int)> cameFrom, (int, int) current)
        {
            var path = new List<(int, int)> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        public bool IsWalkable(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height) return false;
            return _grid[x, y].Walkable;
        }

        public TerrainDef GetTerrain(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return new TerrainDef { Name = "边界", Walkable = false };
            return _grid[x, y];
        }

        // ================================================================
        // Graph Pathfinding (Dijkstra for node-to-node navigation)
        // ================================================================

        /// <summary>
        /// Find shortest path on the navigation graph between two named nodes.
        /// Uses Dijkstra's algorithm (edge weights = TimeCostTicks).
        /// </summary>
        public PathResult FindPathOnGraph(string fromNodeId, string toNodeId)
        {
            var result = new PathResult { Reachable = false };

            if (!_nodes.ContainsKey(fromNodeId) || !_nodes.ContainsKey(toNodeId))
                return result;

            if (fromNodeId == toNodeId)
            {
                result.Reachable = true;
                result.NodePath = new List<string> { fromNodeId };
                return result;
            }

            // Dijkstra
            var dist = new Dictionary<string, double>();
            var prev = new Dictionary<string, string>();
            var prevEdge = new Dictionary<string, NavEdge>();
            var unvisited = new HashSet<string>(_nodes.Keys);

            foreach (var nodeId in _nodes.Keys)
                dist[nodeId] = double.MaxValue;
            dist[fromNodeId] = 0;

            while (unvisited.Count > 0)
            {
                // Find unvisited node with smallest distance
                string current = null;
                double minDist = double.MaxValue;
                foreach (var id in unvisited)
                {
                    if (dist[id] < minDist)
                    {
                        minDist = dist[id];
                        current = id;
                    }
                }

                if (current == null || current == toNodeId && minDist < double.MaxValue)
                    break;

                if (minDist == double.MaxValue)
                    break; // unreachable nodes remain

                unvisited.Remove(current);

                if (!_adjacency.TryGetValue(current, out var neighbors)) continue;

                foreach (var (neighborId, edge) in neighbors)
                {
                    if (!unvisited.Contains(neighborId)) continue;
                    double alt = dist[current] + edge.TimeCostTicks;
                    if (alt < dist[neighborId])
                    {
                        dist[neighborId] = alt;
                        prev[neighborId] = current;
                        prevEdge[neighborId] = edge;
                    }
                }
            }

            if (dist[toNodeId] == double.MaxValue)
                return result;

            // Reconstruct path
            var nodePath = new List<string>();
            double totalDist = 0;
            var gridPath = new List<(int, int)>();
            var curr = toNodeId;

            var reversedNodes = new List<string>();
            var reversedEdges = new List<NavEdge>();
            while (prev.ContainsKey(curr))
            {
                reversedNodes.Add(curr);
                if (prevEdge.ContainsKey(curr))
                    reversedEdges.Add(prevEdge[curr]);
                curr = prev[curr];
            }
            reversedNodes.Add(fromNodeId);
            reversedNodes.Reverse();
            reversedEdges.Reverse();

            result.NodePath = reversedNodes;
            result.TotalTicks = (int)dist[toNodeId];
            result.Reachable = true;

            foreach (var edge in reversedEdges)
            {
                totalDist += edge.Distance;
                if (gridPath.Count == 0)
                    gridPath.AddRange(edge.GridPath);
                else
                    gridPath.AddRange(edge.GridPath.Skip(1)); // avoid duplicate junction points
            }
            result.TotalDistance = totalDist;
            result.GridPath = gridPath;

            return result;
        }

        // ================================================================
        // Graph Queries
        // ================================================================

        /// <summary>Get the edge between two adjacent nodes, or null.</summary>
        public NavEdge GetEdge(string fromNodeId, string toNodeId)
        {
            if (!_adjacency.TryGetValue(fromNodeId, out var neighbors)) return null;
            foreach (var (nId, edge) in neighbors)
                if (nId == toNodeId) return edge;
            return null;
        }

        /// <summary>Get all nodes directly reachable from a given node.</summary>
        public List<string> GetReachableNodes(string fromNodeId)
        {
            if (!_adjacency.TryGetValue(fromNodeId, out var neighbors))
                return new List<string>();
            return neighbors.Select(n => n.neighborId).ToList();
        }

        /// <summary>Get all nodes within a given tick-range of a center node.</summary>
        public List<(string nodeId, int ticks)> GetNodesInRange(string centerNodeId, int maxTicks)
        {
            var result = new List<(string, int)>();
            if (!_nodes.ContainsKey(centerNodeId)) return result;

            var dist = new Dictionary<string, int>();
            foreach (var id in _nodes.Keys) dist[id] = int.MaxValue;
            dist[centerNodeId] = 0;

            var queue = new Queue<string>();
            queue.Enqueue(centerNodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!_adjacency.TryGetValue(current, out var neighbors)) continue;

                foreach (var (nId, edge) in neighbors)
                {
                    int newDist = dist[current] + edge.TimeCostTicks;
                    if (newDist < dist[nId] && newDist <= maxTicks)
                    {
                        dist[nId] = newDist;
                        queue.Enqueue(nId);
                    }
                }
            }

            foreach (var kv in dist)
                if (kv.Value > 0 && kv.Value <= maxTicks)
                    result.Add((kv.Key, kv.Value));

            result.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return result;
        }

        // ================================================================
        // Entity Position Registry
        // ================================================================

        public void RegisterEntity(string entityId, string locationNodeId)
        {
            // Unregister from previous location
            if (_entityLocation.TryGetValue(entityId, out var oldLoc))
            {
                if (_locationEntities.TryGetValue(oldLoc, out var oldSet))
                {
                    oldSet.Remove(entityId);
                    if (oldSet.Count == 0)
                        _locationEntities.Remove(oldLoc);
                }
            }

            _entityLocation[entityId] = locationNodeId;

            if (!_locationEntities.ContainsKey(locationNodeId))
                _locationEntities[locationNodeId] = new HashSet<string>();
            _locationEntities[locationNodeId].Add(entityId);
        }

        public void UnregisterEntity(string entityId, string locationNodeId)
        {
            if (_locationEntities.TryGetValue(locationNodeId, out var set))
            {
                set.Remove(entityId);
                if (set.Count == 0)
                    _locationEntities.Remove(locationNodeId);
            }
            _entityLocation.Remove(entityId);
        }

        public string GetEntityLocation(string entityId)
        {
            _entityLocation.TryGetValue(entityId, out var loc);
            return loc;
        }

        public List<string> GetEntitiesAt(string locationNodeId)
        {
            if (_locationEntities.TryGetValue(locationNodeId, out var set))
                return set.ToList();
            return new List<string>();
        }

        /// <summary>
        /// Get entities near a center node (within maxTicks travel distance),
        /// returning entity IDs with their distance in ticks.
        /// Excludes the querying entity itself.
        /// </summary>
        public Dictionary<string, int> GetNearbyEntities(string centerNodeId, int maxTicks, string? excludeEntityId = null)
        {
            var result = new Dictionary<string, int>();
            var nodesInRange = GetNodesInRange(centerNodeId, maxTicks);

            // Entities at the center node (same location)
            var atCenter = GetEntitiesAt(centerNodeId);
            foreach (var eid in atCenter)
            {
                if (eid != excludeEntityId)
                    result[eid] = 0; // 0 ticks = same location
            }

            // Entities at nearby nodes
            foreach (var (nodeId, ticks) in nodesInRange)
            {
                var entities = GetEntitiesAt(nodeId);
                foreach (var eid in entities)
                {
                    if (eid != excludeEntityId)
                        result[eid] = ticks;
                }
            }

            return result;
        }
    }
}
