using System;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace WindowsFormsMap1
{
    // [Member B] Added: Dashboard Form for Charts and Stats
    public partial class FormChart : Form
    {
        private ESRI.ArcGIS.Controls.AxMapControl _mapControl;
        private Form1 _mainForm; // [Member B] Reference to Main Form for Data Access
        private ComboBox cmbChartType; // [Member B] Chart Type Selector

        public FormChart()
        {
            InitializeComponent();
            InitMyChart();
            InitDashboardControls(); 
        }

        public void SetMapControl(ESRI.ArcGIS.Controls.AxMapControl mapControl)
        {
            _mapControl = mapControl;
        }

        // [Member B] Link to Main Form
        public void SetMainForm(Form1 form)
        {
            _mainForm = form;
            // [Fix] Trigger data update immediately after linking
            // Use a default year or current slider value
            UpdateChartData(trackBar1.Value);
        }

        private void InitMyChart()
        {
            // 初始化示例数据 (初始加载时还没连上 Form1，先留空或用默认)
            this.chart1.Series.Clear();
            Series series = new Series("非遗数量");
            series.ChartType = SeriesChartType.Column; 
            series.ToolTip = "点击查看 #VALX 详情"; 
            
            // 预设城市列表
            string[] cities = new string[] { 
                "济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市", "济宁市", 
                "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市", "滨州市", "菏泽市" 
            };
            
            foreach(var city in cities)
            {
                // 暂时给个初始值 0，等待 Load 或 Slider 触发更新
                series.Points.AddXY(city, 0); 
            }

            this.chart1.Series.Add(series);
            this.chart1.Titles.Add("山东省各市非遗数量统计 (实时数据)");
            this.chart1.ChartAreas[0].AxisX.Interval = 1;
            this.chart1.MouseClick += Chart1_MouseClick;
        }

        private void InitDashboardControls()
        {
            cmbChartType = new ComboBox();
            cmbChartType.Items.AddRange(new object[] { "柱状图", "折线图", "饼图" });
            cmbChartType.SelectedIndex = 0; 
            cmbChartType.DropDownStyle = ComboBoxStyle.DropDownList;
            
            // [Fix] Move to left-bottom to ensure it is not covered by TrackBar
            // TrackBar is usually at Top/Left of the panel or fills it.
            // Let's place ComboBox at Bottom-Left of the panelBottom
            cmbChartType.Size = new System.Drawing.Size(120, 25);
            cmbChartType.Location = new System.Drawing.Point(12, 40); 
            cmbChartType.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            
            cmbChartType.SelectedIndexChanged += CmbChartType_SelectedIndexChanged;
            this.panelBottom.Controls.Add(cmbChartType);
            
            // Critical: Ensure ComboBox is on top of other controls in the panel
            cmbChartType.BringToFront();
        }

        private void CmbChartType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (chart1.Series.Count == 0) return;
            string selected = cmbChartType.SelectedItem.ToString();
            SeriesChartType type = SeriesChartType.Column;
            switch (selected)
            {
                case "柱状图": type = SeriesChartType.Column; break;
                case "折线图": type = SeriesChartType.Line; break;
                case "饼图": type = SeriesChartType.Pie; break;
            }
            foreach (var s in chart1.Series) s.ChartType = type;
        }

        private void Chart1_MouseClick(object sender, MouseEventArgs e)
        {
            HitTestResult result = chart1.HitTest(e.X, e.Y);
            if (result.ChartElementType == ChartElementType.DataPoint)
            {
                var point = chart1.Series[0].Points[result.PointIndex];
                string cityName = point.AxisLabel; 
                
                // [Feedback] Update Title to show interaction is working
                if (chart1.Titles.Count > 0)
                {
                    chart1.Titles[0].Text = $"[正在定位] >>> {cityName}";
                    chart1.Titles[0].ForeColor = System.Drawing.Color.Blue;
                }

                if (_mapControl != null) ZoomToCity(cityName);
            }
        }

        private void ZoomToCity(string cityName)
        {
            try
            {
                if (_mapControl.LayerCount == 0) return;
                string shortName = cityName.Replace("市", "");

                // 1. 寻找最匹配项图层 (优先面图层，其次点图层)
                ESRI.ArcGIS.Carto.IFeatureLayer targetLayer = null;
                ESRI.ArcGIS.Carto.IFeatureLayer fallbackLayer = null; // 备用图层（点图层）
                string realCityField = "";
                string fallbackCityField = "";

                for (int i = 0; i < _mapControl.LayerCount; i++)
                {
                    var layer = _mapControl.get_Layer(i) as ESRI.ArcGIS.Carto.IFeatureLayer;
                    if (layer == null) continue;

                    // [修复] 优先考虑行政区划图层（用于城市定位）
                    string ln = layer.Name;
                    bool isAdminLayer = ln.Contains("行政") || ln.Contains("区划") || ln.Contains("边界") || 
                                       ln.Contains("市县") || ln.Contains("市区") || ln.Contains("区域") ||
                                       ln.Contains("District") || ln.Contains("Admin") ||
                                       ln.Contains("County") || ln.Contains("City") || 
                                       ln.ToLower().Contains("shiqu");
                    
                    // [关键修复] 检查几何类型
                    ESRI.ArcGIS.Geometry.esriGeometryType geomType = layer.FeatureClass.ShapeType;
                    bool isPolygonLayer = (geomType == ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon);
                    
                    // 检查是否存在城市字段
                    string tempCityField = "";
                    string[] cityKeys = { "行政名称", "行政名", "市", "City", "CityName", "Name", "NAME", "地市", "所属地区", "地区", "NAME99" };
                    for (int j = 0; j < layer.FeatureClass.Fields.FieldCount; j++)
                    {
                        string fName = layer.FeatureClass.Fields.get_Field(j).Name;
                        foreach (string k in cityKeys) 
                        {
                            if (fName.Equals(k, StringComparison.OrdinalIgnoreCase)) 
                            { 
                                tempCityField = fName; 
                                break; 
                            }
                        }
                        if (!string.IsNullOrEmpty(tempCityField)) break;
                    }

                    if (!string.IsNullOrEmpty(tempCityField))
                    {
                        // 如果找到了面状的行政区划图层，立即使用！
                        if (isAdminLayer && isPolygonLayer)
                        {
                            targetLayer = layer;
                            realCityField = tempCityField;
                            break; // 找到最佳选项，退出
                        }
                        // 否则作为备用选项
                        else if (fallbackLayer == null)
                        {
                            fallbackLayer = layer;
                            fallbackCityField = tempCityField;
                        }
                    }
                }
                
                // 如果没找到面图层，使用备用图层（点图层）
                if (targetLayer == null && fallbackLayer != null)
                {
                    targetLayer = fallbackLayer;
                    realCityField = fallbackCityField;
                }

                // Fallback to layer 0 if needed
                if (targetLayer == null) targetLayer = _mapControl.get_Layer(0) as ESRI.ArcGIS.Carto.IFeatureLayer;
                if (targetLayer == null || string.IsNullOrEmpty(realCityField)) return;

                // 2. 执行空间定位
                ESRI.ArcGIS.Geodatabase.IQueryFilter queryFilter = new ESRI.ArcGIS.Geodatabase.QueryFilterClass();
                queryFilter.WhereClause = $"{realCityField} = '{cityName}' OR {realCityField} LIKE '%{shortName}%'"; 

                ESRI.ArcGIS.Geodatabase.IFeatureCursor cursor = targetLayer.FeatureClass.Search(queryFilter, false);
                ESRI.ArcGIS.Geodatabase.IFeature feature = cursor.NextFeature();

                if (feature != null)
                {
                    try
                    {
                        // 3. 缩放到该区域
                        ESRI.ArcGIS.Geometry.IEnvelope envelope = feature.Shape.Envelope;
                        if (feature.Shape.GeometryType == ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint)
                        {
                            envelope.Expand(0.18, 0.18, false);
                        }
                        else
                        {
                            envelope.Expand(1.5, 1.5, true); 
                        }
                        
                        _mapControl.ActiveView.Extent = envelope;
                        _mapControl.ActiveView.Refresh();
                        
                        // 成功反馈
                        if (chart1.Titles.Count > 0)
                        {
                            chart1.Titles[0].Text = $"山东省各市非遗数量统计 (已定位: {cityName})";
                            chart1.Titles[0].ForeColor = System.Drawing.Color.Green;
                        }
                        
                        // 尝试高亮(如果失败不影响主流程)
                        try
                        {
                            _mapControl.Map.ClearSelection();
                            _mapControl.Map.SelectFeature(targetLayer, feature);
                            _mapControl.ActiveView.PartialRefresh(ESRI.ArcGIS.Carto.esriViewDrawPhase.esriViewGeoSelection, null, null);
                            _mapControl.FlashShape(feature.Shape, 2, 200, null);
                        }
                        catch { /* 高亮失败不影响定位 */ }
                    }
                    catch (Exception)
                    {
                        // 即使缩放失败,也显示找到了
                        if (chart1.Titles.Count > 0)
                        {
                            chart1.Titles[0].Text = $"[部分成功] 找到'{cityName}'但缩放失败";
                            chart1.Titles[0].ForeColor = System.Drawing.Color.Orange;
                        }
                    }
                }
                else
                {
                    // 如果没搜到，简单提示
                    if (chart1.Titles.Count > 0)
                    {
                        chart1.Titles[0].Text = $"[定位失败] 未找到 '{cityName}'";
                        chart1.Titles[0].ForeColor = System.Drawing.Color.OrangeRed;
                    }
                }
                
                if (cursor != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);
            }
            catch (Exception)
            {
                // 查询过程异常
                if (chart1.Titles.Count > 0)
                {
                    chart1.Titles[0].Text = "[查询异常] 请检查图层结构";
                    chart1.Titles[0].ForeColor = System.Drawing.Color.Red;
                }
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            int year = trackBar1.Value;
            this.lblTime.Text = $"当前年份: {year}";
            UpdateChartData(year);
            
            // [Member B] 同步过滤地图要素
            if (_mainForm != null)
            {
                _mainForm.FilterMapByYear(year);
            }
        }

        public void UpdateChartData(int year)
        {
            if (_mainForm == null) return;

            int totalCount = 0;
            // Iterate over existing points (Cities) and update their Y values
            foreach (var point in chart1.Series[0].Points)
            {
                string city = point.AxisLabel;
                // Call Member D's API with Year
                int count = _mainForm.GetCountByCity(city, year);
                
                point.YValues[0] = count;
                totalCount += count;
            }
            chart1.Invalidate();

            // [Member B Debug Logic - Visual Feedback]
            // Update the existing title or add one if missing
            if (chart1.Titles.Count == 0) chart1.Titles.Add("Status");
            
            var title = chart1.Titles[0];

            if (totalCount > 0)
            {
                title.Text = $"山东省各市非遗数量统计 (年份: {year} | 总数: {totalCount})";
                title.ForeColor = System.Drawing.Color.Black;
            }
            else
            {
                // If total is 0, give a hint
                title.Text = $"[无数据] 请加载非遗SHP (目标字段: 市/Name)";
                title.ForeColor = System.Drawing.Color.Red;
            }
        }
    }
}
