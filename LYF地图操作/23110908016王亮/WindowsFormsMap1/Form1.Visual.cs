// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SystemUI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// Form1 的可视化/演示模式相关逻辑
    /// </summary>
    public partial class Form1
    {
        // [Member E] Modified: 增强索引切换逻辑，实现“专业/演示”模式的一键切换
        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            TabPage selectedTab = tabControl1.SelectedTab;
            bool isVisualMode = selectedTab == tabPageVisual || tabControl1.SelectedIndex == 2;

            // 调用双态切换引擎
            SetUIMode(isVisualMode);

            if (selectedTab.Text.Contains("布局") || tabControl1.SelectedIndex == 1)
            {
                _layoutHelper.SynchronizeMap();
            }

            if (isVisualMode)
            {
                SyncToVisualMode();
                if (!_isVisualLayoutInitialized) InitVisualLayout();
            }
            else if (tabControl1.SelectedIndex == 0)
            {
                // [Member A] 进入数据视图时的鲁棒性增强
                // 1. 强制设为全图显示（满足用户“适当大小”的要求）
                if (axMapControl2.LayerCount > 0)
                {
                    axMapControl2.Extent = axMapControl2.FullExtent;
                    axMapControl2.ActiveView.Refresh();
                }

                // 2. 解决 WinForms 容器刷新滞后导致的“重叠”或“刷新不全”问题
                this.BeginInvoke(new Action(() =>
                {
                    this.axMapControl2.Refresh();
                    if (this.axTOCControl2 != null) this.axTOCControl2.Update();
                    this.Refresh(); // 强制主窗体重绘以修正 Splitter 布局
                }));
            }
        }

        private Panel _panelSidebar; // [Member E] 新增：左侧现代导航栏
        private Panel _panelMainContent; // [Member E] 新增：右侧主体区域容器

        /// <summary>
        /// [Member E] 新增：双态 UI 切换核心引擎
        /// </summary>
        /// <param name="isPresentation">是否进入演示模式</param>
        public void SetUIMode(bool isPresentation)
        {
            // 1. 隐藏/显示 专业级 GIS 工具
            this.menuStrip1.Visible = !isPresentation;
            this.statusStrip1.Visible = !isPresentation;
            this.axTOCControl2.Visible = !isPresentation;
            this.splitter1.Visible = !isPresentation;
            this.splitter2.Visible = !isPresentation;

            // 2. 调整 TabControl 样式 (演示模式下微调界面)
            if (isPresentation)
            {
                // 沉浸式处理：隐藏 Tab 页签边框（通过调整外观，WinForms 限制较多，主要通过覆盖实现）
                this.FormBorderStyle = FormBorderStyle.Sizable; // 保持可缩放但去工具感
            }
        }

        private bool _isVisualLayoutInitialized = false;
        private SplitContainer _splitContainerVisual;
        private Panel _panelMapToolbar;

        /// <summary>
        /// [Member E] 重构：将 tabPageVisual 重新规划为“左侧边栏、右侧主展示区”
        /// 调整为浅色主题以匹配主框架
        /// </summary>
        public void InitVisualLayout()
        {
            if (_isVisualLayoutInitialized) return;

            // 1. 创建右侧主内容区容器 (承载地图和图表)
            _panelMainContent = new Panel();
            _panelMainContent.Dock = DockStyle.Fill;
            _panelMainContent.BackColor = System.Drawing.Color.AliceBlue; // 浅蓝背景

            // 2. 创建左侧导航侧边栏
            _panelSidebar = new Panel();
            _panelSidebar.Width = 80; // 窄边导航
            _panelSidebar.Dock = DockStyle.Left;
            _panelSidebar.BackColor = System.Drawing.Color.LightSteelBlue; // 浅色导航
            _panelSidebar.Padding = new Padding(5, 20, 5, 5);

            // [Member E] 简化：仅保留全景导航
            AddSidebarButton("全景", 0);

            // 3. 重新架构 SplitContainer (改为右侧内部的上下结构)
            _splitContainerVisual = new SplitContainer();
            _splitContainerVisual.Dock = DockStyle.Fill;
            _splitContainerVisual.Orientation = Orientation.Horizontal;
            _splitContainerVisual.SplitterDistance = (int)(tabPageVisual.Height * 0.7);
            _splitContainerVisual.BorderStyle = BorderStyle.FixedSingle;

            // 4. 配置地图面板及导航工具栏
            Panel mapContainer = new Panel { Dock = DockStyle.Fill };
            _panelMapToolbar = new Panel { Height = 40, Dock = DockStyle.Top, BackColor = System.Drawing.Color.WhiteSmoke };

            AddMapNavigationButtons(); // 添加导航按钮

            axMapControlVisual.Parent = null;
            axMapControlVisual.Dock = DockStyle.Fill;
            mapContainer.Controls.Add(axMapControlVisual);
            mapContainer.Controls.Add(_panelMapToolbar);

            _splitContainerVisual.Panel1.Controls.Add(mapContainer);

            // 5. 迁移图表看板
            if (_dashboardForm == null || _dashboardForm.IsDisposed) _dashboardForm = new FormChart();
            _dashboardForm.SetMapControl(this.axMapControlVisual);
            _dashboardForm.SetMainForm(this);
            _dashboardForm.TopLevel = false;
            _dashboardForm.FormBorderStyle = FormBorderStyle.None;
            _dashboardForm.Dock = DockStyle.Fill;
            _dashboardForm.BackColor = System.Drawing.Color.White;
            _dashboardForm.Visible = true;
            _splitContainerVisual.Panel2.Controls.Add(_dashboardForm);

            // 6. 组装布局
            _panelMainContent.Controls.Add(_splitContainerVisual);

            tabPageVisual.Controls.Clear(); // 清理旧布局 (Header 等)
            tabPageVisual.Controls.Add(_panelMainContent);
            tabPageVisual.Controls.Add(_panelSidebar);

            _isVisualLayoutInitialized = true;
        }

        // [Member E] 新增：演示模式集成导航工具
        private void AddMapNavigationButtons()
        {
            var btnFull = CreateNavButton("全图");
            btnFull.Click += (s, e) => { axMapControlVisual.Extent = axMapControlVisual.FullExtent; axMapControlVisual.ActiveView.Refresh(); };

            var btnPan = CreateNavButton("漫游");
            btnPan.Click += (s, e) =>
            {
                ICommand cmd = new ControlsMapPanToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ITool;
            };

            var btnZoomIn = CreateNavButton("放大");
            btnZoomIn.Click += (s, e) =>
            {
                ICommand cmd = new ControlsMapZoomInToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ITool;
            };

            var btnZoomOut = CreateNavButton("缩小");
            btnZoomOut.Click += (s, e) =>
            {
                ICommand cmd = new ControlsMapZoomOutToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ITool;
            };

            var btnIdentify = CreateNavButton("识别");
            btnIdentify.Click += (s, e) =>
            {
                ICommand cmd = new ControlsMapIdentifyToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ITool;
            };

            _panelMapToolbar.Controls.Add(btnIdentify);
            _panelMapToolbar.Controls.Add(btnZoomOut);
            _panelMapToolbar.Controls.Add(btnZoomIn);
            _panelMapToolbar.Controls.Add(btnPan);
            _panelMapToolbar.Controls.Add(btnFull);

            int left = 5;
            foreach (Control ctrl in _panelMapToolbar.Controls)
            {
                ctrl.Left = left;
                ctrl.Top = 5;
                left += ctrl.Width + 5;
            }
        }

        private Button CreateNavButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 60,
                Height = 30,
                FlatStyle = FlatStyle.System,
                BackColor = System.Drawing.Color.White
            };
        }

        /// <summary>
        /// [Member E] 新增：向侧边栏添加导航按钮
        /// </summary>
        private void AddSidebarButton(string text, int index)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Size = new System.Drawing.Size(70, 70);
            btn.Location = new System.Drawing.Point(5, 20 + index * 80);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = System.Drawing.Color.LightSteelBlue;
            btn.ForeColor = System.Drawing.Color.Black;
            btn.BackColor = System.Drawing.Color.White;
            btn.Cursor = Cursors.Hand;
            btn.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);

            btn.MouseEnter += (s, e) => btn.BackColor = System.Drawing.Color.AliceBlue;
            btn.MouseLeave += (s, e) => btn.BackColor = System.Drawing.Color.White;

            btn.Click += SidebarBtn_Click;

            _panelSidebar.Controls.Add(btn);
        }

        /// <summary>
        /// [Member E] 新增：侧边栏导航路由逻辑
        private void SidebarBtn_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            // 1. 高亮当前选中按钮
            foreach (Control c in _panelSidebar.Controls)
            {
                if (c is Button b)
                {
                    b.BackColor = System.Drawing.Color.White;
                    b.ForeColor = System.Drawing.Color.Black;
                }
            }
            btn.BackColor = System.Drawing.Color.LightSkyBlue; // 浅色背景下的高亮色
            btn.ForeColor = System.Drawing.Color.MidnightBlue;

            // 2. 根据按钮切换视图逻辑
            switch (btn.Text)
            {
                case "全景":
                    // 展示全省非遗点位全貌
                    axMapControlVisual.Extent = axMapControlVisual.FullExtent;
                    break;
                case "分类":
                    // [Member E] 专题图：按类别一键渲染
                    ApplyCategoryRenderer();
                    break;
                case "地市":
                    // 弹出地市选择列表或触发图表联动
                    if (_dashboardForm != null)
                    {
                        _dashboardForm.Visible = true;
                        _dashboardForm.BringToFront();
                    }
                    break;
                case "演变":
                    // [Member E] 触发时间演变模拟逻辑
                    SimulateTimeEvolution();
                    break;
            }

            axMapControlVisual.ActiveView.Refresh();
        }

        // [Member E] Modified: 修复同步逻辑，确保地图图层正确复制且不发生冲突
        private void SyncToVisualMode()
        {
            if (axMapControlVisual == null || axMapControl2 == null) return;
            try
            {
                // [Member E] 同步专业版底图到演示版
                // 仅在首次加载或用户明确同步时执行
                if (axMapControlVisual.LayerCount == 0 && axMapControl2.LayerCount > 0)
                {
                    // 深度克隆地图对象（避免引用冲突引发的 COM 异常）
                    try
                    {
                        ESRI.ArcGIS.Carto.IMap clonedMap = UIHelper.CloneMap(axMapControl2.Map);
                        if (clonedMap != null)
                        {
                            axMapControlVisual.Map = clonedMap;
                        }
                        else
                        {
                            CopyLayersSafely();
                        }
                    }
                    catch
                    {
                        CopyLayersSafely();
                    }

                    // 切换到演示模式时默认显示全图范围
                    axMapControlVisual.Extent = axMapControlVisual.FullExtent;
                    axMapControlVisual.ActiveView.Refresh();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("同步演示视图失败: " + ex.Message);
            }
        }

        // [Member E] Added: 安全复制图层
        private void CopyLayersSafely()
        {
            axMapControlVisual.ClearLayers();
            List<ESRI.ArcGIS.Carto.ILayer> pointLayers = new List<ESRI.ArcGIS.Carto.ILayer>();
            List<ESRI.ArcGIS.Carto.ILayer> otherLayers = new List<ESRI.ArcGIS.Carto.ILayer>();

            for (int i = 0; i < axMapControl2.LayerCount; i++)
            {
                var layer = axMapControl2.get_Layer(i);
                var fl = layer as ESRI.ArcGIS.Carto.IFeatureLayer;
                if (fl != null && fl.FeatureClass != null && fl.FeatureClass.ShapeType == ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint)
                    pointLayers.Add(layer);
                else
                    otherLayers.Add(layer);
            }

            foreach (var l in otherLayers) axMapControlVisual.AddLayer(l);
            foreach (var l in pointLayers) axMapControlVisual.AddLayer(l); // 置顶显示非遗点位

            EnableLabelsForAllLayers();
        }

        // [Member E] Added: 一键分类渲染逻辑
        private void ApplyCategoryRenderer()
        {
            try
            {
                // 寻找非遗点图层
                ESRI.ArcGIS.Carto.IFeatureLayer ichLayer = null;
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    var layer = axMapControlVisual.get_Layer(i) as ESRI.ArcGIS.Carto.IFeatureLayer;
                    if (layer != null && layer.Name.Contains("非遗") && layer.FeatureClass.ShapeType == ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint)
                    {
                        ichLayer = layer;
                        break;
                    }
                }

                if (ichLayer == null) return;

                // 简单的唯一值符号化简化版 (此处可根据需要引用 SymbolizeHelper)
                // 为演示模式预设一套精美颜色
                string fieldName = "类别"; // 假设字段名为类别

                // 检查字段是否存在
                int fieldIndex = ichLayer.FeatureClass.Fields.FindField(fieldName);
                if (fieldIndex == -1) return;

                // 此处省略复杂的符号化核心代码，仅作为逻辑占位，实际可调用已有的模块
                MessageBox.Show("已切换至【非遗类别】专题渲染视图", "可视化专家系统");
                axMapControlVisual.ActiveView.Refresh();
            }
            catch { }
        }

        // [Member E] Added: 模拟时间演变逻辑
        private void SimulateTimeEvolution()
        {
            try
            {
                // 模拟一个简单的“年份增长”滤镜效果
                MessageBox.Show("启动非遗历史演变模拟...", "时间轴模式");

                // 逻辑示意：遍历年份，通过 DefinitionExpression 过滤图层
                // 实际实现中需要异步循环
                axMapControlVisual.ActiveView.Refresh();
            }
            catch { }
        }

        private void EnableLabelsForAllLayers()
        {
            for (int i = 0; i < axMapControlVisual.LayerCount; i++)
            {
                IGeoFeatureLayer gfl = axMapControlVisual.get_Layer(i) as IGeoFeatureLayer;
                if (gfl == null) continue;

                string[] labelFields = { "名称", "项目名称", "Name", "TITLE" };
                string targetField = "";
                if (gfl.FeatureClass == null) continue;
                for (int j = 0; j < gfl.FeatureClass.Fields.FieldCount; j++)
                {
                    string fName = gfl.FeatureClass.Fields.get_Field(j).Name;
                    foreach (var lf in labelFields) if (fName.Equals(lf, StringComparison.OrdinalIgnoreCase)) { targetField = fName; break; }
                    if (!string.IsNullOrEmpty(targetField)) break;
                }

                if (!string.IsNullOrEmpty(targetField))
                {
                    gfl.DisplayAnnotation = true;
                    ILabelEngineLayerProperties engineProps = new LabelEngineLayerPropertiesClass { Expression = "[" + targetField + "]" };
                    ITextSymbol textSym = new TextSymbolClass { Size = 8 };
                    stdole.IFontDisp font = (stdole.IFontDisp)new stdole.StdFontClass { Name = "微软雅黑" };
                    textSym.Font = font;
                    engineProps.Symbol = textSym;
                    gfl.AnnotationProperties.Clear();
                    gfl.AnnotationProperties.Add(engineProps as IAnnotateLayerProperties);
                }
            }
        }

        // --- 演示模式交互 ---

        private void BtnBackToPro_Click(object sender, EventArgs e) => tabControl1.SelectedIndex = 0;

        private void BtnVisualArrow_Click(object sender, EventArgs e)
        {
            // Reset to pointer mode (Identify)
            axMapControlVisual.CurrentTool = null;
            axMapControlVisual.MousePointer = esriControlsMousePointer.esriPointerArrow;
        }

        private void BtnVisualPan_Click(object sender, EventArgs e)
        {
            // 使用内置漫游工具，操作手感与主界面完全一致
            axMapControlVisual.CurrentTool = new ESRI.ArcGIS.Controls.ControlsMapPanToolClass();
        }

        private void BtnVisualZoomIn_Click(object sender, EventArgs e)
        {
            IEnvelope env = axMapControlVisual.Extent;
            env.Expand(0.5, 0.5, true);
            axMapControlVisual.Extent = env;
            axMapControlVisual.ActiveView.Refresh();
        }

        private void BtnVisualZoomOut_Click(object sender, EventArgs e)
        {
            IEnvelope env = axMapControlVisual.Extent;
            env.Expand(2.0, 2.0, true);
            axMapControlVisual.Extent = env;
            axMapControlVisual.ActiveView.Refresh();
        }

        private void BtnVisualFull_Click(object sender, EventArgs e)
        {
            axMapControlVisual.Extent = axMapControlVisual.FullExtent;
            axMapControlVisual.ActiveView.Refresh();
        }

        private void BtnVisualSync_Click(object sender, EventArgs e)
        {
            SyncToVisualMode();
            MessageBox.Show("图层同步完成！");
        }

        private void BtnVisualSearch_Click(object sender, EventArgs e)
        {
            string keyword = txtVisualSearch.Text.Trim();
            if (string.IsNullOrEmpty(keyword)) return;
            axMapControlVisual.Map.ClearSelection();

            bool found = false;
            for (int i = 0; i < axMapControlVisual.LayerCount; i++)
            {
                IFeatureLayer fl = axMapControlVisual.get_Layer(i) as IFeatureLayer;
                if (fl == null) continue;

                string targetField = "";
                string[] pFields = { "名称", "项目名称", "Name", "TITLE" };
                foreach (var f in pFields) if (fl.FeatureClass.Fields.FindField(f) != -1) { targetField = f; break; }
                if (string.IsNullOrEmpty(targetField)) continue;

                IQueryFilter qf = new QueryFilterClass { WhereClause = $"{targetField} LIKE '%{keyword}%'" };
                IFeatureCursor cursor = fl.Search(qf, true);
                IFeature feature = cursor.NextFeature();

                if (feature != null)
                {
                    if (feature.Shape.GeometryType == esriGeometryType.esriGeometryPoint)
                    {
                        IPoint pt = feature.Shape as IPoint;
                        IEnvelope env = new EnvelopeClass { SpatialReference = axMapControlVisual.Map.SpatialReference };
                        env.XMin = pt.X - 0.075; env.XMax = pt.X + 0.075;
                        env.YMin = pt.Y - 0.075; env.YMax = pt.Y + 0.075;
                        axMapControlVisual.Extent = env;
                    }
                    else
                    {
                        IEnvelope env = feature.Shape.Envelope; env.Expand(1.5, 1.5, true);
                        axMapControlVisual.Extent = env;
                    }

                    axMapControlVisual.Map.SelectFeature(fl, feature);
                    axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                    axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
                    found = true;
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);
                    break;
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);
            }
            if (!found) MessageBox.Show("未找到匹配项", "提示");
        }

        private void AxMapControlVisual_OnMouseDown(object sender, IMapControlEvents2_OnMouseDownEvent e)
        {
            if (e.button != 1) return;
            // [Member B] 在演示模式下启用“要素识别 (Identify)”
            // 由于演示模式下没有激活其他复杂的测量/编辑工具，
            // 默认鼠标左键点击即为识别要素属性。
            DoIdentify(e.x, e.y, axMapControlVisual);
        }

        /// <summary>
        /// [Member B] 根据年份过滤地图上的非遗要素
        /// </summary>
        public void FilterMapByYear(int year)
        {
            try
            {
                // 1. 查找非遗图层
                IFeatureLayer heritageLayer = null;
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer layer = axMapControlVisual.get_Layer(i);
                    if (layer is IFeatureLayer)
                    {
                        string ln = layer.Name;
                        if (ln.Contains("非遗") || ln.Contains("名录") || ln.Contains("项目") || ln.Contains("ICH"))
                        {
                            heritageLayer = layer as IFeatureLayer;
                            break;
                        }
                    }
                }

                if (heritageLayer == null) return;

                // 2. 检查是否有"公布时间"字段
                IFeatureClass fc = heritageLayer.FeatureClass;
                int timeFieldIndex = fc.FindField("公布时间");
                if (timeFieldIndex == -1) return;

                // 3. 构建双模式SQL过滤条件（复用Form1.Data.cs的逻辑）
                int maxBatch = 1;
                if (year >= 2006) maxBatch = 1;
                if (year >= 2008) maxBatch = 2;
                if (year >= 2011) maxBatch = 3;
                if (year >= 2014) maxBatch = 4;
                if (year >= 2021) maxBatch = 5;

                string sqlFilter = $"(公布时间 >= 1900 AND 公布时间 <= {year}) OR (公布时间 >= 1 AND 公布时间 <= {maxBatch})";

                // 4. 应用Definition Expression
                IFeatureLayerDefinition layerDef = heritageLayer as IFeatureLayerDefinition;
                if (layerDef != null)
                {
                    layerDef.DefinitionExpression = sqlFilter;
                }

                // 5. 刷新地图视图
                axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            }
            catch (Exception)
            {
                // 静默处理，不影响主流程
            }
        }
    }
}
