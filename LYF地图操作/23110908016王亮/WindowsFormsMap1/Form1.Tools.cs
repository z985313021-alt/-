// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.esriSystem;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// Form1 的业务工具逻辑 (加载、分析、数据管理)
    /// </summary>
    public partial class Form1
    {
        // ================= 智能工具箱内部变量 =================
        private IPoint _routeStart;     // 路径规划起点
        private IPoint _routeEnd;       // 路径规划终点
        private IEnvelope _queryEnv;    // 拉框查询范围
        private IPolygon _queryPoly;    // 多边形查询区域

        // ================= 初始化 =================
        // 【智能工具箱初始化】：动态构建主界面顶部的 Ribbon 菜单，注入路径规划、空间分析及数据清理功能
        public void InitSmartTools()
        {
            // 模式要求：创建一个名为“智能工具箱”的项目级顶层菜单
            ToolStripMenuItem smartMenu = new ToolStripMenuItem("智能工具箱");
            smartMenu.Image = ThemeEngine.GetIcon("Toolbox", Color.Black);
            smartMenu.TextImageRelation = TextImageRelation.ImageAboveText;

            // 1. 路径规划子项：呼出路径搜索面板，支持路网构建、点对点最短路径计算
            ToolStripMenuItem routeItem = new ToolStripMenuItem("路径规划");
            routeItem.Click += (s, e) =>
            {
                if (_routeForm == null || _routeForm.IsDisposed)
                {
                    _routeForm = new FormRoute(this, _analysisHelper);
                }
                _routeForm.Show();
                _routeForm.Activate();
            };
            smartMenu.DropDownItems.Add(routeItem);

            // 2. 空间缓冲区：弹出交互式缓冲区参数设置窗口（KM 与地图单位自动转换）
            smartMenu.DropDownItems.Add("交互式缓冲区工具箱", null, (s, e) =>
            {
                if (SmartBufferForm == null || SmartBufferForm.IsDisposed)
                {
                    SmartBufferForm = new FormSmartBuffer(this);
                }
                SmartBufferForm.Show();
                SmartBufferForm.Activate();
            });

            // 3. 画布清理：一键移除地图上的所有临时几何图形（Buffer 圆、规划线路等）
            smartMenu.DropDownItems.Add("清除所有绘图", null, (s, e) =>
            {
                axMapControl2.ActiveView.GraphicsContainer.DeleteAllElements();
                axMapControl2.ActiveView.Refresh();
            });

            // 4. 高级几何查询：支持拉框查询与多边形拓扑包含查询
            ToolStripMenuItem queryItem = new ToolStripMenuItem("几何查询");
            queryItem.DropDownItems.Add("拉框查询", null, (s, e) => { SwitchTool(MapToolMode.QueryBox); });
            queryItem.DropDownItems.Add("多边形查询", null, (s, e) => { SwitchTool(MapToolMode.QueryPolygon); });
            smartMenu.DropDownItems.Add(queryItem);

            // 优先级注入：将该工具箱插入到菜单栏的显著位置（紧随数据加载之后）
            this.menuStrip1.Items.Insert(1, smartMenu);
        }

        // ================= 业务逻辑实现 =================

        // 【构建路网核心逻辑】：支持缓存加速
        public void BuildRoadNetwork()
        {
            ILayer layer = GetSelectedLayer();
            if (layer is IFeatureLayer fl && fl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
            {
                this.Cursor = Cursors.WaitCursor;

                // 1. 尝试从二进制缓存文件读取路网结构（加速加载）
                if (_analysisHelper.TryLoadNetworkCache(fl))
                {
                    this.Cursor = Cursors.Default;
                    MessageBox.Show($"路网缓存加载成功!\n\n已从缓存恢复路网结构,无需重新构建。\n\n提示:如需重新构建,请在路径规划窗口中点击\"重新构建路网\"按钮。",
                        "缓存加载", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // 2. 若无缓存，则遍历所有线要素进行拓扑构建（较为耗时）
                    string msg = _analysisHelper.BuildNetwork(fl);
                    this.Cursor = Cursors.Default;
                    MessageBox.Show(msg);
                }
            }
            else
            {
                MessageBox.Show("请先在TOC中选中一个线状道路图层!\n(右键点击图层 -> 确保选中状态)");
            }
        }

        // [Agent (通用辅助)] Added: 强制重建路网(跳过缓存)
        public void ForceBuildRoadNetwork()
        {
            ILayer layer = GetSelectedLayer();
            if (layer is IFeatureLayer fl && fl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
            {
                this.Cursor = Cursors.WaitCursor;
                string msg = _analysisHelper.BuildNetwork(fl);
                this.Cursor = Cursors.Default;
                MessageBox.Show(msg);
            }
            else
            {
                MessageBox.Show("请先在TOC中选中一个线状道路图层!\n(右键点击图层 -> 确保选中状态)");
            }
        }

        // 【路径求解】：基于 Dijkstra 算法计算最短路径
        // 【路径规划执行】：调用后台分析模块执行最短路径计算
        private void SolvePath()
        {
            if (_routeStart == null || _routeEnd == null)
            {
                MessageBox.Show("导航失败：请先在地图上确定起点与终点！");
                return;
            }
            try
            {
                // 指令分发：调用 Dijkstra 核心算法进行路径追踪
                IPolyline result = _analysisHelper.FindShortestPath(_routeStart, _routeEnd);
                if (result != null)
                {
                    // 结果落画：将生成的几何路径以红色高亮的形式绘制在地图 Graphics 层
                    DrawGeometry(result, new RgbColorClass { Red = 255 });
                    MessageBox.Show($"规划路径已生成！预计长度: {result.Length:F2} 单位");
                }
                else
                {
                    MessageBox.Show("计算无结果：请检查两点间是否具备连通的道路网路。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("分析过程发生系统异常: " + ex.Message);
            }
        }

        private void ExecutePointBuffer(int x, int y)
        {
            IPoint pt = axMapControl2.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(x, y);
            // 简单弹窗输入半径
            string input = Microsoft.VisualBasic.Interaction.InputBox("请输入缓冲半径 (单位: 千米):", "参数输入", "1.0");
            if (double.TryParse(input, out double rKm))
            {
                // [Modified] 转换单位 KM -> MapUnits
                double rMap = ConvertKmToMapUnit(rKm);
                IGeometry bufGeo = AnalysisHelper.GeneratePointBuffer(pt, rMap);
                IRgbColor color = new RgbColorClass { Blue = 255 };
                color.Transparency = 100;
                DrawGeometry(bufGeo, color);

                // 询问是否查询包含的要素
                if (MessageBox.Show("是否查询缓冲区内的要素？", "查询", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    PerformSpatialQuery(bufGeo);
                }
            }
        }

        private void ExecuteLineBuffer(IGeometry lineGeo)
        {
            if (lineGeo == null || lineGeo.IsEmpty) return;

            string input = Microsoft.VisualBasic.Interaction.InputBox("请输入缓冲半径 (单位: 千米):", "参数输入", "0.5");
            if (double.TryParse(input, out double rKm))
            {
                double rMap = ConvertKmToMapUnit(rKm);

                ITopologicalOperator topo = lineGeo as ITopologicalOperator;
                IGeometry bufGeo = topo.Buffer(rMap);

                IRgbColor color = new RgbColorClass { Green = 255, Transparency = 100 };
                DrawGeometry(bufGeo, color);

                if (MessageBox.Show("是否查询缓冲区内的要素？", "查询", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    PerformSpatialQuery(bufGeo);
                }
            }
        }

        private double ConvertKmToMapUnit(double km)
        {
            ISpatialReference mapSR = axMapControl2.SpatialReference;
            if (mapSR is IProjectedCoordinateSystem pcs)
            {
                return (km * 1000.0) / pcs.CoordinateUnit.MetersPerUnit;
            }
            else
            {
                return km / 111.0;
            }
        }

        // 【空间关联查询】：基于多边形（如缓冲区）对指定图层进行拓扑交集查询，并弹出统计报告
        private void PerformSpatialQuery(IGeometry filterGeo)
        {
            ILayer layer = GetSelectedLayer();
            if (layer is IFeatureLayer fl)
            {
                // 执行空间选择集过滤，返回被覆盖的要素总数
                int count = AnalysisHelper.SelectFeatures(fl, filterGeo);
                // 仅刷新选择集绘制阶段，提升交互流畅度
                axMapControl2.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);

                // 呼出专业查询结果窗口，展示匹配要素的属性详情
                FormQueryResult resForm = new FormQueryResult(fl, count);
                resForm.ShowDialog();
            }
            else
            {
                MessageBox.Show("查询模式错误：请先在左侧图层树 (TOC) 中选中一个目标要素图层！");
            }
        }

        // 【临时图形绘制】：在地图的图形容器中快速绘制圆、线、多边形等示意几何体，支持多坐标系自动转换
        public void DrawGeometry(IGeometry geo, IColor color)
        {
            if (geo == null || geo.IsEmpty) return;

            // 空间一致性检查：确保传入的分析结果几何体与当前地图的投影坐标系对齐
            IGeometry drawGeo = geo;
            ISpatialReference mapSR = axMapControl2.SpatialReference;
            if (geo.SpatialReference != null && mapSR != null && geo.SpatialReference.FactoryCode != mapSR.FactoryCode)
            {
                drawGeo = (geo as IClone).Clone() as IGeometry;
                try { drawGeo.Project(mapSR); } catch { }
            }

            // 构造对应的 Element 对象，将其注入到 GraphicsContainer 中
            IElement ele = null;
            if (drawGeo.GeometryType == esriGeometryType.esriGeometryPolyline)
            {
                LineElementClass lineEle = new LineElementClass { Geometry = drawGeo };
                lineEle.Symbol = new SimpleLineSymbolClass { Color = color, Width = 4 };
                ele = lineEle;
            }
            else if (drawGeo.GeometryType == esriGeometryType.esriGeometryPolygon)
            {
                PolygonElementClass polyEle = new PolygonElementClass { Geometry = drawGeo };
                SimpleFillSymbolClass sym = new SimpleFillSymbolClass { Color = color, Style = esriSimpleFillStyle.esriSFSSolid };
                polyEle.Symbol = sym;
                ele = polyEle;
            }
            else if (drawGeo.GeometryType == esriGeometryType.esriGeometryPoint)
            {
                MarkerElementClass mkEle = new MarkerElementClass { Geometry = drawGeo };
                mkEle.Symbol = new SimpleMarkerSymbolClass { Color = color, Size = 12, Style = esriSimpleMarkerStyle.esriSMSCircle };
                ele = mkEle;
            }

            if (ele != null)
            {
                axMapControl2.ActiveView.GraphicsContainer.AddElement(ele, 0);
                // 触发刷新：同步更新地理要素背景与上层的动态标注图形
                axMapControl2.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                axMapControl2.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
            }
        }

        // ================= 数据加载逻辑 =================

        private void 加载地图文档ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "MXD文档(*.mxd)|*.mxd" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                if (axMapControl2.CheckMxFile(dlg.FileName))
                {
                    axMapControl2.LoadMxFile(dlg.FileName);
                    axMapControl2.ActiveView.Refresh();
                    axTOCControl2.Update();
                    CheckBrokenLayers(axMapControl2.Map);
                }
            }
        }

        private void CheckBrokenLayers(IMap map)
        {
            if (map == null) return;
            List<string> broken = new List<string>();
            for (int i = 0; i < map.LayerCount; i++) if (!map.get_Layer(i).Valid) broken.Add(map.get_Layer(i).Name);
            if (broken.Count > 0) MessageBox.Show("以下图层数据丢失：\n" + string.Join("\n", broken), "警告");
        }

        private void 加载shp数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "SHP文件(*.shp)|*.shp" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                axMapControl2.AddShapeFile(System.IO.Path.GetDirectoryName(dlg.FileName), System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));
                axMapControl2.ActiveView.Refresh();
            }
        }

        public void ItemAddXYData_Click(object sender, EventArgs e) => new FormAddXYData(axMapControl2).ShowDialog();

        // ================= 空间分析触发 =================

        public void ItemBuffer_Click(object sender, EventArgs e) => new FormBuffer(axMapControl2).ShowDialog();
        public void ItemOverlay_Click(object sender, EventArgs e) => new FormOverlay(axMapControl2).ShowDialog();

        // ================= 非遗专项业务 =================

        private void ItemInitData_Click(object sender, EventArgs e)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                string targetDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\", "Data");
                if (!System.IO.Directory.Exists(targetDir)) System.IO.Directory.CreateDirectory(targetDir);

                string srcProj = @"c:\Users\Administrator\Desktop\LYF地图操作\【小黄鸭】非物质文化遗产、传承人空间点位数据\国家级非物质文化遗产代表性项目名录.shp";
                string res = DataHelper.SlimDataToGDB(srcProj, targetDir, "ShandongICH.gdb");

                if (res.Contains("成功"))
                {
                    IWorkspaceFactory wf = new ESRI.ArcGIS.DataSourcesGDB.FileGDBWorkspaceFactoryClass();
                    IFeatureWorkspace fw = (IFeatureWorkspace)wf.OpenFromFile(System.IO.Path.Combine(targetDir, "ShandongICH.gdb"), 0);
                    IFeatureClass fc = fw.OpenFeatureClass(System.IO.Path.GetFileNameWithoutExtension(srcProj) + "_SD");
                    DataHelper.DisplaceDuplicatePoints(fc);

                    IFeatureLayer fl = new FeatureLayerClass { FeatureClass = fc, Name = "山东国家级非遗项目 (已散开)" };
                    axMapControl2.AddLayer(fl);
                    axMapControl2.ActiveView.Refresh();
                    axTOCControl2.Update();
                }
                this.Cursor = Cursors.Default;
                MessageBox.Show(res, "初始化结果");
            }
            catch (Exception ex) { this.Cursor = Cursors.Default; MessageBox.Show("初始化失败: " + ex.Message); }
        }

        private void ItemCheckDataQuality_Click(object sender, EventArgs e)
        {
            ILayer selLayer = GetSelectedLayer();
            if (selLayer is IFeatureLayer fl)
            {
                string rpt = global::WindowsFormsMap1.DataAnalyzer.CheckDuplicateLocations(fl);
                ShowReport(rpt, selLayer.Name);
            }
            else MessageBox.Show("请先在TOC选中要素图层");
        }

        private void ItemDisplaceCoordinates_Click(object sender, EventArgs e)
        {
            ILayer selLayer = GetSelectedLayer();
            if (selLayer is IFeatureLayer fl)
            {
                string res = DataHelper.DisplaceDuplicatePoints(fl.FeatureClass);
                axMapControl2.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                MessageBox.Show(res);
            }
        }

        private ILayer GetSelectedLayer()
        {
            esriTOCControlItem item = esriTOCControlItem.esriTOCControlItemNone;
            IBasicMap map = null; ILayer layer = null; object other = null; object index = null;
            axTOCControl2.GetSelectedItem(ref item, ref map, ref layer, ref other, ref index);
            return (item == esriTOCControlItem.esriTOCControlItemLayer) ? layer : null;
        }

        private void ShowReport(string content, string title)
        {
            Form f = new Form { Text = "报告 - " + title, Size = new System.Drawing.Size(600, 400), StartPosition = FormStartPosition.CenterParent };
            f.Controls.Add(new TextBox { Multiline = true, Dock = DockStyle.Fill, Text = content, ReadOnly = true, ScrollBars = ScrollBars.Both });
            f.ShowDialog();
        }
    }
}
