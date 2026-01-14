// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using System;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO;
using System.Text;
using System.Drawing;
using ESRI.ArcGIS.Carto; // Added for IFeatureLayer
using ESRI.ArcGIS.Geodatabase; // Added for IQueryFilter, IFeatureCursor, IFeature
using ESRI.ArcGIS.Geometry; // Added for esriGeometryType, IEnvelope
using ESRI.ArcGIS.Display; // Added for esriViewDrawPhase
using System.Runtime.InteropServices; // Added for Marshal

namespace WindowsFormsMap1
{

    /// <summary>
    /// 【数据统计看板窗口】：集成双图表联动展示、时间轴平滑滚动以及地理联动定位
    /// 实现非遗项目的多维度、跨时空数据可视化分析
    /// </summary>
    public partial class FormChart : Form
    {
        private ESRI.ArcGIS.Controls.AxMapControl _mapControl;
        private Form1 _mainForm; // [Member B] 引用主窗体：用于跨窗口数据交换与业务协同
        private string _selectedCity; // 当前选中的城市名称

        public FormChart()
        {
            InitializeComponent();
            InitMyChart();
            ApplyLightTheme();
        }

        private void ApplyLightTheme()
        {
            ThemeEngine.ApplyTheme(this);
            this.BackColor = Color.White;

            if (chart1 != null)
            {
                chart1.BackColor = Color.White;
                foreach (var area in chart1.ChartAreas)
                {
                    area.BackColor = Color.FromArgb(252, 254, 255);
                    area.AxisX.LabelStyle.ForeColor = ThemeEngine.ColorText;
                    area.AxisY.LabelStyle.ForeColor = ThemeEngine.ColorText;
                    area.AxisX.LineColor = Color.FromArgb(226, 232, 240);
                    area.AxisY.LineColor = Color.FromArgb(226, 232, 240);
                }
                foreach (var title in chart1.Titles)
                {
                    title.ForeColor = ThemeEngine.ColorPrimary;
                    title.Font = new Font(ThemeEngine.FontDefault, 10F, FontStyle.Bold);
                }
            }

            if (chart2 != null)
            {
                chart2.BackColor = Color.White;
                foreach (var title in chart2.Titles)
                {
                    title.ForeColor = ThemeEngine.ColorPrimary;
                    title.Font = new Font(ThemeEngine.FontDefault, 10F, FontStyle.Bold);
                }
            }

            // [Agent Add] 样式化控件
            if (cmbChartType != null)
            {
                cmbChartType.FlatStyle = FlatStyle.Flat;
                cmbChartType.Font = new Font(ThemeEngine.FontDefault, 9F);
            }
            if (btnExport != null)
            {
                ThemeEngine.ApplyButtonTheme(btnExport, false);
            }
            if (lblTime != null)
            {
                lblTime.ForeColor = ThemeEngine.ColorPrimary;
                lblTime.Font = new Font(ThemeEngine.FontDefault, 9F, FontStyle.Bold);
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

        // 【切换展现形式】：动态更改 Series 的绘图类型 (柱/线/饼)
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

        // 【初始化图表引擎】：配置 X 轴 16 地市标签、饼图 ToolTip 以及配色方案
        private void InitMyChart()
        {
            // ========== 图表 1 (左侧): 各市项目数量分布 (支持下钻定位) ==========
            this.chart1.Series.Clear();
            Series series1 = new Series("非遗数量");
            series1.ChartType = SeriesChartType.Column;
            series1.ToolTip = "交互提示：点击柱条立即定位到 #VALX 空间位置";
 
            // 预设地市集合：确保统计口径与行政区划图层完全对齐
            string[] cities = new string[] {
                "济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市", "济宁市",
                "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市", "滨州市", "菏泽市"
            };
 
            foreach (var city in cities) series1.Points.AddXY(city, 0);
 
            this.chart1.Series.Add(series1);
            this.chart1.Titles.Clear();
            this.chart1.Titles.Add("山东省各市非遗数量分布 (交互式定位)");
            this.chart1.ChartAreas[0].AxisX.Interval = 1;
            this.chart1.MouseClick += Chart1_MouseClick;
 
            // ========== 图表 2 (右侧): 项目类别宏观占比 (饼图) ==========
            this.chart2.Series.Clear();
            Series series2 = new Series("类别分布");
            series2.ChartType = SeriesChartType.Pie;
            series2.IsValueShownAsLabel = false; 
            series2.ToolTip = "#VALX: #VAL 项 (#PERCENT)";
            series2.LegendText = "#VALX: #VAL"; 
 
            this.chart2.Series.Add(series2);
            this.chart2.Titles.Clear();
            this.chart2.Titles.Add("全省非遗项目类别占比 (宏观构成)");
        }

        /// <summary>
        /// 【图文映射响应】：当用户点击柱状图标时，自动触发地理围栏命中测试 (HitTest)
        /// 逻辑：获取点击点对应的数据项 -> 提取城市名 -> 驱动主地图组件进行空间定位
        /// </summary>
        private void Chart1_MouseClick(object sender, MouseEventArgs e)
        {
            HitTestResult result = chart1.HitTest(e.X, e.Y);
            if (result.ChartElementType == ChartElementType.DataPoint)
            {
                var point = chart1.Series[0].Points[result.PointIndex];
                string cityName = point.AxisLabel;
                _selectedCity = cityName;
 
                this.Text = $"数据看板 - 当前锁定: {cityName}";
                this.ZoomToCity(cityName); // 执行空间下钻
            }
        }

        /// <summary>
        /// 【空间定位核心引擎】：在地图容器中多层级搜索目标城市几何实体
        /// 策略逻辑：
        /// 1. 优先在大比例尺边界层进行模糊文字匹配
        /// 2. 自动处理 SHP/GDB 数据库的字段转义冲突
        /// 3. 执行要素高亮与 Flash 视觉回馈
        /// </summary>
        private void ZoomToCity(string cityName)
        {
            try
            {
                if (_mapControl.LayerCount == 0 || string.IsNullOrEmpty(cityName)) return;
                string shortName = cityName.Replace("市", "");
 
                IFeatureLayer targetLayer = null;
                string realCityField = "";
                IFeatureLayer fallbackPointLayer = null;
                string fallbackPointField = "";
 
                // 多义性关键字搜索
                string[] cityKeys = { "行政", "市", "City", "Name", "地市", "District", "County", "NAME99" };
 
                for (int i = 0; i < _mapControl.LayerCount; i++)
                {
                    var layer = _mapControl.get_Layer(i) as IFeatureLayer;
                    if (layer == null || layer.FeatureClass == null) continue;
 
                    string ln = layer.Name.ToLower();
                    bool isPolygon = (layer.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolygon);
                    bool isLabelLayer = ln.Contains("名") || ln.Contains("label") || ln.Contains("点");
 
                    string tempCityField = "";
                    for (int j = 0; j < layer.FeatureClass.Fields.FieldCount; j++)
                    {
                        string fName = layer.FeatureClass.Fields.get_Field(j).Name;
                        foreach (string k in cityKeys)
                        {
                            if (fName.ToUpper().Contains(k.ToUpper())) { tempCityField = fName; break; }
                        }
                        if (!string.IsNullOrEmpty(tempCityField)) break;
                    }
 
                    if (string.IsNullOrEmpty(tempCityField)) continue;
 
                    if (isPolygon)
                    {
                        // 逻辑：优先选择“市区行政边界”这类能够提供完整面的图层进行定位
                        if ((ln.Contains("shiqu") || ln.Contains("市区") || ln.Contains("行政")) && !isLabelLayer)
                        {
                            targetLayer = layer; realCityField = tempCityField; break; 
                        }
                        if (targetLayer == null) { targetLayer = layer; realCityField = tempCityField; }
                    }
                    else if (fallbackPointLayer == null) { fallbackPointLayer = layer; fallbackPointField = tempCityField; }
                }
 
                if (targetLayer == null) { targetLayer = fallbackPointLayer; realCityField = fallbackPointField; }
                if (targetLayer == null || string.IsNullOrEmpty(realCityField)) return;
 
                // 执行 SQL 检索：构造健壮的模糊匹配 Where 子句
                IQueryFilter queryFilter = new QueryFilterClass();
                queryFilter.WhereClause = $"{realCityField} = '{cityName}' OR {realCityField} LIKE '%{shortName}%'";
 
                IFeatureCursor cursor = targetLayer.FeatureClass.Search(queryFilter, false);
                IFeature feature = cursor.NextFeature();
 
                // 如果没搜到，尝试去除末尾可能的空格或特殊符号
                if (feature == null)
                {
                    queryFilter.WhereClause = $"{realCityField} LIKE '{shortName}%'";
                    cursor = targetLayer.FeatureClass.Search(queryFilter, false);
                    feature = cursor.NextFeature();
                }

                if (feature != null)
                {
                    IEnvelope envelope = feature.Shape.Envelope;
                    // 自适应缩放比例：点放大 0.18 度，面放大 1.5 倍视图范围
                    if (feature.Shape.GeometryType == esriGeometryType.esriGeometryPoint) envelope.Expand(0.18, 0.18, false);
                    else envelope.Expand(1.5, 1.5, true);
 
                    _mapControl.ActiveView.Extent = envelope;
                    _mapControl.ActiveView.Refresh();
 
                    // 业务逻辑关联：同步更新图表 UI 状态
                    if (chart1.Titles.Count > 0)
                    {
                        chart1.Titles[0].Text = $"已联动定位到：{cityName}";
                        chart1.Titles[0].ForeColor = Color.Green;
                    }
 
                    _mapControl.Map.ClearSelection();
                    _mapControl.Map.SelectFeature(targetLayer, feature);
                    _mapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
                    _mapControl.FlashShape(feature.Shape, 2, 200, null); // 视觉高亮闪烁
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
                if (cursor != null) Marshal.ReleaseComObject(cursor);
            }
            catch { /* 定位引擎容错 */ }
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
                // [优化] 优先取 AxisLabel，若为空则取 XValue (针对不同 AddXY 重载)
                string city = !string.IsNullOrEmpty(point.AxisLabel) ? point.AxisLabel : point.XValue.ToString();

                // 再次清洗城市名，确保匹配成功
                int count = _mainForm.GetCountByCity(city, year);
                point.YValues[0] = count;
                totalCount += count;
            }
            chart1.Series[0].IsValueShownAsLabel = true; // 显示数值标签
            chart1.Invalidate();
            chart1.Update(); // 强制重绘

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

        // 【一键导出看板结果】：支持高清 PNG 截图及结构化文档 (.txt) 导出
        private void btnExport_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "高清图片报告 (*.png)|*.png|结构化统计明细 (*.txt)|*.txt";
            sfd.Title = "数据看板导出助手";
            sfd.FileName = $"非遗统计看板_{trackBar1.Value}_年度报表";
 
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                if (sfd.FilterIndex == 1) // 导出 PNG
                {
                    try
                    {
                        // 逻辑：直接对 SplitContainer 容器进行位图映射，实现所见即所得的完整图表备份
                        int w = splitContainer1.Width, h = splitContainer1.Height;
                        Bitmap bmp = new Bitmap(w, h);
                        splitContainer1.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                        bmp.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        MessageBox.Show("可视化报表已导出至：" + sfd.FileName);
                    }
                    catch (Exception ex) { MessageBox.Show("导出引擎故障：" + ex.Message); }
                }
                else if (sfd.FilterIndex == 2) // 导出 TXT 审计报告
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("╔═══════════════════════════════════════════════════════╗");
                        sb.AppendLine($"║           非遗系统数据审计报告 (年度：{trackBar1.Value})           ║");
                        sb.AppendLine($"╚═══════════════════════════════════════════════════════╝");
                        sb.AppendLine($"生成时间：{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
                        sb.AppendLine("---------------------------------------------------------");
                        sb.AppendLine("一、各市分布量化清单：");
                        if (chart1.Series.Count > 0)
                        {
                            foreach (var dp in chart1.Series[0].Points) sb.AppendLine($"  ▶ {dp.AxisLabel,-10}：{dp.YValues[0],4} 项");
                        }
                        sb.AppendLine("\n二、类别权重明细：");
                        if (chart2.Series.Count > 0)
                        {
                            foreach (var dp in chart2.Series[0].Points) sb.AppendLine($"  ▶ {dp.AxisLabel,-10}：{dp.YValues[0],4} 项");
                        }
 
                        File.WriteAllText(sfd.FileName, sb.ToString());
                        MessageBox.Show("结构化审计报表已导出成功！");
                    }
                    catch (Exception ex) { MessageBox.Show("文案引擎故障：" + ex.Message); }
                }
            }
        }
    }
}
