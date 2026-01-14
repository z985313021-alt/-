// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using System;
using System.Drawing;
using System.Windows.Forms;
using ESRI.ArcGIS.Geometry;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 主窗体 - 中央枢纽
    /// 仅保留变量定义、初始化逻辑和核心状态切换
    /// 具体的业务逻辑已分散在 Form1.xxx.cs 分部类中
    /// </summary>
    public partial class Form1 : Form
    {
        // 【路线功能】：指向路径规划弹窗的引用
        private FormRoute _routeForm;
        // 【缓冲区功能】：指向智能缓冲区配置窗口的引用
        public FormSmartBuffer SmartBufferForm; 

        // 核心 Helper 助手类（负责具体业务逻辑的底层实现）
        private EditorHelper _editorHelper;    // 要素编辑助手
        private MeasureHelper _measureHelper;   // 地图量测助手
        private LayoutHelper _layoutHelper;     // 布局出图助手
        // [Member C] 智能工具箱核心
        public AnalysisHelper _analysisHelper; // 公开以便其他分部类访问

        // 【绘图工具】：在地图上绘制临时点及索引标签（通常用于路径规划的节点展示）
        public void DrawTempPoint(IPoint pt, int index)
        {
            if (pt == null) return;
            // 1. 创建标绘元素
            IMarkerElement markerEle = new MarkerElementClass();
            (markerEle as IElement).Geometry = pt;
            // 设置符号样式：蓝色实心圆，大小为 8
            markerEle.Symbol = new SimpleMarkerSymbolClass { Color = new RgbColorClass { Red = 0, Green = 0, Blue = 255 }, Size = 8, Style = esriSimpleMarkerStyle.esriSMSCircle };
            axMapControl2.ActiveView.GraphicsContainer.AddElement(markerEle as IElement, 0);
 
            // 2. 添加文字序号标签
            ITextElement textEle = new TextElementClass { Text = index.ToString() };
            (textEle as IElement).Geometry = pt;
            // 设置文字样式：黑色，大小 12
            textEle.Symbol = new TextSymbolClass { Size = 12, Color = new RgbColorClass { Red = 0, Green = 0, Blue = 0 } };
            axMapControl2.ActiveView.GraphicsContainer.AddElement(textEle as IElement, 0);
 
            // 局部刷新图形层，避免全图刷新导致的闪烁
            axMapControl2.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }

        public void RefreshMap()
        {
            if (axMapControl2 != null)
            {
                axMapControl2.ActiveView.GraphicsContainer.DeleteAllElements();
                axMapControl2.ActiveView.Refresh();
            }
        }
        // [Member C] 智能工具箱核心

        // 【状态管理】：定义当前活动的地学工具模式
        public enum MapToolMode
        {
            None,           // 无工具（普通指针）
            Pan,            // 漫游平移
            MeasureDistance,// 长度量测
            MeasureArea,    // 面积量测
            CreateFeature,  // 创建新要素（属性编辑）
            BufferPoint,    // 点缓冲区
            BufferLine,     // 线缓冲区
            RouteStart,     // 路径起点拾取
            RouteEnd,       // 路径终点拾取
            QueryBox,       // 拉框空间查询
            QueryPolygon,   // 多边形空间查询
            PickRoutePoint  // 多点路径规划拾取
        }
        private MapToolMode _currentToolMode = MapToolMode.None; // 记录当前选择的工具
        private FormICHDetails _activeDetailsForm = null;     // 当前显示的非遗详情弹窗单例
        private int _dashboardYear = 2025;                    // 看板当前过滤的全局年份
        private bool _isHeatmapMode = false;                  // 是否处于热力图图层渲染模式
        private UIHelper _ui;                                 // 界面辅助管理类
 
        // 【空间参考】：暴露地图的坐标系信息，用于其他模块进行投影变换（如经纬度转平面坐标）
        public ISpatialReference MapSpatialReference => axMapControl2.SpatialReference;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 1. 全局样式与视觉引擎注入（应用统一的玻璃拟态色调与图标库）
            ThemeEngine.ApplyTheme(this);
            ApplyMenuIcons(); 
            ThemeEngine.ApplyMenuStripTheme(menuStrip1);
            ThemeEngine.ApplyStatusStripTheme(statusStrip1);
            ThemeEngine.ApplyTabControlTheme(tabControl1);
            ThemeEngine.ApplyTOCTheme(axTOCControl2);
 
            // 2. 界面细节美化（调整分割线与容器底色）
            splitter1.BackColor = Color.FromArgb(226, 232, 240);
            splitter2.BackColor = Color.FromArgb(226, 232, 240);
            tabPage1.BackColor = Color.White;
            tabPage2.BackColor = Color.White;
 
            // 3. 自定义视图切换导航
            // 隐藏原生 Tab 标签，改用左下角的悬浮按钮组控制“数据/布局/演示”模式
            tabControl1.ItemSize = new Size(0, 1);
            tabControl1.SizeMode = TabSizeMode.Fixed;
            InitViewSwitcher();
 
            // 4. 初始化核心 Helper 助手类 (负责解耦 UI 与底层 GIS 算法)
            _measureHelper = new MeasureHelper(axMapControl2);
            _editorHelper = new EditorHelper(axMapControl2);
            _layoutHelper = new LayoutHelper(this.axPageLayoutControl1, this.axMapControl2);
            _ui = new UIHelper(this, axMapControl2, menuStrip1);
            _analysisHelper = new AnalysisHelper(); 
 
            // 5. 基础事件绑定与图层关联
            axTOCControl2.OnMouseDown += AxTOCControl2_OnMouseDown;
            axTOCControl2.SetBuddyControl(axMapControl2); // 关联 TOC 与主地图
            this.tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged;
 
            // 6. 初始化子功能模块
            this.InitSmartTools(); // 初始化智能工具箱（路径规划等）
 
            // 7. 异步加载数据与同步视图（防止 MXD 加载过程中的界面假死）
            this.BeginInvoke(new Action(() =>
            {
                // 自动加载默认的非遗地图文档（.mxd）
                LoadDefaultMxd();
 
                // 初始化右下角的鹰眼联动功能
                this.InitEagleEye();
 
                // 启动即进入演示模式，给观众最佳的视觉呈现
                if (tabControl1.SelectedIndex != 2)
                    tabControl1.SelectedIndex = 2;
                else
                    TabControl1_SelectedIndexChanged(null, null); 
            }));
        }

        private void ApplyMenuIcons()
        {
            数据加载ToolStripMenuItem.Image = ThemeEngine.GetIcon("Data");
            地图量测ToolStripMenuItem.Image = ThemeEngine.GetIcon("Measure");
            刷新ToolStripMenuItem.Image = ThemeEngine.GetIcon("Refresh");
            清除选择集ToolStripMenuItem.Image = ThemeEngine.GetIcon("Clear");
            漫游ToolStripMenuItem.Image = ThemeEngine.GetIcon("Pan");
            menuMapping.Image = ThemeEngine.GetIcon("Mapping");
            menuQuery.Image = ThemeEngine.GetIcon("Query");
            menuAnalysis.Image = ThemeEngine.GetIcon("Analysis");
            menuLayout.Image = ThemeEngine.GetIcon("Layout");
            menuEditing.Image = ThemeEngine.GetIcon("Edit");
        }

        private void InitViewSwitcher()
        {
            Panel p = new Panel
            {
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(200, Color.White),
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            // 将切换器放在 MapControl 容器上方
            p.Location = new System.Drawing.Point(10, tabPage1.Height - 40);

            Button btnData = CreateSwitcherButton("Data", 0, "数据视图");
            Button btnLayout = CreateSwitcherButton("Layout", 1, "布局视图");
            Button btnVisual = CreateSwitcherButton("Visual", 2, "演示模式");

            btnData.Click += (s, e) => tabControl1.SelectedIndex = 0;
            btnLayout.Click += (s, e) => tabControl1.SelectedIndex = 1;
            btnVisual.Click += (s, e) => tabControl1.SelectedIndex = 2;

            p.Controls.Add(btnData);
            p.Controls.Add(btnLayout);
            p.Controls.Add(btnVisual);

            // 由于 MapControl 在 TabPage 内部，我们将切换器添加到窗体层级以保证可见性，或者添加到每个 TabPage 下方
            // 按照需求，“换到 mapcontrol 的下方”，我们将其添加到对应的 TabPage 中
            tabPage1.Controls.Add(p);
            p.BringToFront();

            // 为布局视图也添加一个
            Panel p2 = new Panel { Size = p.Size, BackColor = p.BackColor, Location = p.Location, Anchor = p.Anchor };
            p2.Controls.Add(CreateSwitcherButton("Data", 0, "数据视图", true));
            p2.Controls.Add(CreateSwitcherButton("Layout", 1, "布局视图", true));
            p2.Controls.Add(CreateSwitcherButton("Visual", 2, "演示模式", true));
            foreach (Control c in p2.Controls)
            {
                if (c is Button b) b.Click += (s, e) => tabControl1.SelectedIndex = int.Parse(b.Tag.ToString());
            }
            tabPage2.Controls.Add(p2);
            p2.BringToFront();
        }

        private Button CreateSwitcherButton(string iconType, int index, string tip, bool isSimple = false)
        {
            Button b = new Button
            {
                Size = new Size(30, 24),
                Location = new System.Drawing.Point(5 + index * 35, 3),
                FlatStyle = FlatStyle.Flat,
                Image = ThemeEngine.GetIcon(iconType, Color.Black),
                Tag = index,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.LightSkyBlue;
            ToolTip tt = new ToolTip();
            tt.SetToolTip(b, tip);
            return b;
        }

        private void LoadDefaultMxd()
        {
            // [Member D] Modified: 优化路径探测，防止组员电脑路径不一导致加载失败
            string fileName = "非遗.mxd";
            string mxdPath = FindMxdPath(Application.StartupPath, fileName);

            if (!string.IsNullOrEmpty(mxdPath) && System.IO.File.Exists(mxdPath))
            {
                axMapControl2.LoadMxFile(mxdPath);
                axMapControl2.Extent = axMapControl2.FullExtent;
                axMapControl2.ActiveView.Refresh();

                // 同步鹰眼底图
                this.SyncEagleEyeLayers();
            }
            else
            {
                Console.WriteLine($"[Warning] 未能在项目树中探测到 {fileName}，请手动加载。");
            }
        }

        /// <summary>
        /// [Member D] 新增：自适应路径探测算法
        /// 从起始目录向上递归搜索指定文件，适用于团队协作中相对位置固定但绝对路径不同的场景
        /// </summary>
        private string FindMxdPath(string startDir, string fileName)
        {
            try
            {
                string current = startDir;
                // 最多向上探测 5 级目录，防止陷入无限循环
                for (int i = 0; i < 6; i++)
                {
                    // 1. 检查当前目录下是否有该文件
                    string directPath = System.IO.Path.Combine(current, fileName);
                    if (System.IO.File.Exists(directPath)) return directPath;

                    // 2. 检查当前目录下的“初步”文件夹（常见存放地）
                    string subPath = System.IO.Path.Combine(current, "初步", fileName);
                    if (System.IO.File.Exists(subPath)) return subPath;

                    // 3. 向上走一级
                    var parent = System.IO.Directory.GetParent(current);
                    if (parent == null) break;
                    current = parent.FullName;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 核心工具切换路由
        /// </summary>
        public void SwitchTool(MapToolMode mode)
        {
            // 清理旧状态
            _measureHelper.Stop();
            _editorHelper.StopCreateFeature();
            (axMapControl2.Map as IActiveView).PartialRefresh(esriViewDrawPhase.esriViewForeground, null, null);

            _currentToolMode = mode;
            axMapControl2.CurrentTool = null;
            axMapControl2.MousePointer = esriControlsMousePointer.esriPointerArrow;

            switch (mode)
            {
                case MapToolMode.Pan:
                    axMapControl2.MousePointer = esriControlsMousePointer.esriPointerPan;
                    break;
                case MapToolMode.MeasureDistance:
                    _measureHelper.StartMeasureDistance();
                    break;
                case MapToolMode.MeasureArea:
                    _measureHelper.StartMeasureArea();
                    break;
                case MapToolMode.CreateFeature:
                    _editorHelper.StartCreateFeature();
                    break;
            }
        }

        // --- 以下部分由分部类实现 ---
        // Form1.Navigation.cs : 处理地图交互、TOC菜单
        // Form1.Visual.cs     : 处理可视化模式、搜索、识别
        // Form1.Tools.cs      : 处理加载、分析、专项功能
        // Form1.Editor.cs     : 处理编辑触发、量测触发
    }
}