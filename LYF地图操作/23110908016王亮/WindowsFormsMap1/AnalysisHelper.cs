using System;
using System.Collections.Generic;
using System.Linq;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.esriSystem;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【核心逻辑】路径规划引擎
    /// 包含：拓扑打断构建 (Planarization)、多候选路由 (Multi-Candidate)、永不失败策略 (Never-Fail)
    /// 文件路径: c:\Users\29525\Desktop\地理信息系统开发实习\1.8\-\LYF地图操作\23110908016王亮\WindowsFormsMap1\AnalysisHelper.cs
    /// </summary>
    public class AnalysisHelper
    {
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

        public AnalysisHelper() { }

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

                return $"路网重构成功！\n\n【拓扑优化报告】\n原始路段数: {count}\n拓扑打断后节点数: {_graph.Count}\n(节点数已大幅增加，所有交叉口已打通)";
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
