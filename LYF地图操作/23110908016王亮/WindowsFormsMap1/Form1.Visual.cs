// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SystemUI;
using System;
using System.Collections.Generic;
using System.Drawing;
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
            // [修复] 统一判定逻辑：索引 2 为演示模式
            bool isVisualMode = tabControl1.SelectedIndex == 2;

            // 调用双态切换引擎
            SetUIMode(isVisualMode);

            if (tabControl1.SelectedIndex == 1) // 布局视图
            {
                _layoutHelper.SynchronizeMap();
            }

            if (isVisualMode)
            {
                // [优化] 确保在初始化布局前完成同步
                SyncToVisualMode();
                if (!_isVisualLayoutInitialized) InitVisualLayout();
            }
            else if (tabControl1.SelectedIndex == 0) // 数据视图
            {
                // 解决刷新延迟并自动全图
                this.BeginInvoke(new Action(() =>
                {
                    if (this.axMapControl2.LayerCount > 0)
                    {
                        this.axMapControl2.Extent = this.axMapControl2.FullExtent;
                    }
                    this.axMapControl2.Refresh();
                    this.axTOCControl2?.Update();
                    this.Refresh();
                }));
            }
        }

        private Panel _panelSidebar; // [Member E] 新增：左侧现代导航栏
        private Panel _panelMainContent; // [Member E] 新增：右侧主体区域容器
        private Label _lblYear;       // [New] 年份显示

        // [Member B] 共享状态
        // [Member B] Added: 绑定地图事件监听器
        // 注意：_dashboardYear 已在主分部类 Form1.cs 中定义

        public void SetUIMode(bool isPresentation)
        {
            // 1. 显隐专业工具
            this.menuStrip1.Visible = !isPresentation;
            this.statusStrip1.Visible = !isPresentation;
            this.axTOCControl2.Visible = !isPresentation;
            this.splitter1.Visible = !isPresentation;
            this.splitter2.Visible = !isPresentation;

            // 2. 鹰眼同步：专业模式下由 Load/ExtentUpdated 驱动，演示模式下需手动唤醒
            if (isPresentation)
            {
                // 演示模式下强制把鹰眼面板置顶
                if (_panelEagleVisual != null) _panelEagleVisual.BringToFront();
            }
        }

        private bool _isVisualLayoutInitialized = false;
        private SplitContainer _splitContainerVisual;
        private Panel _panelMapToolbar;

        public void InitVisualLayout()
        {
            if (_isVisualLayoutInitialized) return;

            // 1. 结构化容器
            _panelMainContent = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.AliceBlue };
            _panelSidebar = new Panel { Width = 80, Dock = DockStyle.Left, BackColor = Color.White, Padding = new Padding(5, 20, 5, 5) };
            AddSidebarButton("全景", 0);
            AddSidebarButton("地市", 1);
            AddSidebarButton("分类", 2);
            AddSidebarButton("热力图", 3);
            AddSidebarButton("返回", 4); // 紧跟在后面

            _splitContainerVisual = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BorderStyle = BorderStyle.FixedSingle };
            _splitContainerVisual.SplitterDistance = (int)(tabPageVisual.Height * 0.7);

            // 2. 地图内容区
            Panel mapContainer = new Panel { Dock = DockStyle.Fill };
            _panelMapToolbar = new Panel { Height = 45, Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(5) };
            AddMapNavigationButtons();

            axMapControlVisual.Parent = null;
            axMapControlVisual.Dock = DockStyle.Fill;

            // [Fix] Add toolbar first to ensure it takes the Top dock space correctly, 
            // then map fills the rest. Or explicitly use BringToFront.
            mapContainer.Controls.Add(_panelMapToolbar);
            mapContainer.Controls.Add(axMapControlVisual);
            _panelMapToolbar.BringToFront(); // Double insurance

            _splitContainerVisual.Panel1.Controls.Add(mapContainer);

            // 3. 看板集成
            if (_dashboardForm == null || _dashboardForm.IsDisposed)
            {
                _dashboardForm = new FormChart();
                _dashboardForm.SetMainForm(this);
            }
            // [Fix] 每次初始化布局都重新绑定当前地图控件（处理演示/专业模式切换）
            _dashboardForm.SetMapControl(axMapControlVisual);

            _dashboardForm.TopLevel = false;
            _dashboardForm.FormBorderStyle = FormBorderStyle.None;
            _dashboardForm.Dock = DockStyle.Fill;
            _dashboardForm.Visible = true;
            _splitContainerVisual.Panel2.Controls.Add(_dashboardForm);

            // 4. 安全组装 (修复：Controls.Clear() 会删掉 EagleEye)
            _panelMainContent.Controls.Add(_splitContainerVisual);

            // [Member B] Added: 绑定地图事件监听器，实现全自动图表联动
            axMapControlVisual.OnMapReplaced += (s, ev) =>
            {
                if (_dashboardForm != null && !_dashboardForm.IsDisposed)
                    _dashboardForm.UpdateChartData(_dashboardYear); // 定义一个字段记录当前年份
            };

            // 备份鹰眼面板
            Control eagleBackup = null;
            if (_panelEagleVisual != null && tabPageVisual.Controls.Contains(_panelEagleVisual))
                eagleBackup = _panelEagleVisual;

            tabPageVisual.Controls.Clear();
            tabPageVisual.Controls.Add(_panelMainContent);
            tabPageVisual.Controls.Add(_panelSidebar);

            // 还原鹰眼并置顶
            if (eagleBackup != null)
            {
                tabPageVisual.Controls.Add(eagleBackup);
                eagleBackup.BringToFront();
            }

            _isVisualLayoutInitialized = true;
        }

        // [Member E] 新增：演示模式集成导航工具
        private void AddMapNavigationButtons()
        {
            // 1. 指针 (恢复默认状态)
            var btnPointer = CreateNavIconBtn("Pointer", "选择/指针");
            btnPointer.Click += (s, e) =>
            {
                axMapControlVisual.CurrentTool = null;
                axMapControlVisual.MousePointer = esriControlsMousePointer.esriPointerArrow;
            };

            // 2. 识别 (自定义识别)
            var btnIdentify = CreateNavIconBtn("Identify", "属性识别");
            btnIdentify.Click += (s, e) =>
            {
                axMapControlVisual.CurrentTool = null;
                axMapControlVisual.MousePointer = esriControlsMousePointer.esriPointerIdentify;
            };

            // 2.1 [Agent Add] 联网搜索
            var btnWebSearch = CreateNavIconBtn("Web", "互联网搜索");
            btnWebSearch.Click += (s, e) =>
            {
                axMapControlVisual.CurrentTool = null;
                axMapControlVisual.MousePointer = esriControlsMousePointer.esriPointerCrosshair;
            };

            // 3. 漫游
            var btnPan = CreateNavIconBtn("Pan", "漫游");
            btnPan.Click += (s, e) =>
            {
                ICommand cmd = new ControlsMapPanToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ITool;
            };

            // 4. 放大
            var btnZoomIn = CreateNavIconBtn("ZoomIn", "放大");
            btnZoomIn.Click += (s, e) =>
            {
                ICommand cmd = new ControlsMapZoomInToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ITool;
            };

            // 5. 缩小
            var btnZoomOut = CreateNavIconBtn("ZoomOut", "缩小");
            btnZoomOut.Click += (s, e) =>
            {
                ICommand cmd = new ControlsMapZoomOutToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ITool;
            };

            // 6. 全图
            var btnFull = CreateNavIconBtn("Full", "全图显示");
            btnFull.Click += (s, e) => { axMapControlVisual.Extent = axMapControlVisual.FullExtent; axMapControlVisual.ActiveView.Refresh(); };

            // 7. 清除 (清除高亮选择)
            var btnClear = CreateNavIconBtn("Clear", "清除选择");
            btnClear.Click += (s, e) =>
            {
                axMapControlVisual.Map.ClearSelection();
                axMapControlVisual.ActiveView.GraphicsContainer.DeleteAllElements();
                axMapControlVisual.ActiveView.Refresh();
            };

            // 按顺序添加到工具栏
            _panelMapToolbar.Controls.Add(btnPointer);
            _panelMapToolbar.Controls.Add(btnIdentify);
            _panelMapToolbar.Controls.Add(btnWebSearch);
            _panelMapToolbar.Controls.Add(btnPan);
            _panelMapToolbar.Controls.Add(btnZoomIn);
            _panelMapToolbar.Controls.Add(btnZoomOut);
            _panelMapToolbar.Controls.Add(btnFull);
            _panelMapToolbar.Controls.Add(btnClear);

            int left = 5;
            foreach (Control ctrl in _panelMapToolbar.Controls)
            {
                ctrl.Left = left;
                ctrl.Top = 5;
                left += ctrl.Width + 5;
            }
        }

        private Button CreateNavIconBtn(string iconType, string toolTip)
        {
            Button btn = new Button
            {
                Size = new Size(40, 40), // 增大点击区域
                FlatStyle = FlatStyle.Flat,
                Image = ThemeEngine.GetIcon(iconType, Color.FromArgb(71, 85, 105)), // 使用中性色图标
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ThemeEngine.ColorSecondary;

            ToolTip tt = new ToolTip();
            tt.SetToolTip(btn, toolTip);

            // 悬停时图标变深
            btn.MouseEnter += (s, e) => { btn.Image = ThemeEngine.GetIcon(iconType, ThemeEngine.ColorPrimary); };
            btn.MouseLeave += (s, e) => { btn.Image = ThemeEngine.GetIcon(iconType, Color.FromArgb(71, 85, 105)); };

            return btn;
        }

        /// <summary>
        /// [Member E] 新增：向侧边栏添加导航按钮
        /// </summary>
        private void AddSidebarButton(string text, int index)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Size = new Size(70, 70); // 紧凑方块
            btn.Location = new System.Drawing.Point(5, 20 + index * 75); // 紧凑间距 (5px 缝隙)
            btn.TextAlign = ContentAlignment.BottomCenter;
            btn.TextImageRelation = TextImageRelation.ImageAboveText;
            btn.Padding = new Padding(0, 0, 0, 4); // 仅留底部微量余白
            btn.ImageAlign = ContentAlignment.TopCenter;

            // 为侧边栏按钮分配图标
            string iconType = "Full";
            if (text == "热力图") iconType = "Heatmap";
            else if (text == "分类") iconType = "Mapping";
            else if (text == "地市") iconType = "Query";
            else if (text == "返回") iconType = "Back";
            else if (text == "全景") iconType = "Full";

            btn.Image = ThemeEngine.GetIcon(iconType, Color.Black);

            ThemeEngine.ApplyButtonTheme(btn, true);
            btn.BackColor = Color.White;
            btn.ForeColor = ThemeEngine.ColorText;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);

            btn.MouseEnter += (s, e) => { btn.BackColor = ThemeEngine.ColorSecondary; btn.ForeColor = ThemeEngine.ColorPrimary; btn.Image = ThemeEngine.GetIcon(iconType, ThemeEngine.ColorPrimary); };
            btn.MouseLeave += (s, e) => { btn.BackColor = Color.White; btn.ForeColor = ThemeEngine.ColorText; btn.Image = ThemeEngine.GetIcon(iconType, Color.Black); };

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
                case "热力图":
                    // [New] 进入时空热力图模式
                    EnterHeatmapMode();
                    break;
                case "返回":
                    tabControl1.SelectedIndex = 0;
                    break;
            }

            // 处理时间轴与热力图状态归一化
            if (btn.Text == "热力图")
            {
                _isHeatmapMode = true;
                EnterHeatmapMode();
            }
            else if (btn.Text != "返回")
            {
                _isHeatmapMode = false;
                if (btn.Text == "全景") ResetRenderer();
            }

            // [Fix] 切换后通过统一年份同步一次地图
            FilterMapByYear(_dashboardYear);

            axMapControlVisual.ActiveView.Refresh();
        }

        // [Member E] Modified: 修复同步逻辑，确保地图图层正确复制且不发生冲突
        private void SyncToVisualMode(bool force = false)
        {
            if (axMapControlVisual == null || axMapControl2 == null) return;
            try
            {
                // [Member E] 同步专业版底图到演示版
                // 仅在首次加载或用户明确同步时执行，或者强制同步时执行
                if (force || (axMapControlVisual.LayerCount == 0 && axMapControl2.LayerCount > 0))
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
                axMapControlVisual.ActiveView.Refresh();
            }
            catch { }
        }

        // --- [New] 时空热力图相关逻辑 ---

        private void EnterHeatmapMode()
        {
            // 1. 切换图层渲染为“热力图”
            ApplyHeatmapRenderer();

            // 2. 触发初始渲染 (使用统一年份)
            RenderCityChoropleth(_dashboardYear);
        }

        private void ResetRenderer()
        {
            // 恢复普通点展示 (通过 FilterMapByYear 解除过滤并重置 Renderer 也可以，或者简单全图刷新)
            // 先重置数据过滤，显示全部数据
            FilterMapByYear(2099);

            // 强制重新同步图层，覆盖热力图的临时符号
            SyncToVisualMode(true);
        }

        private void ApplyHeatmapRenderer()
        {
            try
            {
                // 查找非遗图层
                IFeatureLayer targetLayer = null;
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer l = axMapControlVisual.get_Layer(i);
                    if (l.Name.Contains("非遗") || l.Name.Contains("项目"))
                    {
                        targetLayer = l as IFeatureLayer;
                        break;
                    }
                }
                if (targetLayer == null) return;

                // [Fallback] 由于环境缺少 IHeatmapRenderer，回退到普通点符号渲染
                // 使用红色圆点模拟
                ISimpleMarkerSymbol pMarkerSymbol = new SimpleMarkerSymbolClass();
                pMarkerSymbol.Style = esriSimpleMarkerStyle.esriSMSCircle;
                pMarkerSymbol.Color = new RgbColorClass { Red = 255, Green = 0, Blue = 0 };
                pMarkerSymbol.Size = 8;
                // 注意：ArcEngine 默认 SimpleSymbol 不支持从 API 直接设置透明度(需要 ITransparencyRenderer)，此处仅设置颜色

                ISimpleRenderer pSimpleRenderer = new SimpleRendererClass();
                pSimpleRenderer.Symbol = pMarkerSymbol as ISymbol;

                IGeoFeatureLayer geoLayer = targetLayer as IGeoFeatureLayer;
                if (geoLayer != null)
                {
                    geoLayer.Renderer = pSimpleRenderer as IFeatureRenderer;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("渲染设置失败: " + ex.Message);
            }

            axMapControlVisual.ActiveView.Refresh();
        }

        private void UpdateHeatmapByYear(int year)
        {
            // [Removed] 冗余逻辑，现由 FilterMapByYear 统一驱动
        }

        /// <summary>
        /// [Member E] 实现基于行政区划的“分级色彩热力图”
        /// 逻辑：遍历16地市，查询当前年份的非遗数量，动态赋予颜色
        /// </summary>
        private void RenderCityChoropleth(int year)
        {
            try
            {
                // 1. 寻找地市面图层
                IFeatureLayer cityLayer = null;
                string nameField = "";

                // 智能查找图层 (增强对 Pinyin 'shiqu' 的支持)
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer l = axMapControlVisual.get_Layer(i);
                    if (l is IFeatureLayer && l.Visible && (l as IFeatureLayer).FeatureClass.ShapeType == esriGeometryType.esriGeometryPolygon)
                    {
                        string ln = l.Name.ToLower();
                        // 匹配 shiqu, boundary, 市, 行政
                        if (ln.Contains("shiqu") || ln.Contains("地市") || ln.Contains("行政") || ln.Contains("市") || ln.Contains("区划"))
                        {
                            // 排除明显不是的图层 (如 purely label)
                            if (ln.Contains("ming") || ln.Contains("label") || ln.Contains("anno")) continue;

                            cityLayer = l as IFeatureLayer;
                            // 尝试自动识别名称字段
                            IFields fields = cityLayer.FeatureClass.Fields;
                            string[] pFields = { "NAME", "Name", "名称", "市", "CITY_NAME", "City", "DISHI", "DiShi" };
                            foreach (var f in pFields)
                            {
                                if (fields.FindField(f) != -1) { nameField = f; break; }
                            }

                            // 如果没找到标准字段，尝试找第一个字符串字段
                            if (string.IsNullOrEmpty(nameField))
                            {
                                for (int j = 0; j < fields.FieldCount; j++)
                                {
                                    if (fields.get_Field(j).Type == esriFieldType.esriFieldTypeString && fields.get_Field(j).Length > 1)
                                    {
                                        nameField = fields.get_Field(j).Name;
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }

                if (cityLayer == null || string.IsNullOrEmpty(nameField))
                {
                    // Console.WriteLine("未找到地市图层或名称字段");
                    return;
                }

                // 2. 准备唯一值渲染器
                IUniqueValueRenderer pUVRenderer = new UniqueValueRendererClass();
                pUVRenderer.FieldCount = 1;
                pUVRenderer.set_Field(0, nameField);
                pUVRenderer.UseDefaultSymbol = false;

                // 3. 定义山东省16地市列表
                string[] cities = new string[] {
                    "济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市", "济宁市",
                    "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市", "滨州市", "菏泽市"
                };

                // 4. 遍历城市，计算数量并生成符号
                foreach (string city in cities)
                {
                    int count = GetCountByCityVisual(city, year);

                    // 根据数量获取高对比度热力颜色
                    IColor color = GetHighContrastHeatmapColor(count);

                    ISimpleFillSymbol pFillSym = new SimpleFillSymbolClass();
                    pFillSym.Style = esriSimpleFillStyle.esriSFSSolid;
                    pFillSym.Color = color;
                    pFillSym.Outline.Color = new RgbColorClass { Red = 100, Green = 100, Blue = 100 }; // 深灰边框
                    pFillSym.Outline.Width = 1.0; // 加粗边框

                    pUVRenderer.AddValue(city, "Category", pFillSym as ISymbol);
                    pUVRenderer.AddValue(city.Replace("市", ""), "Category", pFillSym as ISymbol);
                }

                // 5. 应用渲染
                (cityLayer as IGeoFeatureLayer).Renderer = pUVRenderer as IFeatureRenderer;

                // 强制刷新
                axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, cityLayer, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("渲染 Choropleth 失败: " + ex.Message);
            }
        }

        /// <summary>
        /// [Helper] 获取高对比度热力色 (黄 -> 橙 -> 鲜红 -> 深红)
        /// </summary>
        private IColor GetHighContrastHeatmapColor(int count)
        {
            RgbColorClass color = new RgbColorClass();
            // 数量分级策略 (根据实际数据量级可能需要调整)
            if (count == 0) { color.Red = 255; color.Green = 255; color.Blue = 255; } // 白色 (无数据)
            else if (count <= 5) { color.Red = 255; color.Green = 255; color.Blue = 150; } // 浅黄
            else if (count <= 10) { color.Red = 255; color.Green = 200; color.Blue = 0; } // 橙黄
            else if (count <= 20) { color.Red = 255; color.Green = 120; color.Blue = 0; } // 橙色
            else if (count <= 35) { color.Red = 255; color.Green = 50; color.Blue = 0; } // 橘红
            else if (count <= 50) { color.Red = 220; color.Green = 0; color.Blue = 0; } // 鲜红
            else { color.Red = 139; color.Green = 0; color.Blue = 0; } // 深褐红 (最高)
            return color;
        }

        /// <summary>
        /// [Helper] 基于 Visual Map 的轻量级统计
        /// </summary>
        private int GetCountByCityVisual(string cityName, int year)
        {
            // 简单复用逻辑：遍历点图层，统计包含 CityName 且符合 Year 的要素
            try
            {
                IFeatureLayer pointLayer = null;
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer l = axMapControlVisual.get_Layer(i);
                    if (l.Name.Contains("非遗") || l.Name.Contains("项目")) { pointLayer = l as IFeatureLayer; break; }
                }
                if (pointLayer == null) return 0;

                string where = $"({GetCityField(pointLayer)} LIKE '%{cityName.Replace("市", "")}%') AND {GetTimeClause(pointLayer, year)}";
                if (string.IsNullOrEmpty(where)) return 0;

                IQueryFilter qf = new QueryFilterClass { WhereClause = where };
                return pointLayer.FeatureClass.FeatureCount(qf);
            }
            catch { return 0; }
        }

        private string GetCityField(IFeatureLayer ly)
        {
            // 简易查找字段
            string[] keys = { "市", "地区", "City", "Name", "所属" };
            foreach (var k in keys)
            {
                int idx = ly.FeatureClass.Fields.FindField(k);
                if (idx != -1) return ly.FeatureClass.Fields.get_Field(idx).Name;
            }
            return "PleaseCheckField";
        }

        private string GetTimeClause(IFeatureLayer ly, int year)
        {
            IFields fs = ly.FeatureClass.Fields;
            string fName = "公布时间";
            if (fs.FindField("公布时间") == -1)
            {
                if (fs.FindField("Time") != -1) fName = "Time";
                else if (fs.FindField("Year") != -1) fName = "Year";
                else return "1=1"; // 无时间字段则不过滤
            }

            // 批次映射
            int batch = 1;
            if (year >= 2008) batch = 2; if (year >= 2011) batch = 3; if (year >= 2014) batch = 4; if (year >= 2021) batch = 5;

            // 简单混合查询
            return $"(({fName} >= 1900 AND {fName} <= {year}) OR ({fName} > 0 AND {fName} <= {batch} AND {fName} < 20))";
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
                // 1. 同步共享状态 (Member B)
                _dashboardYear = year;

                // 2. 依次过滤专业版和演示版地图 (Member D)
                ApplyYearFilterToControl(axMapControl2, year);
                ApplyYearFilterToControl(axMapControlVisual, year);

                // 3. [Agent Add] 如果处于热力图模式，同步重绘分级色彩
                if (_isHeatmapMode)
                {
                    RenderCityChoropleth(year);
                }
            }
            catch (Exception) { }
        }
    }
}
