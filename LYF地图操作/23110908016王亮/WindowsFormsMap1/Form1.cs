// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 主窗体 - 中央枢纽
    /// 仅保留变量定义、初始化逻辑和核心状态切换
    /// 具体的业务逻辑已分散在 Form1.xxx.cs 分部类中
    /// </summary>
    public partial class Form1 : Form
    {
        // ================= 核心助手 (Helper) =================
        private EditorHelper _editorHelper;
        private MeasureHelper _measureHelper;
        private LayoutHelper _layoutHelper;

        // ================= 状态管理 =================
        public enum MapToolMode { None, Pan, MeasureDistance, MeasureArea, CreateFeature }
        private MapToolMode _currentToolMode = MapToolMode.None;
        private FormICHDetails _activeDetailsForm = null; // [Agent Add] 记录当前打开的详情窗体
        private int _dashboardYear = 2025; // [Member B] Added: 缓存当前看板年份，用于事件驱动刷新
        private bool _isHeatmapMode = false; // [Agent Add] 记录是否处于热力图模式
        private UIHelper _ui;


        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // [Agent Add] 全局样式注入
            ThemeEngine.ApplyTheme(this);
            ThemeEngine.ApplyMenuStripTheme(menuStrip1);
            ThemeEngine.ApplyStatusStripTheme(statusStrip1);
            ThemeEngine.ApplyTabControlTheme(tabControl1);
            ThemeEngine.ApplyTOCTheme(axTOCControl2);

            // 美化分割线与容器
            splitter1.BackColor = Color.FromArgb(226, 232, 240);
            splitter2.BackColor = Color.FromArgb(226, 232, 240);
            tabPage1.BackColor = Color.White;
            tabPage2.BackColor = Color.White;

            // 1. 初始化核心 Helper (必须在 UI 逻辑之前)
            _measureHelper = new MeasureHelper(axMapControl2);
            _editorHelper = new EditorHelper(axMapControl2);
            _layoutHelper = new LayoutHelper(this.axPageLayoutControl1, this.axMapControl2);
            _ui = new UIHelper(this, axMapControl2, menuStrip1);

            // 2. 基础事件绑定
            axTOCControl2.OnMouseDown += AxTOCControl2_OnMouseDown;
            axTOCControl2.SetBuddyControl(axMapControl2);
            this.tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged;

            // 3. 使用异步调用确保 Handle 创建后再执行复杂 UI 同步
            this.BeginInvoke(new Action(() =>
            {
                // 加载默认演示数据
                LoadDefaultMxd();

                // [Member E] 集成：初始化鹰眼图 (此时 TabPage 等容器 Handle 已就绪)
                this.InitEagleEye();

                // 强制进入演示模式逻辑
                if (tabControl1.SelectedIndex != 2)
                    tabControl1.SelectedIndex = 2;
                else
                    TabControl1_SelectedIndexChanged(null, null); // 即使索引已经是2也强制触发
            }));
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