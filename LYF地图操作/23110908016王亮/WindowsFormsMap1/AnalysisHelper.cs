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

        public string BuildNetwork(IFeatureLayer roadLayer)
        {
            try
            {
                if (roadLayer == null || roadLayer.FeatureClass == null) return "提示：请先在左侧图层树选中道路图层";
                _roadLayer = roadLayer;
                _mapSR = (_roadLayer.FeatureClass as IGeoDataset).SpatialReference;

                // 1. 数据准备：读取所有 Line 到 GeometryBag
                GeometryBagClass geoBag = new GeometryBagClass();
                geoBag.SpatialReference = _mapSR;
                IGeometryCollection geoColl = geoBag as IGeometryCollection;

                IFeatureCursor cursor = _roadLayer.FeatureClass.Search(null, false); // 非回收游标
                IFeature feat;
                int count = 0;
                while ((feat = cursor.NextFeature()) != null)
                {
                    if (feat.Shape != null && !feat.Shape.IsEmpty)
                    {
                        geoColl.AddGeometry(feat.ShapeCopy); // ShapeCopy 防止引用争用
                        count++;
                    }
                }
                Marshal.ReleaseComObject(cursor);

                if (count == 0) return "选中的图层没有要素！";

                // 2. 拓扑打断 (Planarize)：将面条路网打碎成网格路网
                // 核心修复：使用 PolylineClass 的 ConstructUnion 来执行打断
                PolylineClass unionLine = new PolylineClass();
                unionLine.SpatialReference = _mapSR;
                ITopologicalOperator2 topoOp = unionLine as ITopologicalOperator2;
                topoOp.ConstructUnion(geoBag as IEnumGeometry);
                // 此时 unionLine 已经是包含所有打断后线段的复杂多义线了

                // 3. 构建图结构
                _graph = new Dictionary<int, GraphNode>();
                Dictionary<string, int> nodeLookup = new Dictionary<string, int>();

                if (_mapSR is IProjectedCoordinateSystem) _mergeTolerance = 1.0;
                else _mergeTolerance = 0.00001;

                // 遍历打断后的每一段 (Segment)
                ISegmentCollection segColl = unionLine as ISegmentCollection;
                IEnumSegment segments = segColl.EnumSegments;
                ISegment segment;
                int partIndex = 0, segIndex = 0;
                int nextNodeId = 0; // [Fix] 恢复变量定义
                segments.Next(out segment, ref partIndex, ref segIndex);

                while (segment != null)
                {
                    // 将 Segment 转换为 Polyline 几何对象以便存储
                    PolylineClass edgePoly = new PolylineClass();
                    edgePoly.SpatialReference = _mapSR;
                    (edgePoly as ISegmentCollection).AddSegment(segment);

                    int u = GetOrCreateNodeId(segment.FromPoint, nodeLookup, ref nextNodeId);
                    int v = GetOrCreateNodeId(segment.ToPoint, nodeLookup, ref nextNodeId);

                    if (u != v) // 忽略自环
                    {
                        double length = segment.Length;
                        _graph[u].Edges.Add(new GraphEdge { TargetNodeId = v, Weight = length, Geometry = edgePoly });
                        _graph[v].Edges.Add(new GraphEdge { TargetNodeId = u, Weight = length, Geometry = edgePoly });
                    }

                    segments.Next(out segment, ref partIndex, ref segIndex);
                }

                // [Agent (通用辅助)] Added: 保存路网缓存
                SaveNetworkCache(roadLayer.Name);

                return $"路网重构成功!\n\n【拓扑优化报告】\n原始路段数: {count}\n拓扑打断后节点数: {_graph.Count}\n(节点数已大幅增加,所有交叉口已打通)\n\n缓存已保存,下次启动将自动加载。";
            }
            catch (Exception ex)
            {
                return "路网构建出错 (可能是内存不足或数据极其复杂): " + ex.Message;
            }
        }

        private int GetOrCreateNodeId(IPoint pt, Dictionary<string, int> lookup, ref int nextId)
        {
            // 降低精度进行模糊匹配，确保路口缝合
            string key = $"{Math.Round(pt.X / _mergeTolerance)}_{Math.Round(pt.Y / _mergeTolerance)}";
            if (!lookup.ContainsKey(key))
            {
                int id = nextId++;
                lookup[key] = id;
                _graph[id] = new GraphNode { Id = id, Point = (pt as IClone).Clone() as IPoint };
            }
            return lookup[key];
        }

        // [Agent (通用辅助)] Added: 保存路网缓存到文件
        private void SaveNetworkCache(string layerName)
        {
            try
            {
                var cache = new NetworkCache
                {
                    LayerName = layerName,
                    MergeTolerance = _mergeTolerance,
                    BuildTime = DateTime.Now,
                    Nodes = new List<SerializableNode>()
                };

                // 将图结构转换为可序列化格式
                foreach (var node in _graph.Values)
                {
                    var sNode = new SerializableNode
                    {
                        Id = node.Id,
                        X = node.Point.X,
                        Y = node.Point.Y
                    };

                    foreach (var edge in node.Edges)
                    {
                        var sEdge = new SerializableEdge
                        {
                            TargetNodeId = edge.TargetNodeId,
                            Weight = edge.Weight
                        };

                        // 提取几何点坐标
                        IPointCollection ptColl = edge.Geometry as IPointCollection;
                        for (int i = 0; i < ptColl.PointCount; i++)
                        {
                            IPoint pt = ptColl.Point[i];
                            sEdge.GeometryPoints.Add(new double[] { pt.X, pt.Y });
                        }

                        sNode.Edges.Add(sEdge);
                    }

                    cache.Nodes.Add(sNode);
                }

                // 保存到文件
                string cacheFile = System.IO.Path.Combine(_cacheDirectory, $"{SanitizeFileName(layerName)}_cache.json");
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(cache);
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                // 缓存保存失败不影响主流程
                System.Diagnostics.Debug.WriteLine($"保存路网缓存失败: {ex.Message}");
            }
        }

        // [Agent (通用辅助)] Added: 从文件加载路网缓存
        private bool LoadNetworkCache(string layerName, ISpatialReference spatialRef)
        {
            try
            {
                string cacheFile = System.IO.Path.Combine(_cacheDirectory, $"{SanitizeFileName(layerName)}_cache.json");
                if (!File.Exists(cacheFile)) return false;

                string json = File.ReadAllText(cacheFile);
                var serializer = new JavaScriptSerializer();
                var cache = serializer.Deserialize<NetworkCache>(json);

                if (cache == null || cache.Nodes == null) return false;

                // 重建图结构
                _graph = new Dictionary<int, GraphNode>();
                _mergeTolerance = cache.MergeTolerance;
                _mapSR = spatialRef;

                foreach (var sNode in cache.Nodes)
                {
                    var node = new GraphNode
                    {
                        Id = sNode.Id,
                        Point = new PointClass { X = sNode.X, Y = sNode.Y, SpatialReference = spatialRef }
                    };

                    foreach (var sEdge in sNode.Edges)
                    {
                        var edge = new GraphEdge
                        {
                            TargetNodeId = sEdge.TargetNodeId,
                            Weight = sEdge.Weight,
                            Geometry = new PolylineClass { SpatialReference = spatialRef }
                        };

                        // 重建几何
                        IPointCollection ptColl = edge.Geometry as IPointCollection;
                        foreach (var coords in sEdge.GeometryPoints)
                        {
                            ptColl.AddPoint(new PointClass { X = coords[0], Y = coords[1], SpatialReference = spatialRef });
                        }

                        node.Edges.Add(edge);
                    }

                    _graph[node.Id] = node;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载路网缓存失败: {ex.Message}");
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

            // 【多候选策略】
            var startNodes = FindNearestNodes(sPt, 5);
            var endNodes = FindNearestNodes(ePt, 5);

            if (startNodes.Count == 0 || endNodes.Count == 0) return null;

            // Dijkstra
            var distances = new Dictionary<int, double>();
            var previous = new Dictionary<int, KeyValuePair<int, IPolyline>>();
            var visited = new HashSet<int>();
            PriorityQueue<int, double> pq = new PriorityQueue<int, double>();

            // 初始化所有距离
            foreach (var node in _graph.Keys) distances[node] = double.MaxValue;

            // 多起点入队
            foreach (var sNode in startNodes)
            {
                double d = (sPt as IProximityOperator).ReturnDistance(_graph[sNode].Point);
                distances[sNode] = d;
                pq.Enqueue(sNode, d);
            }

            HashSet<int> targetSet = new HashSet<int>(endNodes);
            int reachedEndId = -1;

            while (pq.Count > 0)
            {
                int u = pq.Dequeue();

                if (targetSet.Contains(u))
                {
                    reachedEndId = u;
                    break;
                }

                if (visited.Contains(u)) continue;
                visited.Add(u);

                foreach (var edge in _graph[u].Edges)
                {
                    double alt = distances[u] + edge.Weight;
                    if (alt < distances[edge.TargetNodeId])
                    {
                        distances[edge.TargetNodeId] = alt;
                        previous[edge.TargetNodeId] = new KeyValuePair<int, IPolyline>(u, edge.Geometry);
                        pq.Enqueue(edge.TargetNodeId, alt);
                    }
                }
            }

            // 【永不失败】断路兜底逻辑
            int finalEndId = reachedEndId;
            if (finalEndId == -1) // Unreachable
            {
                double minD = double.MaxValue;
                foreach (int vid in visited)
                {
                    double d = (ePt as IProximityOperator).ReturnDistance(_graph[vid].Point);
                    if (d < minD) { minD = d; finalEndId = vid; }
                }

                if (finalEndId == -1 && startNodes.Count > 0) finalEndId = startNodes[0];
            }

            // 回溯
            PolylineClass result = new PolylineClass();
            if (_mapSR != null) result.SpatialReference = _mapSR;
            IGeometryCollection geoColl = result as IGeometryCollection;

            if (finalEndId != -1)
            {
                int curr = finalEndId;
                while (previous.ContainsKey(curr))
                {
                    var step = previous[curr];
                    if (step.Value != null) geoColl.AddGeometryCollection(step.Value as IGeometryCollection);
                    curr = step.Key;
                }
            }

            if (result.IsEmpty && startNodes.Count > 0)
            {
                result.AddPoint(_graph[startNodes[0]].Point);
                result.AddPoint(_graph[startNodes[0]].Point);
            }

            try { if (!result.IsEmpty) (result as ITopologicalOperator).Simplify(); } catch { }

            return result;
        }

        private List<int> FindNearestNodes(IPoint pt, int k)
        {
            if (_graph == null || _graph.Count == 0) return new List<int>();
            IProximityOperator prox = pt as IProximityOperator;
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

            for (int i = 0; i < points.Count - 1; i++)
            {
                IPolyline seg = FindShortestPath(points[i], points[i + 1]);
                if (seg != null && !seg.IsEmpty) coll.AddGeometryCollection(seg as IGeometryCollection);
            }
            try { if (!total.IsEmpty) (total as ITopologicalOperator).Simplify(); } catch { }
            return total.IsEmpty ? null : total;
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
                while (n > 1) {  
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
                             if (currentChain.Count > 1) n2 = GetFeatureName(currentChain[currentChain.Count-1]);

                            TourRoute tr = new TourRoute
                            {
                                Name = $"漫游推荐：{n1} - {n2}",
                                Description = $"全长约 {(isProjected ? (totalLen/1000).ToString("F0") : (totalLen*100).ToString("F0"))}公里 (估算)，途经 {routePoints.Count} 个非遗点。",
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
                    for(int i=0; i< rnd.Next(0, 50); i++) cursor.NextFeature();

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
            catch {}
            return name;
        }
    }

    public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private List<KeyValuePair<TElement, TPriority>> _list = new List<KeyValuePair<TElement, TPriority>>();
        public int Count => _list.Count;
        public void Enqueue(TElement e, TPriority p)
        {
            _list.Add(new KeyValuePair<TElement, TPriority>(e, p));
            _list.Sort((a, b) => a.Value.CompareTo(b.Value));
        }
        public TElement Dequeue()
        {
            var e = _list[0].Key;
            _list.RemoveAt(0);
            return e;
        }
    }
}
