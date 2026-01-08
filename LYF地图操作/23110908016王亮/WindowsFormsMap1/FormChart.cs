// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using System;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO;
using System.Text;
using System.Drawing;

namespace WindowsFormsMap1
{

    // [Member B] 新增：用于图表和统计的数据看板窗体
    public partial class FormChart : Form
    {
        private ESRI.ArcGIS.Controls.AxMapControl _mapControl;
        private Form1 _mainForm; // [Member B] 引用主窗体以进行数据访问和图表联动
        private string _selectedCity; // 当前选中的城市名称

        public FormChart()
        {
            InitializeComponent();
            InitMyChart();
            ApplyLightTheme();
        }

        private void ApplyLightTheme()
        {
            this.BackColor = System.Drawing.Color.AliceBlue;
            if (chart1 != null)
            {
                chart1.BackColor = System.Drawing.Color.AliceBlue;
                foreach (var area in chart1.ChartAreas) area.BackColor = System.Drawing.Color.White;
                foreach (var title in chart1.Titles) title.ForeColor = System.Drawing.Color.DarkSlateBlue;
            }
        }

        public void SetMapControl(ESRI.ArcGIS.Controls.AxMapControl mapControl)
        {
            _mapControl = mapControl;
        }

        // [Member B] 链接到主窗体
        public void SetMainForm(Form1 form)
        {
            _mainForm = form;
            // [修复] 关联后立即触发数据更新
            UpdateChartData(trackBar1.Value);
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
            // 仅更新图表 1
            foreach (var s in chart1.Series) s.ChartType = type;
        }

        private void InitMyChart()
        {
            // ========== 图表 1 (左侧): 城市数量统计 (柱状图/折线图/饼图) ==========
            this.chart1.Series.Clear();
            Series series1 = new Series("非遗数量");
            series1.ChartType = SeriesChartType.Column;
            series1.ToolTip = "点击查看 #VALX 详情";

            // 预设山东省 16 地市列表，用于初始化 X 轴标签
            string[] cities = new string[] {
                "济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市", "济宁市",
                "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市", "滨州市", "菏泽市"
            };

            foreach (var city in cities)
            {
                series1.Points.AddXY(city, 0);
            }

            this.chart1.Series.Add(series1);
            this.chart1.Titles.Clear();
            this.chart1.Titles.Add("山东省各市非遗数量统计 (点击柱状图定位)");
            this.chart1.ChartAreas[0].AxisX.Interval = 1;
            this.chart1.MouseClick += Chart1_MouseClick;

            // ========== 图表 2 (右侧): 类别统计 (饼图) ==========
            this.chart2.Series.Clear();
            Series series2 = new Series("类别分布");
            series2.ChartType = SeriesChartType.Pie;
            series2.IsValueShownAsLabel = false; // [Member B] Modified: 隐藏饼图内标签，避免视觉杂乱
            series2.ToolTip = "#VALX: #VAL (#PERCENT)";
            series2.LegendText = "#VALX: #VAL"; // 在图例中显示 类别: 数量

            this.chart2.Series.Add(series2);
            this.chart2.Titles.Clear();
            this.chart2.Titles.Add("非遗项目类别占比");
        }

        /// <summary>
        /// 图表 1 点击事件：实现“图文联动”，点击柱状图缩放到对应城市
        /// </summary>
        private void Chart1_MouseClick(object sender, MouseEventArgs e)
        {
            HitTestResult result = chart1.HitTest(e.X, e.Y);
            if (result.ChartElementType == ChartElementType.DataPoint)
            {
                var point = chart1.Series[0].Points[result.PointIndex];
                string cityName = point.AxisLabel;
                _selectedCity = cityName;

                // [反馈] 更新标题以显示交互已生效
                this.Text = $"数据看板 - 当前选中: {cityName}";

                // 缩放到选中的城市
                this.ZoomToCity(cityName);
            }
        }

