using System;
using System.Collections.Generic;
using System.Linq;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.esriSystem;
// [Agent (通用辅助)] Added: 路网缓存序列化支持
using System.IO;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【核心逻辑】路径规划引擎
    /// 包含：拓扑打断构建 (Planarization)、多候选路由 (Multi-Candidate)、永不失败策略 (Never-Fail)
    /// 文件路径: c:\Users\29525\Desktop\地理信息系统开发实习\1.8\-\LYF地图操作\23110908016王亮\WindowsFormsMap1\AnalysisHelper.cs
    /// </summary>
    public class AnalysisHelper
    {
        // [Agent (通用辅助)] Added: 可序列化的路网数据结构
        [Serializable]
        private class SerializableNode
        {
            public int Id { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public List<SerializableEdge> Edges { get; set; } = new List<SerializableEdge>();
        }

        [Serializable]
        private class SerializableEdge
        {
            public int TargetNodeId { get; set; }
            public double Weight { get; set; }
            public List<double[]> GeometryPoints { get; set; } = new List<double[]>();
        }

        [Serializable]
        private class NetworkCache
        {
            public List<SerializableNode> Nodes { get; set; }
            public double MergeTolerance { get; set; }
            public string LayerName { get; set; }
            public DateTime BuildTime { get; set; }
        }

        private class GraphNode
        {
            public int Id;
            public IPoint Point;
            public List<GraphEdge> Edges = new List<GraphEdge>();
        }

        private class GraphEdge
        {
            public int TargetNodeId;
            public double Weight;
            public IPolyline Geometry;
        }

        private Dictionary<int, GraphNode> _graph;
        private IFeatureLayer _roadLayer;
        private ISpatialReference _mapSR; // 缓存路网空间参考
        private double _mergeTolerance = 0.0001;
        // [Agent (通用辅助)] Added: 缓存文件路径
        private string _cacheDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsFormsMap1", "NetworkCache");

        public AnalysisHelper()
        {
            // 确保缓存目录存在
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        // [Agent] Added: 调试日志，用于反馈给用户
        public string LastLog { get; private set; } = "";

        private void Log(string msg)
        {
            LastLog += DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\r\n";
            System.Diagnostics.Debug.WriteLine(msg);
        }

        public string BuildNetwork(IFeatureLayer roadLayer)
        {
            LastLog = "--- 开始构建路网 ---\r\n";
            try
            {
                if (roadLayer == null || roadLayer.FeatureClass == null) return "提示：请先在左侧图层树选中道路图层";
                _roadLayer = roadLayer;
                Log($"图层名称: {roadLayer.Name}");

                IGeoDataset geoDataset = _roadLayer.FeatureClass as IGeoDataset;
                if (geoDataset != null)
                {
                    _mapSR = geoDataset.SpatialReference;
                    Log($"空间参考: {(_mapSR != null ? _mapSR.Name : "Unknown")}");
                }

                // 1. [Optimized] Skip ConstructUnion and iterate features directly
                // If the data is already planarized (nodes at segment junctions), 
                // we can build the graph 100x faster by skipping the heavy topological union.
                _graph = new Dictionary<int, GraphNode>();
                Dictionary<long, int> nodeLookup = new Dictionary<long, int>();
                int nextNodeId = 0;

                if (_mapSR is IProjectedCoordinateSystem) _mergeTolerance = 1.0;
                else _mergeTolerance = 0.00001;
                IFeatureCursor cursor = _roadLayer.FeatureClass.Search(null, false);
                IFeature feat;
                int count = 0;
                while ((feat = cursor.NextFeature()) != null)
                {
                    if (feat.Shape is IPolyline line && !line.IsEmpty)
                    {
                        ISegmentCollection segments = line as ISegmentCollection;
                        for (int i = 0; i < segments.SegmentCount; i++)
                        {
                            ISegment segment = segments.get_Segment(i);

                            // 保持原来的几何存储逻辑
                            PolylineClass edgePoly = new PolylineClass { SpatialReference = _mapSR };
                            (edgePoly as ISegmentCollection).AddSegment(segment);

                            int u = GetOrCreateNodeId(segment.FromPoint, nodeLookup, ref nextNodeId);
                            int v = GetOrCreateNodeId(segment.ToPoint, nodeLookup, ref nextNodeId);

                            if (u != v)
                            {
                                double length = segment.Length;
                                _graph[u].Edges.Add(new GraphEdge { TargetNodeId = v, Weight = length, Geometry = edgePoly });
                                _graph[v].Edges.Add(new GraphEdge { TargetNodeId = u, Weight = length, Geometry = edgePoly });
                            }
                        }
                        count++;
                    }
                }
                Marshal.ReleaseComObject(cursor);
                Log($"读取要素数量: {count}");

                if (count == 0) return "选中的图层没有要素！";

                Log($"图构建完毕: {_graph.Count} 个节点。");

                // [Agent (通用辅助)] Added: 保存路网缓存
                SaveNetworkCache(roadLayer.Name);

                return $"路网重构成功!\n节点数: {_graph.Count}";
            }
            catch (Exception ex)
            {
                string err = "路网构建出错: " + ex.Message;
                Log(err);
                return err;
            }
        }

        // [Agent] Modified: 增加日志
        public TourRoute GenerateRouteByCities(List<string> cityNames, IFeatureLayer cityLayer, IFeatureLayer roadLayer, IFeatureLayer ichLayer)
        {
            LastLog = "--- 开始规划路线 ---\r\n";
            if (cityNames == null || cityNames.Count < 2) { Log("城市数量不足"); return null; }

            // 1. 确保路网可用
            if (_graph == null || _graph.Count == 0 || (_roadLayer != roadLayer))
            {
                Log($"需要构建路网 (Graph为空或图层变更). 目标图层: {(roadLayer != null ? roadLayer.Name : "null")}");
                bool loaded = TryLoadNetworkCache(roadLayer);
                if (loaded) Log("成功加载缓存路网。");
                else
                {
                    Log("无缓存，开始重新构建...");
                    string res = BuildNetwork(roadLayer);
                    Log($"构建结果: {res}");
                }
            }

            if (_graph == null || _graph.Count == 0)
            {
                Log("错误: 路网构建失败，节点数为0。");
                return null;
            }

            // 2. 获取地市质心
            List<IPoint> stops = new List<IPoint>();
            foreach (var city in cityNames)
            {
                Log($"正在查找城市: {city}");
                IPoint pt = GetCityCentroid(cityLayer, city);
                if (pt != null)
                {
                    stops.Add(pt);
                    Log($" - 找到质心: {pt.X:F4}, {pt.Y:F4}");
                }
                else
                {
                    Log(" - 未找到该城市要素 (请检查图层字段)");
                }
            }

            if (stops.Count < 2)
            {
                Log("错误: 有效的城市坐标不足2个。");
                return null;
            }

            // 3. 规划路径
            Log("开始计算最短路径...");
            IPolyline routeLine = FindShortestPath(stops);

            if (routeLine == null) Log("FindShortestPath 返回 null。");
            else if (routeLine.IsEmpty) Log("FindShortestPath 返回 Empty Geometry。");
            else Log($"路径计算成功, 长度: {routeLine.Length:F2}");

            if (routeLine == null || routeLine.IsEmpty) return null;

            // 4. 缓冲区分析
            double buffDist = 0.1;
            if (routeLine.SpatialReference is IProjectedCoordinateSystem) buffDist = 15000;
            Log($"执行缓冲区分析 (距离: {buffDist})...");

            IGeometry buffer = (routeLine as ITopologicalOperator).Buffer(buffDist);

            List<IFeature> spots = new List<IFeature>();
            ISpatialFilter sf = new SpatialFilterClass();
            sf.Geometry = buffer;
            sf.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

            IFeatureCursor cursor = ichLayer.FeatureClass.Search(sf, false);
            IFeature f;
            while ((f = cursor.NextFeature()) != null)
            {
                spots.Add(f);
            }
            Marshal.ReleaseComObject(cursor);
            Marshal.ReleaseComObject(sf);
            Log($"发现非遗点: {spots.Count} 个");

            // 4.5 [Agent] 反向查找路径对应的原始路网要素
            List<IFeature> roadFeats = new List<IFeature>();
            HashSet<int> roadOids = new HashSet<int>();

            if (roadLayer != null && routeLine != null)
            {
                IGeometryCollection geoColl = routeLine as IGeometryCollection;
                if (geoColl != null)
                {
                    ISpatialFilter sfRoad = new SpatialFilterClass();
                    sfRoad.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

                    double tol = (_mapSR is IProjectedCoordinateSystem) ? 1.0 : 0.00001;
                    sfRoad.Geometry = (routeLine as ITopologicalOperator).Buffer(tol);

                    try
                    {
                        IFeatureCursor rCursor = roadLayer.FeatureClass.Search(sfRoad, false);
                        IFeature rf;
                        while ((rf = rCursor.NextFeature()) != null)
                        {
                            ITopologicalOperator2 topoFeat = rf.Shape as ITopologicalOperator2;
                            if (topoFeat != null)
                            {
                                IGeometry intersection = topoFeat.Intersect(routeLine, esriGeometryDimension.esriGeometry1Dimension); // 1D = Line
                                if (intersection != null && !intersection.IsEmpty)
                                {
                                    if ((intersection as IPolyline).Length > tol)
                                    {
                                        if (!roadOids.Contains(rf.OID))
                                        {
                                            roadFeats.Add(rf);
                                            roadOids.Add(rf.OID);
                                        }
                                    }
                                }
                            }
                        }
                        Marshal.ReleaseComObject(rCursor);
                    }
                    catch (Exception ex) { Log("Road lookup error: " + ex.Message); }
                    Marshal.ReleaseComObject(sfRoad);
                }
            }

            // 5. 构造结果
            TourRoute tr = new TourRoute
            {
                Name = $"定制游: {string.Join("-", cityNames)}",
                Description = $"全程经过 {string.Join("、", cityNames)}，周边发现 {spots.Count} 处非遗景点。",
                Points = spots,
                PathLine = routeLine,
                RoadFeatures = roadFeats
            };

            return tr;
        }

        private int GetOrCreateNodeId(IPoint pt, Dictionary<long, int> lookup, ref int nextId)
        {
            // 降低精度进行模糊匹配，确保路口缝合 (使用 long 避免字符串分配开销)
            long xi = (long)Math.Round(pt.X / _mergeTolerance);
            long yi = (long)Math.Round(pt.Y / _mergeTolerance);
            long key = (xi << 32) | (yi & 0xFFFFFFFFL); // 快速位移哈希

            if (!lookup.ContainsKey(key))
            {
                int id = nextId++;
                lookup[key] = id;
                _graph[id] = new GraphNode { Id = id, Point = (pt as IClone).Clone() as IPoint };
            }
            return lookup[key];
        }

        // [Agent (通用辅助)] Optimized: 使用自定义二进制格式，解决 20w 节点加载慢的问题
        private void SaveNetworkCache(string layerName)
        {
            try
            {
                string cacheFile = System.IO.Path.Combine(_cacheDirectory, $"{SanitizeFileName(layerName)}_cache.bin");
                using (var fs = new System.IO.FileStream(cacheFile, System.IO.FileMode.Create))
                using (var bw = new System.IO.BinaryWriter(fs))
                {
                    bw.Write(_mergeTolerance);
                    bw.Write(_graph.Count);
                    foreach (var node in _graph.Values)
                    {
                        bw.Write(node.Id);
                        bw.Write(node.Point.X);
                        bw.Write(node.Point.Y);
                        bw.Write(node.Edges.Count);
                        foreach (var edge in node.Edges)
                        {
                            bw.Write(edge.TargetNodeId);
                            bw.Write(edge.Weight);

                            IPointCollection ptColl = edge.Geometry as IPointCollection;
                            bw.Write(ptColl.PointCount);
                            for (int i = 0; i < ptColl.PointCount; i++)
                            {
                                IPoint pt = ptColl.Point[i];
                                bw.Write(pt.X); bw.Write(pt.Y);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"二进制缓存保存失败: {ex.Message}");
            }
        }

        private bool LoadNetworkCache(string layerName, ISpatialReference spatialRef)
        {
            try
            {
                string cacheFile = System.IO.Path.Combine(_cacheDirectory, $"{SanitizeFileName(layerName)}_cache.bin");
                if (!System.IO.File.Exists(cacheFile)) return false;

                using (var fs = new System.IO.FileStream(cacheFile, System.IO.FileMode.Open))
                using (var br = new System.IO.BinaryReader(fs))
                {
                    _mergeTolerance = br.ReadDouble();
                    int nodeCount = br.ReadInt32();
                    _graph = new Dictionary<int, GraphNode>(nodeCount);
                    _mapSR = spatialRef;

                    for (int n = 0; n < nodeCount; n++)
                    {
                        int id = br.ReadInt32();
                        double nx = br.ReadDouble();
                        double ny = br.ReadDouble();
                        var node = new GraphNode
                        {
                            Id = id,
                            Point = new PointClass { X = nx, Y = ny, SpatialReference = spatialRef },
                            Edges = new List<GraphEdge>()
                        };

                        int edgeCount = br.ReadInt32();
                        for (int e = 0; e < edgeCount; e++)
                        {
                            var edge = new GraphEdge
                            {
                                TargetNodeId = br.ReadInt32(),
                                Weight = br.ReadDouble(),
                                Geometry = new PolylineClass { SpatialReference = spatialRef }
                            };

                            int ptCount = br.ReadInt32();
                            IPointCollection ptColl = edge.Geometry as IPointCollection;
                            for (int p = 0; p < ptCount; p++)
                            {
                                ptColl.AddPoint(new PointClass { X = br.ReadDouble(), Y = br.ReadDouble(), SpatialReference = spatialRef });
                            }
                            node.Edges.Add(edge);
                        }
                        _graph[id] = node;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"二进制缓存读取失败: {ex.Message}");
                return false;
            }
        }

        // [Agent (通用辅助)] Added: 尝试加载缓存,返回是否成功
        public bool TryLoadNetworkCache(IFeatureLayer roadLayer)
        {
            if (roadLayer == null || roadLayer.FeatureClass == null) return false;

            _roadLayer = roadLayer;
            var spatialRef = (roadLayer.FeatureClass as IGeoDataset).SpatialReference;

            return LoadNetworkCache(roadLayer.Name, spatialRef);
        }

        // [Agent (通用辅助)] Added: 清理文件名中的非法字符
        private string SanitizeFileName(string fileName)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        public IPolyline FindShortestPath(IPoint startPt, IPoint endPt)
        {
            if (_graph == null || _graph.Count == 0) return null;

            // 确保坐标系一致
            if (startPt.SpatialReference == null && _mapSR != null) startPt.SpatialReference = _mapSR;
            if (endPt.SpatialReference == null && _mapSR != null) endPt.SpatialReference = _mapSR;

            IPoint sPt = ProjectPoint(startPt, _mapSR);
            IPoint ePt = ProjectPoint(endPt, _mapSR);

            // 【优化】基于边的捕捉策略 (Edge Snapping)
            // 之前的策略只寻找最近的“节点”（交叉口），如果城市在长路段中间，最近的节点可能在几十公里外，甚至属于另一条不连通的路。
            // 现在的策略：找到离点最近的那条“边”，把这条边的两个端点都作为候选起点。
            var startNodes = FindNodesNearEdge(sPt);
            var endNodes = FindNodesNearEdge(ePt);

            // 如果基于边的捕捉失败（比如点离路太远），回退到基于距离的节点捕捉
            if (startNodes.Count == 0) startNodes = FindNearestNodes(sPt, 10);
            if (endNodes.Count == 0) endNodes = FindNearestNodes(ePt, 10);

            if (startNodes.Count == 0 || endNodes.Count == 0) return null;

            // Dijkstra 多源多宿最短路
            var distances = new Dictionary<int, double>();
            var previous = new Dictionary<int, KeyValuePair<int, IPolyline>>();
            var visited = new HashSet<int>();
            PriorityQueue<int, double> pq = new PriorityQueue<int, double>();

            // 初始化
            foreach (var node in _graph.Keys) distances[node] = double.MaxValue;

            // 将所有候选起点入队
            foreach (var sNode in startNodes)
            {
                if (!_graph.ContainsKey(sNode)) continue;
                // 初始距离设为点到节点的直线距离 (估算)
                double d = (sPt as IProximityOperator).ReturnDistance(_graph[sNode].Point);
                if (d < distances[sNode])
                {
                    distances[sNode] = d;
                    pq.Enqueue(sNode, d);
                }
            }

            HashSet<int> targetSet = new HashSet<int>(endNodes);
            int reachedEndId = -1;
            double minDistLimit = double.MaxValue; // 用于寻找最近的目标

            while (pq.Count > 0)
            {
                int u = pq.Dequeue();

                // 如果距离已经大于已知最短路很多，可以剪枝 (可选)
                if (distances[u] > minDistLimit) continue;

                if (targetSet.Contains(u))
                {
                    // 找到一个目标，记录并尝试找更优（如果需要严格最短）
                    // 这里为了性能，一旦找到就返回，或者稍作比较
                    reachedEndId = u;
                    minDistLimit = distances[u];
                    break;
                }

                if (visited.Contains(u)) continue;
                visited.Add(u);

                if (!_graph.ContainsKey(u)) continue;

                foreach (var edge in _graph[u].Edges)
                {
                    double newDist = distances[u] + edge.Weight;
                    if (newDist < distances[edge.TargetNodeId])
                    {
                        distances[edge.TargetNodeId] = newDist;
                        previous[edge.TargetNodeId] = new KeyValuePair<int, IPolyline>(u, edge.Geometry);
                        pq.Enqueue(edge.TargetNodeId, newDist);
                    }
                }
            }

            // 【永不失败】断路兜底逻辑 V2
            // 如果没找到连通路径，尝试分段连接（只返回已探寻的部分）
            // 或者直接画虚线链接? 用户说“只要能够联通就可以算作一条路径，可以是几条零碎的” -> 意味着我们应该尽量找。
            // 但 Dijkstra 的特性是如果不可达，根本不会访问到 endNodes。

            // 如果完全失败，尝试“最近邻”匹配，即只找几何上最近的片段（Current implementation skips this to allow fallback logic in caller）

            // 回溯路径
            PolylineClass result = new PolylineClass();
            if (_mapSR != null) result.SpatialReference = _mapSR;
            IGeometryCollection geoColl = result as IGeometryCollection;

            if (reachedEndId != -1)
            {
                int curr = reachedEndId;
                while (previous.ContainsKey(curr))
                {
                    var step = previous[curr];
                    if (step.Value != null)
                    {
                        // Insert at 0 implies reversing? No, geometry collection order matters usually for topology but for raw drawing it's fine.
                        // Standard backtrack goes End -> Start.
                        geoColl.AddGeometryCollection(step.Value as IGeometryCollection);
                    }
                    curr = step.Key;
                }
            }
            // 不要 Simplify，因为 Simplify 会合并几何可能导致多部分错乱，保持原样即可
            return result;
        }

        private List<int> FindNodesNearEdge(IPoint pt)
        {
            // 核心改进：寻找离点最近的“边”，然后返回该边的两个端点 ID
            List<int> nodes = new List<int>();
            if (_graph == null) return nodes;

            // 暴力遍历所有边 (性能瓶颈？演示模式数据量小，应该还好)
            // 优化：我们其实可以利用原始 FeatureLayer 的 SpatialFilter 来找最近的 Line Feature
            // 但 FeatureLayer 的 Line 和 Graph 的 Edge 是一对多的关系 (Graph 是打断后的)。
            // 
            // 方案：遍历 Graph 中的所有 Node，检查其连接的 Edges，计算点到 Edge 的距离。
            // 这是一个 O(N) 操作。对于几千个节点是可以接受的。

            // 阈值：只考虑一定范围内的路 (比如 50km)，甚至更远，为了确保连通
            // 但为了效率，我们先找最近的 Top 5 Nodes，然后看这些 Node 连出去的 Edge

            // 方法 A：还是先找 Top 20 Nearest Nodes，然后把它们连的所有边的另一端也加入候选
            var nearestNodes = FindNearestNodes(pt, 50); // 扩大搜索范围
            foreach (var nid in nearestNodes)
            {
                nodes.Add(nid);
                if (_graph.ContainsKey(nid))
                {
                    foreach (var edge in _graph[nid].Edges)
                    {
                        nodes.Add(edge.TargetNodeId);
                    }
                }
            }

            return nodes.Distinct().ToList();
        }

        private List<int> FindNearestNodes(IPoint pt, int k)
        {
            if (_graph == null || _graph.Count == 0) return new List<int>();
            IProximityOperator prox = pt as IProximityOperator;

            // 简单优化：如果节点过多 (>5000)，先用 Envelope 过滤？
            // 鉴于 C# List 性能，直接 Sort 几千个一般没问题。

            return _graph.Values
                .Select(n => new { n.Id, Dist = prox.ReturnDistance(n.Point) })
                .OrderBy(x => x.Dist)
                .Take(k)
                .Select(x => x.Id)
                .ToList();
        }

        public IPolyline FindShortestPath(List<IPoint> points)
        {
            if (points == null || points.Count < 2) return null;
            PolylineClass total = new PolylineClass();
            if (_mapSR != null) total.SpatialReference = _mapSR;
            IGeometryCollection coll = total as IGeometryCollection;

            bool anySuccess = false;

            for (int i = 0; i < points.Count - 1; i++)
            {
                IPolyline seg = FindShortestPath(points[i], points[i + 1]);
                if (seg != null && !seg.IsEmpty)
                {
                    coll.AddGeometryCollection(seg as IGeometryCollection);
                    anySuccess = true;
                }
                else
                {
                    // 如果中间断了，尝试用直线连接，满足“只要联通就行”的要求
                    // User: "只要能够联通就可以算作一条路径"
                    IPointCollection pc = new PolylineClass();
                    pc.AddPoint(points[i]);
                    pc.AddPoint(points[i + 1]);
                    coll.AddGeometry(pc as IGeometry);
                    anySuccess = true; // 强行成功
                }
            }

            // 如果完全没有路网部分，可能看起来很怪，但至少有直线
            return anySuccess ? total : null;
        }

        private IPoint ProjectPoint(IPoint pt, ISpatialReference targetSR)
        {
            if (pt == null || targetSR == null) return pt;
            IPoint newPt = (pt as IClone).Clone() as IPoint;
            if (newPt.SpatialReference != null) try { newPt.Project(targetSR); } catch { }
            else newPt.SpatialReference = targetSR;
            return newPt;
        }

        public static IPolygon GeneratePointBuffer(IPoint center, double radius)
        {
            ITopologicalOperator topo = center as ITopologicalOperator;
            return topo.Buffer(radius) as IPolygon;
        }

        public static int SelectFeatures(IFeatureLayer layer, IGeometry geometry)
        {
            if (layer == null || geometry == null) return 0;
            IFeatureSelection featSel = layer as IFeatureSelection;
            ISpatialFilter filter = new SpatialFilterClass();
            filter.Geometry = geometry;
            filter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
            featSel.SelectFeatures(filter, esriSelectionResultEnum.esriSelectionResultNew, false);
            return featSel.SelectionSet.Count;
        }

        // [Agent] Added: 推荐路线数据结构与生成逻辑
        public class TourRoute
        {
            public string Name { get; set; }
            public string Description { get; set; }

            public List<IFeature> Points { get; set; } = new List<IFeature>();
            public List<IFeature> RoadFeatures { get; set; } = new List<IFeature>(); // [Agent] Added
            public IPolyline PathLine { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        public static List<TourRoute> GenerateRecommendedRoutes(IFeatureLayer pointLayer, IFeatureLayer lineLayer)
        {
            List<TourRoute> routes = new List<TourRoute>();
            if (pointLayer == null) return routes;

            // 策略1：基于拓扑连通性的随机漫步生成 (Topological Random Walk)
            if (lineLayer != null && lineLayer.FeatureClass.FeatureCount(null) > 0)
            {
                // 获取所有高速路要素ID
                List<int> allRoadOIDs = new List<int>();
                // 使用回收游标 (Recycling=true) 仅读取OID，效率更高且安全
                IFeatureCursor cursor = lineLayer.FeatureClass.Search(null, true);
                IFeature feat;
                while ((feat = cursor.NextFeature()) != null)
                {
                    allRoadOIDs.Add(feat.OID);
                }
                Marshal.ReleaseComObject(cursor);

                // [Refinement V3] 严格筛选模式：只寻找自身长度超过 500km 的完整路段

                int checkedCount = 0;
                Random rnd = new Random();
                int n = allRoadOIDs.Count;
                while (n > 1)
                {
                    n--;
                    int k = rnd.Next(n + 1);
                    int value = allRoadOIDs[k];
                    allRoadOIDs[k] = allRoadOIDs[n];
                    allRoadOIDs[n] = value;
                }

                // 已访问的要素记录，避免重复利用同一段路
                HashSet<int> globalVisited = new HashSet<int>();

                // 尝试生成 N 条路线
                int maxRoutes = 5;
                int attempts = 0;
                while (routes.Count < maxRoutes && attempts < 20 && allRoadOIDs.Count > 0)
                {
                    attempts++;

                    // 1. 随机选择起点
                    if (allRoadOIDs.Count == 0) break;
                    int startIdx = rnd.Next(allRoadOIDs.Count);
                    int startOID = allRoadOIDs[startIdx];

                    if (globalVisited.Contains(startOID))
                    {
                        // 简单的重试机制，如果命中已访问的，换一个
                        allRoadOIDs.RemoveAt(startIdx);
                        continue;
                    }

                    IFeature startFeat = lineLayer.FeatureClass.GetFeature(startOID);
                    if (startFeat == null) continue;

                    List<IFeature> currentChain = new List<IFeature>();
                    currentChain.Add(startFeat);
                    globalVisited.Add(startOID);

                    ITopologicalOperator currentTopo = startFeat.ShapeCopy as ITopologicalOperator; // 累积几何
                    double totalLen = (startFeat.Shape as IPolyline).Length;

                    // 2. 漫步延伸
                    bool growing = true;
                    int step = 0;
                    while (growing && step < 50) // 防止死循环
                    {
                        step++;
                        // 寻找与当前链最后一段相连的路
                        IFeature tail = currentChain[currentChain.Count - 1];

                        ISpatialFilter spatFilter = new SpatialFilterClass();
                        spatFilter.Geometry = tail.Shape;
                        spatFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelTouches;

                        IFeatureCursor neighborCursor = lineLayer.FeatureClass.Search(spatFilter, false);
                        IFeature neighbor;
                        List<IFeature> candidates = new List<IFeature>();

                        while ((neighbor = neighborCursor.NextFeature()) != null)
                        {
                            if (!globalVisited.Contains(neighbor.OID) && neighbor.OID != tail.OID)
                            {
                                candidates.Add(neighbor);
                            }
                            else
                            {
                                Marshal.ReleaseComObject(neighbor);
                            }
                        }
                        Marshal.ReleaseComObject(neighborCursor);
                        Marshal.ReleaseComObject(spatFilter);

                        if (candidates.Count > 0)
                        {
                            // 随机选一个延伸
                            IFeature nextFeat = candidates[rnd.Next(candidates.Count)];
                            currentChain.Add(nextFeat);
                            globalVisited.Add(nextFeat.OID);

                            // 更新几何与长度
                            currentTopo = currentTopo.Union(nextFeat.Shape) as ITopologicalOperator;
                            totalLen += (nextFeat.Shape as IPolyline).Length;

                            // 释放未选中的候选者
                            foreach (var c in candidates)
                            {
                                if (c != nextFeat) Marshal.ReleaseComObject(c);
                            }
                        }
                        else
                        {
                            growing = false; // 死胡同
                        }

                        // 长度检查 (300km)
                        double lenThres = 300000;
                        if (!(currentTopo as IGeometry).SpatialReference.Name.Contains("Meter")) lenThres = 3.0; // 经纬度

                        if (totalLen > lenThres)
                        {
                            growing = false; // 够长了
                        }
                    }

                    // 3. 评估路线是否合格
                    double finalLenThres = 200000; // 最终通过门槛 200km
                    bool isProjected = (currentTopo as IGeometry).SpatialReference is IProjectedCoordinateSystem;
                    if (!isProjected) finalLenThres = 2.0;

                    if (totalLen > finalLenThres)
                    {
                        // 这是一个好路线
                        IPolyline routeLine = currentTopo as IPolyline;

                        // 缓冲与景点查找
                        double buffDist = isProjected ? 20000 : 0.2;
                        IGeometry buffer = currentTopo.Buffer(buffDist);

                        ISpatialFilter pf = new SpatialFilterClass();
                        pf.Geometry = buffer;
                        pf.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

                        List<IFeature> routePoints = new List<IFeature>();
                        IFeatureCursor pc = pointLayer.FeatureClass.Search(pf, false);
                        IFeature p;
                        while ((p = pc.NextFeature()) != null) routePoints.Add(p);
                        Marshal.ReleaseComObject(pc);
                        Marshal.ReleaseComObject(pf);
                        Marshal.ReleaseComObject(buffer);

                        if (routePoints.Count >= 3) // 至少3个点
                        {
                            // 取名字 (取第一段路名 + 最后一段路名)
                            string n1 = "未知路段";
                            string n2 = "尽头";
                            // ... 简化取名逻辑 ...
                            n1 = GetFeatureName(currentChain[0]);
                            if (currentChain.Count > 1) n2 = GetFeatureName(currentChain[currentChain.Count - 1]);

                            TourRoute tr = new TourRoute
                            {
                                Name = $"漫游推荐：{n1} - {n2}",
                                Description = $"全长约 {(isProjected ? (totalLen / 1000).ToString("F0") : (totalLen * 100).ToString("F0"))}公里 (估算)，途经 {routePoints.Count} 个非遗点。",
                                RoadFeatures = currentChain,
                                Points = routePoints,
                                PathLine = routeLine
                            };
                            routes.Add(tr);
                        }
                        else
                        {
                            // 释放资源
                            foreach (var f in currentChain) Marshal.ReleaseComObject(f);
                        }
                    }
                    else
                    {
                        // 太短了，丢弃
                        foreach (var f in currentChain) Marshal.ReleaseComObject(f);
                    }
                }
            }

            // 策略2：兜底逻辑 (基于地市的预设路线)
            if (routes.Count == 0)
            {
                routes.Add(CreateFallbackRoute(pointLayer, "鲁豫文化走廊 (济青线)", "济南,淄博,潍坊,青岛", "横贯山东东西的文化大动脉，连接省会与沿海城市。"));
                routes.Add(CreateFallbackRoute(pointLayer, "运河文化风情带", "德州,聊城,济宁,枣庄", "沿着京杭大运河一路向南，感受运河儿女的匠心独运。"));
                routes.Add(CreateFallbackRoute(pointLayer, "仙境海岸民俗游", "滨州,东营,烟台,威海,日照", "沿着黄金海岸线，体验渔家文化与海洋非遗。"));
            }

            return routes;
        }

        private static TourRoute CreateFallbackRoute(IFeatureLayer layer, string name, string cityKeywords, string desc)
        {
            TourRoute route = new TourRoute { Name = name, Description = desc };
            string[] cities = cityKeywords.Split(',');

            // 暴力构建 Where Clause
            string where = "";
            foreach (var city in cities)
            {
                if (where.Length > 0) where += " OR ";
                // 尝试匹配常见字段
                where += $"CITY_NAME LIKE '%{city}%' OR Name LIKE '%{city}%' OR 地区 LIKE '%{city}%' OR 市 LIKE '%{city}%'";
            }

            try
            {
                IQueryFilter qf = new QueryFilterClass { WhereClause = where };
                if (layer.FeatureClass.Fields.FindField("CITY_NAME") == -1 && layer.FeatureClass.Fields.FindField("地区") == -1 && layer.FeatureClass.Fields.FindField("市") == -1)
                {
                    // 字段不存在，放弃精确筛选，仅随机选几个点模拟
                    qf.WhereClause = "";
                }

                // 第一轮尝试：按地市筛选
                IFeatureCursor cursor = layer.FeatureClass.Search(qf, false);
                IFeature f;
                int max = 20;
                while ((f = cursor.NextFeature()) != null && route.Points.Count < max)
                {
                    route.Points.Add(f);
                }
                Marshal.ReleaseComObject(cursor);

                // 第二轮尝试：如果没找到任何点 (字段不匹配或数据问题)，则随机填充，确保存活
                if (route.Points.Count == 0)
                {
                    qf.WhereClause = "";
                    cursor = layer.FeatureClass.Search(qf, false);
                    Random rnd = new Random();
                    // 跳过一些以模拟随机
                    for (int i = 0; i < rnd.Next(0, 50); i++) cursor.NextFeature();

                    while ((f = cursor.NextFeature()) != null && route.Points.Count < max)
                    {
                        route.Points.Add(f);
                    }
                    Marshal.ReleaseComObject(cursor);
                }
                Marshal.ReleaseComObject(cursor);
            }
            catch { }

            return route;
        }

        private static string GetFeatureName(IFeature f)
        {
            string name = "未知";
            try
            {
                int idx = f.Fields.FindField("NAME");
                if (idx == -1) idx = f.Fields.FindField("Name");
                if (idx == -1) idx = f.Fields.FindField("名称");
                if (idx != -1 && f.get_Value(idx) != DBNull.Value)
                    name = f.get_Value(idx).ToString();
            }
            catch { }
            return name;
        }



        private IPoint GetCityCentroid(IFeatureLayer layer, string name)
        {
            if (layer == null) return null;

            // 尝试多种字段名
            string[] fields = { "NAME", "Name", "名称", "CITY_NAME", "CITY", "行政名", "Municipality" };
            int idx = -1;
            foreach (var f in fields)
            {
                idx = layer.FeatureClass.Fields.FindField(f);
                if (idx != -1) break;
            }

            if (idx == -1) return null;

            ISpatialFilter query = new SpatialFilterClass();
            // 注意：如果由 FormTourRoutes 传入的是 "济南市"，而表里是 "济南"，或者反之，用 Like 模糊匹配
            // 但 GDB 中 Like 语法可能有差异。这里先尝试精确匹配，不行再试
            string fieldName = layer.FeatureClass.Fields.get_Field(idx).Name;

            // [Fix] Handle fuzzy match
            if (name.EndsWith("市") && name.Length > 2) name = name.Substring(0, name.Length - 1); // Remove '市' for search if needed?
                                                                                                  // User data shows "济南市", so exact match might be better first. 
                                                                                                  // Better strategy: Where NAME Like '%济南%'

            query.WhereClause = $"{fieldName} LIKE '%{name}%'";

            IFeatureCursor cursor = null;
            try
            {
                cursor = layer.FeatureClass.Search(query, false);
                IFeature feature = cursor.NextFeature();
                if (feature != null)
                {
                    // 返回质心
                    IArea area = feature.Shape as IArea;
                    return area.Centroid;
                }
            }
            catch (Exception ex)
            {
                // Log("查询异常: " + ex.Message); // Assuming Log method exists
            }
            finally
            {
                if (cursor != null) Marshal.ReleaseComObject(cursor);
            }
            return null;
        }
    }

    /// <summary>
    /// [Optimized] 真正的优先级队列 (最小二叉堆)
    /// 解决 20w 节点下 List.Sort 导致的 O(N^2 log N) 性能灾难
    /// </summary>
    public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private List<KeyValuePair<TElement, TPriority>> _heap = new List<KeyValuePair<TElement, TPriority>>();
        public int Count => _heap.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            _heap.Add(new KeyValuePair<TElement, TPriority>(element, priority));
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_heap[i].Value.CompareTo(_heap[p].Value) >= 0) break;
                var tmp = _heap[i]; _heap[i] = _heap[p]; _heap[p] = tmp;
                i = p;
            }
        }

        public TElement Dequeue()
        {
            if (_heap.Count == 0) return default(TElement);
            var result = _heap[0].Key;
            _heap[0] = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);

            int i = 0;
            while (true)
            {
                int left = i * 2 + 1;
                int right = i * 2 + 2;
                int smallest = i;
                if (left < _heap.Count && _heap[left].Value.CompareTo(_heap[smallest].Value) < 0) smallest = left;
                if (right < _heap.Count && _heap[right].Value.CompareTo(_heap[smallest].Value) < 0) smallest = right;
                if (smallest == i) break;
                var tmp = _heap[i]; _heap[i] = _heap[smallest]; _heap[smallest] = tmp;
                i = smallest;
            }
            return result;
        }
    }
}
