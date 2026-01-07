using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using System;
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
        private UIHelper _ui;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 1. 初始化 Helper
            _measureHelper = new MeasureHelper(axMapControl2);
            _editorHelper = new EditorHelper(axMapControl2);
            _layoutHelper = new LayoutHelper(this.axPageLayoutControl1, this.axMapControl2);

            // 2. 初始化助手
            _ui = new UIHelper(this, axMapControl2, menuStrip1);
            // 不再手动生成运行时 UI，回归设计器

            // [Member B Integration]
            // InitDashboardModule(); // User requested manual trigger or silent load
            
            // Dynamic Menu for Dashboard (Member B) - Removed: Embedded into Visual Tab
            // ToolStripMenuItem itemDashboard = new ToolStripMenuItem("数据看板(Dashboard)");
            // itemDashboard.Click += (s, ev) => InitDashboardModule();
            // menuStrip1.Items.Add(itemDashboard);


            // 3. 基础事件绑定
            axTOCControl2.OnMouseDown += AxTOCControl2_OnMouseDown;
            axTOCControl2.SetBuddyControl(axMapControl2);
            this.tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged;

            // 4. 加载默认演示数据
            LoadDefaultMxd();

            // [Member B Integration Fix]
            // Ensure app starts on Data View (Index 0). 
            // This prevents starting on Visual Tab without triggering the layout initialization logic.
            this.tabControl1.SelectedIndex = 0;
        }

        private void LoadDefaultMxd()
        {
            string mxdPath = System.IO.Path.Combine(Application.StartupPath, @"..\..\..\初步\非遗.mxd");
            if (!System.IO.File.Exists(mxdPath)) mxdPath = @"c:\Users\Administrator\Desktop\LYF地图操作\初步\非遗.mxd";

            if (System.IO.File.Exists(mxdPath))
            {
                axMapControl2.LoadMxFile(mxdPath);
                axMapControl2.ActiveView.Refresh();

                // [重要] 可视化演示页在 Designer 中的索引是 2
                // 强制触发一次 IndexChanged 以执行 UI 显隐逻辑
                if (tabControl1.SelectedIndex == 2) TabControl1_SelectedIndexChanged(null, null);
                else tabControl1.SelectedIndex = 2;
            }
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