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

        // [Member E] Added: Dynamic PageLayoutControl for Thematic Maps
        private ESRI.ArcGIS.Controls.AxPageLayoutControl _axPageLayoutVisual;

        public void InitVisualLayout()
        {
            if (_isVisualLayoutInitialized) return;

            // [Optimization] 挂起布局逻辑，防止界面闪烁
            this.SuspendLayout();
            try
            {
                // 1. 结构化容器
                _panelMainContent = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.AliceBlue };
                _panelSidebar = new Panel { Width = 80, Dock = DockStyle.Left, BackColor = Color.White, Padding = new Padding(5, 20, 5, 5) };

                AddSidebarButton("全景", 0);
                AddSidebarButton("搜索", 1);
                AddSidebarButton("分类", 2);
                AddSidebarButton("热力图", 3);
                AddSidebarButton("游览", 4);
                AddSidebarButton("WEB", 5);
                AddSidebarButton("返回", 6);

                _splitContainerVisual = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BorderStyle = BorderStyle.FixedSingle };
                _splitContainerVisual.SplitterDistance = (int)(tabPageVisual.Height * 0.7);

                // 2. 地图内容区
                Panel mapContainer = new Panel { Dock = DockStyle.Fill };
                _panelMapToolbar = new Panel { Height = 45, Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(5) };
                AddMapNavigationButtons();

                // [New] Init Layout Toolbar & Search
                InitLayoutToolbar();
                InitSearchPanel();
                mapContainer.Controls.Add(_panelSearch);
                _panelSearch.BringToFront();

                axMapControlVisual.Parent = null;
                axMapControlVisual.Dock = DockStyle.Fill;

                // [Agent Add] Initialize Dynamic PageLayoutControl
                _axPageLayoutVisual = new ESRI.ArcGIS.Controls.AxPageLayoutControl();
                ((System.ComponentModel.ISupportInitialize)(this._axPageLayoutVisual)).BeginInit();
                _axPageLayoutVisual.Dock = DockStyle.Fill;
                _axPageLayoutVisual.Visible = false;

                mapContainer.Controls.Add(_panelMapToolbar);
                mapContainer.Controls.Add(_panelLayoutToolbar);
                mapContainer.Controls.Add(axMapControlVisual);
                mapContainer.Controls.Add(_axPageLayoutVisual);
                ((System.ComponentModel.ISupportInitialize)(this._axPageLayoutVisual)).EndInit();

                _panelMapToolbar.BringToFront();
                if (_panelLayoutToolbar != null) _panelLayoutToolbar.BringToFront();

                _splitContainerVisual.Panel1.Controls.Add(mapContainer);

                // 3. 看板集成
                if (_dashboardForm == null || _dashboardForm.IsDisposed)
                {
                    _dashboardForm = new FormChart();
                    _dashboardForm.SetMainForm(this);
                }
                _dashboardForm.SetMapControl(axMapControlVisual);

                _dashboardForm.TopLevel = false;
                _dashboardForm.FormBorderStyle = FormBorderStyle.None;
                _dashboardForm.Dock = DockStyle.Fill;
                _dashboardForm.Visible = true;
                _splitContainerVisual.Panel2.Controls.Add(_dashboardForm);

                // 4. 安全组装
                _panelMainContent.Controls.Add(_splitContainerVisual);

                axMapControlVisual.OnMapReplaced += (s, ev) =>
                {
                    if (_dashboardForm != null && !_dashboardForm.IsDisposed)
                        _dashboardForm.UpdateChartData(_dashboardYear);
                };

                // 备份鹰眼面板
                Control eagleBackup = null;
                if (_panelEagleVisual != null && tabPageVisual.Controls.Contains(_panelEagleVisual))
                    eagleBackup = _panelEagleVisual;

                tabPageVisual.Controls.Clear();
                tabPageVisual.Controls.Add(_panelMainContent);
                tabPageVisual.Controls.Add(_panelSidebar);

                if (eagleBackup != null)
                {
                    tabPageVisual.Controls.Add(eagleBackup);
                    eagleBackup.BringToFront();
                }

                _isVisualLayoutInitialized = true;
            }
            finally
            {
                this.ResumeLayout(true);
            }
        }

        private bool _isVisualLayoutInitialized = false;
        private SplitContainer _splitContainerVisual;
        private Panel _panelMapToolbar;

        private Panel _panelLayoutToolbar; // [New] Dedicated Layout Toolbar

        private Panel _panelSearch; // [Agent Add] Floating Search Panel
        private TextBox _txtVisualSearch;
        private Panel _panelResultList; // [New] Floating Result List Panel
        private ListBox _lstResults;
        private FormTourRoutes _tourRoutesForm; // [Agent Add]
        private bool _isHeatmapActive = false; // [Agent Add] Track heatmap state

        // [Member E] 新增：演示模式集成导航工具 (支持双视图智能切换)
        private void AddMapNavigationButtons()
        {
            // 1. 指针 (恢复默认状态)
            var btnPointer = CreateNavIconBtn("Pointer", "选择/指针");
            btnPointer.Click += (s, e) =>
            {
                axMapControlVisual.CurrentTool = null;
                axMapControlVisual.MousePointer = esriControlsMousePointer.esriPointerArrow;
            };

            // 2. 识别 (自定义识别 - 仅限地图模式)
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
                ESRI.ArcGIS.SystemUI.ICommand cmd = new ESRI.ArcGIS.Controls.ControlsMapPanToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ESRI.ArcGIS.SystemUI.ITool;
            };

            // 4. 放大
            var btnZoomIn = CreateNavIconBtn("ZoomIn", "放大");
            btnZoomIn.Click += (s, e) =>
            {
                ESRI.ArcGIS.SystemUI.ICommand cmd = new ESRI.ArcGIS.Controls.ControlsMapZoomInToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ESRI.ArcGIS.SystemUI.ITool;
            };

            // 5. 缩小
            var btnZoomOut = CreateNavIconBtn("ZoomOut", "缩小");
            btnZoomOut.Click += (s, e) =>
            {
                ESRI.ArcGIS.SystemUI.ICommand cmd = new ESRI.ArcGIS.Controls.ControlsMapZoomOutToolClass();
                cmd.OnCreate(axMapControlVisual.Object);
                axMapControlVisual.CurrentTool = cmd as ESRI.ArcGIS.SystemUI.ITool;
            };

            // 6. 全图
            var btnFull = CreateNavIconBtn("Full", "全图显示");
            btnFull.Click += (s, e) =>
            {
                axMapControlVisual.Extent = axMapControlVisual.FullExtent;
                axMapControlVisual.ActiveView.Refresh();
            };

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
            else if (text == "搜索") iconType = "Search"; // [Modified]
            else if (text == "游览") iconType = "Navigation"; // 复用 Navigation 图标
            else if (text == "返回") iconType = "Back";
            else if (text == "全景") iconType = "Full";
            else if (text == "WEB") iconType = "Web"; // [Member E] Added: Web icon mapping

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

        // [Member E] Switch to Data View (Hide Layout Control)
        private void SwitchToDataView()
        {
            if (_axPageLayoutVisual != null) _axPageLayoutVisual.Visible = false;
            if (_panelLayoutToolbar != null) _panelLayoutToolbar.Visible = false;

            if (axMapControlVisual != null) axMapControlVisual.Visible = true;
            if (_panelMapToolbar != null) _panelMapToolbar.Visible = true;
        }

        // [Member E] Switch to Layout View (Show Layout Control)
        private void SwitchToLayoutView()
        {
            if (axMapControlVisual != null) axMapControlVisual.Visible = false;
            if (_panelMapToolbar != null) _panelMapToolbar.Visible = false;

            if (_axPageLayoutVisual != null) _axPageLayoutVisual.Visible = true;
            if (_panelLayoutToolbar != null) _panelLayoutToolbar.Visible = true;
        }

        // ... existing code ...

        /// <summary>
        /// [Member E] 新增：侧边栏导航路由逻辑
        /// </summary>
        private void SidebarBtn_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            // [Agent] Ensure we are in Data View by default unless "Classification" switches it
            if (btn.Text != "分类")
            {
                SwitchToDataView();
            }

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

            // [Agent Fix] Auto-close Search UI when switching modes
            if (btn.Text != "搜索")
            {
                if (_panelSearch != null) _panelSearch.Visible = false;
                if (_panelResultList != null) _panelResultList.Visible = false;
            }

            // [Agent Fix] Auto-reset Heatmap when switching modes
            if (btn.Text != "热力图" && _isHeatmapActive)
            {
                ResetRenderer();
                _isHeatmapActive = false;
            }

            // 2. 根据按钮切换视图逻辑
            switch (btn.Text)
            {
                case "全景":
                    // 展示全省非遗点位全貌
                    axMapControlVisual.Extent = axMapControlVisual.FullExtent;
                    break;
                case "分类":
                    // [Member E] 专题图：按类别弹出菜单选择
                    ShowCategoryMenu(btn);
                    break;
                case "搜索":
                    // [Agent Upgrade] 切换搜索面板可见性
                    if (_panelSearch != null)
                    {
                        SwitchToDataView(); // 确保在地图模式
                        _panelSearch.Visible = !_panelSearch.Visible;
                        if (_panelSearch.Visible)
                        {
                            _txtVisualSearch.Focus();
                        }
                        else
                        {
                            // Close result list if search panel is closed
                            if (_panelResultList != null) _panelResultList.Visible = false;
                        }
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
                case "游览":
                    // [Agent Add] 推荐游览路线
                    ShowTourRoutes();
                    break;
                case "返回":
                    tabControl1.SelectedIndex = 0;
                    break;
                case "WEB":
                    OpenWebVisualMode();
                    break;
            }

            // ... (rest of the method) ...
        }

        // [Refactored] 初始化布局视图的独立工具栏
        private void InitLayoutToolbar()
        {
            _panelLayoutToolbar = new Panel
            {
                Height = 32, // Slimmer than main toolbar (45)
                Dock = DockStyle.Top,
                BackColor = Color.AliceBlue,
                Padding = new Padding(2),
                Visible = false // Hidden by default
            };

            // 1. Pan
            var btnPan = CreateNavIconBtn("Pan", "漫游");
            btnPan.Size = new Size(28, 28); // Smaller buttons
            btnPan.Click += (s, e) =>
            {
                ESRI.ArcGIS.SystemUI.ICommand cmd = new ESRI.ArcGIS.Controls.ControlsPagePanToolClass();
                cmd.OnCreate(_axPageLayoutVisual.Object);
                _axPageLayoutVisual.CurrentTool = cmd as ESRI.ArcGIS.SystemUI.ITool;
            };

            // 2. Zoom In
            var btnZoomIn = CreateNavIconBtn("ZoomIn", "放大/框选");
            btnZoomIn.Size = new Size(28, 28);
            btnZoomIn.Click += (s, e) =>
            {
                ESRI.ArcGIS.SystemUI.ICommand cmd = new ESRI.ArcGIS.Controls.ControlsPageZoomInToolClass();
                cmd.OnCreate(_axPageLayoutVisual.Object);
                _axPageLayoutVisual.CurrentTool = cmd as ESRI.ArcGIS.SystemUI.ITool;
            };

            // 3. Zoom Out
            var btnZoomOut = CreateNavIconBtn("ZoomOut", "缩小");
            btnZoomOut.Size = new Size(28, 28);
            btnZoomOut.Click += (s, e) =>
            {
                ESRI.ArcGIS.SystemUI.ICommand cmd = new ESRI.ArcGIS.Controls.ControlsPageZoomOutToolClass();
                cmd.OnCreate(_axPageLayoutVisual.Object);
                _axPageLayoutVisual.CurrentTool = cmd as ESRI.ArcGIS.SystemUI.ITool;
            };

            // 4. Full Page
            var btnFull = CreateNavIconBtn("Full", "整页显示");
            btnFull.Size = new Size(28, 28);
            btnFull.Click += (s, e) =>
            {
                _axPageLayoutVisual.ZoomToWholePage();
            };

            _panelLayoutToolbar.Controls.Add(btnPan);
            _panelLayoutToolbar.Controls.Add(btnZoomIn);
            _panelLayoutToolbar.Controls.Add(btnZoomOut);
            _panelLayoutToolbar.Controls.Add(btnFull);

            int left = 5;
            foreach (Control ctrl in _panelLayoutToolbar.Controls)
            {
                ctrl.Left = left;
                ctrl.Top = 2;
                left += ctrl.Width + 5;
            }
        }

        // [Member E] Display Context Menu for Thematic Maps
        private void ShowCategoryMenu(Button btn)
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            // Add menu items with click handlers (using the layout-supporting LoadThematicMap)
            menu.Items.Add("山东省非遗分布专题图", null, (s, e) => LoadThematicMap("山东省非遗分布专题图", "山东省非遗分布专题图.mxd"));
            menu.Items.Add("山东省非遗曲艺分布专题图", null, (s, e) => LoadThematicMap("山东省非遗曲艺分布专题图", "山东省曲艺非遗分布专题图.mxd", "ARCGIS"));
            menu.Items.Add("山东省美食非遗分布专题图", null, (s, e) => LoadThematicMap("山东省美食非遗分布专题图", "山东非遗.mxd"));

            // Show at button location
            menu.Show(btn, new System.Drawing.Point(btn.Width, 0));
        }

        private void LoadThematicMap(string folderName, string mxdName, string subFolder = "")
        {
            try
            {
                // 1. Try to find the root data directory "山东省arcgis处理数据及底图"
                string rootDataDir = FindDataRootDirectory("山东省arcgis处理数据及底图");

                if (string.IsNullOrEmpty(rootDataDir))
                {
                    MessageBox.Show("未找到数据根目录: 山东省arcgis处理数据及底图", "路径错误");
                    return;
                }

                // 2. Construct full path
                string fullPath = System.IO.Path.Combine(rootDataDir, folderName);
                if (!string.IsNullOrEmpty(subFolder))
                {
                    fullPath = System.IO.Path.Combine(fullPath, subFolder);
                }
                fullPath = System.IO.Path.Combine(fullPath, mxdName);

                // 3. Load Map into PageLayoutControl
                if (System.IO.File.Exists(fullPath))
                {
                    // [Agent] Switch to Layout View before loading
                    SwitchToLayoutView();

                    _axPageLayoutVisual.LoadMxFile(fullPath);
                    _axPageLayoutVisual.ZoomToWholePage(); // [Fix] Ensure map fills the control
                    _axPageLayoutVisual.ActiveView.Refresh();
                }
                else
                {
                    MessageBox.Show($"文件不存在: {fullPath}", "加载失败");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载专题图出错: " + ex.Message);
            }
        }

        // [Agent Add] Initialize Stylish Floating Search Panel
        private void InitSearchPanel()
        {
            _panelSearch = new Panel
            {
                Size = new Size(320, 70), // Increased height for instructions
                Location = new System.Drawing.Point(20, 60),
                BackColor = Color.White,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            _panelSearch.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, _panelSearch.ClientRectangle, Color.LightGray, ButtonBorderStyle.Solid); };

            Label lblHint = new Label
            {
                Text = "支持搜索：地市名称(如:济南) 或 非遗名称(如:皮影)\n回车或点击按钮开始",
                Location = new System.Drawing.Point(10, 38),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("微软雅黑", 8F)
            };

            _txtVisualSearch = new TextBox
            {
                Location = new System.Drawing.Point(10, 10),
                Width = 220,
                BorderStyle = BorderStyle.FixedSingle, // FixedSingle for better visibility
                Font = new Font("微软雅黑", 10F),
                Text = "输入地市或非遗名称..."
            };

            _txtVisualSearch.GotFocus += (s, e) => { if (_txtVisualSearch.Text == "输入地市或非遗名称...") { _txtVisualSearch.Text = ""; _txtVisualSearch.ForeColor = Color.Black; } };
            _txtVisualSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_txtVisualSearch.Text)) { _txtVisualSearch.Text = "输入地市或非遗名称..."; _txtVisualSearch.ForeColor = Color.Gray; } };
            _txtVisualSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PerformVisualSearch(_txtVisualSearch.Text); };

            Button btnGo = new Button
            {
                Text = "搜索",
                Location = new System.Drawing.Point(240, 8),
                Size = new Size(70, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeEngine.ColorPrimary,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnGo.FlatAppearance.BorderSize = 0;
            btnGo.Click += (s, e) => PerformVisualSearch(_txtVisualSearch.Text);

            _panelSearch.Controls.Add(_txtVisualSearch);
            _panelSearch.Controls.Add(btnGo);
            _panelSearch.Controls.Add(lblHint);
        }

        // [New] Show Search Results in Floating List
        private void ShowSearchResults(List<string> items, string title)
        {
            if (_panelResultList == null)
            {
                _panelResultList = new Panel
                {
                    Size = new Size(250, 300),
                    Location = new System.Drawing.Point(20, 140), // Below search panel
                    BackColor = Color.White,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left,
                    Visible = false
                };
                _panelResultList.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, _panelResultList.ClientRectangle, ThemeEngine.ColorPrimary, ButtonBorderStyle.Solid);

                Label lblTitle = new Label { Text = "搜索结果", Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5, 0, 0, 0), Font = new Font("微软雅黑", 9, FontStyle.Bold), BackColor = ThemeEngine.ColorSecondary };

                Button btnClose = new Button { Text = "×", Dock = DockStyle.Right, Width = 30, FlatStyle = FlatStyle.Flat, ForeColor = Color.Red };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (s, e) => _panelResultList.Visible = false;
                lblTitle.Controls.Add(btnClose);

                _lstResults = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("微软雅黑", 9), ItemHeight = 20 };

                _panelResultList.Controls.Add(_lstResults);
                _panelResultList.Controls.Add(lblTitle);

                // Add to map container if not present
                if (_panelSearch.Parent != null && !_panelSearch.Parent.Controls.Contains(_panelResultList))
                {
                    _panelSearch.Parent.Controls.Add(_panelResultList);
                    _panelResultList.BringToFront();
                }
            }

            _lstResults.Items.Clear();
            if (items == null || items.Count == 0)
            {
                _lstResults.Items.Add("未找到相关结果");
            }
            else
            {
                foreach (var item in items) _lstResults.Items.Add(item);
            }

            // Update Title
            (_panelResultList.Controls[1] as Label).Text = title; // Index 1 is title label because ListBox added first? No, ListBox Dock Fill, Label Dock Top. Controls collection order depends on Add.
            // Actually careful with index. Let's just find it.
            foreach (Control c in _panelResultList.Controls) if (c is Label) c.Text = title;

            _panelResultList.Visible = true;
            _panelResultList.BringToFront();
        }

        // [Agent Add] Core Layout Search Logic
        private void PerformVisualSearch(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword) || keyword.Contains("输入地市")) return;

            try
            {
                this.Cursor = Cursors.WaitCursor;
                axMapControlVisual.Map.ClearSelection();
                axMapControlVisual.ActiveView.GraphicsContainer.DeleteAllElements();

                bool found = false;
                List<string> resultNames = new List<string>();

                // Strategy 1: Search City (Region)
                IFeatureLayer cityLayer = null;
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer l = axMapControlVisual.get_Layer(i);
                    // [Fix] User specified layer name "shiqu"
                    if ((l.Name.Contains("shiqu") || l.Name.Contains("地市") || l.Name.Contains("行政")) && l is IFeatureLayer fl && fl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolygon)
                    {
                        cityLayer = fl;
                        break;
                    }
                }

                if (cityLayer != null)
                {
                    IQueryFilter qf = new QueryFilterClass();
                    // [Fix] User specified: City layer is "shiqu", Field is "行政名"
                    string nameField = "行政名";
                    if (cityLayer.FeatureClass.Fields.FindField("行政名") == -1)
                    {
                        if (cityLayer.FeatureClass.Fields.FindField("Name") != -1) nameField = "Name";
                    }

                    qf.WhereClause = $"{nameField} LIKE '%{keyword}%'";

                    IFeatureCursor cursor = cityLayer.Search(qf, false);
                    IFeature cityFeature = cursor.NextFeature(); // Only take first city match
                    if (cityFeature != null)
                    {
                        found = true;
                        // Zoom to city
                        axMapControlVisual.Extent = cityFeature.Shape.Envelope;
                        axMapControlVisual.FlashShape(cityFeature.Shape, 1, 300, null);

                        // [Agent Add] Highlight "shiqujie" (City Boundary) if exists
                        IFeatureLayer boundLayer = null;
                        for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                        {
                            ILayer l = axMapControlVisual.get_Layer(i);
                            if (l.Name.Contains("shiqujie") || l.Name.Contains("界"))
                            {
                                boundLayer = l as IFeatureLayer;
                                break;
                            }
                        }

                        if (boundLayer != null)
                        {
                            // [Defense] Check if field exists before setting WhereClause
                            bool fieldFound = false;
                            string boundField = "行政名";
                            if (boundLayer.FeatureClass.Fields.FindField("行政名") != -1)
                            {
                                boundField = "行政名";
                                fieldFound = true;
                            }
                            else if (boundLayer.FeatureClass.Fields.FindField("Name") != -1)
                            {
                                boundField = "Name";
                                fieldFound = true;
                            }

                            if (fieldFound)
                            {
                                try
                                {
                                    IQueryFilter qfBound = new QueryFilterClass();
                                    qfBound.WhereClause = $"{boundField} LIKE '%{keyword}%'";

                                    IFeatureSelection boundSel = boundLayer as IFeatureSelection;
                                    if (boundSel != null)
                                    {
                                        boundSel.SelectFeatures(qfBound, esriSelectionResultEnum.esriSelectionResultAdd, false);
                                    }
                                }
                                catch { /* Ignore boundary highlight errors to correct main search */ }
                            }
                        }

                        // Select all ICH points inside this city
                        IFeatureLayer ichLayer = null;
                        for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                        {
                            ILayer l = axMapControlVisual.get_Layer(i);
                            // [Fix] User specified layer name "山东国家级非遗项目"
                            if (l.Name.Contains("山东国家级非遗项目") || l.Name.Contains("非遗")) { ichLayer = l as IFeatureLayer; break; }
                        }

                        if (ichLayer != null)
                        {
                            ISpatialFilter sf = new SpatialFilterClass();
                            sf.Geometry = cityFeature.Shape;
                            sf.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;

                            // [Fix] Correctly use IFeatureSelection to select features
                            IFeatureSelection featureSelection = ichLayer as IFeatureSelection;
                            if (featureSelection != null)
                            {
                                featureSelection.SelectFeatures(sf, esriSelectionResultEnum.esriSelectionResultNew, false);
                                axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);

                                // [New] Collect names for valid list
                                try
                                {
                                    ISelectionSet selSet = featureSelection.SelectionSet;
                                    if (selSet.Count > 0)
                                    {
                                        ICursor selCursor;
                                        selSet.Search(null, true, out selCursor);
                                        IFeature selFeat;
                                        int idxName = ichLayer.FeatureClass.Fields.FindField("名称");
                                        if (idxName == -1) idxName = ichLayer.FeatureClass.Fields.FindField("项目名称");

                                        while ((selFeat = selCursor.NextRow() as IFeature) != null)
                                        {
                                            if (idxName != -1) resultNames.Add(selFeat.get_Value(idxName).ToString());
                                            else resultNames.Add("项目ID:" + selFeat.OID);
                                        }
                                    }
                                }
                                catch { /* Ignore list generation errors */ }
                            }
                        }

                        string cityName = cityFeature.get_Value(cityLayer.FeatureClass.Fields.FindField(nameField)).ToString();
                        ShowSearchResults(resultNames, $"在 {cityName} 发现 {resultNames.Count} 个项目");
                    }
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);
                }

                // Strategy 2: If not city, search for Spot Name
                if (!found)
                {
                    IFeatureLayer ichLayer = null;
                    for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                    {
                        ILayer l = axMapControlVisual.get_Layer(i);
                        // [Fix] User specified layer name "山东国家级非遗项目"
                        if (l.Name.Contains("山东国家级非遗项目") || l.Name.Contains("非遗")) { ichLayer = l as IFeatureLayer; break; }
                    }

                    if (ichLayer != null)
                    {
                        // [Fix] User specified field is "名称"
                        string nameField = "名称";
                        if (ichLayer.FeatureClass.Fields.FindField("名称") == -1)
                        {
                            if (ichLayer.FeatureClass.Fields.FindField("Name") != -1) nameField = "Name";
                            else if (ichLayer.FeatureClass.Fields.FindField("项目名称") != -1) nameField = "项目名称";
                        }

                        IQueryFilter qf = new QueryFilterClass();
                        qf.WhereClause = $"{nameField} LIKE '%{keyword}%'";

                        // [Fix] Search all matches
                        IFeatureCursor cursor = ichLayer.Search(qf, false);
                        IFeature spotFeature;
                        IEnvelope unionEnv = null;

                        IFeatureSelection featureSelection = ichLayer as IFeatureSelection;
                        if (featureSelection != null) featureSelection.Clear(); // Clear previous

                        while ((spotFeature = cursor.NextFeature()) != null)
                        {
                            found = true;
                            resultNames.Add(spotFeature.get_Value(ichLayer.FeatureClass.Fields.FindField(nameField)).ToString());

                            // Highlight
                            axMapControlVisual.Map.SelectFeature(ichLayer, spotFeature);

                            // Union Extent
                            if (unionEnv == null) unionEnv = spotFeature.Shape.Envelope;
                            else unionEnv.Union(spotFeature.Shape.Envelope);
                        }
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);

                        if (found)
                        {
                            if (unionEnv != null)
                            {
                                unionEnv.Expand(1.5, 1.5, true);
                                // Limit zoom out if too scattered? standard expand is ok.
                                axMapControlVisual.Extent = unionEnv;
                            }
                            ShowSearchResults(resultNames, $"找到 {resultNames.Count} 个相关项目");
                        }
                    }
                }

                if (!found)
                {
                    MessageBox.Show("未找到与“" + keyword + "”相关的地市或景点。", "搜索结果");
                }

                axMapControlVisual.ActiveView.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show("搜索出错: " + ex.Message);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // [Agent Add] 显示游览路线窗口
        private void ShowTourRoutes()
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;

                // 1. 查找关键图层
                IFeatureLayer ichLayer = null;
                IFeatureLayer cityLayer = null;
                List<IFeatureLayer> lineLayers = new List<IFeatureLayer>();

                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer l = axMapControlVisual.get_Layer(i);
                    if (l is IFeatureLayer fl)
                    {
                        string name = fl.Name.ToLower();
                        if (name.Contains("非遗") || name.Contains("项目")) ichLayer = fl;

                        // Collect all line layers for road selection
                        if (fl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                        {
                            lineLayers.Add(fl);
                        }

                        // [Agent] Find City Layer
                        if ((name.Contains("city") || name.Contains("地市") || name.Contains("行政") || name.Contains("shiqu") || name.Contains("boundary")) && fl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolygon)
                        {
                            if (!name.Contains("label") && !name.Contains("anno")) // Exclude annotation layers
                                cityLayer = fl;
                        }
                    }
                }

                this.Cursor = Cursors.Default;

                // 2. 展示窗口
                if (_tourRoutesForm == null || _tourRoutesForm.IsDisposed)
                {
                    _tourRoutesForm = new FormTourRoutes(this, cityLayer, lineLayers, ichLayer);
                }
                _tourRoutesForm.Show();
                _tourRoutesForm.Activate();
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show("打开路线窗口失败: " + ex.Message);
            }
        }

        // [Agent Add] 在地图上展示特定路线 (由 FormTourRoutes 调用)
        public void DisplayTourRoute(AnalysisHelper.TourRoute route)
        {
            if (route == null) return;

            axMapControlVisual.Map.ClearSelection();
            axMapControlVisual.ActiveView.GraphicsContainer.DeleteAllElements();

            // 1. 高亮路线点
            IFeatureLayer ichLayer = null;

            // 尝试根据点位数据的来源类查找图层 (更准确)
            if (route.Points != null && route.Points.Count > 0)
            {
                IFeatureClass ptClass = route.Points[0].Class as IFeatureClass;
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer l = axMapControlVisual.get_Layer(i);
                    if (l is IFeatureLayer fl && fl.FeatureClass != null && fl.FeatureClass == ptClass)
                    {
                        ichLayer = fl;
                        break;
                    }
                }
            }

            // 如果没找到 (或者没点)，回退到按名字找
            if (ichLayer == null)
            {
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer l = axMapControlVisual.get_Layer(i);
                    if (l.Name.Contains("非遗") || l.Name.Contains("项目"))
                    {
                        ichLayer = l as IFeatureLayer;
                        break;
                    }
                }
            }

            if (ichLayer != null && route.Points != null && route.Points.Count > 0)
            {
                IGeoFeatureLayer gfl = ichLayer as IGeoFeatureLayer;
                if (gfl != null)
                {
                    // 构造选择集 (使用 Add 而非 SelectFeatures，避免 SQL 长度限制导致的 crash)
                    IFeatureSelection fs = gfl as IFeatureSelection;
                    fs.Clear();
                    ISelectionSet selSet = fs.SelectionSet;

                    foreach (var pt in route.Points)
                    {
                        selSet.Add(pt.OID);
                    }

                    // 注意：不需要调用 SelectFeatures，直接操作 SelectionSet 即可
                    // 视图刷新在方法最后统一处理
                }
            }

            // 1.5 高亮高速路线 (新增需求)
            if (route.RoadFeatures != null && route.RoadFeatures.Count > 0)
            {
                // 查找高速图层 (简单遍历)
                IFeatureLayer roadLayer = null;
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer l = axMapControlVisual.get_Layer(i);
                    // 假设高速图层名字包含 "高速" 或 "Line" 且包含 route.RoadFeatures[0] 所在的 FeatureClass
                    if (l is IFeatureLayer fl && fl.FeatureClass != null &&
                        fl.FeatureClass.AliasName == route.RoadFeatures[0].Class.AliasName)
                    {
                        roadLayer = fl;
                        break;
                    }
                }

                // 如果找不到精确匹配，尝试按名字找
                if (roadLayer == null)
                {
                    for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                    {
                        ILayer l = axMapControlVisual.get_Layer(i);
                        if (l.Name.Contains("高速") && l is IFeatureLayer) { roadLayer = l as IFeatureLayer; break; }
                    }
                }

                if (roadLayer != null)
                {
                    List<int> roadOids = new List<int>();
                    foreach (var f in route.RoadFeatures) roadOids.Add(f.OID);

                    if (roadOids.Count > 0)
                    {
                        IFeatureSelection fs = roadLayer as IFeatureSelection;
                        // 先清空当前选择
                        fs.Clear();

                        // 批量添加到选择集 (使用循环确保稳定性，避免 WHERE 语句超长)
                        ISelectionSet selSet = fs.SelectionSet;
                        foreach (int oid in roadOids)
                        {
                            selSet.Add(oid);
                        }

                        // 刷新视图以显示选择
                        // The partial refresh happens at end of method
                    }
                }
            }

            // 2. 绘制连线 (示意性)
            // 如果 route.PathLine 为空，我们可以简单地按顺序连接点
            IGeometry lineGeo = route.PathLine;
            if ((lineGeo == null || lineGeo.IsEmpty) && route.Points.Count > 1)
            {
                IPointCollection pc = new PolylineClass();
                foreach (var pt in route.Points)
                {
                    pc.AddPoint(pt.ShapeCopy as IPoint);
                }
                lineGeo = pc as IGeometry;
            }

            if (lineGeo != null && !lineGeo.IsEmpty)
            {
                // 简单绘制
                SimpleLineSymbolClass lineSym = new SimpleLineSymbolClass();
                // 使用高亮青色，更醒目
                lineSym.Color = new RgbColorClass { Red = 0, Green = 255, Blue = 255 };
                lineSym.Width = 5; // 加粗
                lineSym.Style = esriSimpleLineStyle.esriSLSSolid; // 实线

                LineElementClass le = new LineElementClass { Geometry = lineGeo, Symbol = lineSym };
                axMapControlVisual.ActiveView.GraphicsContainer.AddElement(le, 0);
            }

            // 3. 缩放到范围
            // 3. 缩放到范围
            if (ichLayer != null && (ichLayer as IFeatureSelection).SelectionSet.Count > 0)
            {
                // 3. 缩放到范围
                ISelectionSet selectionSet = (ichLayer as IFeatureSelection).SelectionSet;
                ICursor cursor;
                selectionSet.Search(null, false, out cursor);
                IFeatureCursor featureCursor = cursor as IFeatureCursor;

                IFeature f;
                IEnvelope env = new EnvelopeClass { SpatialReference = axMapControlVisual.Map.SpatialReference };
                bool first = true;
                while ((f = featureCursor.NextFeature()) != null)
                {
                    if (first) { env = f.Extent; first = false; }
                    else env.Union(f.Extent);
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(featureCursor);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);

                env.Expand(1.2, 1.2, true);
                axMapControlVisual.Extent = env;
            }

            axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
            axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
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



        private string FindDataRootDirectory(string targetDirName)
        {
            string current = Application.StartupPath;
            for (int i = 0; i < 6; i++)
            {
                string path = System.IO.Path.Combine(current, targetDirName);
                // Also check if targetDirName is in a sibling folder (common in dev envs structure like '1.8/-')

                // Check direct subdirectory
                string[] dirs = System.IO.Directory.GetDirectories(current, targetDirName, System.IO.SearchOption.TopDirectoryOnly);
                if (dirs.Length > 0) return dirs[0];

                // Special case for the user structure seen: "LYF地图操作\山东省arcgis处理数据及底图"
                string lyfPath = System.IO.Path.Combine(current, "LYF地图操作", targetDirName);
                if (System.IO.Directory.Exists(lyfPath)) return lyfPath;

                // Move up
                var parent = System.IO.Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            // Broad search for the directory if simple traversal fails? 
            // Better to stay safe and just return null if not found near project.
            return null;
        }

        // [Member E] Added: 一键分类渲染逻辑 (Depreciated by Menu, but kept as placeholder if needed or removed)
        private void ApplyCategoryRenderer()
        {
            // Replaced by ShowCategoryMenu
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

            _isHeatmapActive = true;
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

        // [Member E] Added: Open independent Web Visualization Window
        private void OpenWebVisualMode()
        {
            try
            {
                FormWebVisual webForm = new FormWebVisual();
                webForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动Web演示模式失败: " + ex.Message + "\n请确认WebView2环境已就绪。", "系统提示");
            }
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
