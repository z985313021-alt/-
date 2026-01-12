// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.esriSystem;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// Form1 的业务工具逻辑 (加载、分析、数据管理)
    /// </summary>
    public partial class Form1
    {
        // ================= 智能工具箱变量 =================
        private IPoint _routeStart;
        private IPoint _routeEnd;
        private IEnvelope _queryEnv;
        private IPolygon _queryPoly;

        // ================= 初始化 =================
        // ================= 初始化 =================
        public void InitSmartTools()
        {
            // [Modified] 用户要求显示为 "智能工具箱" 且放在显著位置
            ToolStripMenuItem smartMenu = new ToolStripMenuItem("智能工具箱");

            // 1. 路径规划 (合并入口)
            // 用户要求：点击“路径规划”后，弹出一个弹窗来，在里面进行构建路网和规划面板
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

            // 2. 缓冲区
            // 2. 缓冲区 (统一入口)
            smartMenu.DropDownItems.Add("交互式缓冲区工具箱", null, (s, e) => 
            { 
                if (SmartBufferForm == null || SmartBufferForm.IsDisposed)
                {
                    SmartBufferForm = new FormSmartBuffer(this);
                }
                SmartBufferForm.Show();
                SmartBufferForm.Activate();
            });

            // 3. 辅助功能
            smartMenu.DropDownItems.Add("清除所有绘图", null, (s, e) => { 
                axMapControl2.ActiveView.GraphicsContainer.DeleteAllElements();
                axMapControl2.ActiveView.Refresh();
            });

            // 4. 几何查询
            ToolStripMenuItem queryItem = new ToolStripMenuItem("几何查询");
            queryItem.DropDownItems.Add("拉框查询", null, (s, e) => { SwitchTool(MapToolMode.QueryBox); });
            queryItem.DropDownItems.Add("多边形查询", null, (s, e) => { SwitchTool(MapToolMode.QueryPolygon); });
            smartMenu.DropDownItems.Add(queryItem);

            // 插入到第二个位置 (索引1)，仅次于“数据加载”
            this.menuStrip1.Items.Insert(1, smartMenu);
        }

        // ================= 业务逻辑实现 =================

        public void BuildRoadNetwork()
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
                MessageBox.Show("请先在TOC中选中一个线状道路图层！\n(右键点击图层 -> 确保选中状态)");
            }
        }

        private void SolvePath()
        {
            if (_routeStart == null || _routeEnd == null)
            {
                MessageBox.Show("请先设置起点和终点！");
                return;
            }
            try
            {
                IPolyline result = _analysisHelper.FindShortestPath(_routeStart, _routeEnd);
                if (result != null)
                {
                    // 绘制结果 (红色粗线)
                    DrawGeometry(result, new RgbColorClass { Red = 255 });
                    MessageBox.Show($"规划成功！总长度: {result.Length:F2}");
                }
                else
                {
                    MessageBox.Show("未找到路径，请确保起点终点在路网附近，且路网连通。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("计算失败: " + ex.Message);
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

        private void PerformSpatialQuery(IGeometry filterGeo)
        {
            ILayer layer = GetSelectedLayer();
            if (layer is IFeatureLayer fl)
            {
                int count = AnalysisHelper.SelectFeatures(fl, filterGeo);
                axMapControl2.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
                
                // [Modified] Use new Query Result Form
                FormQueryResult resForm = new FormQueryResult(fl, count);
                resForm.ShowDialog();
            }
            else
            {
                MessageBox.Show("请先选中目标图层！");
            }
        }

        public void DrawGeometry(IGeometry geo, IColor color)
        {
            if (geo == null || geo.IsEmpty) return;

            // [Refinement] 确保绘制的几何体与地图坐标系一致
            IGeometry drawGeo = geo;
            ISpatialReference mapSR = axMapControl2.SpatialReference;
            if (geo.SpatialReference != null && mapSR != null && geo.SpatialReference.FactoryCode != mapSR.FactoryCode)
            {
                drawGeo = (geo as IClone).Clone() as IGeometry;
                try { drawGeo.Project(mapSR); } catch { }
            }

            // 简单绘制临时元素 (Element)
            IElement ele = null;
            if (drawGeo.GeometryType == esriGeometryType.esriGeometryPolyline)
            {
                LineElementClass lineEle = new LineElementClass { Geometry = drawGeo };
                // 增加线宽到 4，确保清晰可见
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
                // 强制刷新地理背景和图形层
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
