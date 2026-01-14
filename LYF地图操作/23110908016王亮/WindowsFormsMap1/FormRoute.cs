
using ESRI.ArcGIS.Geometry;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ESRI.ArcGIS.Display;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【路径规划交互窗体】：支持多点途经点设置、路网拓扑重建以及路径最优解展示
    /// 集成了路网缓存检测、空间投影纠偏以及高亮闪烁等交互特性
    /// </summary>
    public partial class FormRoute : Form
    {
        private Form1 _mainForm;
        private List<IPoint> _routePoints; // 存储用户在地图上点击拾取的途经点集合
        private AnalysisHelper _analyzer;

        public FormRoute(Form1 mainForm, AnalysisHelper analyzer)
        {
            InitializeComponent();
            _mainForm = mainForm;
            _analyzer = analyzer;
            _routePoints = new List<IPoint>();

            // [Agent (通用辅助)] Added: 为构建路网按钮添加右键菜单
            ContextMenuStrip buildMenu = new ContextMenuStrip();
            buildMenu.Items.Add("智能加载(优先使用缓存)", null, (s, e) =>
            {
                _mainForm.BuildRoadNetwork();
                lblInfo.Text = "尝试加载路网缓存...";
            });
            buildMenu.Items.Add("强制重新构建", null, (s, e) =>
            {
                _mainForm.ForceBuildRoadNetwork();
                lblInfo.Text = "正在重新构建路网...";
            });
            btnBuildNetwork.ContextMenuStrip = buildMenu;
        }

        // 【新增途经点】：将地图坐标同步到列表，并生成实时点标记
        public void AddPoint(IPoint pt)
        {
            if (pt == null) return;
            _routePoints.Add(pt);

            // 列表回显：展示地理坐标 (格式化为 3 位精度)
            lstPoints.Items.Add($"途经点 {_routePoints.Count}: ({pt.X:F3}, {pt.Y:F3})");

            // 在地图主容器绘制临时标记 (带序列号)
            _mainForm.DrawTempPoint(pt, _routePoints.Count);
        }

        private void btnAddPoint_Click(object sender, EventArgs e)
        {
            // 切换主窗体工具为选点模式
            _mainForm.SwitchTool(Form1.MapToolMode.PickRoutePoint);
            this.lblInfo.Text = "请在地图上点击添加途经点...";
        }

        private void btnRemovePoint_Click(object sender, EventArgs e)
        {
            int idx = lstPoints.SelectedIndex;
            if (idx >= 0)
            {
                lstPoints.Items.RemoveAt(idx);
                _routePoints.RemoveAt(idx);
                _mainForm.RefreshMap(); // 清除旧的绘制
                lblInfo.Text = "已移除选定点。";
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            lstPoints.Items.Clear();
            _routePoints.Clear();
            _mainForm.RefreshMap();
            lblInfo.Text = "已清空。";
        }

        // [Agent (通用辅助)] Modified: 左键默认智能加载,右键菜单提供强制重建选项
        private void btnBuildNetwork_Click(object sender, EventArgs e)
        {
            // 左键点击:默认智能加载(优先使用缓存)
            _mainForm.BuildRoadNetwork();
            lblInfo.Text = "尝试加载路网缓存...";
        }

        /// <summary>
        /// 【执行路径求解】：驱动 Dijkstra 算法计算多点间的最优连通路径
        /// 包含：单位纠偏逻辑 (地理 vs 投影)、结果高亮及业务状态回显
        /// </summary>
        private void btnSolve_Click(object sender, EventArgs e)
        {
            if (_routePoints.Count < 2)
            {
                MessageBox.Show("规划失败：至少需要标注“起点”与“终点”两个有效位置！");
                return;
            }

            try
            {
                this.Cursor = Cursors.WaitCursor;
                lblInfo.Text = "算法计算中，请稍候...";

                // 调用分析引擎执行多点路径搜索
                // [Agent] Fixed: FindShortestPath now returns RouteResult
                var rr = _analyzer.FindShortestPath(_routePoints);
                IPolyline result = rr?.PathLine;

                this.Cursor = Cursors.Default;

                if (result != null && !result.IsEmpty)
                {
                    double lenKm = 0;

                    // 【空间校准】：确保计算结果与当前地图投影系统一致
                    ISpatialReference mapSR = _mainForm.MapSpatialReference;
                    if (mapSR != null && result.SpatialReference != null)
                    {
                        // 逻辑：如果结果仍为地理坐标 (度)，则强转为地图目前的投影坐标 (米) 以获得准确物理长度
                        if (mapSR is IProjectedCoordinateSystem && !(result.SpatialReference is IProjectedCoordinateSystem))
                        {
                            try { result.Project(mapSR); } catch { }
                        }
                    }

                    // 物理长度换算 (MapUnits 默认为 Meters)
                    double lenMeters = result.Length;
                    lenKm = lenMeters / 1000.0;

                    // 结果呈现：绘制醒目的红色高亮路径
                    _mainForm.DrawGeometry(result, new RgbColorClass { Red = 255 });

                    string info = $"规划成功！总长度估计: {lenKm:F2} km";
                    lblInfo.Text = info;
                    MessageBox.Show($"{info}\n\n已完成路径同步绘制，请查看地图红色线段。", "解算成功");
                }
                else
                {
                    lblInfo.Text = "路径不可达。";
                    MessageBox.Show("计算无解：可能是途经点不在路网连通域内，或路网未成功加载。", "提示");
                }
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show("求解过程崩溃：" + ex.Message);
            }
        }

        private void FormRoute_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 窗体关闭时，重置地图工具
            _mainForm.SwitchTool(Form1.MapToolMode.None);
            _mainForm.RefreshMap(); // 清理临时点
        }
    }
}