        private void ZoomToCity(string cityName)
        {
            try
            {
                if (_mapControl.LayerCount == 0 || string.IsNullOrEmpty(cityName)) return;
                string shortName = cityName.Replace("市", "");

                // 1. 寻找最匹配项图层 (强优先级：面状边界图层 > 普通面图层 > 点状标注层)
                ESRI.ArcGIS.Carto.IFeatureLayer targetLayer = null;
                string realCityField = "";

                ESRI.ArcGIS.Carto.IFeatureLayer fallbackPointLayer = null;
                string fallbackPointField = "";

                // 定义识别字段的关键字
                string[] cityKeys = { "行政", "市", "City", "Name", "地市", "District", "County", "NAME99" };

                for (int i = 0; i < _mapControl.LayerCount; i++)
                {
                    var layer = _mapControl.get_Layer(i) as ESRI.ArcGIS.Carto.IFeatureLayer;
                    if (layer == null || layer.FeatureClass == null) continue;

                    string ln = layer.Name.ToLower();
                    bool isPolygon = (layer.FeatureClass.ShapeType == ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon);

                    // 检查图层是否包含“名”、“点”、“Label”、“Symbol”等标注类关键词
                    bool isLabelLayer = ln.Contains("名") || ln.Contains("label") || ln.Contains("点") || ln.Contains("symbol");

                    // 查找城市名字段
                    string tempCityField = "";
                    for (int j = 0; j < layer.FeatureClass.Fields.FieldCount; j++)
                    {
                        string fName = layer.FeatureClass.Fields.get_Field(j).Name;
                        foreach (string k in cityKeys)
                        {
                            if (fName.ToUpper().Contains(k.ToUpper()))
                            {
                                tempCityField = fName;
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(tempCityField)) break;
                    }

                    if (string.IsNullOrEmpty(tempCityField)) continue;

                    // 优先级判定
                    if (isPolygon)
                    {
                        // 最佳匹配：包含 shiqu/市区/行政 且不含 标注关键词 的面图层
                        bool isStrictBoundary = (ln.Contains("shiqu") || ln.Contains("市区") || ln.Contains("行政")) && !isLabelLayer;
                        if (isStrictBoundary)
                        {
                            targetLayer = layer;
                            realCityField = tempCityField;
                            break; // 找到最完美的定位层，直接退出循环
                        }

                        // 次佳匹配：任意包含城市字段的面图层
                        if (targetLayer == null)
                        {
                            targetLayer = layer;
                            realCityField = tempCityField;
                        }
                    }
                    else if (fallbackPointLayer == null)
                    {
                        // 备选：点图层
                        fallbackPointLayer = layer;
                        fallbackPointField = tempCityField;
                    }
                }

                // 如果没找到面图层，则使用点图层作为保底
                if (targetLayer == null)
                {
                    targetLayer = fallbackPointLayer;
                    realCityField = fallbackPointField;
                }

                if (targetLayer == null || string.IsNullOrEmpty(realCityField)) return;

                // 2. 执行空间定位 (增强版：处理字段名引用格式)
                ESRI.ArcGIS.Geodatabase.IQueryFilter queryFilter = new ESRI.ArcGIS.Geodatabase.QueryFilterClass();

                // 自动处理 Shapefile (不需要引号) vs FileGDB (可能需要引号)
                // 简单起见，使用通配符和更灵活的匹配
                string where = $"{realCityField} = '{cityName}' OR {realCityField} LIKE '%{shortName}%'";

                // 对于特殊字符或字段名，尝试包裹字段名（视情况而定，这里先用最通用的）
                queryFilter.WhereClause = where;

                ESRI.ArcGIS.Geodatabase.IFeatureCursor cursor = targetLayer.FeatureClass.Search(queryFilter, false);
                ESRI.ArcGIS.Geodatabase.IFeature feature = cursor.NextFeature();

                // 如果没搜到，尝试去除末尾可能的空格或特殊符号
                if (feature == null)
                {
                    queryFilter.WhereClause = $"{realCityField} LIKE '{shortName}%'";
                    cursor = targetLayer.FeatureClass.Search(queryFilter, false);
                    feature = cursor.NextFeature();
                }

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

            // [Member B] 同步过滤地图要素，实现时间维度上的动态演示
            if (_mainForm != null)
            {
                _mainForm.FilterMapByYear(year);
            }
        }

        public void UpdateChartData(int year)
        {
            if (_mainForm == null) return;

            // 1. 更新左侧图表 (城市数量统计)
            int totalCount = 0;
            foreach (var point in chart1.Series[0].Points)
            {
                string city = point.AxisLabel;
                int count = _mainForm.GetCountByCity(city, year);
                point.YValues[0] = count;
                totalCount += count;
            }
            chart1.Invalidate();

            if (chart1.Titles.Count > 0)
            {
                var title = chart1.Titles[0];
                if (totalCount > 0)
                {
                    title.Text = $"各市非遗统计 ({year}) 总数:{totalCount}";
                    title.ForeColor = System.Drawing.Color.Black;
                }
                else
                {
                    title.Text = $"[无数据] 请加载非遗数据";
                    title.ForeColor = System.Drawing.Color.Red;
                }
            }
            chart1.Invalidate();

            // 2. 更新右侧图表 (项目类别占比)
            var catStats = _mainForm.GetCategoryStats(year);
            chart2.Series[0].Points.Clear();
            if (catStats.Count > 0)
            {
                foreach (var kvp in catStats)
                {
                    chart2.Series[0].Points.AddXY(kvp.Key, kvp.Value);
                }
                if (chart2.Titles.Count > 0) chart2.Titles[0].Text = $"非遗类别占比 ({year})";
            }
            else
            {
                chart2.Series[0].Points.AddXY("无数据", 1); // Placeholder
                if (chart2.Titles.Count > 0) chart2.Titles[0].Text = "暂无类别数据";
            }
            chart2.Invalidate();
            chart2.Invalidate();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "PNG Image (*.png)|*.png|Text Report (*.txt)|*.txt";
            sfd.Title = "Export Dashboard Data";
            sfd.FileName = "Dashboard_Report_" + trackBar1.Value;

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                if (sfd.FilterIndex == 1) // PNG
                {
                    try
                    {
                        // Capture the SplitContainer (both charts)
                        int w = splitContainer1.Width;
                        int h = splitContainer1.Height;
                        Bitmap bmp = new Bitmap(w, h);
                        splitContainer1.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                        bmp.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        MessageBox.Show("Export Successful: " + sfd.FileName, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Export Failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else if (sfd.FilterIndex == 2) // TXT
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("=== Dashboard Report ===");
                        sb.AppendLine("Year: " + trackBar1.Value);
                        sb.AppendLine("Generated on: " + DateTime.Now.ToString());
                        sb.AppendLine();

                        sb.AppendLine("--- City Statistics ---");
                        // Iterate chart1 series
                        if (chart1.Series.Count > 0)
                        {
                            foreach (var dp in chart1.Series[0].Points)
                            {
                                sb.AppendLine($"{dp.AxisLabel}: {dp.YValues[0]}");
                            }
                        }
                        sb.AppendLine();

                        sb.AppendLine("--- Category Statistics ---");
                        // Iterate chart2 series
                        if (chart2.Series.Count > 0)
                        {
                            foreach (var dp in chart2.Series[0].Points)
                            {
                                sb.AppendLine($"{dp.AxisLabel}: {dp.YValues[0]}");
                            }
                        }

                        File.WriteAllText(sfd.FileName, sb.ToString());
                        MessageBox.Show("Export Successful: " + sfd.FileName, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Export Failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}

